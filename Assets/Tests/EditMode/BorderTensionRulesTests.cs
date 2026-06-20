using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>BorderTensionRules の EditMode テスト。</summary>
    public class BorderTensionRulesTests
    {
        private BorderTensionRules.Params P => BorderTensionRules.Params.Default;

        [Test]
        public void TroopConcentration_RaisesTarget()
        {
            float low = BorderTensionRules.TensionTarget(0.1f, 0f, 0f, P);
            float high = BorderTensionRules.TensionTarget(0.9f, 0f, 0f, P);
            Assert.Greater(high, low);
        }

        [Test]
        public void Dispute_RaisesTarget()
        {
            float low = BorderTensionRules.TensionTarget(0f, 0.1f, 0f, P);
            float high = BorderTensionRules.TensionTarget(0f, 0.9f, 0f, P);
            Assert.Greater(high, low);
        }

        [Test]
        public void GoodOpinion_LowersTarget()
        {
            float hostile = BorderTensionRules.TensionTarget(0.8f, 0.8f, -100f, P);
            float friendly = BorderTensionRules.TensionTarget(0.8f, 0.8f, 100f, P);
            Assert.Less(friendly, hostile);
        }

        [Test]
        public void Target_ClampedToRange()
        {
            // 入力過大でも上限を超えない。
            float t = BorderTensionRules.TensionTarget(5f, 5f, -100f, P);
            Assert.LessOrEqual(t, P.maxTension);
            Assert.GreaterOrEqual(t, 0f);
        }

        [Test]
        public void Tick_ConvergesUpward_NoOvershoot()
        {
            // 目標が現在より上：1ステップで目標を超えない。
            float next = BorderTensionRules.Tick(10f, 12f, 1f, P); // step=5
            Assert.AreEqual(12f, next, 1e-5f);
        }

        [Test]
        public void Tick_ConvergesDownward_NoOvershoot()
        {
            // 目標が現在より下かつ近い：1ステップで目標を下回らない。
            float next = BorderTensionRules.Tick(50f, 48f, 1f, P); // step=5
            Assert.AreEqual(48f, next, 1e-5f);
        }

        [Test]
        public void Tick_DecayMovesTowardLowerTarget()
        {
            // 目標が遠く下：1ステップ分だけ減衰。
            float next = BorderTensionRules.Tick(50f, 0f, 1f, P); // step=5 -> 45
            Assert.AreEqual(45f, next, 1e-5f);
        }

        [Test]
        public void Tick_RisesTowardHigherTarget()
        {
            // 目標が遠く上：1ステップ分だけ上昇。
            float next = BorderTensionRules.Tick(10f, 100f, 1f, P); // step=5 -> 15
            Assert.AreEqual(15f, next, 1e-5f);
        }

        [Test]
        public void Tick_Clamped()
        {
            float next = BorderTensionRules.Tick(1000f, 1000f, 1f, P);
            Assert.LessOrEqual(next, P.maxTension);
            Assert.GreaterOrEqual(next, 0f);
        }

        [Test]
        public void IsFlashpoint_Threshold()
        {
            Assert.IsTrue(BorderTensionRules.IsFlashpoint(70f, 70f));
            Assert.IsTrue(BorderTensionRules.IsFlashpoint(71f, 70f));
            Assert.IsFalse(BorderTensionRules.IsFlashpoint(69f, 70f));
        }

        [Test]
        public void Deterministic()
        {
            float a = BorderTensionRules.TensionTarget(0.6f, 0.4f, 20f, P);
            float b = BorderTensionRules.TensionTarget(0.6f, 0.4f, 20f, P);
            Assert.AreEqual(a, b, 1e-5f);
        }

        [Test]
        public void DefaultOverloads_MatchExplicit()
        {
            Assert.AreEqual(
                BorderTensionRules.TensionTarget(0.5f, 0.5f, 0f, BorderTensionRules.Params.Default),
                BorderTensionRules.TensionTarget(0.5f, 0.5f, 0f), 1e-5f);
            Assert.AreEqual(
                BorderTensionRules.Tick(20f, 10f, 1f, BorderTensionRules.Params.Default),
                BorderTensionRules.Tick(20f, 10f, 1f), 1e-5f);
        }
    }
}
