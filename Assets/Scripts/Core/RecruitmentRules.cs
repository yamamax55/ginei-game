using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 人材登用ループの純ロジック（CDR-3 #2313）。学校（士官学校/科挙/大学/王家）以外のロスター供給＝
    /// 在野の埋もれた名将の発掘・捕虜/敗将・敵将の登用。説得は思想差・厚遇・相性（ADM-4）で決まり、
    /// 三顧の礼（反復）で成功率が累積する。決定論（roll を外から注入）・test-first。
    /// </summary>
    public static class RecruitmentRules
    {
        /// <summary>在野人材を発見する確率（情報能力で上昇）。情報50で base、100で1.5×、0で0.5×。</summary>
        public static float DiscoveryChance(float intelligence, float baseRate)
            => Mathf.Clamp01(Mathf.Max(0f, baseRate) * (0.5f + Mathf.Clamp(intelligence, 0f, 100f) / 100f));

        /// <summary>
        /// 説得（登用）成功率。厚遇 hospitality(0..1)・相性 affinity(0..1) で上がり、思想差 ideologyDistance(0..1) で下がる。
        /// </summary>
        public static float PersuasionChance(float ideologyDistance, float hospitality, float affinity)
        {
            float c = 0.2f
                + Mathf.Clamp01(hospitality) * 0.3f
                + Mathf.Clamp01(affinity) * 0.3f
                - Mathf.Clamp01(ideologyDistance) * 0.4f;
            return Mathf.Clamp01(c);
        }

        /// <summary>1回の説得が成立するか（roll∈[0,1) を注入）。</summary>
        public static bool Persuade(float chance, float roll)
            => roll < Mathf.Clamp01(chance);

        /// <summary>
        /// 三顧の礼＝反復説得の累積成功率（1回の成功率を attempts 回試みたときに少なくとも1回成立する確率）。
        /// = 1 - (1-chance)^attempts。誠意を重ねるほど靡く。
        /// </summary>
        public static float RepeatedPersuasionChance(float chance, int attempts)
        {
            float c = Mathf.Clamp01(chance);
            int n = Mathf.Max(0, attempts);
            return Mathf.Clamp01(1f - Mathf.Pow(1f - c, n));
        }
    }
}
