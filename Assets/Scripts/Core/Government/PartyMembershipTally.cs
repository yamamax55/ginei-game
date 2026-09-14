namespace Ginei
{
    /// <summary>
    /// 政党の一般党員の集計（全国／星系ごと・単位＝人）。<see cref="Party.memberIds"/>（ネームドの所属政治家）とも、
    /// 議員名簿（<see cref="LegislatorRecord"/>）とも別物＝人物IDの数から換算しない。
    /// <see cref="known"/>=false は「不明（未設定）」で、<see cref="members"/> は読まない。値を入れるときは出所と時点を添える。
    /// 更新は <see cref="PartyMembershipRules.SetGeneralMembership"/> が窓口。純データ（セーブに乗る・旧セーブは不明のまま）。
    /// </summary>
    [System.Serializable]
    public class PartyMembershipTally
    {
        /// <summary>全国集計を表す星系ID。</summary>
        public const int NationalScope = -1;

        /// <summary>集計の範囲（<see cref="NationalScope"/>＝全国、0以上＝その星系）。</summary>
        public int systemId = NationalScope;

        /// <summary>集計値があるか（false＝不明）。</summary>
        public bool known;

        /// <summary>一般党員の数（人）。<see cref="known"/> が true のときだけ意味を持つ。</summary>
        public long members;

        /// <summary>数値の出所（シナリオ・党の公表など）。</summary>
        public string source = "";

        /// <summary>集計の時点（宇宙暦の年・0=不明）。</summary>
        public int asOfYear;

        public PartyMembershipTally() { }

        public PartyMembershipTally(int systemId) { this.systemId = systemId; }
    }
}
