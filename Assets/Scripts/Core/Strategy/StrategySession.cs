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

        /// <summary>
        /// 援軍（ワープイン）の台帳（#38 C-5）。戦略で派遣した艦隊が、所要時間ののち進行中の戦場へ現れる。
        /// <see cref="Clock"/> と同じく<b>戦略↔会戦を跨いで生きる</b>＝潜行中も銀河時間が流れるので到着が進む。
        /// </summary>
        public static WarpReinforcementLedger Reinforcements = new WarpReinforcementLedger();

        /// <summary>ロード復元用の人物ロスター置き場（continue・セーブから復元）。GalaxyView が次の構築で消費する。null=なし。</summary>
        public static List<Person> PendingPeople;

        /// <summary>ロード復元用の主人公立身出世の置き場（TKO #2477・P1-c）。`ProtagonistCareerDirector` が Setup で消費する。null=なし（新規/後方互換）。</summary>
        public static ProtagonistCareerSave PendingProtagonistCareer;

        /// <summary>朝廷の権威（官僚制基盤・名実の乖離の中央権威）。Battle 往復・潜行で形骸化の進み具合を失わない（null=未設定）。</summary>
        public static CourtAuthority CourtAuthority;

        // ===== 稟議・決裁（#稟議完成②）=====
        // ★3つまとめてここに置く。以前は Game 層の static（RingiDirector.Ledger／FleetRingiDirector.Ledger／
        // DecisionDeck.Queue）に散っていて、寿命がばらばらだった：Battle 往復で Director の
        // インスタンス状態だけが消えて「稟議のないカード」が生まれ、新規戦役では片方だけが消えていた。
        // ここへ集めると、寿命が1か所（Clear）で決まり、保存も Data 層から手が届く。

        /// <summary>税などの稟議在庫（建白→伝播→決裁→執行）。<c>RingiDirector.Ledger</c> はこれを直接返す。</summary>
        public static PetitionLedger Petitions = new PetitionLedger();

        /// <summary>編制（艦隊の設立・解散）の稟議在庫。<c>FleetRingiDirector.Ledger</c> はこれを直接返す。</summary>
        public static PetitionLedger FleetPetitions = new PetitionLedger();

        /// <summary>決裁カード（未決と決裁済みの履歴）。</summary>
        public static DecisionQueue Decisions = new DecisionQueue();

        public static bool HasState => Map != null && Reg != null;

        public static void Set(GalaxyMap map, StrategicFleetRegistry reg) { Map = map; Reg = reg; }
        public static void Clear()
        {
            Map = null; Reg = null; Provinces = null; Campaign = null;
            Clock = new GameClock(); Reinforcements = new WarpReinforcementLedger();
            PendingPeople = null; PendingProtagonistCareer = null; CourtAuthority = null;
            // 稟議・決裁も戦役固有＝まとめて捨てる（片方だけ残すと孤児のカードができる）。
            Petitions = new PetitionLedger(); FleetPetitions = new PetitionLedger(); Decisions = new DecisionQueue();
        }
    }
}
