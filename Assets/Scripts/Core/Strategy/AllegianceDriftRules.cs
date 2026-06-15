using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 提督個人の忠誠の動態＝下剋上の純ロジック（CDR-2 #2312）。静的な旗幟（`FleetStrength.loyalty`#817）に対し、
    /// 提督個人の君主への忠誠が<b>時間で動く</b>：厚遇（論功行賞 #900-905）で上がり、不遇・高い功名心で下がる。
    /// 低忠誠は離反、低忠誠×高功名心は簒奪（下剋上）。`RenownRules.DefectionResistance`（ADM-3）と噛み合う。
    /// 収束は基準値パターン（GovernanceRules 流儀）。決定論・test-first。
    /// </summary>
    public static class AllegianceDriftRules
    {
        /// <summary>これ未満で離反しうる忠誠。</summary>
        public const float DefectThreshold = 0.3f;
        /// <summary>これ未満かつ高功名心で簒奪（下剋上）しうる忠誠。</summary>
        public const float UsurpThreshold = 0.2f;
        /// <summary>簒奪に要する功名心。</summary>
        public const int UsurpAmbition = 80;

        /// <summary>
        /// 忠誠の目標値。厚遇 treatment(0..1) が高いほど高く、功名心が50超なら下げる（功名の士は御しにくい）。
        /// </summary>
        public static float LoyaltyTarget(float treatment, int ambition)
        {
            float t = Mathf.Clamp01(treatment);
            float ambitionPenalty = Mathf.Max(0, ambition - 50) / 200f;
            return Mathf.Clamp01(t - ambitionPenalty);
        }

        /// <summary>忠誠を目標へ漸近させる（1tickの寄り幅は rate×dt をクランプ）。</summary>
        public static float Drift(float currentLoyalty, float target, float rate, float dt)
        {
            float cur = Mathf.Clamp01(currentLoyalty);
            float tgt = Mathf.Clamp01(target);
            float k = Mathf.Clamp01(Mathf.Max(0f, rate) * Mathf.Max(0f, dt));
            return Mathf.Clamp01(Mathf.Lerp(cur, tgt, k));
        }

        /// <summary>離反しうるか（忠誠がしきい値未満）。武名による抑制(ADM-3)は呼び出し側で忠誠に反映する想定。</summary>
        public static bool WouldDefect(float loyalty)
            => Mathf.Clamp01(loyalty) < DefectThreshold;

        /// <summary>簒奪（下剋上）しうるか（忠誠が簒奪閾値未満かつ高功名心）。</summary>
        public static bool WouldUsurp(float loyalty, int ambition)
            => Mathf.Clamp01(loyalty) < UsurpThreshold && ambition >= UsurpAmbition;
    }
}
