using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 人物の要求物資・消費需要（PFIN-2・#2056・#14/#1969/#181 連携・純ロジック）。
    /// 階級#14 が高いほど高い生活水準を期待＝消費需要が大きい。俸給#1969 が消費を支え、足りないと生活水準#181 が下がる。test-first。
    /// </summary>
    public static class PersonDemandRules
    {
        public const float RankNeedFactor = 0.1f; // 階級1段あたりの需要増（高位ほど見栄）

        /// <summary>消費需要＝基準需要×(1+階級#14×係数)。高位の人物ほど期待消費が大きい。</summary>
        public static float ConsumptionNeed(int rankTier, float baseNeed)
            => Mathf.Max(0f, baseNeed) * (1f + Mathf.Max(0, rankTier) * RankNeedFactor);

        /// <summary>生活水準#181＝min(1, 支出/需要)。期待に対する充足（高位で低俸給だと見栄が苦しい）。需要0以下は1。</summary>
        public static float LivingStandard(float spending, float need)
            => need <= 0f ? 1f : Mathf.Clamp01(Mathf.Max(0f, spending) / need);
    }
}
