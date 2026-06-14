using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 中央銀行・金融政策の薄いオーケストレータ <see cref="MonetaryAuthorityRules"/> を固定する：
    /// テイラー則での利上げ/利下げ追従、実効金利の財政プレミアム上乗せ、最後の貸し手の弁別、null安全、委譲の一致。
    /// </summary>
    public class MonetaryAuthorityRulesTests
    {
        // ===== TickYear：政策金利の追従 =====

        [Test]
        public void TickYear_RaisesPolicyRate_WhenInflationHigh()
        {
            var cb = new CentralBank("CB", policyRate: 0.02f, inflationTarget: 0.02f);
            float before = cb.policyRate;
            // 高インフレ（目標2%超）→ テイラー則の目標は高い → 政策金利は上がる方向へ寄る。
            MonetaryAuthorityRules.TickYear(cb, inflationRate: 0.08f, outputGap: 0f, dt: 1f);
            Assert.Greater(cb.policyRate, before);
        }

        [Test]
        public void TickYear_LowersPolicyRate_WhenRecessionAndLowInflation()
        {
            var cb = new CentralBank("CB", policyRate: 0.10f, inflationTarget: 0.02f);
            float before = cb.policyRate;
            // デフレ気味＋不況（産出ギャップ負）→ テイラー則の目標は低い → 政策金利は下がる方向へ寄る。
            MonetaryAuthorityRules.TickYear(cb, inflationRate: 0f, outputGap: -0.05f, dt: 1f);
            Assert.Less(cb.policyRate, before);
        }

        [Test]
        public void TickYear_MovesTowardTaylorTarget_ByAdjustSpeed()
        {
            var cb = new CentralBank("CB", policyRate: 0.02f, inflationTarget: 0.02f);
            float target = MonetaryPolicyRules.TaylorRate(cb, 0.08f, 0f); // 委譲先の目標
            float prevRate = cb.policyRate;
            MonetaryAuthorityRules.TickYear(cb, 0.08f, 0f, 1f);
            // RateAdjustSpeed=0.5, dt=1 → Lerp(prev, target, 0.5) と厳密一致（委譲の一致）。
            float expected = UnityEngine.Mathf.Lerp(prevRate, target, 0.5f);
            Assert.AreEqual(expected, cb.policyRate, 1e-4f);
        }

        [Test]
        public void TickYear_NullSafe_NoThrow()
        {
            Assert.DoesNotThrow(() => MonetaryAuthorityRules.TickYear(null, 0.05f, 0f, 1f));
            // dt<=0 は何もしない。
            var cb = new CentralBank("CB", policyRate: 0.02f);
            MonetaryAuthorityRules.TickYear(cb, 0.5f, 0f, 0f);
            Assert.AreEqual(0.02f, cb.policyRate, 1e-6f);
        }

        // ===== EffectiveInterestRate：政策金利＋財政プレミアム =====

        [Test]
        public void EffectiveInterestRate_AddsFiscalPremium_ForHighDebt()
        {
            var cb = new CentralBank("CB", policyRate: 0.03f);
            // 高債務（債務比率 safe=0.6 を大きく超過）→ プレミアム上乗せ。
            var fs = new FiscalState(revenue: 100f, baseExpenditure: 100f, debt: 200f);
            float economy = 100f; // 債務比率 = 2.0
            float rate = MonetaryAuthorityRules.EffectiveInterestRate(cb, fs, economy);
            // 政策金利 0.03 より高い（プレミアムぶん）。
            Assert.Greater(rate, 0.03f);

            // 低債務なら政策金利のまま（プレミアム0）。
            var lowDebt = new FiscalState(100f, 100f, 0f);
            float lowRate = MonetaryAuthorityRules.EffectiveInterestRate(cb, lowDebt, economy);
            Assert.AreEqual(0.03f, lowRate, 1e-4f);
        }

        [Test]
        public void EffectiveInterestRate_MatchesDelegatedPremium()
        {
            var cb = new CentralBank("CB", policyRate: 0.03f);
            var fs = new FiscalState(100f, 100f, 200f);
            float economy = 100f;
            var p = FiscalRules.FiscalParams.Default;
            float fiscalRate = FiscalRules.InterestRate(fs, economy, p);
            float expectedPremium = fiscalRate - p.baseInterestRate;
            float expected = 0.03f + expectedPremium; // 政策金利＋プレミアム（委譲の一致）。
            float actual = MonetaryAuthorityRules.EffectiveInterestRate(cb, fs, economy);
            Assert.AreEqual(expected, actual, 1e-4f);
        }

        [Test]
        public void EffectiveInterestRate_NullSafe()
        {
            // fs=null は政策金利のみ、cb=null は0。
            var cb = new CentralBank("CB", policyRate: 0.04f);
            Assert.AreEqual(0.04f, MonetaryAuthorityRules.EffectiveInterestRate(cb, null, 100f), 1e-4f);
            Assert.AreEqual(0f, MonetaryAuthorityRules.EffectiveInterestRate(null, null, 100f), 1e-6f);
        }

        // ===== 最後の貸し手 =====

        [Test]
        public void ShouldProvideLiquidity_TrueForSolventIlliquid_FalseForNoCrisis()
        {
            // 危機（取付けの深さ・非流動資産あり）かつ担保良好（solvent）→ 救う。
            Assert.IsTrue(MonetaryAuthorityRules.ShouldProvideLiquidity(
                panicSeverity: 0.8f, illiquidAssets: 0.5f, assetQuality: 0.9f, liabilities: 0.5f));
            // 危機なし（深さ0）→ 供給不要。
            Assert.IsFalse(MonetaryAuthorityRules.ShouldProvideLiquidity(
                panicSeverity: 0f, illiquidAssets: 0.5f, assetQuality: 0.9f, liabilities: 0.5f));
        }

        [Test]
        public void LiquidityToProvide_MatchesDelegatedNeed()
        {
            float expected = LenderOfLastResortRules.LiquidityNeeded(0.6f, 0.4f);
            float actual = MonetaryAuthorityRules.LiquidityToProvide(0.6f, 0.4f);
            Assert.AreEqual(expected, actual, 1e-6f);
        }
    }
}
