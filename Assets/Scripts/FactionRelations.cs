namespace Ginei
{
    /// <summary>
    /// 勢力間の敵対判定を一箇所に集約する静的ヘルパー。
    /// 双方に FactionData があれば FactionData.IsHostileTo で判定し、
    /// 無ければ旧 enum Faction の違いで判定する（後方互換＝陣営違い＝敵）。
    /// 「陣営違い＝敵」の判定を各所に重複実装しないこと（ここを参照する）。
    /// </summary>
    public static class FactionRelations
    {
        /// <summary>勢力 a と b が敵対するか（FactionData 優先、無ければ enum で判定）。</summary>
        public static bool IsHostile(FactionData aData, Faction aLegacy, FactionData bData, Faction bLegacy)
        {
            if (aData != null && bData != null) return aData.IsHostileTo(bData);
            return aLegacy != bLegacy;
        }

        /// <summary>2 つの個艦が敵対するか。</summary>
        public static bool IsHostile(IShipTarget a, IShipTarget b)
        {
            if (a == null || b == null) return false;
            return IsHostile(a.FactionData, a.Faction, b.FactionData, b.Faction);
        }

        /// <summary>個艦 a が、勢力(aData/aLegacy)から見て敵対する相手か。</summary>
        public static bool IsHostile(FactionData viewerData, Faction viewerLegacy, IShipTarget other)
        {
            if (other == null) return false;
            return IsHostile(viewerData, viewerLegacy, other.FactionData, other.Faction);
        }
    }
}
