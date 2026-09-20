using UnityEngine;

namespace Ginei
{
    /// <summary>艦種差を保ちながら、航行中に配下艦が旗艦から永久に脱落しない追従速度を決める。</summary>
    public static class EscortFollowRules
    {
        public const float DefaultMinimumCatchUpRatio = 1.05f;

        public static float EffectiveCatchUpMultiplier(float catchUpRatio, float classSpeedMultiplier,
                                                       float minimumCatchUpRatio = DefaultMinimumCatchUpRatio)
        {
            float requested = Mathf.Max(0.1f, catchUpRatio) * Mathf.Max(0.1f, classSpeedMultiplier);
            return Mathf.Max(Mathf.Max(1f, minimumCatchUpRatio), requested);
        }
    }
}
