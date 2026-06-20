using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 神権国家（宗教国家）の統治係数の調整値（純構造体・既定 .Default）。マジックナンバーを1か所へ集約する。
    /// </summary>
    public readonly struct TheocracyParams
    {
        /// <summary>国教と住民信仰が一致するときの正統性/安定ボーナス上限（devotion=1 でこの値）。</summary>
        public readonly float legitimacyBonus;
        /// <summary>異端の住民を支配することの安定 penalty 上限（異端 devotion=1 でこの値）。</summary>
        public readonly float heresyPenalty;
        /// <summary>戦闘性（militancy）が異端弾圧で penalty を軽減できる最大割合。</summary>
        public readonly float suppressionRelief;
        /// <summary>寛容度（tolerance）が異種信仰 penalty を緩和できる最大割合。</summary>
        public readonly float toleranceRelief;

        public TheocracyParams(float legitimacyBonus, float heresyPenalty, float suppressionRelief, float toleranceRelief)
        {
            this.legitimacyBonus = legitimacyBonus;
            this.heresyPenalty = heresyPenalty;
            this.suppressionRelief = suppressionRelief;
            this.toleranceRelief = toleranceRelief;
        }

        /// <summary>既定の調整値（GovernanceRules の安定度スケール 0..100 に乗る加点/減点）。</summary>
        public static TheocracyParams Default => new TheocracyParams(
            legitimacyBonus: 20f,
            heresyPenalty: 25f,
            suppressionRelief: 0.6f,
            toleranceRelief: 0.6f);
    }

    /// <summary>
    /// 神権国家（宗教国家・#172-175）の統治修正子（GovernanceRules の薄い国策レイヤーに乗る係数プロバイダ・純ロジック test-first）。
    /// 国教 <see cref="ReligionDef"/>＋住民信仰の強さ＋異端の有無から、安定度への加点/減点を返す。
    /// 同信仰なら正統性で安定+（信仰が強いほど大）／異種・異端を支配すると安定−（戦闘性の弾圧と寛容で緩和）。
    /// 異端/正統の一次判定は <see cref="ReligionRules.IsHeresy"/> へ委譲し再実装しない＝state を持たない純粋な factor provider。
    /// 返す加点は GovernanceRules.EquilibriumStability の adminBonus 経路（または ideologyMod）へ渡す想定。
    /// Game層非依存＝Core 純ロジック・null安全。
    /// </summary>
    public static class TheocracyRules
    {
        /// <summary>
        /// 国教と住民信仰の一致による正統性安定ボーナス(0..legitimacyBonus)。
        /// 一致しないなら0。一致するなら住民信仰の強さ(devotion)に比例（熱心な信徒ほど統治が安定）。
        /// </summary>
        public static float LegitimacyBonus(string officialFaith, string localFaith, float localDevotion, TheocracyParams p)
        {
            if (string.IsNullOrEmpty(officialFaith) || officialFaith != localFaith) return 0f;
            return p.legitimacyBonus * Mathf.Clamp01(localDevotion);
        }

        /// <summary>
        /// 異信仰（異端）の住民を支配することの安定 penalty(≤0)。
        /// 国教と住民信仰が別物（<see cref="ReligionRules.IsHeresy"/>）なら、住民の信仰の強さに比例した不満。
        /// 国教の戦闘性(militancy)は弾圧で penalty を軽減し、寛容度(tolerance)は共存で penalty を緩和する。
        /// 同信仰・無信仰なら 0。返り値は ≤0（安定への減点）。
        /// </summary>
        public static float HeresyPenalty(ReligionDef officialDef, string localFaith, float localDevotion, TheocracyParams p)
        {
            if (!ReligionRules.IsHeresy(localFaith, officialDef.name)) return 0f;
            float raw = p.heresyPenalty * Mathf.Clamp01(localDevotion);
            // 戦闘的な国教は異端を弾圧して不満を抑える／寛容な国教は共存で不満を抑える（緩和は加算でなく相乗）。
            float suppress = 1f - p.suppressionRelief * officialDef.militancy;
            float tolerate = 1f - p.toleranceRelief * officialDef.tolerance;
            float relief = Mathf.Clamp01(suppress * tolerate);
            return -(raw * relief);
        }

        /// <summary>
        /// 神権統治の安定度への純加点（正統性ボーナス＋異端 penalty の合算）。
        /// 国教と住民信仰が一致＝正の加点、異端＝負の加点。GovernanceRules の adminBonus/ideologyMod 経路へ渡す想定。
        /// <paramref name="officialDef"/> は国教の教義（<see cref="ReligionCatalog.For"/> で解決）。null相当（無信仰国家）は加点0。
        /// </summary>
        public static float StabilityModifier(ReligionDef officialDef, string localFaith, float localDevotion, TheocracyParams p)
        {
            if (string.IsNullOrEmpty(officialDef.name)) return 0f; // 国教なし＝神権でない＝従来通り
            float bonus = LegitimacyBonus(officialDef.name, localFaith, localDevotion, p);
            float penalty = HeresyPenalty(officialDef, localFaith, localDevotion, p);
            return bonus + penalty;
        }

        /// <summary>
        /// 国教名で安定度加点を引くショートカット（<see cref="ReligionCatalog.For"/> で教義解決→<see cref="StabilityModifier"/>）。
        /// 神権でない（officialFaith 空）なら 0。
        /// </summary>
        public static float StabilityModifier(string officialFaith, string localFaith, float localDevotion, TheocracyParams p)
            => StabilityModifier(ReligionCatalog.For(officialFaith), localFaith, localDevotion, p);

        /// <summary>
        /// 神権国家として成立しているか（国教があり、その国教が住民へ十分浸透している）。
        /// officialFaith が空＝世俗国家＝false。devotion がしきい値以上で神権が機能する。
        /// </summary>
        public static bool IsTheocratic(string officialFaith, float rulerDevotion, float threshold = 0.5f)
            => !string.IsNullOrEmpty(officialFaith) && Mathf.Clamp01(rulerDevotion) >= Mathf.Clamp01(threshold);
    }
}
