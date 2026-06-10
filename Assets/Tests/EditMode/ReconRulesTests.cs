using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 偵察・戦場の霧（#119）を固定する：偵察精度で推定誤差が縮み、探知が成立しやすくなる。推定は roll で決定論、
    /// 信頼区間は真値±誤差、探知は距離・精度・ステルスで決まり距離外は0。境界・クランプを担保。
    /// </summary>
    public class ReconRulesTests
    {
        private static readonly ReconParams P = ReconParams.Default; // 誤差±0.6 / 基礎探知0.3 / 探知距離30

        [Test]
        public void ErrorFraction_ShrinksWithRecon()
        {
            Assert.AreEqual(0.6f, ReconRules.ErrorFraction(0f, P), 1e-5f);   // 偵察ゼロ＝最大誤差
            Assert.AreEqual(0f, ReconRules.ErrorFraction(1f, P), 1e-5f);     // 完全偵察＝誤差なし
            Assert.AreEqual(0.3f, ReconRules.ErrorFraction(0.5f, P), 1e-5f); // 中間は線形
        }

        [Test]
        public void EstimateStrength_BiasedByRoll_WithinBand()
        {
            // recon=0.5 → 誤差0.3。roll=0 で真値、roll=+1 で+30%、roll=-1 で-30%
            Assert.AreEqual(100f, ReconRules.EstimateStrength(100f, 0.5f, 0f, P), 1e-4f);
            Assert.AreEqual(130f, ReconRules.EstimateStrength(100f, 0.5f, 1f, P), 1e-4f);
            Assert.AreEqual(70f, ReconRules.EstimateStrength(100f, 0.5f, -1f, P), 1e-4f);
        }

        [Test]
        public void EstimateStrength_NeverNegative_AndRollClamped()
        {
            // 大誤差・極端な負バイアスでも 0 未満にならない
            Assert.GreaterOrEqual(ReconRules.EstimateStrength(10f, 0f, -5f, P), 0f);
            // roll は [-1,1] にクランプ＝roll=5 は roll=1 と同じ
            Assert.AreEqual(ReconRules.EstimateStrength(100f, 0f, 1f, P),
                            ReconRules.EstimateStrength(100f, 0f, 5f, P), 1e-4f);
        }

        [Test]
        public void EstimateBand_BracketsTrueValue()
        {
            ReconRules.EstimateBand(200f, 0.5f, P, out float low, out float high); // 誤差0.3
            Assert.AreEqual(140f, low, 1e-4f);
            Assert.AreEqual(260f, high, 1e-4f);
            // 完全偵察ならバンドは真値に収束
            ReconRules.EstimateBand(200f, 1f, P, out float l2, out float h2);
            Assert.AreEqual(200f, l2, 1e-4f);
            Assert.AreEqual(200f, h2, 1e-4f);
        }

        [Test]
        public void DetectionChance_ZeroBeyondRange()
        {
            Assert.AreEqual(0f, ReconRules.DetectionChance(30f, 1f, 0f, P), 1e-5f); // ちょうど範囲端＝0
            Assert.AreEqual(0f, ReconRules.DetectionChance(50f, 1f, 0f, P), 1e-5f); // 範囲外＝0
        }

        [Test]
        public void DetectionChance_HigherWhenCloseAndHighRecon()
        {
            float far = ReconRules.DetectionChance(20f, 1f, 0f, P);
            float near = ReconRules.DetectionChance(5f, 1f, 0f, P);
            Assert.Greater(near, far);
            // 高偵察は低偵察より探知しやすい
            float lowRecon = ReconRules.DetectionChance(5f, 0f, 0f, P);
            Assert.Greater(near, lowRecon);
        }

        [Test]
        public void DetectionChance_StealthReducesIt()
        {
            float open = ReconRules.DetectionChance(5f, 1f, 0f, P);
            float stealthy = ReconRules.DetectionChance(5f, 1f, 0.5f, P);
            Assert.Greater(open, stealthy);
            Assert.GreaterOrEqual(stealthy, 0f); // クランプ
        }

        [Test]
        public void IsDetected_DeterministicByRoll()
        {
            float chance = ReconRules.DetectionChance(5f, 1f, 0f, P);
            Assert.IsTrue(ReconRules.IsDetected(5f, 1f, 0f, chance - 0.01f, P));
            Assert.IsFalse(ReconRules.IsDetected(5f, 1f, 0f, chance + 0.01f, P));
        }
    }
}
