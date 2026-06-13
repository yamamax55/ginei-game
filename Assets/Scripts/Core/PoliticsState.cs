using System.Collections.Generic;

namespace Ginei
{
    /// <summary>
    /// 勢力の政治状態（政党システムの配線・在席状態）。政党（<see cref="parties"/>）と二院（下院/上院）の選挙日程を束ねる。
    /// <see cref="FactionState.politics"/> に保持し、<see cref="PoliticsTickRules"/> が年次で回す（成熟度で二大政党へ収束・選挙日程・分断危機）。
    /// budget/fiscal と同じく在席のセッション状態（戦役セーブ非対象＝復元時は既定で再構築）。純データ。
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

        public PoliticsState() { }
    }
}
