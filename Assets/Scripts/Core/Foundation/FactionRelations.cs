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
        /// <summary>
        /// 現在の外交状態（外交EPIC #189・任意）。設定されていれば交戦/同盟/不可侵/属国が敵対判定を駆動する。
        /// null（既定）なら従来どおり FactionData/enum で判定＝<b>後方互換</b>。戦略セッション開始時に張り替える想定。
        /// </summary>
        public static DiplomacyState ActiveDiplomacy;

        /// <summary>勢力 a と b が敵対するか（外交状態優先→FactionData→enum の順で判定）。</summary>
        public static bool IsHostile(FactionData aData, Faction aLegacy, FactionData bData, Faction bLegacy)
        {
            // 外交状態が明示（交戦/同盟等）なら最優先。平時/レコード無しは null＝従来判定へフォールバック。
            if (ActiveDiplomacy != null && aData != null && bData != null)
            {
                bool? diplomatic = DiplomacyRules.IsHostile(ActiveDiplomacy, aData.factionName, bData.factionName);
                if (diplomatic.HasValue) return diplomatic.Value;
            }
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
