using UnityEngine;

namespace Ginei
{
    /// <summary>医療トリアージの調整係数（限られた医療資源の優先配分）。全フィールド ctor で Clamp。</summary>
    public readonly struct MedicalTriageParams
    {
        /// <summary>緊急度の基準倍率（傷の重さ×悪化速度に掛ける）。</summary>
        public readonly float urgencyScale;
        /// <summary>致命度係数（傷の重さからどれだけ救命を奪うか。大きいほど重傷=即・救命困難）。</summary>
        public readonly float lethality;
        /// <summary>見送り（黒タグ）の重症閾値（これ以上の重さで黒タグ候補）。</summary>
        public readonly float expectantSeverityThreshold;
        /// <summary>見送り（黒タグ）の救命可能性閾値（これ以下の救命可能性で黒タグ確定）。</summary>
        public readonly float expectantSalvageThreshold;
        /// <summary>待機中の悪化の基準倍率（待機時間×悪化速度に掛ける）。</summary>
        public readonly float deteriorationScale;
        /// <summary>トリアージ効果での待機悪化のペナルティ係数（待機悪化を効果からどれだけ引くか）。</summary>
        public readonly float waitingPenalty;

        public MedicalTriageParams(float urgencyScale, float lethality, float expectantSeverityThreshold,
            float expectantSalvageThreshold, float deteriorationScale, float waitingPenalty)
        {
            this.urgencyScale = Mathf.Max(0f, urgencyScale);
            this.lethality = Mathf.Clamp01(lethality);
            this.expectantSeverityThreshold = Mathf.Clamp01(expectantSeverityThreshold);
            this.expectantSalvageThreshold = Mathf.Clamp01(expectantSalvageThreshold);
            this.deteriorationScale = Mathf.Max(0f, deteriorationScale);
            this.waitingPenalty = Mathf.Max(0f, waitingPenalty);
        }

        public static MedicalTriageParams Default => new MedicalTriageParams(1f, 1f, 0.8f, 0.25f, 1f, 1f);
    }

    /// <summary>
    /// 医療トリアージの純ロジック。大量傷病者発生時の治療優先順位を、限られた医療資源で最大救命する
    /// 観点から計算する。緊急度（<see cref="Urgency"/>）と救命可能性（<see cref="Salvageability"/>）を
    /// 掛けて治療優先度（<see cref="TriagePriority"/>）を出し、重症かつ救命困難は見送り（黒タグ＝
    /// <see cref="ExpectantCategory"/>）。資源配分（<see cref="ResourceAllocation"/>）から最大救命数
    /// （<see cref="LivesMaximized"/>）を導き、待機中の悪化（<see cref="WaitingDeterioration"/>）を差し引いた
    /// トリアージの効果（<see cref="TriageEffectiveness"/>）を返す。決定論・入力 Clamp・基準値非破壊（実効値パターン）。
    /// 純データ／純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class MedicalTriageRules
    {
        /// <summary>ゼロ除算回避の極小値。</summary>
        private const float Epsilon = 1e-4f;

        /// <summary>緊急度＝傷の重さ×悪化速度（0..1）。重く速く悪化するほど緊急。</summary>
        public static float Urgency(float injurySeverity, float deteriorationRate, MedicalTriageParams p)
        {
            float sev = Mathf.Clamp01(injurySeverity);
            float rate = Mathf.Clamp01(deteriorationRate);
            return Mathf.Clamp01(sev * rate * p.urgencyScale);
        }

        public static float Urgency(float injurySeverity, float deteriorationRate)
            => Urgency(injurySeverity, deteriorationRate, MedicalTriageParams.Default);

        /// <summary>救命可能性＝(1-致命度)×治療可能性（0..1）。重傷ほど助からず、治療資源があるほど助かる。</summary>
        public static float Salvageability(float injurySeverity, float treatmentAvailable, MedicalTriageParams p)
        {
            float sev = Mathf.Clamp01(injurySeverity);
            float treat = Mathf.Clamp01(treatmentAvailable);
            float survivable = Mathf.Clamp01(1f - sev * p.lethality); // 致命度ぶん助からない
            return Mathf.Clamp01(survivable * treat);
        }

        public static float Salvageability(float injurySeverity, float treatmentAvailable)
            => Salvageability(injurySeverity, treatmentAvailable, MedicalTriageParams.Default);

        /// <summary>治療優先度＝緊急度×救命可能性（0..1）。緊急かつ助かる者を優先（黒タグや軽傷は下げる）。</summary>
        public static float TriagePriority(float urgency, float salvageability, MedicalTriageParams p)
        {
            float u = Mathf.Clamp01(urgency);
            float s = Mathf.Clamp01(salvageability);
            return Mathf.Clamp01(u * s);
        }

        public static float TriagePriority(float urgency, float salvageability)
            => TriagePriority(urgency, salvageability, MedicalTriageParams.Default);

        /// <summary>
        /// 見送り（黒タグ）の判定。重症（閾値以上）かつ救命困難（救命可能性が閾値以下）なら true。
        /// 限られた資源を救える者へ回すための苦渋の判断。
        /// </summary>
        public static bool ExpectantCategory(float injurySeverity, float salvageability, MedicalTriageParams p)
        {
            float sev = Mathf.Clamp01(injurySeverity);
            float s = Mathf.Clamp01(salvageability);
            return sev >= p.expectantSeverityThreshold && s <= p.expectantSalvageThreshold;
        }

        public static bool ExpectantCategory(float injurySeverity, float salvageability)
            => ExpectantCategory(injurySeverity, salvageability, MedicalTriageParams.Default);

        /// <summary>
        /// 資源配分＝優先度×(利用可能資源/需要)（0..1）。需要に対し資源が足りないほど配分は薄まる。
        /// 需要 0 は実質飽和（資源で頭打ち）として扱う。
        /// </summary>
        public static float ResourceAllocation(float triagePriority, float availableResources, float demandVolume, MedicalTriageParams p)
        {
            float prio = Mathf.Clamp01(triagePriority);
            float res = Mathf.Max(0f, availableResources);
            float demand = Mathf.Max(0f, demandVolume);
            float ratio = Mathf.Clamp01(res / Mathf.Max(demand, Epsilon)); // 資源/需要（1で充足、超過は頭打ち）
            return Mathf.Clamp01(prio * ratio);
        }

        public static float ResourceAllocation(float triagePriority, float availableResources, float demandVolume)
            => ResourceAllocation(triagePriority, availableResources, demandVolume, MedicalTriageParams.Default);

        /// <summary>最大救命数＝配分×救命可能性（0..1）。資源を回しても救命可能性が低ければ救えない。</summary>
        public static float LivesMaximized(float resourceAllocation, float salvageability, MedicalTriageParams p)
        {
            float alloc = Mathf.Clamp01(resourceAllocation);
            float s = Mathf.Clamp01(salvageability);
            return Mathf.Clamp01(alloc * s);
        }

        public static float LivesMaximized(float resourceAllocation, float salvageability)
            => LivesMaximized(resourceAllocation, salvageability, MedicalTriageParams.Default);

        /// <summary>待機中の悪化＝待機時間×悪化速度（0..1）。順番待ちの間に容態が悪化する。</summary>
        public static float WaitingDeterioration(float waitTime, float deteriorationRate, MedicalTriageParams p)
        {
            float wait = Mathf.Clamp01(waitTime);
            float rate = Mathf.Clamp01(deteriorationRate);
            return Mathf.Clamp01(wait * rate * p.deteriorationScale);
        }

        public static float WaitingDeterioration(float waitTime, float deteriorationRate)
            => WaitingDeterioration(waitTime, deteriorationRate, MedicalTriageParams.Default);

        /// <summary>
        /// トリアージの効果＝最大救命−待機悪化（0..1）。救えた数から待機による喪失を差し引いた正味の成果。
        /// 待機悪化が救命を上回れば 0 に張り付く。
        /// </summary>
        public static float TriageEffectiveness(float livesMaximized, float waitingDeterioration, MedicalTriageParams p)
        {
            float lives = Mathf.Clamp01(livesMaximized);
            float waste = Mathf.Clamp01(waitingDeterioration);
            return Mathf.Clamp01(lives - waste * p.waitingPenalty);
        }

        public static float TriageEffectiveness(float livesMaximized, float waitingDeterioration)
            => TriageEffectiveness(livesMaximized, waitingDeterioration, MedicalTriageParams.Default);
    }
}
