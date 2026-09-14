using System.Collections.Generic;

namespace Ginei
{
    /// <summary>
    /// 党内の派閥（GOV-7 #165・自民党型）。領袖（<see cref="bossId"/>）が議員票を束ねる単位＝内部勢力 #113 の一種。
    /// 学閥/文官閥（#155/#156）の一般化（新派閥システムを作らない）。純データ。
    /// </summary>
    [System.Serializable]
    public class PartyFaction
    {
        public int id;
        public string name;

        /// <summary>領袖（faction boss＝<see cref="Person.id"/>。議員票を束ねる）。</summary>
        public int bossId = -1;

        /// <summary>派閥に属するネームドの党員（<see cref="Person.id"/>・党の <see cref="Party.memberIds"/> に含まれる人だけ・一人一派閥）。</summary>
        public List<int> memberIds = new List<int>();

        /// <summary>政策傾向（<see cref="Person.creed"/> の名前と一致すれば政策が近い。空＝特になし）。</summary>
        public string policyStance = "";

        /// <summary>結束（0..1）。領袖の推薦が所属者の投票に効く強さ（1でも全員を強制はしない）。</summary>
        public float cohesion = 0.6f;

        /// <summary>直近の総裁選で推した候補（-1＝自主投票・未実施）。</summary>
        public int endorsedCandidateId = -1;

        /// <summary>直近の総裁選で勝者を推した（主流派）か。</summary>
        public bool mainstream;

        public PartyFaction() { }

        public PartyFaction(int id, string name, int bossId = -1)
        {
            this.id = id;
            this.name = name;
            this.bossId = bossId;
        }

        /// <summary>束ねる所属者の数（重複ID・負のIDを数えない）。票そのものではない（票は一人1票で数える）。</summary>
        public int Weight
        {
            get
            {
                if (memberIds == null) return 0;
                var seen = new HashSet<int>();
                for (int i = 0; i < memberIds.Count; i++)
                    if (memberIds[i] >= 0) seen.Add(memberIds[i]);
                return seen.Count;
            }
        }
    }

    /// <summary>
    /// 政党の純データ（GOV-6 #159）。政党＝内部勢力 #113 の一種（新派閥システムを作らない）。綱領（<see cref="platform"/>）・
    /// 階級基盤（<see cref="classBase"/> #110）・党首（政治家 <see cref="leaderId"/>）・ネームド所属政治家（<see cref="memberIds"/>）・
    /// 支持率（<see cref="support"/>）・一般党員集計（<see cref="nationalMembership"/>）を持つ。確定議席は <see cref="ChamberSeats"/>、議員は <see cref="LegislatorRecord"/>。党内の派閥は <see cref="factions"/>（GOV-7）。
    /// 党勢で首班を決める最小選挙・党首選出は <see cref="PartyRules"/>/<see cref="LeadershipElectionRules"/> が窓口。純データ。
    /// </summary>
    [System.Serializable]
    public class Party
    {
        public int id;
        public string partyName;

        /// <summary>綱領・思想（政体/思想 #117 と連動）。</summary>
        public string platform = "";

        public Faction faction;

        /// <summary>階級基盤（貴族党/ブルジョワ党/労働党… #110）。</summary>
        public string classBase = "";

        /// <summary>党首（政治家＝<see cref="Person.id"/>。-1＝空席）。民主国家では政府の長になりうる。</summary>
        public int leaderId = -1;

        /// <summary>
        /// ネームドの所属政治家（<see cref="Person.id"/>・一人一党）。当選済みの議員とは別（議員は <see cref="LegislatorRecord"/> で照会）、
        /// 一般党員の人数とも別（<see cref="nationalMembership"/>）。入党・離党・移籍は <see cref="PartyMembershipRules"/> が窓口。
        /// </summary>
        public List<int> memberIds = new List<int>();

        /// <summary>支持率（0..1・#113 と連動）。確定議席（<see cref="ChamberSeats"/>）とは別で、議席を上書きしない。</summary>
        public float support;

        /// <summary>一般党員の全国集計（人・既定は不明）。<see cref="memberIds"/> の数から換算しない。</summary>
        public PartyMembershipTally nationalMembership = new PartyMembershipTally(PartyMembershipTally.NationalScope);

        /// <summary>一般党員の星系ごとの集計（載っていない星系は不明）。</summary>
        public List<PartyMembershipTally> regionalMemberships = new List<PartyMembershipTally>();

        /// <summary>党内派閥（GOV-7 #165）。</summary>
        public List<PartyFaction> factions = new List<PartyFaction>();

        /// <summary>党の役職への就任（党首以外＝幹事長/政調会長等。党首は <see cref="leaderId"/> が出所）。<see cref="PartyOrganizationRules"/> が窓口。</summary>
        public List<PartyAppointment> posts = new List<PartyAppointment>();

        /// <summary>党首の任期と総裁選の記録（#165・<see cref="PartyLeadershipRules"/> が窓口。国政の議席・首相とは別）。</summary>
        public PartyLeadershipState leadership = new PartyLeadershipState();

        public Party() { }

        public Party(int id, string partyName, Faction faction)
        {
            this.id = id;
            this.partyName = partyName;
            this.faction = faction;
        }

        public bool HasLeader => leaderId >= 0;
    }
}
