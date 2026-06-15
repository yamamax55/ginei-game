using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 惑星の総需要算出（POPDEM-2・#2042・#153/#1969 連携・純ロジック）。
    /// カテゴリ別総需要＝人口#153×1人当たり原単位×購買力係数。必需は硬直（所得に依らずほぼ一定＝食べる必要）、
    /// 上位財（快適/奢侈）は弾力的（高賃金#1969ほど需要増・困窮で消える）。惑星×カテゴリの集約。test-first。
    /// </summary>
    public static class ConsumptionDemandRules
    {
        /// <summary>
        /// カテゴリ別総需要＝人口×原単位×max(0, 1+弾力性×(購買力−1))。
        /// 購買力1.0で基準、必需は弾力0.1ゆえ困窮でも約9割要る、奢侈は弾力1.0ゆえ購買力0で需要0・購買力2で倍。
        /// </summary>
        public static float TotalDemand(float population, ConsumptionCategory c, float purchasingPower)
        {
            float perCap = ConsumptionGoodsRules.PerCapitaDemand(c);
            float elasticity = ConsumptionGoodsRules.IncomeElasticity(c);
            float ppFactor = Mathf.Max(0f, 1f + elasticity * (purchasingPower - 1f));
            return Mathf.Max(0f, population) * perCap * ppFactor;
        }
    }
}
