using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 中古品・リユース（買取再販）のロジック（業種細分化・小売 #2017 のリユースサブ業種・#2025・純ロジック・唯一の窓口）：買取原価（REUSE-1）／
    /// 再販マージン（REUSE-2）／在庫消化率（REUSE-3）／利益（REUSE-4）。
    /// 消費者から安く買い取り高く売り直す＝仕入れ原価を自分で決められる（買取査定）が、売れ残りは死蔵在庫になる。小売#2017の逆流（消費の出口の再利用）。マクロ近似。test-first。
    /// </summary>
    public static class ReuseRules
    {
        /// <summary>買取原価＝買取点数×1点あたり買取価格（査定で仕入れ値を自分で決める＝マージンの源泉）。</summary>
        public static float BuybackCost(int items, float buybackPricePerItem)
            => Mathf.Max(0, items) * Mathf.Max(0f, buybackPricePerItem);

        /// <summary>再販マージン＝(再販価格−買取価格)/再販価格（安く買い高く売る＝中古の高粗利）。再販価格0以下は0。</summary>
        public static float ResaleMargin(float resalePrice, float buybackPrice)
            => resalePrice <= 0f ? 0f : (resalePrice - Mathf.Max(0f, buybackPrice)) / resalePrice;

        /// <summary>在庫消化率＝再販数/仕入数（売れ残りは死蔵在庫＝査定の目利きが命）。仕入数0以下は0。</summary>
        public static float SellThroughInventory(float resoldItems, float acquiredItems)
            => acquiredItems <= 0f ? 0f : Mathf.Clamp01(Mathf.Max(0f, resoldItems) / acquiredItems);

        /// <summary>リユース利益＝再販収入−買取原価−固定費。</summary>
        public static float ReuseProfit(float resaleRevenue, float buybackCost, float fixedCost)
            => resaleRevenue - Mathf.Max(0f, buybackCost) - Mathf.Max(0f, fixedCost);
    }
}
