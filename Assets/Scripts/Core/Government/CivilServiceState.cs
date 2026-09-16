using System.Collections.Generic;

namespace Ginei
{
    /// <summary>
    /// 省内職位＝職業官僚の省庁内での段階（#141）。官位（<see cref="CourtRank"/>＝身分の序列）とは別軸で、
    /// 「どの省のどの段にいるか」だけを表す。内閣の政治任用（<see cref="CabinetPostKind"/>）・軍の階級（rankTier）・
    /// 艦隊の指揮権とは無関係＝この段に就いても作戦指揮権も政府の決裁権も生じない。
    /// ※値は末尾追加せず序数を保つ（下から上へ並ぶ＝昇任は隣の段へ1つずつ）。
    /// </summary>
    public enum BureaucratGrade
    {
        一般官僚,   // 入省時の段（実務）
        課長級,     // 課の長
        局長級,     // 局の長
        事務次官級  // 省の事務方の長（大臣の下）
    }

    /// <summary>省内職位の在任記録が今どうなっているか。<see cref="在任"/> 以外は退任済み（退任年が入る）。</summary>
    public enum CivilServiceStatus
    {
        在任,   // 現に就いている
        異動,   // 他省へ移って終わった
        昇任,   // 1つ上の段へ移って終わった
        降任,   // 1つ下の段へ移って終わった
        解任,   // 免じられて終わった
        退職    // 死亡・拘束・他勢力などで職を離れた
    }

    /// <summary>
    /// 人事台帳の1件（省ID・人物ID・職位・就任/退任年・任命者・理由・状態）。純データ（戦役セーブに乗る）。
    /// 窓口は <see cref="CivilServicePostRules"/>。内閣の職（<see cref="CabinetPost"/>）を写したものではない。
    /// </summary>
    [System.Serializable]
    public class CivilServiceRecord
    {
        /// <summary>配属先の省（<see cref="Ministry.id"/>）。</summary>
        public int ministryId = -1;
        /// <summary>省名（省庁ツリーは保存されないため表示用に写しを持つ）。</summary>
        public string ministryName = "";
        /// <summary>官僚本人（<see cref="Person.id"/>）。</summary>
        public int personId = -1;
        public BureaucratGrade grade = BureaucratGrade.一般官僚;
        public int appointedYear;
        /// <summary>退任年（0＝在任中）。</summary>
        public int vacatedYear;
        /// <summary>承認した任命権者（首相・大臣・委任を受けた副大臣の <see cref="Person.id"/>。-1＝自動整理）。</summary>
        public int appointedById = -1;
        public string reason = "";
        public CivilServiceStatus status = CivilServiceStatus.在任;

        public bool IsServing => status == CivilServiceStatus.在任;
    }

    /// <summary>
    /// 勢力の省内職位の人事台帳（#141）。在任中の記録（<see cref="records"/>）と退任済みの記録（<see cref="history"/>・上限つき）だけを持ち、
    /// 官職（<see cref="GovernmentRegistry"/>）・内閣（<see cref="CabinetState"/>）・軍の編制を複製しない。
    /// <see cref="FactionState.civilService"/> に保持（戦役セーブに乗る・旧セーブは null＝空の台帳として扱う）。純データ。
    /// </summary>
    [System.Serializable]
    public class CivilServiceState
    {
        /// <summary>在任中の記録（省ID昇順・同省は職位の高い順に整理される）。</summary>
        public List<CivilServiceRecord> records = new List<CivilServiceRecord>();

        /// <summary>退任済みの記録（古い順・上限つき）。</summary>
        public List<CivilServiceRecord> history = new List<CivilServiceRecord>();

        /// <summary>上限で捨てた履歴の件数（黙って切り捨てない）。</summary>
        public int historyDropped;
    }
}
