namespace Ginei
{
    /// <summary>
    /// 党の役職（政党システム・GOV-6 #159）。<b>党首</b>（総裁）＋<b>党三役</b>（幹事長/政調会長/総務会長）＋国対委員長。
    /// 党首は <see cref="Party.leaderId"/> が単一の出所、それ以外は <see cref="Party.posts"/> に就任を保持する（<see cref="PartyOrganizationRules"/> が窓口）。
    /// </summary>
    public enum PartyPost
    {
        党首,       // 総裁（党の長＝leaderId と同一）
        幹事長,     // 党務の総括（党三役の筆頭・選挙/資金）
        政調会長,   // 政策（党三役）
        総務会長,   // 党運営（党三役）
        国対委員長  // 国会対策（議事運営）
    }

    /// <summary>党の一役職への就任（政党システム）。純データ（直列化可・戦役セーブに乗る）。</summary>
    [System.Serializable]
    public class PartyAppointment
    {
        public PartyPost post;
        /// <summary>就任者（<see cref="Person.id"/>。-1＝空席）。</summary>
        public int holderId = -1;

        /// <summary>就任年（0＝不明＝旧セーブ）。</summary>
        public int appointedYear;
        /// <summary>任命した党首（-1＝不明＝旧セーブ。党首が替わると党三役は改めて任命される・<see cref="PartyExecutiveRules"/>）。</summary>
        public int appointedById = -1;
        /// <summary>任命・選定の理由。</summary>
        public string reason = "";
        /// <summary>党首不在の間の暫定の期限（この年まで・0＝暫定でない）。</summary>
        public int caretakerUntilYear;

        public PartyAppointment() { }

        public PartyAppointment(PartyPost post, int holderId = -1)
        {
            this.post = post;
            this.holderId = holderId;
        }
    }
}
