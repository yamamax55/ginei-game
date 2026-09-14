using System.Collections.Generic;

namespace Ginei
{
    /// <summary>国政選挙後の組閣の状態（連立交渉は未実装＝過半なしは第一党の少数政権として明示する）。</summary>
    public enum CabinetStatus
    {
        未実施,     // まだ下院が構成されていない
        単独過半,   // 第一党が下院の過半数を持つ
        少数政権,   // 第一党が過半数に届かない（連立は未実装）
        組閣未成立, // 第一党に適格な党首がいない等＝首相は空席
        対象外      // 非民主の政体（選挙で首班を選ばない）
    }

    /// <summary>星系知事選の状態。</summary>
    public enum LocalElectionStatus
    {
        未実施, // 日程のみ（まだ選挙をしていない）
        当選,   // 知事が在任中
        不成立, // 候補者不在などで選挙が成り立たなかった
        対象外, // 非民主の政体（知事は任命制）
        失職    // 死亡・占領・首相就任などで知事が失職し、補欠選挙待ち
    }

    /// <summary>ある政党の議院内の議席（上院は改選区分 A/B ごと。下院は A のみを使う）。</summary>
    [System.Serializable]
    public class PartySeatCount
    {
        public int partyId;
        public int classA;
        public int classB;

        public PartySeatCount() { }
        public PartySeatCount(int partyId) { this.partyId = partyId; }

        /// <summary>全区分の議席合計。</summary>
        public int Total => classA + classB;
    }

    /// <summary>
    /// 議院の確定議席（支持率とは別に、開票で確定した議席を次の選挙まで保持する）。
    /// 上院は区分 A/B の半数改選で、改選しない区分の議席はそのまま残る。
    /// </summary>
    [System.Serializable]
    public class ChamberSeats
    {
        public LegislativeChamber chamber;
        /// <summary>区分 A の定数（下院は全議席）。</summary>
        public int classSeatsA;
        /// <summary>区分 B の定数（下院は0）。</summary>
        public int classSeatsB;
        /// <summary>一度でも全区分が選ばれたか（false の間は議院が未構成）。</summary>
        public bool seated;
        /// <summary>区分ごとの直近の改選年（0=未実施）。同じ年の二重開票を防ぐ。</summary>
        public int lastElectionYearA;
        public int lastElectionYearB;
        public List<PartySeatCount> parties = new List<PartySeatCount>();

        public ChamberSeats() { }

        public ChamberSeats(LegislativeChamber chamber, int classSeatsA, int classSeatsB)
        {
            this.chamber = chamber;
            this.classSeatsA = classSeatsA;
            this.classSeatsB = classSeatsB;
        }

        /// <summary>議院の定数。</summary>
        public int TotalSeats => classSeatsA + classSeatsB;
    }

    /// <summary>一政党の開票結果（得票率と獲得議席）。</summary>
    [System.Serializable]
    public class PartyVoteResult
    {
        public int partyId;
        public string partyName = "";
        public float voteShare;
        /// <summary>今回の改選で得た議席。</summary>
        public int seatsWon;
        /// <summary>開票後の議院内の議席（非改選を含む）。</summary>
        public int seatsAfter;
    }

    /// <summary>国政選挙1回の開票記録（直近数件だけ保持する）。</summary>
    [System.Serializable]
    public class NationalElectionRecord
    {
        public string electionId = "";
        public int year;
        public LegislativeChamber chamber;
        /// <summary>改選区分（-1=全議席、0=A、1=B）。</summary>
        public int classUp = -1;
        public int seatsUp;
        public int totalSeats;
        /// <summary>議院を初めて構成した選挙か。</summary>
        public bool inaugural;
        public List<PartyVoteResult> results = new List<PartyVoteResult>();
    }

    /// <summary>国政選挙で決まった政府（首相と与党）。首相の出所＝下院の第一党党首。</summary>
    [System.Serializable]
    public class GovernmentFormation
    {
        public CabinetStatus status = CabinetStatus.未実施;
        public int premierPersonId = -1;
        public int partyId = -1;
        public int partySeats;
        public int totalSeats;
        public int formedYear;
        /// <summary>この組閣の根拠になった選挙の ID。</summary>
        public string sourceElectionId = "";
        /// <summary>少数政権・組閣未成立・空席などの理由。</summary>
        public string reason = "";
    }

    /// <summary>知事選の一候補の得票。</summary>
    [System.Serializable]
    public class LocalCandidateResult
    {
        public int personId;
        public int partyId = -1;
        public float votes;
        public float voteShare;
    }

    /// <summary>一星系の知事選の状態（日程・現職・直近結果・不成立理由）。</summary>
    [System.Serializable]
    public class LocalElectionState
    {
        public int systemId;
        public LocalElectionStatus status = LocalElectionStatus.未実施;
        public int nextElectionYear;
        /// <summary>直近に当選者が出た年（0=なし）。</summary>
        public int lastElectionYear;
        /// <summary>直近に選挙を試みた年（不成立を含む・同年の再処理を防ぐ）。</summary>
        public int lastAttemptYear;
        public int governorPersonId = -1;
        public int governorPartyId = -1;
        public int termEndYear;
        public string lastElectionId = "";
        public string reason = "";
        public List<LocalCandidateResult> lastResults = new List<LocalCandidateResult>();

        public LocalElectionState() { }
        public LocalElectionState(int systemId) { this.systemId = systemId; }
    }

    /// <summary>国政選挙の地域票の素材（星系ごとの人口と安定度）。</summary>
    public readonly struct RegionalElectorate
    {
        public readonly int systemId;
        public readonly float population;
        /// <summary>安定度（0..1）。</summary>
        public readonly float stability01;

        public RegionalElectorate(int systemId, float population, float stability01)
        {
            this.systemId = systemId;
            this.population = population;
            this.stability01 = stability01;
        }
    }

    /// <summary>知事選の選挙区（星系）。人口・安定度・土着の思想を持つ。</summary>
    public readonly struct LocalConstituency
    {
        public readonly int systemId;
        public readonly float population;
        /// <summary>安定度（0..1）。</summary>
        public readonly float stability01;
        public readonly string nativeIdeology;

        public LocalConstituency(int systemId, float population, float stability01, string nativeIdeology)
        {
            this.systemId = systemId;
            this.population = population;
            this.stability01 = stability01;
            this.nativeIdeology = nativeIdeology ?? "";
        }
    }
}
