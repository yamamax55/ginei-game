using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 法の支配が及ぼす効果（LAW-2・#2126・実効値パターン）。高い法の支配は正統性・経済信頼を上げ腐敗を抑え、
    /// 同時に統治者の恣意的権力を縛る。法治どまり（権力を縛らない）ほど恣意が残り腐敗が育つ。test-first。
    /// </summary>
    public static class RuleOfLawEffectRules
    {
        /// <summary>正統性への増減＝(指数−0.5)×スケール（0.5基準＝高い法の支配で正統性↑・低いと↓）。</summary>
        public static float LegitimacyDelta(float rolIndex, float scale)
            => (Mathf.Clamp01(rolIndex) - 0.5f) * Mathf.Max(0f, scale);

        /// <summary>腐敗抵抗＝指数（高いほど腐敗の伸びを抑える倍率＝0..1）。</summary>
        public static float CorruptionResistance(float rolIndex)
            => Mathf.Clamp01(rolIndex);

        /// <summary>経済信頼＝Lerp(min, 1, 指数)（予測可能性→投資/経済の信頼）。</summary>
        public static float EconomicConfidence(float rolIndex, float minFactor)
            => Mathf.Lerp(Mathf.Clamp01(minFactor), 1f, Mathf.Clamp01(rolIndex));

        /// <summary>恣意的権力の余地＝1−指数（高い法の支配は統治者を縛る＝恣意↓）。</summary>
        public static float ArbitraryPowerFactor(float rolIndex)
            => 1f - Mathf.Clamp01(rolIndex);
    }
}
