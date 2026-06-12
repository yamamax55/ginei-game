namespace Ginei
{
    /// <summary>域内配送の結果（DIST-6・#2112）。総余剰/総不足/輸送可能/配送/充足率。</summary>
    public struct DistributionResult
    {
        public float totalSurplus;
        public float totalDeficit;
        public float transportable; // 回廊容量で律速した実輸送可能量
        public float delivered;     // 輸送ロス後の配送量
        public float fillRate;      // 領域の不足充足率
    }

    /// <summary>
    /// 域内配送のオーケストレータ（DIST-6・#2112 配線・純ロジック）。連結領域の在庫群に対し、
    /// 余剰/不足算定→プール→配分→在庫へ実適用を1パスで行う薄い窓口。各段は DIST-1〜5 へ委譲。test-first。
    /// </summary>
    public static class RegionalDistributionTickRules
    {
        /// <summary>
        /// 連結領域の在庫群で品目を再配分（破壊的）。各ノードの生産＝stock.Get、需要＝demand[]。
        /// 余剰惑星から引き、不足惑星へ配送（回廊容量 throughputCap で律速・loss で目減り）。結果を返す。
        /// </summary>
        public static DistributionResult Distribute(CommodityStock[] stocks, int commodityId, float[] demand,
            float throughputCap, float loss)
        {
            var res = new DistributionResult();
            int n = stocks?.Length ?? 0;
            if (n == 0) return res;

            var surpluses = new float[n];
            var deficits = new float[n];
            for (int i = 0; i < n; i++)
            {
                float prod = stocks[i] != null ? stocks[i].Get(commodityId) : 0f;
                float dem = (demand != null && i < demand.Length) ? demand[i] : 0f;
                surpluses[i] = SupplyBalanceRules.Surplus(prod, dem);
                deficits[i] = SupplyBalanceRules.Deficit(prod, dem);
                res.totalSurplus += surpluses[i];
                res.totalDeficit += deficits[i];
            }

            res.transportable = DistributionPoolRules.Transportable(res.totalSurplus, throughputCap);
            res.delivered = DistributionPoolRules.Delivered(res.transportable, loss);

            float[] pulls = PoolAllocationRules.Pulls(surpluses, res.transportable);
            float[] receives = PoolAllocationRules.Receives(deficits, res.delivered);
            RedistributionApplyRules.Apply(stocks, commodityId, pulls, receives);

            res.fillRate = DistributionPoolRules.FillRate(res.delivered, res.totalDeficit);
            return res;
        }
    }
}
