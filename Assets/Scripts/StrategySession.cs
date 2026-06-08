using System.Collections.Generic;

namespace Ginei
{
    /// <summary>
    /// 戦略マップの世界状態（銀河グラフ＋艦隊レジストリ＋内政）をシーン遷移（戦略↔実会戦）を跨いで保持する（C-3）。
    /// 純データの静的保管庫。Battle シーンへ往復しても銀河の状態を失わない（再生中は static が生き続ける）。
    /// </summary>
    public static class StrategySession
    {
        public static GalaxyMap Map;
        public static StrategicFleetRegistry Reg;

        /// <summary>内政状態（星系ID→Province・#109/#759）。Battle 往復でも安定度/統合を失わない。</summary>
        public static Dictionary<int, Province> Provinces;

        /// <summary>勢力ごとの統治政策（#112・内政三層の勢力層 #767）。Battle 往復でも保持。</summary>
        public static Dictionary<Faction, GovernancePolicy> Policies;

        public static bool HasState => Map != null && Reg != null;

        public static void Set(GalaxyMap map, StrategicFleetRegistry reg) { Map = map; Reg = reg; }
        public static void Clear() { Map = null; Reg = null; Provinces = null; Policies = null; }
    }
}
