using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 配分の在庫への実適用（DIST-5・#2112・破壊的）。計算された pull/receive を `CommodityStock[]` へ反映する。
    /// test-first。
    /// </summary>
    public static class RedistributionApplyRules
    {
        /// <summary>各ノードへ −pull+receive を在庫へ加算（余剰は引かれ・不足は補充される）。</summary>
        public static void Apply(CommodityStock[] stocks, int commodityId, float[] pulls, float[] receives)
        {
            if (stocks == null) return;
            for (int i = 0; i < stocks.Length; i++)
            {
                if (stocks[i] == null) continue;
                float delta = 0f;
                if (pulls != null && i < pulls.Length) delta -= pulls[i];
                if (receives != null && i < receives.Length) delta += receives[i];
                stocks[i].Add(commodityId, delta);
            }
        }

        /// <summary>1対の移送＝from から amount を引き、to へ amount×(1−ロス) を加える。</summary>
        public static void Move(CommodityStock from, CommodityStock to, int commodityId, float amount, float loss)
        {
            if (from == null || to == null || amount <= 0f) return;
            from.Add(commodityId, -amount);
            to.Add(commodityId, amount * (1f - Mathf.Clamp01(loss)));
        }
    }
}
