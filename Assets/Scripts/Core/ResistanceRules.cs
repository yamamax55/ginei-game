using UnityEngine;

namespace Ginei
{
    /// <summary>占領地レジスタンスの調整係数。</summary>
    public readonly struct ResistanceParams
    {
        /// <summary>未統合（integration=0）の占領地で立つ抵抗強度の上限。</summary>
        public readonly float maxIntensity;
        /// <summary>抵抗強度1のときの産出・補給の漏れ（破壊工作の被害率）。</summary>
        public readonly float sabotageLossRatio;
        /// <summary>抵抗強度1のときの情報漏れ（占領軍の動きが敵へ筒抜けになる度合い）。</summary>
        public readonly float intelLeakRatio;
        /// <summary>弾圧が抵抗強度を削る速度（per dt・弾圧努力1のとき）。</summary>
        public readonly float crackdownRate;
        /// <summary>弾圧が統合度を逆に削る副作用（怨恨＝per dt・弾圧努力1のとき）。</summary>
        public readonly float crackdownResentment;
        /// <summary>懐柔が統合度を進める速度（per dt・懐柔努力1のとき）。</summary>
        public readonly float conciliationRate;

        public ResistanceParams(float maxIntensity, float sabotageLossRatio, float intelLeakRatio,
                                float crackdownRate, float crackdownResentment, float conciliationRate)
        {
            this.maxIntensity = Mathf.Clamp01(maxIntensity);
            this.sabotageLossRatio = Mathf.Clamp01(sabotageLossRatio);
            this.intelLeakRatio = Mathf.Clamp01(intelLeakRatio);
            this.crackdownRate = Mathf.Max(0f, crackdownRate);
            this.crackdownResentment = Mathf.Max(0f, crackdownResentment);
            this.conciliationRate = Mathf.Max(0f, conciliationRate);
        }

        /// <summary>既定＝強度上限0.8・破壊被害0.3・情報漏れ0.5・弾圧0.2・怨恨0.05・懐柔0.05。</summary>
        public static ResistanceParams Default => new ResistanceParams(0.8f, 0.3f, 0.5f, 0.2f, 0.05f, 0.05f);
    }

    /// <summary>
    /// 占領地レジスタンスの純ロジック。未統合の占領地では抵抗が立ち、破壊工作（産出・補給の漏れ）と
    /// 情報漏れ（占領軍の動きが敵へ筒抜け）で統治コストを課す。対処は二択のジレンマ＝
    /// **弾圧**は抵抗を速く削るが怨恨で統合を後退させ（火種は残る）、**懐柔**は統合を進めて抵抗の土壌
    /// そのものを痩せさせる（遅いが根治）。統合度の時間収束は <see cref="GovernanceRules"/>（read-only）、
    /// 亡命政権からの支援は <see cref="GovernmentInExileRules.ResistanceSupport"/> が出す（加算入力）。
    /// 乱数なし・決定論。純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class ResistanceRules
    {
        /// <summary>
        /// 抵抗強度（0..maxIntensity）＝未統合分（1−integration）×上限＋外部支援 exileSupport(0..1) の上乗せ
        /// （合計は上限でクランプ）。統合が完成すれば抵抗の土壌は消える。
        /// </summary>
        public static float Intensity(float integration, float exileSupport, ResistanceParams p)
        {
            float ground = (1f - Mathf.Clamp01(integration)) * p.maxIntensity;
            float boosted = ground * (1f + Mathf.Clamp01(exileSupport));
            return Mathf.Min(p.maxIntensity, boosted);
        }

        public static float Intensity(float integration, float exileSupport)
            => Intensity(integration, exileSupport, ResistanceParams.Default);

        /// <summary>破壊工作の被害率（0..sabotageLossRatio）＝産出・補給に掛ける漏れ。</summary>
        public static float SabotageLoss(float intensity, ResistanceParams p)
        {
            return Mathf.Clamp01(intensity) * p.sabotageLossRatio;
        }

        public static float SabotageLoss(float intensity) => SabotageLoss(intensity, ResistanceParams.Default);

        /// <summary>情報漏れ（0..intelLeakRatio）＝敵の偵察精度への加算入力（占領軍に秘密はない）。</summary>
        public static float IntelLeak(float intensity, ResistanceParams p)
        {
            return Mathf.Clamp01(intensity) * p.intelLeakRatio;
        }

        public static float IntelLeak(float intensity) => IntelLeak(intensity, ResistanceParams.Default);

        /// <summary>
        /// 弾圧の1tick：抵抗強度は crackdownRate×努力×dt で速く削れる。**統合度への怨恨副作用**は
        /// <see cref="CrackdownResentment"/> で別途差し引く（呼び出し側が integration を更新）。
        /// </summary>
        public static float CrackdownTick(float intensity, float effort, float dt, ResistanceParams p)
        {
            float cut = p.crackdownRate * Mathf.Clamp01(effort) * Mathf.Max(0f, dt);
            return Mathf.Max(0f, Mathf.Clamp01(intensity) - cut);
        }

        public static float CrackdownTick(float intensity, float effort, float dt)
            => CrackdownTick(intensity, effort, dt, ResistanceParams.Default);

        /// <summary>弾圧の怨恨＝統合度の後退量（per dt）。鎮めた分だけ憎まれる＝火種は地下に潜る。</summary>
        public static float CrackdownResentment(float effort, float dt, ResistanceParams p)
        {
            return p.crackdownResentment * Mathf.Clamp01(effort) * Mathf.Max(0f, dt);
        }

        public static float CrackdownResentment(float effort, float dt)
            => CrackdownResentment(effort, dt, ResistanceParams.Default);

        /// <summary>懐柔の1tick後の統合度（0..1）。遅いが土壌から枯らす根治＝強度は Intensity 経由で痩せる。</summary>
        public static float ConciliationTick(float integration, float effort, float dt, ResistanceParams p)
        {
            float gain = p.conciliationRate * Mathf.Clamp01(effort) * Mathf.Max(0f, dt);
            return Mathf.Clamp01(Mathf.Clamp01(integration) + gain);
        }

        public static float ConciliationTick(float integration, float effort, float dt)
            => ConciliationTick(integration, effort, dt, ResistanceParams.Default);
    }
}
