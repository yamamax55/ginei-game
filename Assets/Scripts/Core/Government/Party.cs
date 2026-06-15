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

        /// <summary>所属議員（<see cref="Person.id"/>）。</summary>
        public List<int> memberIds = new List<int>();

        public PartyFaction() { }

        public PartyFaction(int id, string name, int bossId = -1)
        {
            this.id = id;
            this.name = name;
            this.bossId = bossId;
        }

        /// <summary>束ねる議員票（所属議員数）。</summary>
        public int Weight => memberIds.Count;
    }

    /// <summary>
    /// 政党の純データ（GOV-6 #159）。政党＝内部勢力 #113 の一種（新派閥システムを作らない）。綱領（<see cref="platform"/>）・
    /// 階級基盤（<see cref="classBase"/> #110）・党首（政治家 <see cref="leaderId"/>）・議員（<see cref="memberIds"/>）・
    /// 支持率/議席（<see cref="support"/>）を持つ。党内の派閥は <see cref="factions"/>（GOV-7）。
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

        /// <summary>所属議員（政治家 <see cref="Person.id"/>）。</summary>
        public List<int> memberIds = new List<int>();

        /// <summary>支持率/議席シェア（0..1・#113 と連動）。</summary>
        public float support;

        /// <summary>党内派閥（GOV-7 #165）。</summary>
        public List<PartyFaction> factions = new List<PartyFaction>();

        /// <summary>党の役職への就任（党首以外＝幹事長/政調会長等。党首は <see cref="leaderId"/> が出所）。<see cref="PartyOrganizationRules"/> が窓口。</summary>
        public List<PartyAppointment> posts = new List<PartyAppointment>();

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
