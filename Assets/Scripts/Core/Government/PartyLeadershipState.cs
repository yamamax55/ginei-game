using System.Collections.Generic;

namespace Ginei
{
    /// <summary>総裁選の決着の種類（当選・暫定・選出不能を区別する）。</summary>
    public enum LeadershipOutcome
    {
        未実施,
        第1回当選,   // 有効票の厳密な過半数（&gt;50%）
        決選当選,     // 上位2人の決選投票
        無投票当選,   // 推薦要件を満たした候補が1人
        暫定続投,     // 候補がいない＝現党首が1年の暫定任期で続投
        暫定選出,     // 候補も現党首もいない＝党内序列の最上位を1年の暫定党首に
        選出不能      // 適格な党員がいない／有効票がない＝党首は空席のまま（捏造しない）
    }

    /// <summary>総裁選での候補の状態。</summary>
    public enum LeadershipCandidateStatus
    {
        立候補,
        推薦人不足で撤回,
        第1回落選,
        決選進出,
        決選落選,
        当選
    }

    /// <summary>プレイヤーが進める総裁選の段階。投開票後も結果画面用に完了を保持する。</summary>
    public enum LeadershipElectionPhase
    {
        未告示,
        立候補受付,
        投開票待ち,
        完了
    }

    /// <summary>告示から投開票までの手動進行。総裁選記録とは分け、途中保存できる。</summary>
    [System.Serializable]
    public class LeadershipElectionProcess
    {
        public int year;
        public string trigger = "";
        public LeadershipElectionPhase phase;
        public List<LeadershipCandidacyData> candidacies = new List<LeadershipCandidacyData>();

        public bool Active => phase == LeadershipElectionPhase.立候補受付 || phase == LeadershipElectionPhase.投開票待ち;
    }

    /// <summary>保存可能な立候補届。推薦人は告示後の党内調整で自動確保する。</summary>
    [System.Serializable]
    public class LeadershipCandidacyData
    {
        public int candidateId = -1;
        public bool withdrawn;
        public int declaredYear;

        public LeadershipCandidacyData() { }
        public LeadershipCandidacyData(int candidateId, int year) { this.candidateId = candidateId; declaredYear = year; }
    }

    /// <summary>
    /// 政党ごとの党首（総裁）の任期と総裁選の履歴（#165 GOV-7）。<see cref="Party.leadership"/> に1つ。
    /// 党首本人は <see cref="Party.leaderId"/> が単一の出所（ここは任期・日程・記録だけ）。国政の議席・首相・閣僚とは別の履歴。
    /// 更新は <see cref="PartyLeadershipRules"/> が唯一の窓口。純データ（セーブに乗る・旧セーブは既定値＝未管理）。
    /// </summary>
    [System.Serializable]
    public class PartyLeadershipState
    {
        /// <summary>党首を総裁選で選ぶ管理下にあるか（false＝旧来の自動補充。年次の総裁選が最初に true にする）。</summary>
        public bool managed;
        /// <summary>現党首の任期の始まり（0=未起算）。</summary>
        public int termStartYear;
        /// <summary>現党首の任期の終わり（この年に総裁選。0=未起算）。</summary>
        public int termEndYear;
        /// <summary>現党首の連続任期数（無投票・決選とも1期。暫定は数えない）。</summary>
        public int consecutiveTerms;
        /// <summary>次の総裁選の年（0=未定）。</summary>
        public int nextElectionYear;
        /// <summary>任期の起算の注記（就任年不明で起算した等）。</summary>
        public string termNote = "";
        /// <summary>党首が空席・暫定のときの理由（選出待ち）。</summary>
        public string pendingReason = "";
        /// <summary>最後に総裁選を処理した年（同じ年に二度行わない）。</summary>
        public int lastElectionYear;
        /// <summary>直近の総裁選の記録（古い順・上限つき）。</summary>
        public List<LeadershipElectionRecord> records = new List<LeadershipElectionRecord>();
        /// <summary>手動総裁選の途中状態（旧セーブでは未告示）。</summary>
        public LeadershipElectionProcess process = new LeadershipElectionProcess();

        public PartyLeadershipState() { }

        /// <summary>直近の記録（無ければ null）。</summary>
        public LeadershipElectionRecord Latest => records != null && records.Count > 0 ? records[records.Count - 1] : null;
    }

    /// <summary>一回の総裁選の記録（候補・票の内訳・決選・選出理由・派閥の動き・seed）。</summary>
    [System.Serializable]
    public class LeadershipElectionRecord
    {
        /// <summary>総裁選の ID（勢力:党:年）。国政選挙の ID とは別系統。</summary>
        public string electionId = "";
        public int year;
        public int partyId = -1;
        /// <summary>乱数の種（選挙IDから導く＝同じ選挙は同じ結果）。</summary>
        public int seed;
        /// <summary>実施の理由（任期満了・党首の死去/離反による欠缺 等）。</summary>
        public string trigger = "";
        public int previousLeaderId = -1;
        public int winnerId = -1;
        public LeadershipOutcome outcome;
        /// <summary>選出理由（過半数・決選の票差・同票のくじ・暫定の理由）。</summary>
        public string reason = "";
        /// <summary>制限・推計の注記（党員票不明・集計議席の匿名票・地方票なし 等）。</summary>
        public List<string> restrictions = new List<string>();

        // --- 推薦 ---
        /// <summary>立候補に要る推薦人数（党の規模から算定）。</summary>
        public int requiredEndorsers;
        /// <summary>推薦人・投票者の母集団の説明（国政議員／ネームド党員）。</summary>
        public string voterBasis = "";

        // --- 第1回 ---
        /// <summary>投票したネームド議員（または小党例外のネームド党員）の数＝一人1票。</summary>
        public int namedVoters;
        /// <summary>集計議席の匿名票の数（0=使わない）。</summary>
        public int aggregateVotes;
        /// <summary>集計議席の匿名票の算式と出所。</summary>
        public string aggregateFormula = "";
        /// <summary>一般党員の生票が分かっているか（false＝不明＝党員票なし）。</summary>
        public bool memberVotesKnown;
        /// <summary>一般党員の生票の総数（人）。</summary>
        public long memberRawTotal;
        /// <summary>党員票の算定票の総数（＝議員票の総数。議員票0の小党は既定の配分数）。</summary>
        public int memberAllotment;
        /// <summary>第1回の有効票の総数。</summary>
        public int round1Total;

        // --- 決選 ---
        public bool runoffHeld;
        /// <summary>決選の有効票の総数。</summary>
        public int runoffTotal;
        /// <summary>決選の地方票（党組織のある星系ごと1票）。</summary>
        public List<LeadershipRegionResult> regions = new List<LeadershipRegionResult>();

        public List<LeadershipCandidateResult> candidates = new List<LeadershipCandidateResult>();
        public List<LeadershipFactionStance> factions = new List<LeadershipFactionStance>();
        /// <summary>派閥に属さない投票者の数。</summary>
        public int unaffiliatedVoters;

        // --- 結果の任期 ---
        public int termStartYear;
        public int termEndYear;
        public int nextElectionYear;

        public LeadershipElectionRecord() { }

        /// <summary>候補の結果を引く（無ければ null）。</summary>
        public LeadershipCandidateResult Candidate(int personId)
        {
            if (candidates == null) return null;
            for (int i = 0; i < candidates.Count; i++)
                if (candidates[i] != null && candidates[i].personId == personId) return candidates[i];
            return null;
        }
    }

    /// <summary>候補一人の推薦・得票の内訳。</summary>
    [System.Serializable]
    public class LeadershipCandidateResult
    {
        public int personId = -1;
        public LeadershipCandidateStatus status;
        /// <summary>この候補を推薦した人（一人一候補）。</summary>
        public List<int> endorserIds = new List<int>();
        /// <summary>国政の累積当選回数（履歴未登録なら0）。</summary>
        public int nationalWins;
        /// <summary>議員の当選履歴が登録されているか。</summary>
        public bool historyRegistered;
        /// <summary>候補評価（能力・実績と年功の合成）。</summary>
        public float strength;

        public int round1Named;
        public int round1Aggregate;
        public long round1MemberRaw;
        public int round1MemberConverted;
        public int round1Total;

        public int runoffNamed;
        public int runoffAggregate;
        public int runoffRegional;
        public int runoffTotal;

        public LeadershipCandidateResult() { }
        public LeadershipCandidateResult(int personId) { this.personId = personId; }
    }

    /// <summary>決選の地方票（一星系＝1票）。</summary>
    [System.Serializable]
    public class LeadershipRegionResult
    {
        public int systemId = -1;
        /// <summary>この星系の一般党員（人）。</summary>
        public long members;
        /// <summary>第1回で決選の2人が得た生票。</summary>
        public long votesFirst;
        public long votesSecond;
        /// <summary>地方票の行き先（-1＝同数のため無効）。</summary>
        public int voteFor = -1;

        public LeadershipRegionResult() { }
    }

    /// <summary>総裁選での派閥の動き（推薦・支持変更・結束の実績・主流/反主流）。</summary>
    [System.Serializable]
    public class LeadershipFactionStance
    {
        public int factionId;
        public string name = "";
        public int bossId = -1;
        /// <summary>第1回の推薦（-1＝自主投票）。</summary>
        public int endorsedRound1 = -1;
        /// <summary>決選の推薦（-1＝自主投票・決選なし）。</summary>
        public int endorsedRunoff = -1;
        /// <summary>投票した所属者（一人1票・重複なし）。</summary>
        public int membersVoted;
        /// <summary>そのうち推薦どおりに投じた人（造反＝差）。</summary>
        public int membersFollowed;
        public bool mainstream;
        public string reason = "";

        public LeadershipFactionStance() { }
    }
}
