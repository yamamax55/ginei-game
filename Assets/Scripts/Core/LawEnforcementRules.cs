using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 取締りと公共秩序の純ロジック（LAW-4・#2126）。警察力の人口あたり capacity と、犯罪を抑えた公共秩序水準を出す。test-first。
    /// </summary>
    public static class LawEnforcementRules
    {
        public const float DefaultSuppressionFactor = 0.5f; // 取締りが犯罪を抑える強さ

        /// <summary>取締り力＝clamp01(警察力/(人口×1人あたり必要量))。人口0以下は1。</summary>
        public static float EnforcementCapacity(float policeForce, float population, float perCapitaNeed)
        {
            float need = Mathf.Max(0f, population) * Mathf.Max(0f, perCapitaNeed);
            return need <= 0f ? 1f : Mathf.Clamp01(Mathf.Max(0f, policeForce) / need);
        }

        /// <summary>公共秩序＝1−実効犯罪（取締りで犯罪を抑えた残り）。</summary>
        public static float OrderLevel(float crimePressure, float enforcement, float suppressionFactor = DefaultSuppressionFactor)
            => Mathf.Clamp01(1f - CrimeRules.EffectiveCrime(crimePressure, enforcement, suppressionFactor));
    }
}
