using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 行政物資不足が統治へ及ぼす効果（STATEDEM-5・#2077・実効値パターン）。
    /// 行政物資が枯渇すると統治が回らず、安定度#109・支持#113・産出#93 が同時に落ちる（悪循環）。test-first。
    /// </summary>
    public static class StateConsumptionEffectRules
    {
        public const float MaxStabilityPenalty = 20f; // 充足0で安定度を最大これだけ削る
        public const float SupportScale = 15f;        // 充足0で支持をこれだけ削る
        public const float MinOutputFactor = 0.5f;    // 充足0でも産出はこの倍率を下限に残す

        /// <summary>安定度#109 ペナルティ＝(1−充足)×最大ペナルティ（充足1で0）。</summary>
        public static float StabilityPenalty(float fulfillment)
            => (1f - Mathf.Clamp01(fulfillment)) * MaxStabilityPenalty;

        /// <summary>支持#113 の増減＝−(1−充足)×スケール（不足で低下）。</summary>
        public static float SupportDelta(float fulfillment)
            => -(1f - Mathf.Clamp01(fulfillment)) * SupportScale;

        /// <summary>産出#93 倍率＝Lerp(最小, 1, 充足)（不足で生産も落ちる）。</summary>
        public static float OutputFactor(float fulfillment)
            => Mathf.Lerp(MinOutputFactor, 1f, Mathf.Clamp01(fulfillment));
    }
}
