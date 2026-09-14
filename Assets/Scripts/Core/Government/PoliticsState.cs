using System.Collections.Generic;

namespace Ginei
{
    /// <summary>
    /// 勢力の政治状態（政党システムの配線・在席状態）。政党（<see cref="parties"/>）と二院（下院/上院）の選挙日程を束ねる。
    /// <see cref="FactionState.politics"/> に保持し、<see cref="PoliticsTickRules"/> が年次で回す（成熟度で二大政党へ収束・選挙日程・分断危機）。
    /// 選挙の結果（確定議席・政府・星系知事選）は <see cref="ElectionCycleRules"/>／<see cref="LocalElectionRules"/> が更新する。
    /// 戦役セーブに乗る（<see cref="FactionStateSave.politics"/>・旧セーブは null＝次の年次で初期化）。純データ。
    /// </summary>
    [System.Serializable]
    public class PoliticsState
    {
        /// <summary>勢力の政党（支持率を持つ・<see cref="PartySystemRules"/> が収束させる）。</summary>
        public List<Party> parties = new List<Party>();

        /// <summary>下院（衆議院相当）の選挙日程（<see cref="ElectionScheduleRules"/>）。</summary>
        public ChamberSchedule lowerHouse;

        /// <summary>上院（参議院相当）の選挙日程。</summary>
        public ChamberSchedule upperHouse;

        /// <summary>分断危機が継続中か（通知の立ち上がり検出用＝毎年通知しない）。</summary>
        public bool dividedCrisisActive;

        /// <summary>下院の確定議席（支持率とは別。null=未構成）。</summary>
        public ChamberSeats lowerSeats;

        /// <summary>上院の確定議席（区分 A/B の半数改選。null=未構成）。</summary>
        public ChamberSeats upperSeats;

        /// <summary>国政選挙で決まった政府（首相・与党・少数政権/組閣未成立の理由）。null=未組閣。</summary>
        public GovernmentFormation government;

        /// <summary>直近の国政選挙の開票記録（古い順・上限つき）。</summary>
        public List<NationalElectionRecord> recentResults = new List<NationalElectionRecord>();

        /// <summary>星系知事選の状態（星系ID昇順）。</summary>
        public List<LocalElectionState> locals = new List<LocalElectionState>();

        /// <summary>知事選の日程を一度でも組んだか（初回は即時・以後の新規編入は猶予つき）。</summary>
        public bool localsSeeded;

        public PoliticsState() { }
    }
}
