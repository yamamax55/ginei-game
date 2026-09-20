using System.Collections.Generic;
using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    public class EducationAnnualRulesTests
    {
        [Test]
        public void BuildPlans_UsesAgeCohortRatesAndUniversityCapacity()
        {
            Assert.AreEqual(10f, EducationAnnualRules.AnnualAgeCohort(150f, 1000f), 1e-5f);
            Assert.AreEqual(12.5f, EducationAnnualRules.AnnualAgeCohort(0f, 1000f), 1e-5f);
            List<EducationIntakePlan> plans = EducationAnnualRules.BuildPlans(10f, 0.9f, 0.8f, 0.6f, 3f);
            Assert.AreEqual(4, plans.Count);
            Assert.AreEqual(9f, plans[0].capacity, 1e-5f);
            Assert.AreEqual(8f, plans[1].capacity, 1e-5f);
            Assert.AreEqual(6f, plans[2].capacity, 1e-5f);
            Assert.AreEqual(3f, plans[3].capacity, 1e-5f);
        }

        [Test]
        public void Intake_IsBoundedByEligibleCapacityAndFunding()
        {
            var state = new EducationState();
            var plans = new List<EducationIntakePlan>
            {
                new EducationIntakePlan(SchoolType.小学校, eligibleStudents: 120f, capacity: 80f)
            };

            EducationAnnualResult result = EducationAnnualRules.TickYear(state, 800, funding: 50f, fundingNeed: 100f, plans);

            Assert.IsTrue(result.processed);
            Assert.AreEqual(0.5f, result.fundingFactor, 1e-5f);
            Assert.AreEqual(40f, result.admitted, 1e-5f);
            Assert.AreEqual(1, state.activeCohorts.Count);
            Assert.AreEqual(806, state.activeCohorts[0].graduationYear);
            Assert.AreEqual(6, state.activeCohorts[0].AgeAt(800));
        }

        [Test]
        public void Graduation_IsCountedOnceAndRemovedFromEnrollment()
        {
            var state = new EducationState
            {
                lastProcessedYear = 805,
                activeCohorts = new List<EducationCohort>
                {
                    new EducationCohort(7, SchoolType.小学校, 800, 30f)
                }
            };

            EducationAnnualResult first = EducationAnnualRules.TickYear(state, 806, 0f, 100f, null);
            EducationAnnualResult duplicate = EducationAnnualRules.TickYear(state, 806, 0f, 100f, null);

            Assert.AreEqual(30f, first.graduated, 1e-5f);
            Assert.AreEqual(30f, state.totalGraduates, 1e-5f);
            Assert.AreEqual(0, state.activeCohorts.Count);
            Assert.IsFalse(duplicate.processed);
            Assert.AreEqual(0f, duplicate.graduated, 1e-5f);
            Assert.AreEqual(30f, state.totalGraduates, 1e-5f);
            Assert.AreEqual(30f, EducationAnnualRules.AvailableGraduates(state, SchoolType.小学校), 1e-5f);
        }

        [Test]
        public void GraduateSupply_IsSeparatedBySchoolAndConsumedOnlyOnce()
        {
            var state = new EducationState
            {
                lastProcessedYear = 803,
                activeCohorts = new List<EducationCohort>
                {
                    new EducationCohort(1, SchoolType.大学, 800, 4.5f),
                    new EducationCohort(2, SchoolType.短大, 802, 3f)
                }
            };

            EducationAnnualRules.TickYear(state, 804, 0f, 100f, null);

            Assert.AreEqual(4.5f, EducationAnnualRules.AvailableGraduates(state, SchoolType.大学), 1e-5f);
            Assert.AreEqual(3f, EducationAnnualRules.AvailableGraduates(state, SchoolType.短大), 1e-5f);
            Assert.AreEqual(4, EducationAnnualRules.ConsumeGraduates(state, SchoolType.大学, 10));
            Assert.AreEqual(0, EducationAnnualRules.ConsumeGraduates(state, SchoolType.大学, 10));
            Assert.AreEqual(0.5f, EducationAnnualRules.AvailableGraduates(state, SchoolType.大学), 1e-5f);
            Assert.AreEqual(3, EducationAnnualRules.ConsumeGraduates(state, SchoolType.短大, 3));
        }

        [Test]
        public void ExistingIdsAndNullList_AreRecoveredForOldState()
        {
            var emptyOld = new EducationState { activeCohorts = null, nextCohortId = 0 };
            Assert.DoesNotThrow(() => EducationAnnualRules.TickYear(emptyOld, 800, 100f, 100f,
                new List<EducationIntakePlan> { new EducationIntakePlan(SchoolType.中学校, 10f, 10f) }));
            Assert.AreEqual(1, emptyOld.activeCohorts[0].id);

            var loaded = new EducationState
            {
                nextCohortId = 1,
                activeCohorts = new List<EducationCohort> { new EducationCohort(12, SchoolType.高校, 799, 5f) }
            };
            EducationAnnualRules.TickYear(loaded, 800, 100f, 100f,
                new List<EducationIntakePlan> { new EducationIntakePlan(SchoolType.大学, 4f, 4f) });
            Assert.AreEqual(13, loaded.activeCohorts[1].id);
        }

        [Test]
        public void QualityUsesExistingGenerationLagRules()
        {
            var state = new EducationState { schoolQuality = 0f, talentQuality = 0f };
            EducationAnnualRules.TickYear(state, 800, 100f, 100f, null);

            Assert.AreEqual(0.05f, state.schoolQuality, 1e-5f);
            Assert.AreEqual(0.0025f, state.talentQuality, 1e-5f,
                "学校の質0.05へ世代遅延1/20で追従する");
        }

        [Test]
        public void InvalidOrShortTrainingPlans_DoNotCreatePersistentCohorts()
        {
            var state = new EducationState();
            var plans = new List<EducationIntakePlan>
            {
                new EducationIntakePlan(SchoolType.新兵訓練, 100f, 100f),
                new EducationIntakePlan(SchoolType.大学, -10f, 20f)
            };

            EducationAnnualResult result = EducationAnnualRules.TickYear(state, 800, 100f, 100f, plans);

            Assert.AreEqual(0f, result.admitted, 1e-5f);
            Assert.AreEqual(0, state.activeCohorts.Count);
        }

        [Test]
        public void CampaignSave_RoundTripPreservesCohortsAndPreventsDuplicateGraduation()
        {
            var campaign = new CampaignState(new GalaxyMap());
            var faction = new FactionState(Faction.同盟);
            faction.education.schoolQuality = 0.7f;
            faction.education.talentQuality = 0.4f;
            faction.education.lastProcessedYear = 805;
            faction.education.nextCohortId = 9;
            faction.education.totalGraduates = 12f;
            faction.education.activeCohorts.Add(new EducationCohort(8, SchoolType.小学校, 800, 30f));
            faction.education.graduateSupply.Add(new EducationGraduateSupply(SchoolType.大学, 2.5f));
            faction.budget.education = 25f;
            campaign.states.Add(faction);

            CampaignState loaded = CampaignSerializer.FromJson(CampaignSerializer.ToJson(campaign));
            EducationState restored = loaded.states[0].education;

            Assert.IsNotNull(restored);
            Assert.AreEqual(0.7f, restored.schoolQuality, 1e-5f);
            Assert.AreEqual(1, restored.activeCohorts.Count);
            Assert.AreEqual(9, restored.nextCohortId);
            Assert.AreEqual(2.5f, EducationAnnualRules.AvailableGraduates(restored, SchoolType.大学), 1e-5f);
            Assert.AreEqual(25f, loaded.states[0].budget.education, 1e-5f);
            EducationAnnualResult result = EducationAnnualRules.TickYear(restored, 806, 0f, 100f, null);
            Assert.AreEqual(30f, result.graduated, 1e-5f);
            Assert.AreEqual(42f, restored.totalGraduates, 1e-5f);
            Assert.IsFalse(EducationAnnualRules.TickYear(restored, 806, 0f, 100f, null).processed);
            Assert.AreEqual(42f, restored.totalGraduates, 1e-5f);
        }

        [Test]
        public void OldSaveWithoutEducationGetsBaselineState()
        {
            var raw = CampaignSerializer.ToSaveData(new CampaignState(new GalaxyMap()));
            raw.states.Add(new FactionStateSave { faction = (int)Faction.帝国, hasEducation = false, education = null });

            EducationState restored = CampaignSerializer.FromSaveData(raw).states[0].education;

            Assert.IsNotNull(restored);
            Assert.AreEqual(0.5f, restored.schoolQuality, 1e-5f);
            Assert.AreEqual(0, restored.activeCohorts.Count);
        }
    }
}
