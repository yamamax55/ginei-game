using System.Collections.Generic;

namespace Ginei
{
    /// <summary>総裁選の手動操作と派閥の結成・移動を、画面から独立して扱う共通入口。</summary>
    public static class PartyLeadershipOperationRules
    {
        public static bool Announce(Party party, int year, string trigger, out string reason)
        {
            reason = "";
            if (party == null || year <= 0) { reason = "政党または年が無効"; return false; }
            if (party.leadership == null) party.leadership = new PartyLeadershipState();
            PartyLeadershipState st = party.leadership;
            if (st.lastElectionYear >= year) { reason = "この年の総裁選は実施済み"; return false; }
            if (st.process != null && st.process.Active) { reason = "総裁選はすでに告示済み"; return false; }
            st.managed = true;
            st.process = new LeadershipElectionProcess
            {
                year = year,
                trigger = string.IsNullOrWhiteSpace(trigger) ? "臨時の総裁選" : trigger.Trim(),
                phase = LeadershipElectionPhase.立候補受付
            };
            reason = "総裁選を告示し、立候補受付を開始";
            return true;
        }

        public static bool Declare(Party party, Faction faction, int candidateId, int year, IList<Person> roster, out string reason)
        {
            reason = "";
            LeadershipElectionProcess p = Process(party);
            if (p == null || p.phase != LeadershipElectionPhase.立候補受付) { reason = "立候補受付中ではない"; return false; }
            Person person = ElectionCycleRules.FindPerson(roster, candidateId);
            if (!ElectionCycleRules.IsEligiblePolitician(person, faction) || party.memberIds == null || !party.memberIds.Contains(candidateId))
            { reason = "同党の適格な政治家ではない"; return false; }
            LeadershipCandidacyData old = Find(p, candidateId);
            if (old != null) { old.withdrawn = false; old.declaredYear = year; reason = "立候補を再届け出"; return true; }
            p.candidacies.Add(new LeadershipCandidacyData(candidateId, year));
            reason = "立候補を届け出";
            return true;
        }

        public static bool Withdraw(Party party, int candidateId, out string reason)
        {
            reason = "";
            LeadershipElectionProcess p = Process(party);
            if (p == null || p.phase != LeadershipElectionPhase.立候補受付) { reason = "立候補受付中ではない"; return false; }
            LeadershipCandidacyData c = Find(p, candidateId);
            if (c == null || c.withdrawn) { reason = "有効な立候補届がない"; return false; }
            c.withdrawn = true;
            reason = "立候補を撤回";
            return true;
        }

        public static bool CloseNominations(Party party, out string reason)
        {
            reason = "";
            LeadershipElectionProcess p = Process(party);
            if (p == null || p.phase != LeadershipElectionPhase.立候補受付) { reason = "立候補受付中ではない"; return false; }
            p.phase = LeadershipElectionPhase.投開票待ち;
            reason = "立候補受付を締め切り、投開票待ちへ移行";
            return true;
        }

        public static LeadershipElectionRecord Conduct(PoliticsState pol, Faction faction, Party party, IList<Person> roster,
            ICollection<int> ownedSystems, PartyLeadershipParams prm, out string reason)
        {
            reason = "";
            LeadershipElectionProcess p = Process(party);
            if (p == null || p.phase != LeadershipElectionPhase.投開票待ち) { reason = "投開票待ちではない"; return null; }
            var active = new List<LeadershipCandidacyData>();
            for (int i = 0; i < p.candidacies.Count; i++)
                if (p.candidacies[i] != null && !p.candidacies[i].withdrawn) active.Add(p.candidacies[i]);

            // 推薦人の個別指定はUIの範囲外。候補本人を除く適格党員を候補全員の推薦候補とし、既存規則が一人一候補に配分する。
            var declared = new List<LeadershipCandidacy>();
            for (int i = 0; i < active.Count; i++)
            {
                var endorsers = new List<int>();
                if (party.memberIds != null)
                    for (int k = 0; k < party.memberIds.Count; k++)
                    {
                        int id = party.memberIds[k];
                        if (id == active[i].candidateId) continue;
                        Person person = ElectionCycleRules.FindPerson(roster, id);
                        if (ElectionCycleRules.IsEligiblePolitician(person, faction)) endorsers.Add(id);
                    }
                declared.Add(new LeadershipCandidacy(active[i].candidateId, endorsers));
            }
            LeadershipElectionRecord rec = PartyLeadershipRules.RunElection(pol, faction, party, p.year, roster, ownedSystems, prm, p.trigger, declared);
            if (rec == null) { reason = "同年の実施済み、または選挙条件を満たさない"; return null; }
            p.phase = LeadershipElectionPhase.完了;
            reason = rec.reason;
            return rec;
        }

        public static bool CreateFaction(Party party, int founderId, string name, string policy, out string reason)
        {
            reason = "";
            if (!IsMember(party, founderId)) { reason = "党員ではない"; return false; }
            if (PartyLeadershipRules.FactionOf(party, founderId) != null) { reason = "すでに派閥へ所属"; return false; }
            if (string.IsNullOrWhiteSpace(name)) { reason = "派閥名が必要"; return false; }
            if (party.factions == null) party.factions = new List<PartyFaction>();
            int id = 1;
            for (int i = 0; i < party.factions.Count; i++) if (party.factions[i] != null && party.factions[i].id >= id) id = party.factions[i].id + 1;
            var f = new PartyFaction(id, name.Trim(), founderId) { policyStance = (policy ?? "").Trim() };
            f.memberIds.Add(founderId);
            party.factions.Add(f);
            PartyLeadershipRules.NormalizeFactions(party);
            reason = "派閥を結成";
            return true;
        }

        public static bool JoinFaction(Party party, int memberId, int factionId, out string reason)
        {
            reason = "";
            if (!IsMember(party, memberId)) { reason = "党員ではない"; return false; }
            PartyFaction target = FindFaction(party, factionId);
            if (target == null) { reason = "派閥が存在しない"; return false; }
            PartyFaction old = PartyLeadershipRules.FactionOf(party, memberId);
            if (old == target) { reason = "すでに所属"; return false; }
            if (old != null)
            {
                old.memberIds.RemoveAll(x => x == memberId);
                if (old.bossId == memberId) old.bossId = -1;
            }
            if (!target.memberIds.Contains(memberId)) target.memberIds.Add(memberId);
            PartyLeadershipRules.NormalizeFactions(party);
            reason = old == null ? "派閥へ加入" : "支持派閥を変更";
            return true;
        }

        public static bool LeaveFaction(Party party, int memberId, out string reason)
        {
            reason = "";
            PartyFaction old = PartyLeadershipRules.FactionOf(party, memberId);
            if (old == null) { reason = "派閥に所属していない"; return false; }
            old.memberIds.RemoveAll(x => x == memberId);
            if (old.bossId == memberId) old.bossId = -1;
            PartyLeadershipRules.NormalizeFactions(party);
            reason = "派閥を離脱";
            return true;
        }

        public static bool SetFactionSupport(Party party, int factionId, int candidateId, out string reason)
        {
            reason = "";
            PartyFaction f = FindFaction(party, factionId);
            LeadershipElectionProcess p = Process(party);
            if (f == null || p == null || !p.Active) { reason = "告示中の総裁選または派閥がない"; return false; }
            LeadershipCandidacyData c = Find(p, candidateId);
            if (c == null || c.withdrawn) { reason = "有効な候補ではない"; return false; }
            f.endorsedCandidateId = candidateId;
            reason = "派閥の支持候補を変更";
            return true;
        }

        private static LeadershipElectionProcess Process(Party p)
        {
            if (p == null || p.leadership == null) return null;
            if (p.leadership.process == null) p.leadership.process = new LeadershipElectionProcess();
            if (p.leadership.process.candidacies == null) p.leadership.process.candidacies = new List<LeadershipCandidacyData>();
            return p.leadership.process;
        }
        private static LeadershipCandidacyData Find(LeadershipElectionProcess p, int id)
        {
            if (p == null || p.candidacies == null) return null;
            for (int i = 0; i < p.candidacies.Count; i++) if (p.candidacies[i] != null && p.candidacies[i].candidateId == id) return p.candidacies[i];
            return null;
        }
        private static PartyFaction FindFaction(Party p, int id)
        {
            if (p == null || p.factions == null) return null;
            for (int i = 0; i < p.factions.Count; i++) if (p.factions[i] != null && p.factions[i].id == id) return p.factions[i];
            return null;
        }
        private static bool IsMember(Party p, int id) => p != null && id >= 0 && p.memberIds != null && p.memberIds.Contains(id);
    }
}
