using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    public class UnemploymentRulesTests
    {
        private const float Tol = 1e-5f;

        [Test]
        public void DemandMeetsSupply_ReturnsFrictionalFloor()
        {
            var p = UnemploymentRules.Params.Default;
            // 需要=供給 → 生の率0 だが摩擦的下限へクランプ
            float r = UnemploymentRules.UnemploymentRate(100f, 100f, p);
            Assert.AreEqual(p.frictionalFloor, r, Tol);
        }

        [Test]
        public void DemandExceedsSupply_ReturnsFrictionalFloor()
        {
            var p = UnemploymentRules.Params.Default;
            // 需要過多でも負にならず下限
            float r = UnemploymentRules.UnemploymentRate(100f, 150f, p);
            Assert.AreEqual(p.frictionalFloor, r, Tol);
        }

        [Test]
        public void ZeroDemand_HighRate()
        {
            // 需要ゼロ → 失業率1（全失業）
            float r = UnemploymentRules.UnemploymentRate(100f, 0f);
            Assert.AreEqual(1f, r, Tol);
        }

        [Test]
        public void ZeroSupply_ReturnsFloor()
        {
            var p = UnemploymentRules.Params.Default;
            float r = UnemploymentRules.UnemploymentRate(0f, 50f, p);
            Assert.AreEqual(p.frictionalFloor, r, Tol);
        }

        [Test]
        public void RateNeverBelowFloor_NorAboveOne()
        {
            var p = UnemploymentRules.Params.Default;
            for (float demand = -50f; demand <= 200f; demand += 10f)
            {
                float r = UnemploymentRules.UnemploymentRate(100f, demand, p);
                Assert.GreaterOrEqual(r, p.frictionalFloor - Tol);
                Assert.LessOrEqual(r, 1f + Tol);
            }
        }

        [Test]
        public void PartialDemand_IntermediateRate()
        {
            // 供給100 需要75 → 0.25
            float r = UnemploymentRules.UnemploymentRate(100f, 75f);
            Assert.AreEqual(0.25f, r, Tol);
        }

        [Test]
        public void Deterministic()
        {
            float a = UnemploymentRules.UnemploymentRate(100f, 60f);
            float b = UnemploymentRules.UnemploymentRate(100f, 60f);
            Assert.AreEqual(a, b, Tol);
        }

        [Test]
        public void Discontent_MonotonicInRate()
        {
            float low = UnemploymentRules.Discontent(0.1f);
            float high = UnemploymentRules.Discontent(0.3f);
            Assert.Greater(high, low);
        }

        [Test]
        public void Discontent_Clamped01()
        {
            // 高失業×重み で1を超えないこと
            float d = UnemploymentRules.Discontent(1f);
            Assert.LessOrEqual(d, 1f + Tol);
            Assert.GreaterOrEqual(d, 0f - Tol);
        }

        [Test]
        public void EmploymentRate_Complementary()
        {
            Assert.AreEqual(0.7f, UnemploymentRules.EmploymentRate(0.3f), Tol);
            Assert.AreEqual(1f, UnemploymentRules.EmploymentRate(0f), Tol);
            Assert.AreEqual(0f, UnemploymentRules.EmploymentRate(1f), Tol);
        }

        [Test]
        public void EmploymentRate_ClampsOutOfRange()
        {
            // 範囲外入力でも0..1へ
            Assert.AreEqual(1f, UnemploymentRules.EmploymentRate(-0.5f), Tol);
            Assert.AreEqual(0f, UnemploymentRules.EmploymentRate(1.5f), Tol);
        }
    }
}
