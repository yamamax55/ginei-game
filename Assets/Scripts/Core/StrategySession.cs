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

        /// <summary>戦役の世界状態（勢力ごとの国家状態・#817 旗幟の基準忠誠の出所）。Battle 往復でも腐敗/合意を失わない。</summary>
        public static CampaignState Campaign;

        /// <summary>統一ゲーム時間の唯一の権威（TIME-1 #947）。戦略/戦術が共有し、Battle 往復でも時間を失わない。</summary>
        public static GameClock Clock = new GameClock();

        /// <summary>ロード復元用の人物ロスター置き場（continue・セーブから復元）。GalaxyView が次の構築で消費する。null=なし。</summary>
        public static List<Person> PendingPeople;

        /// <summary>朝廷の権威（官僚制基盤・名実の乖離の中央権威）。Battle 往復・潜行で形骸化の進み具合を失わない（null=未設定）。</summary>
        public static CourtAuthority CourtAuthority;

        public static bool HasState => Map != null && Reg != null;

        public static void Set(GalaxyMap map, StrategicFleetRegistry reg) { Map = map; Reg = reg; }
        public static void Clear() { Map = null; Reg = null; Provinces = null; Campaign = null; Clock = new GameClock(); PendingPeople = null; CourtAuthority = null; }
    }
}
