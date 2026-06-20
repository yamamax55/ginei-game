using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 法と秩序の効果と、法の支配なき取締り＝抑圧の罠（LAW-5・#2126・実効値パターン）。
    /// 秩序は安定#109/支持#113 を上げるが、<b>法の支配が低いほど取締りは抑圧化</b>し人権侵害→正統性/支持を蝕む（トレードオフ）。test-first。
    /// </summary>
    public static class LawOrderEffectRules
    {
        /// <summary>安定度#109 への増減＝(秩序−0.5)×スケール。</summary>
        public static float StabilityDelta(float orderLevel, float scale)
            => (Mathf.Clamp01(orderLevel) - 0.5f) * Mathf.Max(0f, scale);

        /// <summary>支持#113 への増減＝(秩序−0.5)×スケール。</summary>
        public static float SupportDelta(float orderLevel, float scale)
            => (Mathf.Clamp01(orderLevel) - 0.5f) * Mathf.Max(0f, scale);

        /// <summary>抑圧度＝取締り×(1−法の支配指数)。法の支配が低いほど取締りが抑圧（恣意的弾圧）になる。</summary>
        public static float RepressionLevel(float enforcement, float ruleOfLawIndex)
            => Mathf.Clamp01(enforcement) * (1f - Mathf.Clamp01(ruleOfLawIndex));

        /// <summary>抑圧の支持/正統性ペナルティ＝抑圧度×スケール（人権侵害→支持↓）。正値で返す（呼び側が減算）。</summary>
        public static float RepressionSupportPenalty(float repressionLevel, float scale)
            => Mathf.Clamp01(repressionLevel) * Mathf.Max(0f, scale);
    }
}
