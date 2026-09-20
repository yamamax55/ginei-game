using System;
using System.Collections.Generic;
using UnityEngine;

namespace Ginei
{
    /// <summary>学校種別ごとの年次入学計画。対象人口と定員は人数、予算は年次処理全体で共通。</summary>
    [Serializable]
    public class EducationIntakePlan
    {
        public SchoolType schoolType;
        public float eligibleStudents;
        public float capacity;

        public EducationIntakePlan() { }

        public EducationIntakePlan(SchoolType schoolType, float eligibleStudents, float capacity)
        {
            this.schoolType = schoolType;
            this.eligibleStudents = Mathf.Max(0f, eligibleStudents);
            this.capacity = Mathf.Max(0f, capacity);
        }
    }

    /// <summary>同じ年に入学し、同じ年に卒業する在学者の集約。</summary>
    [Serializable]
    public class EducationCohort
    {
        public int id;
        public SchoolType schoolType;
        public int entryYear;
        public int graduationYear;
        public float students;

        public EducationCohort() { }

        public EducationCohort(int id, SchoolType schoolType, int entryYear, float students)
        {
            this.id = id;
            this.schoolType = schoolType;
            this.entryYear = entryYear;
            graduationYear = entryYear + Mathf.Max(0, SchoolAgeRules.DurationYears(schoolType));
            this.students = Mathf.Max(0f, students);
        }

        public int AgeAt(int year)
            => SchoolAgeRules.EntryAge(schoolType) + Mathf.Max(0, year - entryYear);
    }

    /// <summary>教育の永続状態。activeCohorts は在学者だけを持ち、卒業済みは累計へ移す。</summary>
    [Serializable]
    public class EducationState
    {
        public float schoolQuality = 0.5f;
        public float talentQuality = 0.5f;
        public int lastProcessedYear;
        public int nextCohortId = 1;
        public float totalGraduates;
        public List<EducationCohort> activeCohorts = new List<EducationCohort>();
    }

    /// <summary>1年分の教育処理結果。表示・人材供給側が状態を再計算せず読める。</summary>
    public readonly struct EducationAnnualResult
    {
        public readonly bool processed;
        public readonly float fundingFactor;
        public readonly float admitted;
        public readonly float graduated;
        public readonly int activeCohorts;

        public EducationAnnualResult(bool processed, float fundingFactor, float admitted, float graduated, int activeCohorts)
        {
            this.processed = processed;
            this.fundingFactor = fundingFactor;
            this.admitted = admitted;
            this.graduated = graduated;
            this.activeCohorts = activeCohorts;
        }
    }

    /// <summary>
    /// 教育の年次共通入口。予算充足率で入学規模を制約し、既存の教育品質と世代遅延を進める。
    /// 同一年の再実行を拒否するため、保存・再開や画面遷移で卒業者が重複しない。
    /// </summary>
    public static class EducationAnnualRules
    {
        /// <summary>旧セーブや欠落フィールドを、年次処理を進めず安全な状態へ正規化する。</summary>
        public static void NormalizeLoaded(EducationState state)
        {
            if (state == null) return;
            EnsureInitialized(state);
        }

        public static float FundingFactor(float funding, float need)
        {
            float required = Mathf.Max(0f, need);
            if (required <= 0f) return 1f;
            return Mathf.Clamp01(Mathf.Max(0f, funding) / required);
        }

        public static EducationAnnualResult TickYear(
            EducationState state,
            int year,
            float funding,
            float fundingNeed,
            IList<EducationIntakePlan> plans,
            EducationParams parameters)
        {
            if (state == null) return new EducationAnnualResult(false, 0f, 0f, 0f, 0);
            EnsureInitialized(state);
            if (year <= state.lastProcessedYear)
                return new EducationAnnualResult(false, FundingFactor(funding, fundingNeed), 0f, 0f, state.activeCohorts.Count);

            float graduated = 0f;
            for (int i = state.activeCohorts.Count - 1; i >= 0; i--)
            {
                EducationCohort cohort = state.activeCohorts[i];
                if (cohort == null || cohort.students <= 0f)
                {
                    state.activeCohorts.RemoveAt(i);
                    continue;
                }
                if (cohort.graduationYear <= year)
                {
                    graduated += cohort.students;
                    state.activeCohorts.RemoveAt(i);
                }
            }

            float factor = FundingFactor(funding, fundingNeed);
            float admitted = 0f;
            if (plans != null)
            {
                for (int i = 0; i < plans.Count; i++)
                {
                    EducationIntakePlan plan = plans[i];
                    if (plan == null) continue;
                    int duration = SchoolAgeRules.DurationYears(plan.schoolType);
                    if (duration <= 0) continue;
                    float count = Mathf.Min(Mathf.Max(0f, plan.eligibleStudents), Mathf.Max(0f, plan.capacity)) * factor;
                    if (count <= 0f) continue;
                    state.activeCohorts.Add(new EducationCohort(state.nextCohortId++, plan.schoolType, year, count));
                    admitted += count;
                }
            }

            state.schoolQuality = EducationRules.SchoolQualityTick(state.schoolQuality, factor, 1f, parameters);
            state.talentQuality = EducationRules.TalentQualityTick(state.talentQuality, state.schoolQuality, 1f, parameters);
            state.totalGraduates = Mathf.Max(0f, state.totalGraduates) + graduated;
            state.lastProcessedYear = year;
            return new EducationAnnualResult(true, factor, admitted, graduated, state.activeCohorts.Count);
        }

        public static EducationAnnualResult TickYear(
            EducationState state, int year, float funding, float fundingNeed, IList<EducationIntakePlan> plans)
            => TickYear(state, year, funding, fundingNeed, plans, EducationParams.Default);

        private static void EnsureInitialized(EducationState state)
        {
            if (state.activeCohorts == null) state.activeCohorts = new List<EducationCohort>();
            int maxId = 0;
            for (int i = 0; i < state.activeCohorts.Count; i++)
                if (state.activeCohorts[i] != null) maxId = Mathf.Max(maxId, state.activeCohorts[i].id);
            if (state.nextCohortId <= maxId) state.nextCohortId = maxId + 1;
            if (state.nextCohortId <= 0) state.nextCohortId = 1;
            state.schoolQuality = Mathf.Clamp01(state.schoolQuality);
            state.talentQuality = Mathf.Clamp01(state.talentQuality);
        }
    }
}
