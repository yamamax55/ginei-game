using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    public class FormationDensityRulesTests
    {
        [Test]
        public void Step_SpeedSpikeShorterThanHold_DoesNotOpenFormation()
        {
            var state = new FormationDensityState();
            state = FormationDensityRules.Step(state, 1f, false, 0.5f, 0.6f, 0.5f, 0.2f);
            state = FormationDensityRules.Step(state, 0f, false, 0.5f, 0.6f, 0.5f, 0.1f);
            Assert.IsFalse(state.cruising);
        }

        [Test]
        public void Step_SustainedMovementOpens_AndHysteresisKeepsFormationStable()
        {
            var state = new FormationDensityState();
            state = FormationDensityRules.Step(state, 1f, false, 0.5f, 0.6f, 0.5f, 0.3f);
            state = FormationDensityRules.Step(state, 1f, false, 0.5f, 0.6f, 0.5f, 0.3f);
            Assert.IsTrue(state.cruising);

            state = FormationDensityRules.Step(state, 0.4f, false, 0.5f, 0.6f, 0.5f, 1f);
            Assert.IsTrue(state.cruising, "進入閾値未満でも離脱閾値より速ければ航行隊形を保つ");
        }

        [Test]
        public void Step_CombatImmediatelyClosesFormation()
        {
            var state = new FormationDensityState { cruising = true };
            state = FormationDensityRules.Step(state, 10f, true, 0.5f, 0.6f, 0.5f, 0.01f);
            Assert.IsFalse(state.cruising);
            Assert.IsFalse(state.pending);
        }
    }
}
