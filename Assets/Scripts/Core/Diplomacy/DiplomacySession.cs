using System.Collections.Generic;

namespace Ginei
{
    /// <summary>
    /// 外交セッション（DIPLO-1・#2119・static ホルダー）。`DiplomacyState` を保持し、敵対判定の窓口
    /// `FactionRelations.ActiveDiplomacy` に張る＝外交状態が会戦の敵対判定を駆動する。`StrategySession` 同型。
    /// 未初期化（State=null）なら従来の enum/FactionData 判定のまま（後方互換）。Core 純ロジック・test-first。
    /// </summary>
    public static class DiplomacySession
    {
        public static DiplomacyState State { get; private set; }
        public static bool HasState => State != null;

        /// <summary>State を用意し、勢力ペアの Entry を初期化、`FactionRelations.ActiveDiplomacy` に配線（冪等）。</summary>
        public static DiplomacyState Ensure(IReadOnlyList<string> factions)
        {
            if (State == null) State = new DiplomacyState();
            if (factions != null)
                for (int i = 0; i < factions.Count; i++)
                    for (int j = i + 1; j < factions.Count; j++)
                        State.GetEntry(factions[i], factions[j], create: true);
            FactionRelations.ActiveDiplomacy = State;
            return State;
        }

        /// <summary>セッションを破棄し、敵対判定を従来動作へ戻す。</summary>
        public static void Clear()
        {
            State = null;
            FactionRelations.ActiveDiplomacy = null;
        }
    }
}
