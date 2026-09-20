using System.Collections.Generic;
using UnityEngine;

namespace Ginei
{
    public partial class GalaxyView
    {
        private const float EducationBudgetNeedShare = 0.15f;

        /// <summary>国家予算と所有人口から、勢力ごとの入学・卒業・教育品質を年次で進める。</summary>
        private void RunEducationAnnualTick() => RunEducationAnnualTick(campaignYear);

        private void RunEducationAnnualTick(int year)
        {
            CampaignState campaign = StrategySession.Campaign;
            if (campaign == null || campaign.states == null) return;
            for (int i = 0; i < campaign.states.Count; i++)
            {
                FactionState state = campaign.states[i];
                if (state == null) continue;
                if (state.education == null) state.education = new EducationState();

                float total = 0f, youth = 0f;
                if (map != null && provinces != null)
                    for (int s = 0; s < map.systems.Count; s++)
                    {
                        StarSystem system = map.systems[s];
                        if (system == null || system.owner != state.faction ||
                            !provinces.TryGetValue(system.id, out Province province) || province == null) continue;
                        total += Mathf.Max(0f, province.population);
                        if (province.demographics != null) youth += Mathf.Max(0f, province.demographics.youth);
                    }

                float annualCohort = EducationAnnualRules.AnnualAgeCohort(youth, total);
                ElementarySchool elementary = ElementarySchoolOf(state.faction);
                MiddleSchool middle = MiddleSchoolOf(state.faction);
                HighSchool high = HighSchoolOf(state.faction);
                float universityCapacity = 0f;
                if (universities != null)
                    for (int u = 0; u < universities.Count; u++)
                        if (universities[u] != null && universities[u].faction == state.faction &&
                            universities[u].track != CareerTrack.科挙)
                            universityCapacity += Mathf.Max(0, universities[u].capacity);

                List<EducationIntakePlan> plans = EducationAnnualRules.BuildPlans(
                    annualCohort,
                    elementary != null ? elementary.enrollmentRate : 0.95f,
                    middle != null ? middle.enrollmentRate : 0.8f,
                    high != null ? high.enrollmentRate : 0.6f,
                    universityCapacity);
                AppendHigherEducationPlan(plans, state.faction, annualCohort, SchoolType.高専);
                AppendHigherEducationPlan(plans, state.faction, annualCohort, SchoolType.短大);
                AppendHigherEducationPlan(plans, state.faction, annualCohort, SchoolType.専門学校);
                float revenue = FiscalRules.TaxRevenue(CampaignRules.EconomyBase(state), state.taxRate);
                float need = Mathf.Max(1f, revenue * EducationBudgetNeedShare);
                float funding = state.budget != null ? BudgetRules.Get(state.budget, BudgetCategory.教育) : 0f;
                EducationAnnualResult result = EducationAnnualRules.TickYear(state.education, year, funding, need, plans);
                if (!result.processed) continue;

                ApplyEducationQuality(state.faction, state.education.schoolQuality);
                if (result.graduated > 0f)
                    NotificationCenter.Push(NotificationCategory.人事, NotificationSeverity.情報,
                        $"{state.faction} 教育：{result.graduated:0.#}人が卒業（在学コホート {result.activeCohorts}）");
            }
        }

        private void AppendHigherEducationPlan(List<EducationIntakePlan> plans, Faction faction, float eligible, SchoolType type)
        {
            int capacity = 0;
            if (type == SchoolType.高専 && colleges != null)
            {
                for (int i = 0; i < colleges.Count; i++)
                    if (colleges[i] != null && colleges[i].faction == faction) capacity += Mathf.Max(0, colleges[i].capacity);
            }
            else if (type == SchoolType.短大 && juniorColleges != null)
            {
                for (int i = 0; i < juniorColleges.Count; i++)
                    if (juniorColleges[i] != null && juniorColleges[i].faction == faction) capacity += Mathf.Max(0, juniorColleges[i].capacity);
            }
            else if (type == SchoolType.専門学校 && vocationalSchools != null)
            {
                for (int i = 0; i < vocationalSchools.Count; i++)
                    if (vocationalSchools[i] != null && vocationalSchools[i].faction == faction) capacity += Mathf.Max(0, vocationalSchools[i].capacity);
            }
            if (capacity > 0) plans.Add(new EducationIntakePlan(type, eligible, capacity));
        }

        private int TakeEducationGraduates(Faction faction, SchoolType schoolType, int requested)
        {
            FactionState state = StrategySession.Campaign != null ? StateOf(faction) : null;
            return EducationAnnualRules.ConsumeGraduates(state != null ? state.education : null, schoolType, requested);
        }

        public void BindEducationInstitutionsForQa(
            List<University> qaUniversities,
            List<TechnicalCollege> qaColleges,
            List<JuniorCollege> qaJuniorColleges,
            List<VocationalSchool> qaVocationalSchools)
        {
            universities = qaUniversities;
            colleges = qaColleges;
            juniorColleges = qaJuniorColleges;
            vocationalSchools = qaVocationalSchools;
        }

        public int ConsumeEducationGraduatesForQa(Faction faction, SchoolType schoolType, int requested)
            => TakeEducationGraduates(faction, schoolType, requested);

        private void ApplyEducationQuality(Faction faction, float quality)
        {
            float q = Mathf.Clamp01(quality);
            ElementarySchool elementary = ElementarySchoolOf(faction);
            MiddleSchool middle = MiddleSchoolOf(faction);
            HighSchool high = HighSchoolOf(faction);
            if (elementary != null) elementary.quality = q;
            if (middle != null) middle.quality = q;
            if (high != null) high.quality = q;
        }

        /// <summary>PlayMode試験用：本番と同じ教育年次経路を指定年で一度進める。</summary>
        public void RunEducationAnnualTickForQa(int year) => RunEducationAnnualTick(year);
    }
}
