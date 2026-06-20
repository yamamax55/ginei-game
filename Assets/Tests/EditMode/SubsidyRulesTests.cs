using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>SubsidyRules（産業補助金）の EditMode テスト。</summary>
    public class SubsidyRulesTests
    {
        [Test]
        public void Deterministic_SameInputsSameOutput()
        {
            float a = SubsidyRules.OutputBoost(100f);
            float b = SubsidyRules.OutputBoost(100f);
            Assert.AreEqual(a, b, 1e-5f);
        }

        [Test]
        public void ZeroSubsidy_BoostIsOne()
        {
            Assert.AreEqual(1f, SubsidyRules.OutputBoost(0f), 1e-5f);
        }

        [Test]
        public void ZeroSubsidy_NetBenefitIsZero()
        {
            Assert.AreEqual(0f, SubsidyRules.NetBenefit(0f, 1000f), 1e-5f);
        }

        [Test]
        public void Boost_IncreasesWithSubsidy()
        {
            float low = SubsidyRules.OutputBoost(50f);
            float high = SubsidyRules.OutputBoost(200f);
            Assert.Greater(high, low);
        }

        [Test]
        public void Boost_SaturatesBelowMax()
        {
            var p = SubsidyRules.Params.Default;
            float huge = SubsidyRules.OutputBoost(1_000_000f, p);
            // 上限 1+maxBoost を超えない。
            Assert.LessOrEqual(huge, 1f + p.maxBoost + 1e-5f);
            // かつ漸近しているので上限に十分近い。
            Assert.Greater(huge, 1f + p.maxBoost - 1e-3f);
        }

        [Test]
        public void DiminishingReturns_SecondUnitAddsLess()
        {
            // 等量の追加投資に対し、後半ほど追加ブーストが小さい。
            float b0 = SubsidyRules.OutputBoost(0f);
            float b1 = SubsidyRules.OutputBoost(100f);
            float b2 = SubsidyRules.OutputBoost(200f);
            float firstGain = b1 - b0;
            float secondGain = b2 - b1;
            Assert.Greater(firstGain, secondGain);
        }

        [Test]
        public void NetBenefit_PositiveWhenOutputLarge()
        {
            // 産出規模が大きければ少額補助は割に合う。
            float net = SubsidyRules.NetBenefit(100f, 1_000_000f);
            Assert.Greater(net, 0f);
        }

        [Test]
        public void NetBenefit_NegativeWhenSubsidyLargeVsOutput()
        {
            // 産出が小さいのに大きく補助すると赤字。
            float net = SubsidyRules.NetBenefit(100000f, 10f);
            Assert.Less(net, 0f);
        }

        [Test]
        public void IsWorthwhile_ConsistentWithNetBenefitSign()
        {
            float subsidy = 100f;
            float outputBig = 1_000_000f;
            float outputSmall = 10f;

            Assert.AreEqual(SubsidyRules.NetBenefit(subsidy, outputBig) > 0f,
                            SubsidyRules.IsWorthwhile(subsidy, outputBig));
            Assert.AreEqual(SubsidyRules.NetBenefit(100000f, outputSmall) > 0f,
                            SubsidyRules.IsWorthwhile(100000f, outputSmall));
        }

        [Test]
        public void NegativeSubsidy_ClampedToZeroBoost()
        {
            Assert.AreEqual(1f, SubsidyRules.OutputBoost(-50f), 1e-5f);
        }
    }
}
