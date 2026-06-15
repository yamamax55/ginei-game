using UnityEngine;

namespace Ginei
{
    /// <summary>立憲主義の調整係数（#170）。権力分立・法の支配の制約の重みと、権利保護の正統性換算。</summary>
    public readonly struct ConstitutionParams
    {
        /// <summary>権力分立1あたりの基準権力の逓減率（0..1）。</summary>
        public readonly float separationWeight;
        /// <summary>法の支配1あたりの基準権力の逓減率（0..1）。</summary>
        public readonly float ruleOfLawWeight;
        /// <summary>逓減後に残す最低権力の割合（完全な無力化を防ぐ）。</summary>
        public readonly float minAuthorityRatio;
        /// <summary>権利保護1あたりの正統性ボーナス。</summary>
        public readonly float rightsLegitimacyGain;

        public ConstitutionParams(float separationWeight, float ruleOfLawWeight, float minAuthorityRatio, float rightsLegitimacyGain)
        {
            this.separationWeight = separationWeight;
            this.ruleOfLawWeight = ruleOfLawWeight;
            this.minAuthorityRatio = minAuthorityRatio;
            this.rightsLegitimacyGain = rightsLegitimacyGain;
        }

        public static ConstitutionParams Default => new ConstitutionParams(0.4f, 0.3f, 0.25f, 0.3f);
    }

    /// <summary>
    /// 立憲主義の純ロジック（#170・test-first）。権力者の意志を憲法的拘束（権力分立／権利保護／法の支配）で
    /// 制約する唯一の窓口。基準権力（<c>baseAuthority</c>）は上書きせず＝実効値パターンで逓減した値を返す：
    /// 権力分立と法の支配が強いほど単独の権力行使を抑え（<see cref="ConstrainedAuthority"/>）、
    /// 権利保護は正統性を底上げする（<see cref="RightsLegitimacy"/>）。
    /// 立憲君主制の判定（<see cref="IsConstitutionalMonarchy"/>）も提供。値は徹底して clamp する。
    /// </summary>
    public static class ConstitutionRules
    {
        // --- 立憲君主制の既定しきい値（法の支配＋権利保護がこの水準以上で「立憲的」とみなす） ---
        public const float DefaultMonarchyThreshold = 0.5f;

        /// <summary>
        /// 基準権力を憲法的拘束で逓減した実効権力（基準値は非破壊）。
        /// 権力分立と法の支配の合成逓減を 0..1 にクランプし、<c>minAuthorityRatio</c> を下限に残す。
        /// </summary>
        public static float ConstrainedAuthority(float baseAuthority, Constitution c, ConstitutionParams prm)
        {
            if (c == null) return baseAuthority;
            float reduction = prm.separationWeight * Mathf.Clamp01(c.powerSeparation)
                            + prm.ruleOfLawWeight * Mathf.Clamp01(c.ruleOfLaw);
            reduction = Mathf.Clamp01(reduction);
            float ratio = Mathf.Lerp(1f, Mathf.Clamp01(prm.minAuthorityRatio), reduction);
            return baseAuthority * ratio;
        }

        public static float ConstrainedAuthority(float baseAuthority, Constitution c)
            => ConstrainedAuthority(baseAuthority, c, ConstitutionParams.Default);

        /// <summary>権利保護→正統性ボーナス（権利を守る統治ほど正統性が高い）。0..rightsLegitimacyGain。</summary>
        public static float RightsLegitimacy(Constitution c, ConstitutionParams prm)
        {
            if (c == null) return 0f;
            return Mathf.Clamp01(c.rightsProtection) * prm.rightsLegitimacyGain;
        }

        public static float RightsLegitimacy(Constitution c) => RightsLegitimacy(c, ConstitutionParams.Default);

        /// <summary>
        /// 立憲君主制か（法の支配と権利保護がともにしきい値以上＝君主の権力が法と権利で縛られている）。
        /// </summary>
        public static bool IsConstitutionalMonarchy(Constitution c, float threshold)
        {
            if (c == null) return false;
            return Mathf.Clamp01(c.ruleOfLaw) >= threshold && Mathf.Clamp01(c.rightsProtection) >= threshold;
        }

        public static bool IsConstitutionalMonarchy(Constitution c) => IsConstitutionalMonarchy(c, DefaultMonarchyThreshold);
    }
}
