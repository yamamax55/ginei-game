using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>WelfareProvisionRules（福祉・カバー率・貧困削減・希望）の EditMode テスト。</summary>
    public class WelfareProvisionRulesTests
    {
        private const float Tol = 1e-5f;

        [Test]
        public void Coverage_IsDeterministic()
        {
            float a = WelfareProvisionRules.Coverage(50f, 100f);
            float b = WelfareProvisionRules.Coverage(50f, 100f);
            Assert.AreEqual(a, b, Tol);
        }

        [Test]
        public void Coverage_RisesWithSpend()
        {
            float low = WelfareProvisionRules.Coverage(10f, 100f);
            float high = WelfareProvisionRules.Coverage(50f, 100f);
            Assert.Less(low, high);
        }

        [Test]
        public void Coverage_FallsWithPopulation()
        {
            float small = WelfareProvisionRules.Coverage(50f, 100f);
            float large = WelfareProvisionRules.Coverage(50f, 500f);
            Assert.Greater(small, large);
        }

        [Test]
        public void Coverage_ClampedToOne()
        {
            // 1人あたり支出が過大でも 1 を超えない
            float cov = WelfareProvisionRules.Coverage(1000f, 1f);
            Assert.AreEqual(1f, cov, Tol);
            Assert.LessOrEqual(cov, 1f);
        }

        [Test]
        public void Coverage_ZeroPopulation_Guarded()
        {
            // ゼロ人口でも例外・NaN を出さず clamp 済みの値を返す
            float cov = WelfareProvisionRules.Coverage(50f, 0f);
            Assert.AreEqual(1f, cov, Tol);
            Assert.IsFalse(float.IsNaN(cov));
            Assert.IsFalse(float.IsInfinity(cov));
        }

        [Test]
        public void PovertyReduction_DiminishingReturns()
        {
            // 同じ幅(0.25)の区間どうしで、低カバー域より高カバー域の伸びが小さい（逓減＝sqrt の凹性）。
            var p = WelfareProvisionRules.Params.Default;
            float at25 = WelfareProvisionRules.PovertyReduction(0.25f, p);
            float at50 = WelfareProvisionRules.PovertyReduction(0.50f, p);
            float at75 = WelfareProvisionRules.PovertyReduction(0.75f, p);
            float at100 = WelfareProvisionRules.PovertyReduction(1.00f, p);
            float lowSpan = at50 - at25;   // 低域の同幅区間
            float highSpan = at100 - at75; // 高域の同幅区間
            Assert.Less(highSpan, lowSpan);
        }

        [Test]
        public void PovertyReduction_MaxAtFullCoverage()
        {
            var p = WelfareProvisionRules.Params.Default;
            float red = WelfareProvisionRules.PovertyReduction(1f, p);
            Assert.AreEqual(p.maxPovertyReduction, red, Tol);
        }

        [Test]
        public void PovertyReduction_ZeroCoverage_IsZero()
        {
            Assert.AreEqual(0f, WelfareProvisionRules.PovertyReduction(0f), Tol);
        }

        [Test]
        public void HopeBonus_Monotonic()
        {
            float low = WelfareProvisionRules.HopeBonus(0.2f);
            float high = WelfareProvisionRules.HopeBonus(0.8f);
            Assert.Less(low, high);
        }

        [Test]
        public void HopeBonus_Clamped()
        {
            // 重みが大きくカバー率最大でも 1 を超えない
            var p = new WelfareProvisionRules.Params(1f, 0.6f, 3f);
            float bonus = WelfareProvisionRules.HopeBonus(1f, p);
            Assert.AreEqual(1f, bonus, Tol);
            Assert.LessOrEqual(bonus, 1f);
        }
    }
}
