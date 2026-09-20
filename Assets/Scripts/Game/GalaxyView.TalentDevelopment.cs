using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Ginei
{
    public partial class GalaxyView
    {
        private void RunTalentDevelopmentAnnualTick()
        {
            CampaignState campaign = StrategySession.Campaign;
            if (campaign == null || campaign.states == null) return;
            int year = campaignYear;
            for (int i = 0; i < campaign.states.Count; i++)
            {
                FactionState state = campaign.states[i];
                if (state == null) continue;
                if (state.talentDevelopment == null) state.talentDevelopment = new TalentDevelopmentState();
                ReviewRoster(state, commanders, year);
                ReviewRoster(state, civilians, year);
                InterruptUnavailableTraining(state);
                int completed = TalentDevelopmentRules.TickYear(state.talentDevelopment, year);
                if (completed <= 0) continue;
                ClearFinishedSchoolPostings(state, year);
                NotificationCenter.Push(NotificationCategory.人事, NotificationSeverity.情報,
                    $"{state.faction} 育成：{completed}件が修了");
            }
        }

        private static void ReviewRoster(FactionState state, IReadOnlyList<Person> roster, int year)
        {
            if (state == null || roster == null) return;
            for (int i = 0; i < roster.Count; i++)
            {
                Person person = roster[i];
                if (person == null || person.faction != state.faction || person.deathYear > 0) continue;
                float performance = person.merit != null && person.merit.HasRecord
                    ? Mathf.Clamp01(person.merit.AverageScore / 9f)
                    : 0.5f;
                float potential = Mathf.Clamp01(Mathf.Max(
                    person.MilitaryAptitude, Mathf.Max(person.CivilAptitude, person.TechnicalAptitude)) / 100f);
                TalentDevelopmentRules.ReviewPerson(
                    state.talentDevelopment, person.id, performance, potential, year, GrowthArchetypeOf(person));
            }
        }

        private static GrowthArchetype GrowthArchetypeOf(Person person)
        {
            if (person == null) return GrowthArchetype.叩き上げ;
            if (person.hammockNumber == 1 || person.examRank == 1) return GrowthArchetype.首席型;
            if (person.MilitaryAptitude >= 80f || person.CivilAptitude >= 80f || person.TechnicalAptitude >= 80f)
                return GrowthArchetype.在野俊英型;
            if (person.rankTier >= 6) return GrowthArchetype.老練型;
            return GrowthArchetype.叩き上げ;
        }

        private void InterruptUnavailableTraining(FactionState state)
        {
            if (state == null || state.talentDevelopment == null || state.talentDevelopment.programs == null) return;
            for (int i = 0; i < state.talentDevelopment.programs.Count; i++)
            {
                DevelopmentProgram p = state.talentDevelopment.programs[i];
                if (p == null || p.status != DevelopmentProgramStatus.実施中) continue;
                if (p.kind == DevelopmentProgramKind.艦隊訓練)
                {
                    StrategicFleet fleet = FindStrategicFleet(p.fleetId);
                    if (fleet == null || fleet.faction != state.faction || fleet.IsMoving || fleet.engaged)
                        TalentDevelopmentRules.Interrupt(state.talentDevelopment, p.id, "出撃・交戦のため中断");
                }
                else if (p.kind == DevelopmentProgramKind.軍団共同演習 && CorpsUnavailable(state.faction, p.corpsId))
                {
                    TalentDevelopmentRules.Interrupt(state.talentDevelopment, p.id, "隷下艦隊の出撃・交戦で中断");
                }
            }
        }

        private bool CorpsUnavailable(Faction faction, int corpsId)
        {
            if (reg == null || reg.fleets == null) return true;
            bool found = false;
            for (int i = 0; i < reg.fleets.Count; i++)
            {
                StrategicFleet fleet = reg.fleets[i];
                if (fleet == null || fleet.faction != faction || fleet.corpsId != corpsId) continue;
                found = true;
                if (fleet.IsMoving || fleet.engaged) return true;
            }
            return !found;
        }

        private StrategicFleet FindStrategicFleet(int fleetId)
        {
            if (reg == null || reg.fleets == null) return null;
            for (int i = 0; i < reg.fleets.Count; i++)
                if (reg.fleets[i] != null && reg.fleets[i].id == fleetId) return reg.fleets[i];
            return null;
        }

        private void ClearFinishedSchoolPostings(FactionState state, int year)
        {
            ClearFinishedSchoolPostings(state, commanders, year);
            ClearFinishedSchoolPostings(state, civilians, year);
        }

        private static void ClearFinishedSchoolPostings(FactionState state, IReadOnlyList<Person> roster, int year)
        {
            if (state == null || roster == null) return;
            for (int i = 0; i < roster.Count; i++)
            {
                Person p = roster[i];
                if (p != null && p.faction == state.faction && p.schoolPostingUntilYear <= year)
                    p.schoolPostingUntilYear = 0;
            }
        }

        public bool StartRecommendedDevelopment(int personId, out string reason)
        {
            reason = "";
            Person person = FindDevelopmentPerson(personId);
            if (person == null) { reason = "人物が見つかりません"; return false; }
            FactionState state = StateOf(person.faction);
            if (state == null) { reason = "勢力状態がありません"; return false; }
            if (state.talentDevelopment == null) state.talentDevelopment = new TalentDevelopmentState();
            ReviewRoster(state, new[] { person }, campaignYear);
            TalentDevelopmentProfile profile = TalentDevelopmentRules.FindPerson(state.talentDevelopment, personId);
            DevelopmentProgramKind kind = TalentDevelopmentRules.RecommendedProgram(profile);
            if (kind == DevelopmentProgramKind.現役士官教育 && person.role != PersonRole.軍人)
                kind = DevelopmentProgramKind.抜擢配置;
            int mentorId = kind == DevelopmentProgramKind.師事 ? BestMentorId(person) : -1;
            if (kind == DevelopmentProgramKind.師事 && mentorId < 0) { reason = "適任の師がいません"; return false; }
            string key = $"person:{personId}:{kind}:{campaignYear}";
            DevelopmentActionResult result = TalentDevelopmentRules.StartPersonProgram(
                state.talentDevelopment, kind, personId, mentorId, campaignYear, 1,
                PerformanceReviewRules.DevelopmentPriority(profile.performance, profile.potential), key);
            if (!result.success) { reason = result.reason; return false; }
            if (kind == DevelopmentProgramKind.現役士官教育) person.schoolPostingUntilYear = campaignYear + 1;
            reason = $"{kind}を開始しました";
            return true;
        }

        public bool StartFleetTraining(int fleetId, out string reason)
        {
            reason = "";
            StrategicFleet fleet = FindStrategicFleet(fleetId);
            if (fleet == null) { reason = "艦隊が見つかりません"; return false; }
            if (fleet.IsMoving || fleet.engaged) { reason = "移動・交戦中は訓練できません"; return false; }
            FactionState state = StateOf(fleet.faction);
            if (state == null) { reason = "勢力状態がありません"; return false; }
            DevelopmentActionResult result = TalentDevelopmentRules.StartFleetTraining(
                state.talentDevelopment, fleet.id, campaignYear, 1, 0.7f, $"fleet:{fleet.id}:{campaignYear}");
            reason = result.success ? "艦隊訓練を開始しました" : result.reason;
            return result.success;
        }

        public bool StartCorpsExercise(Faction faction, int corpsId, out string reason)
        {
            reason = "";
            if (CorpsUnavailable(faction, corpsId)) { reason = "隷下艦隊が揃っていません"; return false; }
            FactionState state = StateOf(faction);
            if (state == null) { reason = "勢力状態がありません"; return false; }
            DevelopmentActionResult result = TalentDevelopmentRules.StartCorpsExercise(
                state.talentDevelopment, corpsId, campaignYear, 1, 0.8f, $"corps:{corpsId}:{campaignYear}");
            reason = result.success ? "軍団共同演習を開始しました" : result.reason;
            return result.success;
        }

        public bool InterruptDevelopment(Faction faction, int programId, string cause, out string reason)
        {
            FactionState state = StateOf(faction);
            DevelopmentProgram program = FindProgram(state != null ? state.talentDevelopment : null, programId);
            DevelopmentActionResult result = TalentDevelopmentRules.Interrupt(
                state != null ? state.talentDevelopment : null, programId, cause);
            if (result.success && program != null && program.kind == DevelopmentProgramKind.現役士官教育)
            {
                Person person = FindDevelopmentPerson(program.personId);
                if (person != null) person.schoolPostingUntilYear = 0;
            }
            reason = result.success ? "中断しました" : result.reason;
            return result.success;
        }

        public bool ResumeDevelopment(Faction faction, int programId, out string reason)
        {
            FactionState state = StateOf(faction);
            DevelopmentProgram program = FindProgram(state != null ? state.talentDevelopment : null, programId);
            DevelopmentActionResult result = TalentDevelopmentRules.Resume(
                state != null ? state.talentDevelopment : null, programId);
            if (result.success && program != null && program.kind == DevelopmentProgramKind.現役士官教育)
            {
                Person person = FindDevelopmentPerson(program.personId);
                if (person != null) person.schoolPostingUntilYear = campaignYear + Mathf.Max(1, program.durationYears - program.progressYears);
            }
            reason = result.success ? "再開しました" : result.reason;
            return result.success;
        }

        public TalentDevelopmentState TalentDevelopmentOf(Faction faction)
        {
            FactionState state = StateOf(faction);
            if (state == null) return null;
            if (state.talentDevelopment == null) state.talentDevelopment = new TalentDevelopmentState();
            TalentDevelopmentRules.NormalizeLoaded(state.talentDevelopment);
            return state.talentDevelopment;
        }

        public TalentDevelopmentState RefreshTalentDevelopmentProfiles(Faction faction)
        {
            FactionState state = StateOf(faction);
            if (state == null) return null;
            if (state.talentDevelopment == null) state.talentDevelopment = new TalentDevelopmentState();
            ReviewRoster(state, commanders, campaignYear);
            ReviewRoster(state, civilians, campaignYear);
            return state.talentDevelopment;
        }

        public StrategicFleet FindFleetForDevelopment(int fleetId) => FindStrategicFleet(fleetId);

        public Person FindDevelopmentPerson(int personId)
        {
            Person person = FindInRoster(commanders, personId);
            return person ?? FindInRoster(civilians, personId);
        }

        private static Person FindInRoster(IReadOnlyList<Person> roster, int personId)
        {
            if (roster == null) return null;
            for (int i = 0; i < roster.Count; i++)
                if (roster[i] != null && roster[i].id == personId) return roster[i];
            return null;
        }

        public void InterruptFleetTrainingForSortie(StrategicFleet fleet)
        {
            if (fleet == null) return;
            FactionState state = StateOf(fleet.faction);
            DevelopmentProgram p = ActiveFleetProgram(state != null ? state.talentDevelopment : null, fleet.id);
            if (p != null) TalentDevelopmentRules.Interrupt(state.talentDevelopment, p.id, "出撃のため中断");
            if (fleet.corpsId < 0) return;
            DevelopmentProgram corps = ActiveCorpsProgram(state != null ? state.talentDevelopment : null, fleet.corpsId);
            if (corps != null) TalentDevelopmentRules.Interrupt(state.talentDevelopment, corps.id, "隷下艦隊の出撃で中断");
        }

        public string BuildTalentDevelopmentDump()
        {
            var sb = new StringBuilder(4096);
            sb.Append("<b>教育・人材育成</b>　評価→投資→修了→実効効果\n");
            sb.Append("<color=#5b6b7a>──────────────────────────────────────────────</color>\n");
            CampaignState campaign = StrategySession.Campaign;
            if (campaign == null || campaign.states == null) return sb.Append("教育データがありません").ToString();
            for (int i = 0; i < campaign.states.Count; i++) AppendTalentState(sb, campaign.states[i]);
            return sb.ToString();
        }

        private void AppendTalentState(StringBuilder sb, FactionState faction)
        {
            if (faction == null) return;
            TalentDevelopmentState state = faction.talentDevelopment;
            TalentDevelopmentRules.NormalizeLoaded(state);
            sb.Append("\n<color=#e7e0b0>◤ ").Append(faction.faction).Append("</color>\n");
            int candidates = 0;
            for (int i = 0; i < state.people.Count; i++)
            {
                TalentDevelopmentProfile p = state.people[i];
                if (!TalentDevelopmentRules.IsInvestmentCandidate(p)) continue;
                candidates++;
                Person person = FindDevelopmentPerson(p.personId);
                sb.Append("  ").Append(person != null ? person.name : "人物#" + p.personId)
                  .Append("　").Append(PerformanceReviewRules.Box(p.performance, p.potential))
                  .Append("　実績 ").Append(p.performance.ToString("0.00"))
                  .Append("／潜在 ").Append(p.potential.ToString("0.00"))
                  .Append("　推奨 ").Append(TalentDevelopmentRules.RecommendedProgram(p))
                  .Append("　経験 ").Append(p.growth != null ? p.growth.experience.ToString("0.0") : "0").Append('\n');
            }
            if (candidates == 0) sb.Append("  育成投資候補なし\n");
            for (int i = 0; i < state.programs.Count; i++)
            {
                DevelopmentProgram p = state.programs[i];
                if (p == null) continue;
                sb.Append("  #").Append(p.id).Append(' ').Append(p.kind).Append("　").Append(p.status)
                  .Append("　").Append(p.progressYears).Append('/').Append(p.durationYears).Append("年")
                  .Append("　即応-").Append((p.readinessPenalty * 100f).ToString("0")).Append('%');
                if (!string.IsNullOrEmpty(p.result)) sb.Append("　").Append(p.result);
                sb.Append('\n');
            }
        }

        private int BestMentorId(Person target)
        {
            if (target == null) return -1;
            int bestId = -1;
            float best = target.role == PersonRole.軍人 ? target.MilitaryAptitude : target.CivilAptitude;
            IReadOnlyList<Person> roster = target.role == PersonRole.軍人 ? commanders : civilians;
            if (roster == null) return -1;
            for (int i = 0; i < roster.Count; i++)
            {
                Person p = roster[i];
                if (p == null || p.id == target.id || p.faction != target.faction || p.deathYear > 0) continue;
                float skill = target.role == PersonRole.軍人 ? p.MilitaryAptitude : p.CivilAptitude;
                if (skill <= best) continue;
                best = skill;
                bestId = p.id;
            }
            return bestId;
        }

        private static DevelopmentProgram ActiveFleetProgram(TalentDevelopmentState state, int fleetId)
        {
            if (state == null || state.programs == null) return null;
            for (int i = 0; i < state.programs.Count; i++)
            {
                DevelopmentProgram p = state.programs[i];
                if (p != null && p.status == DevelopmentProgramStatus.実施中 && p.fleetId == fleetId) return p;
            }
            return null;
        }

        private static DevelopmentProgram ActiveCorpsProgram(TalentDevelopmentState state, int corpsId)
        {
            if (state == null || state.programs == null) return null;
            for (int i = 0; i < state.programs.Count; i++)
            {
                DevelopmentProgram p = state.programs[i];
                if (p != null && p.status == DevelopmentProgramStatus.実施中 && p.corpsId == corpsId) return p;
            }
            return null;
        }

        private static DevelopmentProgram FindProgram(TalentDevelopmentState state, int programId)
        {
            if (state == null || state.programs == null) return null;
            for (int i = 0; i < state.programs.Count; i++)
                if (state.programs[i] != null && state.programs[i].id == programId) return state.programs[i];
            return null;
        }

        public void RunTalentDevelopmentAnnualTickForQa() => RunTalentDevelopmentAnnualTick();

        public void RunTalentDevelopmentAnnualTickForQa(int year)
        {
            campaignYear = year;
            RunTalentDevelopmentAnnualTick();
        }
    }
}
