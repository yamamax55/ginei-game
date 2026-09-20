using System;
using System.Collections.Generic;
using UnityEngine;

namespace Ginei
{
    public enum DevelopmentProgramKind
    {
        師事,
        抜擢配置,
        現役士官教育,
        艦隊訓練,
        軍団共同演習
    }

    public enum DevelopmentProgramStatus
    {
        実施中,
        中断,
        修了,
        取消
    }

    [Serializable]
    public class TalentDevelopmentProfile
    {
        public int personId = -1;
        public float performance;
        public float potential;
        public int lastReviewYear;
        public float totalInvestment;
        public Growth growth = new Growth();
    }

    [Serializable]
    public class FleetTrainingProfile
    {
        public int fleetId = -1;
        public float proficiency;
        public int completedCycles;
    }

    [Serializable]
    public class CorpsTrainingProfile
    {
        public int corpsId = -1;
        public float coordination;
        public int completedCycles;
    }

    [Serializable]
    public class DevelopmentProgram
    {
        public int id;
        public string eventKey = "";
        public DevelopmentProgramKind kind;
        public DevelopmentProgramStatus status;
        public int personId = -1;
        public int mentorPersonId = -1;
        public int fleetId = -1;
        public int corpsId = -1;
        public int startYear;
        public int durationYears = 1;
        public int progressYears;
        public int completedYear;
        public int repetitions;
        public float difficulty = 0.5f;
        public float readinessPenalty;
        public float experienceAwarded;
        public string result = "";
    }

    [Serializable]
    public class TalentDevelopmentState
    {
        public int nextProgramId = 1;
        public int lastProcessedYear;
        public List<TalentDevelopmentProfile> people = new List<TalentDevelopmentProfile>();
        public List<FleetTrainingProfile> fleets = new List<FleetTrainingProfile>();
        public List<CorpsTrainingProfile> corps = new List<CorpsTrainingProfile>();
        public List<DevelopmentProgram> programs = new List<DevelopmentProgram>();
        public List<string> completedEventKeys = new List<string>();
    }

    public readonly struct DevelopmentActionResult
    {
        public readonly bool success;
        public readonly string reason;
        public readonly DevelopmentProgram program;

        public DevelopmentActionResult(bool success, string reason, DevelopmentProgram program)
        {
            this.success = success;
            this.reason = reason ?? "";
            this.program = program;
        }
    }

    /// <summary>
    /// 9-box評価、師事、成長曲線、現役教育、艦隊訓練、軍団共同演習を一つの保存台帳へ束ねる。
    /// 能力基準値は変えず、人物は <see cref="Growth"/>、部隊は熟練・連携の実効値へ蓄積する。
    /// </summary>
    public static class TalentDevelopmentRules
    {
        public const float ActiveReadinessPenalty = 0.2f;
        public const float FleetReadinessBonus = 0.1f;
        public const float CorpsReadinessBonus = 0.08f;
        public const float MinimumInvestmentPriority = 0.45f;

        public static void NormalizeLoaded(TalentDevelopmentState state)
        {
            if (state == null) return;
            if (state.people == null) state.people = new List<TalentDevelopmentProfile>();
            if (state.fleets == null) state.fleets = new List<FleetTrainingProfile>();
            if (state.corps == null) state.corps = new List<CorpsTrainingProfile>();
            if (state.programs == null) state.programs = new List<DevelopmentProgram>();
            if (state.completedEventKeys == null) state.completedEventKeys = new List<string>();

            int maxId = 0;
            for (int i = state.people.Count - 1; i >= 0; i--)
            {
                TalentDevelopmentProfile p = state.people[i];
                if (p == null || p.personId < 0) { state.people.RemoveAt(i); continue; }
                p.performance = Mathf.Clamp01(p.performance);
                p.potential = Mathf.Clamp01(p.potential);
                p.totalInvestment = Mathf.Max(0f, p.totalInvestment);
                if (p.growth == null) p.growth = new Growth();
                p.growth.experience = Mathf.Max(0f, p.growth.experience);
            }
            for (int i = state.fleets.Count - 1; i >= 0; i--)
            {
                FleetTrainingProfile p = state.fleets[i];
                if (p == null || p.fleetId < 0) { state.fleets.RemoveAt(i); continue; }
                p.proficiency = Mathf.Clamp01(p.proficiency);
                p.completedCycles = Mathf.Max(0, p.completedCycles);
            }
            for (int i = state.corps.Count - 1; i >= 0; i--)
            {
                CorpsTrainingProfile p = state.corps[i];
                if (p == null || p.corpsId < 0) { state.corps.RemoveAt(i); continue; }
                p.coordination = Mathf.Clamp01(p.coordination);
                p.completedCycles = Mathf.Max(0, p.completedCycles);
            }
            for (int i = state.programs.Count - 1; i >= 0; i--)
            {
                DevelopmentProgram p = state.programs[i];
                if (p == null) { state.programs.RemoveAt(i); continue; }
                maxId = Mathf.Max(maxId, p.id);
                p.durationYears = Mathf.Max(1, p.durationYears);
                p.progressYears = Mathf.Clamp(p.progressYears, 0, p.durationYears);
                p.difficulty = Mathf.Clamp01(p.difficulty);
                p.readinessPenalty = Mathf.Clamp01(p.readinessPenalty);
                p.experienceAwarded = Mathf.Max(0f, p.experienceAwarded);
            }
            if (state.nextProgramId <= maxId) state.nextProgramId = maxId + 1;
            if (state.nextProgramId <= 0) state.nextProgramId = 1;
        }

        public static TalentDevelopmentProfile ReviewPerson(
            TalentDevelopmentState state, int personId, float performance, float potential,
            int year, GrowthArchetype archetype)
        {
            if (state == null || personId < 0) return null;
            NormalizeLoaded(state);
            TalentDevelopmentProfile profile = FindPerson(state, personId);
            if (profile == null)
            {
                profile = new TalentDevelopmentProfile { personId = personId, growth = new Growth(archetype) };
                state.people.Add(profile);
            }
            profile.performance = Mathf.Clamp01(performance);
            profile.potential = Mathf.Clamp01(potential);
            profile.lastReviewYear = year;
            if (profile.growth == null) profile.growth = new Growth(archetype);
            return profile;
        }

        public static DevelopmentProgramKind RecommendedProgram(TalentDevelopmentProfile profile)
        {
            if (profile == null) return DevelopmentProgramKind.現役士官教育;
            TalentBox box = PerformanceReviewRules.Box(profile.performance, profile.potential);
            if (box == TalentBox.有望株) return DevelopmentProgramKind.師事;
            if (box == TalentBox.安定貢献者 || box == TalentBox.スター人材)
                return DevelopmentProgramKind.抜擢配置;
            return DevelopmentProgramKind.現役士官教育;
        }

        public static bool IsInvestmentCandidate(TalentDevelopmentProfile profile)
            => profile != null && PerformanceReviewRules.DevelopmentPriority(
                profile.performance, profile.potential) >= MinimumInvestmentPriority;

        public static DevelopmentActionResult StartPersonProgram(
            TalentDevelopmentState state, DevelopmentProgramKind kind, int personId, int mentorPersonId,
            int year, int durationYears, float investment, string eventKey)
        {
            if (state == null) return Fail("育成台帳がありません");
            NormalizeLoaded(state);
            if (kind != DevelopmentProgramKind.師事 && kind != DevelopmentProgramKind.抜擢配置 &&
                kind != DevelopmentProgramKind.現役士官教育) return Fail("人物向けの育成種別ではありません");
            TalentDevelopmentProfile profile = FindPerson(state, personId);
            if (profile == null) return Fail("人事評価がありません");
            if (!IsInvestmentCandidate(profile)) return Fail("育成投資の優先度が不足しています");
            if (kind == DevelopmentProgramKind.師事 && (mentorPersonId < 0 || mentorPersonId == personId))
                return Fail("有効な師を指定してください");
            string duplicate = DuplicateReason(state, eventKey, personId, -1, -1);
            if (duplicate.Length > 0) return Fail(duplicate);

            var program = NewProgram(state, kind, year, durationYears, eventKey);
            program.personId = personId;
            program.mentorPersonId = kind == DevelopmentProgramKind.師事 ? mentorPersonId : -1;
            program.readinessPenalty = kind == DevelopmentProgramKind.現役士官教育 ? ActiveReadinessPenalty : 0f;
            profile.totalInvestment += Mathf.Max(0f, investment);
            state.programs.Add(program);
            return Ok(program);
        }

        public static DevelopmentActionResult StartFleetTraining(
            TalentDevelopmentState state, int fleetId, int year, int durationYears,
            float difficulty, string eventKey)
        {
            if (state == null) return Fail("育成台帳がありません");
            NormalizeLoaded(state);
            if (fleetId < 0) return Fail("艦隊がありません");
            string duplicate = DuplicateReason(state, eventKey, -1, fleetId, -1);
            if (duplicate.Length > 0) return Fail(duplicate);
            var program = NewProgram(state, DevelopmentProgramKind.艦隊訓練, year, durationYears, eventKey);
            program.fleetId = fleetId;
            program.difficulty = Mathf.Clamp01(difficulty);
            program.readinessPenalty = ActiveReadinessPenalty;
            program.repetitions = GetFleet(state, fleetId, true).completedCycles;
            state.programs.Add(program);
            return Ok(program);
        }

        public static DevelopmentActionResult StartCorpsExercise(
            TalentDevelopmentState state, int corpsId, int year, int durationYears,
            float difficulty, string eventKey)
        {
            if (state == null) return Fail("育成台帳がありません");
            NormalizeLoaded(state);
            if (corpsId < 0) return Fail("軍団がありません");
            string duplicate = DuplicateReason(state, eventKey, -1, -1, corpsId);
            if (duplicate.Length > 0) return Fail(duplicate);
            var program = NewProgram(state, DevelopmentProgramKind.軍団共同演習, year, durationYears, eventKey);
            program.corpsId = corpsId;
            program.difficulty = Mathf.Clamp01(difficulty);
            program.readinessPenalty = ActiveReadinessPenalty;
            program.repetitions = GetCorps(state, corpsId, true).completedCycles;
            state.programs.Add(program);
            return Ok(program);
        }

        public static DevelopmentActionResult Interrupt(TalentDevelopmentState state, int programId, string reason)
        {
            DevelopmentProgram program = FindProgram(state, programId);
            if (program == null) return Fail("育成計画がありません");
            if (program.status != DevelopmentProgramStatus.実施中) return Fail("実施中ではありません");
            program.status = DevelopmentProgramStatus.中断;
            program.result = string.IsNullOrEmpty(reason) ? "中断" : reason;
            return Ok(program);
        }

        public static DevelopmentActionResult Resume(TalentDevelopmentState state, int programId)
        {
            DevelopmentProgram program = FindProgram(state, programId);
            if (program == null) return Fail("育成計画がありません");
            if (program.status != DevelopmentProgramStatus.中断) return Fail("中断中ではありません");
            program.status = DevelopmentProgramStatus.実施中;
            program.result = "再開";
            return Ok(program);
        }

        /// <summary>同一年は一度だけ進め、中断中は進めない。修了効果も一度だけ反映する。</summary>
        public static int TickYear(TalentDevelopmentState state, int year)
        {
            if (state == null) return 0;
            NormalizeLoaded(state);
            if (year <= state.lastProcessedYear) return 0;
            int completed = 0;
            for (int i = 0; i < state.programs.Count; i++)
            {
                DevelopmentProgram program = state.programs[i];
                if (program == null || program.status != DevelopmentProgramStatus.実施中 || year <= program.startYear) continue;
                program.progressYears = Mathf.Min(program.durationYears, program.progressYears + 1);
                if (program.progressYears < program.durationYears) continue;
                Complete(state, program, year);
                completed++;
            }
            state.lastProcessedYear = year;
            return completed;
        }

        public static float EffectiveFleetReadiness(TalentDevelopmentState state, int fleetId, float baseReadiness)
        {
            float value = Mathf.Clamp01(baseReadiness);
            DevelopmentProgram active = ActiveFor(state, -1, fleetId, -1);
            if (active != null) value *= 1f - Mathf.Clamp01(active.readinessPenalty);
            FleetTrainingProfile profile = GetFleet(state, fleetId, false);
            if (profile != null) value += profile.proficiency * FleetReadinessBonus;
            return Mathf.Clamp01(value);
        }

        public static float EffectiveCorpsCoordination(TalentDevelopmentState state, int corpsId, float baseCoordination)
        {
            float value = Mathf.Clamp01(baseCoordination);
            DevelopmentProgram active = ActiveFor(state, -1, -1, corpsId);
            if (active != null) value *= 1f - Mathf.Clamp01(active.readinessPenalty);
            CorpsTrainingProfile profile = GetCorps(state, corpsId, false);
            if (profile != null) value += profile.coordination * CorpsReadinessBonus;
            return Mathf.Clamp01(value);
        }

        public static TalentDevelopmentProfile FindPerson(TalentDevelopmentState state, int personId)
        {
            if (state == null || state.people == null) return null;
            for (int i = 0; i < state.people.Count; i++)
                if (state.people[i] != null && state.people[i].personId == personId) return state.people[i];
            return null;
        }

        public static DevelopmentProgram FindProgram(TalentDevelopmentState state, int programId)
        {
            if (state == null || state.programs == null) return null;
            for (int i = 0; i < state.programs.Count; i++)
                if (state.programs[i] != null && state.programs[i].id == programId) return state.programs[i];
            return null;
        }

        private static void Complete(TalentDevelopmentState state, DevelopmentProgram program, int year)
        {
            if (program.status != DevelopmentProgramStatus.実施中) return;
            program.status = DevelopmentProgramStatus.修了;
            program.completedYear = year;
            if (!string.IsNullOrEmpty(program.eventKey) && !Contains(state.completedEventKeys, program.eventKey))
                state.completedEventKeys.Add(program.eventKey);

            if (program.kind == DevelopmentProgramKind.艦隊訓練)
            {
                FleetTrainingProfile fleet = GetFleet(state, program.fleetId, true);
                float yield = DrillYield(program.repetitions, program.difficulty);
                fleet.proficiency = Mathf.Clamp01(fleet.proficiency + yield / 10f);
                fleet.completedCycles++;
                program.experienceAwarded = yield;
                program.result = "艦隊練度が向上";
                return;
            }
            if (program.kind == DevelopmentProgramKind.軍団共同演習)
            {
                CorpsTrainingProfile corps = GetCorps(state, program.corpsId, true);
                float yield = DrillYield(program.repetitions, program.difficulty);
                corps.coordination = Mathf.Clamp01(corps.coordination + yield / 10f);
                corps.completedCycles++;
                program.experienceAwarded = yield;
                program.result = "軍団連携が向上";
                return;
            }

            TalentDevelopmentProfile person = FindPerson(state, program.personId);
            if (person == null) { program.result = "対象者不在"; return; }
            float amount;
            if (program.kind == DevelopmentProgramKind.師事)
            {
                float quality = Mathf.Clamp01(0.5f + person.potential * 0.5f);
                amount = 12f * MentorshipRules.LearningMultiplier(quality, person.potential);
            }
            else if (program.kind == DevelopmentProgramKind.抜擢配置)
            {
                amount = 15f * PerformanceReviewRules.PromotionReadiness(person.performance, person.potential);
            }
            else
            {
                amount = DrillYield(program.repetitions, person.potential) * 8f;
            }
            float before = person.growth.experience;
            GrowthRules.GainExperience(person.growth, amount, 1f);
            program.experienceAwarded = Mathf.Max(0f, person.growth.experience - before);
            program.result = "育成修了";
        }

        private static float DrillYield(int repetitions, float difficulty)
            => TrainingCycleRules.ExperienceYield(
                new AutoBattleResult(true, 100, 60d), repetitions, false, difficulty);

        private static DevelopmentProgram NewProgram(
            TalentDevelopmentState state, DevelopmentProgramKind kind, int year, int durationYears, string eventKey)
        {
            return new DevelopmentProgram
            {
                id = state.nextProgramId++, kind = kind, status = DevelopmentProgramStatus.実施中,
                startYear = year, durationYears = Mathf.Max(1, durationYears),
                eventKey = eventKey ?? "", readinessPenalty = 0f
            };
        }

        private static string DuplicateReason(
            TalentDevelopmentState state, string eventKey, int personId, int fleetId, int corpsId)
        {
            if (!string.IsNullOrEmpty(eventKey) && Contains(state.completedEventKeys, eventKey))
                return "同じ育成イベントは修了済みです";
            if (!string.IsNullOrEmpty(eventKey))
                for (int i = 0; i < state.programs.Count; i++)
                    if (state.programs[i] != null && state.programs[i].eventKey == eventKey)
                        return "同じ育成イベントが登録済みです";
            if (OpenFor(state, personId, fleetId, corpsId) != null) return "対象は別の育成を実施中または中断中です";
            return "";
        }

        private static DevelopmentProgram OpenFor(TalentDevelopmentState state, int personId, int fleetId, int corpsId)
        {
            if (state == null || state.programs == null) return null;
            for (int i = 0; i < state.programs.Count; i++)
            {
                DevelopmentProgram p = state.programs[i];
                if (p == null || (p.status != DevelopmentProgramStatus.実施中 && p.status != DevelopmentProgramStatus.中断)) continue;
                if (personId >= 0 && p.personId == personId) return p;
                if (fleetId >= 0 && p.fleetId == fleetId) return p;
                if (corpsId >= 0 && p.corpsId == corpsId) return p;
            }
            return null;
        }

        private static DevelopmentProgram ActiveFor(TalentDevelopmentState state, int personId, int fleetId, int corpsId)
        {
            if (state == null || state.programs == null) return null;
            for (int i = 0; i < state.programs.Count; i++)
            {
                DevelopmentProgram p = state.programs[i];
                if (p == null || p.status != DevelopmentProgramStatus.実施中) continue;
                if (personId >= 0 && p.personId == personId) return p;
                if (fleetId >= 0 && p.fleetId == fleetId) return p;
                if (corpsId >= 0 && p.corpsId == corpsId) return p;
            }
            return null;
        }

        private static FleetTrainingProfile GetFleet(TalentDevelopmentState state, int fleetId, bool create)
        {
            if (state == null || state.fleets == null) return null;
            for (int i = 0; i < state.fleets.Count; i++)
                if (state.fleets[i] != null && state.fleets[i].fleetId == fleetId) return state.fleets[i];
            if (!create) return null;
            var result = new FleetTrainingProfile { fleetId = fleetId };
            state.fleets.Add(result);
            return result;
        }

        private static CorpsTrainingProfile GetCorps(TalentDevelopmentState state, int corpsId, bool create)
        {
            if (state == null || state.corps == null) return null;
            for (int i = 0; i < state.corps.Count; i++)
                if (state.corps[i] != null && state.corps[i].corpsId == corpsId) return state.corps[i];
            if (!create) return null;
            var result = new CorpsTrainingProfile { corpsId = corpsId };
            state.corps.Add(result);
            return result;
        }

        private static bool Contains(List<string> values, string value)
        {
            if (values == null || string.IsNullOrEmpty(value)) return false;
            for (int i = 0; i < values.Count; i++) if (values[i] == value) return true;
            return false;
        }

        private static DevelopmentActionResult Ok(DevelopmentProgram program)
            => new DevelopmentActionResult(true, "", program);

        private static DevelopmentActionResult Fail(string reason)
            => new DevelopmentActionResult(false, reason, null);
    }
}
