using System.Collections.Generic;

namespace Ginei
{
    /// <summary>内閣の政治任用職の種別（職業官僚・事務次官・省職員とは別系統）。</summary>
    public enum CabinetPostKind
    {
        大臣,   // 所管の政策方針と決裁
        副大臣, // 大臣の補佐。明示の委任範囲だけを期限つきで代行
        政務官  // 政策の提案・調整（最終決裁権なし）
    }

    /// <summary>大臣が副大臣へ明示的に委ねる範囲（ここに無いことは代行できない）。</summary>
    [System.Flags]
    public enum CabinetDelegation
    {
        なし = 0,
        所管政策 = 1, // 所管の政策方針の決定
        所管決裁 = 2  // 所管案件の決裁
    }

    /// <summary>内閣の一職（省×種別）。純データ（戦役セーブに乗る）。</summary>
    [System.Serializable]
    public class CabinetPost
    {
        /// <summary>所管の省（<see cref="Ministry.id"/>）。</summary>
        public int ministryId = -1;
        /// <summary>省名（省庁ツリーは保存されないため表示用に写しを持つ）。</summary>
        public string ministryName = "";
        public OfficeDomain domain = OfficeDomain.内政;
        public CabinetPostKind kind;

        /// <summary>在任者（<see cref="Person.id"/>。-1＝空席）。</summary>
        public int holderId = -1;
        /// <summary>在任者の所属党（-1＝無所属）。</summary>
        public int partyId = -1;
        public int appointedYear;
        /// <summary>任命した首相（<see cref="Person.id"/>）。</summary>
        public int appointedById = -1;
        /// <summary>任命・選定の理由。</summary>
        public string appointmentReason = "";
        /// <summary>空席の理由（在任中は空）。</summary>
        public string vacancyReason = "";

        /// <summary>直前の在任者（続投の判定用・-1＝なし）。</summary>
        public int lastHolderId = -1;
        /// <summary>直前に空席になった年（0＝なし）。</summary>
        public int vacatedYear;

        /// <summary>副大臣への委任範囲（副大臣の職だけで使う）。</summary>
        public CabinetDelegation delegation = CabinetDelegation.なし;
        /// <summary>委任した大臣（この人が今も大臣でなければ委任は無効）。</summary>
        public int delegatedById = -1;
        /// <summary>委任の期限（この年まで有効）。</summary>
        public int delegationEndYear;

        public bool IsVacant => holderId < 0;
    }

    /// <summary>任免の履歴1件（内閣・党三役で共用）。</summary>
    [System.Serializable]
    public class AppointmentHistoryEntry
    {
        /// <summary>出来事の安定ID（勢力:系統:年:職:人物:出来事）。</summary>
        public string eventId = "";
        public int year;
        /// <summary>就任/続投/解任/失職/総辞職/職務執行/委任/委任解除/委任失効/党首交代/空席。</summary>
        public string action = "";
        /// <summary>職名（例：兵部大臣・幹事長）。</summary>
        public string postLabel = "";
        /// <summary>内閣の職なら省ID（党役職は -1）。</summary>
        public int ministryId = -1;
        public int personId = -1;
        /// <summary>任免した人（首相・党首。自動の整理は -1）。</summary>
        public int actorId = -1;
        public string reason = "";
    }

    /// <summary>
    /// 勢力の内閣（首相が任免する大臣・副大臣・政務官）。首相そのものは <see cref="GovernmentFormation.premierPersonId"/> が単一の出所で、
    /// ここは「どの首相の内閣か」の結び付けと各職の在任・委任・履歴だけを持つ。<see cref="PoliticsState.cabinet"/> に保持（戦役セーブに乗る）。
    /// 窓口は <see cref="CabinetAppointmentRules"/>。純データ。
    /// </summary>
    [System.Serializable]
    public class CabinetState
    {
        /// <summary>この内閣を率いる首相（-1＝首相不在）。</summary>
        public int premierPersonId = -1;
        /// <summary>与党（組閣時の政府の党）。</summary>
        public int partyId = -1;
        public int formedYear;
        /// <summary>組閣の根拠になった政府の選挙ID（総選挙後の首班指名で内閣を組み直す）。</summary>
        public string sourceElectionId = "";

        /// <summary>職務執行内閣か（首相不在の間、大臣は所管の決裁だけを期限まで続ける）。</summary>
        public bool caretaker;
        /// <summary>職務執行を始めた時の首相。</summary>
        public int caretakerOfPremierId = -1;
        /// <summary>職務執行の期限（この年まで）。</summary>
        public int caretakerUntilYear;
        public string caretakerReason = "";

        public List<CabinetPost> posts = new List<CabinetPost>();
        /// <summary>任免の履歴（古い順・上限つき）。</summary>
        public List<AppointmentHistoryEntry> history = new List<AppointmentHistoryEntry>();
        /// <summary>上限で捨てた履歴の件数（黙って切り捨てない）。</summary>
        public int historyDropped;
    }
}
