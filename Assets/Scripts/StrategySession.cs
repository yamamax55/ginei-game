namespace Ginei
{
    /// <summary>
    /// 戦略マップの世界状態（銀河グラフ＋艦隊レジストリ）をシーン遷移（戦略↔実会戦）を跨いで保持する（C-3）。
    /// 純データの静的保管庫。Battle シーンへ往復しても銀河の状態を失わない（再生中は static が生き続ける）。
    /// </summary>
    public static class StrategySession
    {
        public static GalaxyMap Map;
        public static StrategicFleetRegistry Reg;

        public static bool HasState => Map != null && Reg != null;

        public static void Set(GalaxyMap map, StrategicFleetRegistry reg) { Map = map; Reg = reg; }
        public static void Clear() { Map = null; Reg = null; }
    }
}
