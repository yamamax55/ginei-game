using System.Collections.Generic;
using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    public class TalentDevelopmentRulesTests
    {
        [Test]
        public void Review_ConnectsHighPotentialToMentorshipAndStretchPlacement()
        {
            var state = new TalentDevelopmentState();
            TalentDevelopmentProfile promising = TalentDevelopmentRules.ReviewPerson(
                state, 10, 0.1f, 0.9f, 800, GrowthArchetype.在野俊英型);
            TalentDevelopmentProfile star = TalentDevelopmentRules.ReviewPerson(
                state, 11, 0.9f, 0.9f, 800, GrowthArchetype.首席型);

            Assert.AreEqual(DevelopmentProgramKind.師事, TalentDevelopmentRules.RecommendedProgram(promising));
            Assert.AreEqual(DevelopmentProgramKind.抜擢配置, TalentDevelopmentRules.RecommendedProgram(star));
            Assert.IsTrue(TalentDevelopmentRules.IsInvestmentCandidate(promising));
        }

        [Test]
        public void PersonProgram_CompletesOnceAndGrowsWithoutChangingReviewScores()
        {
            var state = new TalentDevelopmentState();
            TalentDevelopmentProfile profile = TalentDevelopmentRules.ReviewPerson(
                state, 10, 0.1f, 0.9f, 800, GrowthArchetype.在野俊英型);
            float before = profile.growth.experience;
            DevelopmentActionResult start = TalentDevelopmentRules.StartPersonProgram(
                state, DevelopmentProgramKind.師事, 10, 20, 800, 1, 50f, "mentor:10:800");

            Assert.IsTrue(start.success, start.reason);
            Assert.AreEqual(1, TalentDevelopmentRules.TickYear(state, 801));
            float after = profile.growth.experience;
            Assert.Greater(after, before);
            Assert.AreEqual(0, TalentDevelopmentRules.TickYear(state, 801));
            Assert.AreEqual(after, profile.growth.experience, 1e-5f);
            Assert.AreEqual(0.1f, profile.performance, 1e-5f);
            Assert.AreEqual(0.9f, profile.potential, 1e-5f);
            Assert.IsFalse(TalentDevelopmentRules.StartPersonProgram(
                state, DevelopmentProgramKind.師事, 10, 20, 802, 1, 1f, "mentor:10:800").success);
        }

        [Test]
        public void Interruption_StopsProgressAndReadinessPenaltyUntilResumed()
        {
            var state = new TalentDevelopmentState();
            DevelopmentProgram program = TalentDevelopmentRules.StartFleetTraining(
                state, 7, 800, 2, 0.8f, "fleet:7:800").program;
            Assert.Less(TalentDevelopmentRules.EffectiveFleetReadiness(state, 7, 0.8f), 0.8f);

            Assert.IsTrue(TalentDevelopmentRules.Interrupt(state, program.id, "出撃").success);
            Assert.AreEqual(0.8f, TalentDevelopmentRules.EffectiveFleetReadiness(state, 7, 0.8f), 1e-5f);
            Assert.IsFalse(TalentDevelopmentRules.StartFleetTraining(
                state, 7, 801, 1, 0.8f, "fleet:7:801").success, "中断中の課程を残して二重登録できてしまう");
            Assert.AreEqual(0, TalentDevelopmentRules.TickYear(state, 801));
            Assert.AreEqual(0, program.progressYears);

            Assert.IsTrue(TalentDevelopmentRules.Resume(state, program.id).success);
            Assert.AreEqual(0, TalentDevelopmentRules.TickYear(state, 802));
            Assert.AreEqual(1, program.progressYears);
            Assert.AreEqual(1, TalentDevelopmentRules.TickYear(state, 803));
            Assert.AreEqual(DevelopmentProgramStatus.修了, program.status);
            Assert.Greater(TalentDevelopmentRules.EffectiveFleetReadiness(state, 7, 0.8f), 0.8f);
        }

        [Test]
        public void FleetAndCorpsTraining_UseDiminishingCyclesAndImproveReadiness()
        {
            var state = new TalentDevelopmentState();
            DevelopmentProgram first = TalentDevelopmentRules.StartFleetTraining(
                state, 1, 800, 1, 1f, "fleet:1:800").program;
            TalentDevelopmentRules.TickYear(state, 801);
            float firstYield = first.experienceAwarded;
            DevelopmentProgram second = TalentDevelopmentRules.StartFleetTraining(
                state, 1, 801, 1, 1f, "fleet:1:801").program;
            TalentDevelopmentRules.TickYear(state, 802);
            Assert.Less(second.experienceAwarded, firstYield);

            DevelopmentProgram corps = TalentDevelopmentRules.StartCorpsExercise(
                state, 3, 802, 1, 0.8f, "corps:3:802").program;
            Assert.Less(TalentDevelopmentRules.EffectiveCorpsCoordination(state, 3, 0.6f), 0.6f);
            TalentDevelopmentRules.TickYear(state, 803);
            Assert.AreEqual(DevelopmentProgramStatus.修了, corps.status);
            Assert.Greater(TalentDevelopmentRules.EffectiveCorpsCoordination(state, 3, 0.6f), 0.6f);
        }

        [Test]
        public void NormalizeLoaded_RepairsNullListsAndPreventsIdReuse()
        {
            var state = new TalentDevelopmentState
            {
                nextProgramId = 0,
                people = null,
                fleets = null,
                corps = null,
                completedEventKeys = null,
                programs = new List<DevelopmentProgram>
                {
                    new DevelopmentProgram { id = 12, durationYears = 0, progressYears = 9 }
                }
            };

            TalentDevelopmentRules.NormalizeLoaded(state);

            Assert.IsNotNull(state.people);
            Assert.IsNotNull(state.completedEventKeys);
            Assert.AreEqual(13, state.nextProgramId);
            Assert.AreEqual(1, state.programs[0].durationYears);
            Assert.AreEqual(1, state.programs[0].progressYears);
        }

        [Test]
        public void CampaignSave_RoundTripKeepsInterruptedTrainingAndPreventsDuplicateCompletion()
        {
            var campaign = new CampaignState(new GalaxyMap());
            var faction = new FactionState(Faction.同盟);
            TalentDevelopmentRules.ReviewPerson(
                faction.talentDevelopment, 10, 0.1f, 0.9f, 800, GrowthArchetype.在野俊英型);
            DevelopmentProgram person = TalentDevelopmentRules.StartPersonProgram(
                faction.talentDevelopment, DevelopmentProgramKind.師事, 10, 20, 800, 1, 10f, "mentor:10:800").program;
            DevelopmentProgram fleet = TalentDevelopmentRules.StartFleetTraining(
                faction.talentDevelopment, 7, 800, 2, 0.8f, "fleet:7:800").program;
            TalentDevelopmentRules.Interrupt(faction.talentDevelopment, fleet.id, "出撃");
            campaign.states.Add(faction);

            CampaignState loaded = CampaignSerializer.FromJson(CampaignSerializer.ToJson(campaign));
            TalentDevelopmentState restored = loaded.states[0].talentDevelopment;

            Assert.IsNotNull(restored);
            Assert.AreEqual(2, restored.programs.Count);
            Assert.AreEqual(DevelopmentProgramStatus.中断,
                TalentDevelopmentRules.FindProgram(restored, fleet.id).status);
            Assert.AreEqual(1, TalentDevelopmentRules.TickYear(restored, 801));
            float experience = TalentDevelopmentRules.FindPerson(restored, 10).growth.experience;
            Assert.Greater(experience, 0f);
            Assert.AreEqual(0, TalentDevelopmentRules.TickYear(restored, 801));
            Assert.AreEqual(experience, TalentDevelopmentRules.FindPerson(restored, 10).growth.experience, 1e-5f);
            Assert.IsFalse(TalentDevelopmentRules.StartPersonProgram(
                restored, DevelopmentProgramKind.師事, 10, 20, 802, 1, 1f, person.eventKey).success);
        }
    }
}
