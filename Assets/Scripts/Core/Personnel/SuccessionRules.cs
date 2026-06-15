using UnityEngine;

namespace Ginei
{
    /// <summary>継承・カリスマの日常化の調整係数（#812/#814/#816）。</summary>
    public readonly struct SuccessionParams
    {
        /// <summary>継承後の結束がこれ未満なら組織崩壊（fragmented）。</summary>
        public readonly float fragmentThreshold;
        /// <summary>急な中央集権化（リファクタリング bug3）の離反ペナルティ係数。</summary>
        public readonly float refactorPenalty;

        public SuccessionParams(float fragmentThreshold, float refactorPenalty)
        {
            this.fragmentThreshold = fragmentThreshold;
            this.refactorPenalty = refactorPenalty;
        }

        public static SuccessionParams Default => new SuccessionParams(0.4f, 0.5f);
    }

    /// <summary>継承の結果（#812/#816）。</summary>
    public readonly struct SuccessionResult
    {
        public readonly bool survived;     // 組織が存続したか
        public readonly bool fragmented;   // 崩壊したか
        public readonly float newCohesion; // 継承後の結束

        public SuccessionResult(bool survived, bool fragmented, float newCohesion)
        {
            this.survived = survived;
            this.fragmented = fragmented;
            this.newCohesion = newCohesion;
        }
    }

    /// <summary>
    /// カリスマの日常化＝英雄死後の組織存続の純ロジック（#812/#814/#816 SHINGEN・本線 SPINE-1 #795）。
    /// 継承時、結束のうち <b>制度化(institutionalization)分は制度が支えて残り</b>、<b>個人カリスマ分は
    /// 後継者の(正統性×カリスマ)だけ引き継がれる</b>。＝英雄に最適化された属人組織は死と共に滅び、
    /// 制度化した組織は創設者を超えて続く。3つのバグ（正統性/カリスマ再現の呪縛/急な中央集権化）を併設。
    /// 「英雄を失い劣った手駒で続ける」を遊べる形にする本丸。test-first。
    /// </summary>
    public static class SuccessionRules
    {
        /// <summary>存命中に制度化へ投資する（法・後継者育成・権限委譲＝日常化）。amount を加算（0..1にクランプ）。</summary>
        public static void InvestInstitution(Organization org, float amount)
        {
            if (org == null) return;
            org.institutionalization = Mathf.Clamp01(org.institutionalization + amount);
        }

        /// <summary>
        /// 継承を解決する。newCohesion = cohesion × [ inst + (1-inst)×(後継者正統性×後継者カリスマ) ]。
        /// 閾値割れで崩壊。org の cohesion / leaderCharisma / fragmented を更新する。
        /// </summary>
        public static SuccessionResult ResolveSuccession(Organization org, float successorLegitimacy, float successorCharisma, SuccessionParams p)
        {
            if (org == null) return new SuccessionResult(false, true, 0f);
            float inst = Mathf.Clamp01(org.institutionalization);
            float transfer = Mathf.Clamp01(successorLegitimacy) * Mathf.Clamp01(successorCharisma);
            float newCohesion = Mathf.Clamp01(org.cohesion * (inst + (1f - inst) * transfer));
            bool fragmented = newCohesion < p.fragmentThreshold;

            org.cohesion = newCohesion;
            org.leaderCharisma = Mathf.Clamp01(successorCharisma);
            org.fragmented = fragmented;
            return new SuccessionResult(!fragmented, fragmented, newCohesion);
        }

        public static SuccessionResult ResolveSuccession(Organization org, float successorLegitimacy, float successorCharisma)
            => ResolveSuccession(org, successorLegitimacy, successorCharisma, SuccessionParams.Default);

        /// <summary>
        /// 急な中央集権化（リファクタリング bug3）：旧来の自律分散を急に官僚制へ改修すると、
        /// 絆で繋がっていた国衆が外圧の瞬間に離反する。abruptness×externalPressure×係数 ぶん結束を削る。
        /// 削った量を返す。閾値割れで崩壊。
        /// </summary>
        public static float Refactor(Organization org, float abruptness, float externalPressure, SuccessionParams p)
        {
            if (org == null) return 0f;
            float loss = Mathf.Clamp01(abruptness) * Mathf.Clamp01(externalPressure) * p.refactorPenalty;
            org.cohesion = Mathf.Max(0f, org.cohesion - loss);
            if (org.cohesion < p.fragmentThreshold) org.fragmented = true;
            return loss;
        }

        /// <summary>
        /// カリスマ再現の呪縛（bug2・長篠）：後継者の正統性が低いほど、求心力を証明しようと
        /// 無謀な賭けに走る圧力(0..1)。1-正統性。高いほど「ここで退けない」過剰最適化エラーに陥りやすい。
        /// </summary>
        public static float MythPressure(float successorLegitimacy)
            => Mathf.Clamp01(1f - Mathf.Clamp01(successorLegitimacy));

        /// <summary>
        /// 正統性コンフリクト（bug1）：後継者の正統性が低いほど、先代の宿老が従わず結束が下がる係数(0..1)。
        /// 継承の transfer を弱める方向に使う想定（ResolveSuccession の successorLegitimacy がこれを担う）。
        /// </summary>
        public static float LegitimacyConflict(float successorLegitimacy)
            => Mathf.Clamp01(1f - Mathf.Clamp01(successorLegitimacy));
    }
}
