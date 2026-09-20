using System.Collections.Generic;
using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    public class EducationAnnualRulesTests
    {
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
    }
}
