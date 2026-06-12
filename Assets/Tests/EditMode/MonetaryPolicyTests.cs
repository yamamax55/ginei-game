using System.Collections.Generic;
using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 中央銀行／FRB（#1945 CBNK・<see cref="MonetaryPolicyRules"/>）を固定する：テイラー則(CB-1)、貨幣数量説(CB-2)、
    /// 公開市場操作(CB-3)、最後の貸し手(CB-4)、独立性とインフレバイアス(CB-5)。
    /// </summary>
    public class MonetaryPolicyTests
    {
        // ===== CB-1 政策金利（テイラー則） =====
        [Test]
        public void TaylorRate_RaisesWhenInflationAboveTarget()
        {
            // 0.02 + 0.05 + 1.5*(0.05-0.02) + 0.5*0 = 0.115
            float r = MonetaryPolicyRules.TaylorRate(0.05f, 0.02f, 0f, MonetaryPolicyRules.NeutralRate);
            Assert.AreEqual(0.115f, r, 1e-4f);
        }

        [Test]
        public void TaylorRate_ClampsAtZeroLowerBound_LiquidityTrap()
        {
            // 不況＋デフレ：0.02 + 0 + 1.5*(0-0.02) + 0.5*(-0.05) = -0.035 → ZLB=0 にクランプ
            float r = MonetaryPolicyRules.TaylorRate(0f, 0.02f, -0.05f, MonetaryPolicyRules.NeutralRate);
            Assert.AreEqual(0f, r, 1e-4f);
            Assert.IsTrue(MonetaryPolicyRules.IsLiquidityTrap(r)); // ゼロ下限＝流動性の罠
            Assert.IsFalse(MonetaryPolicyRules.IsLiquidityTrap(0.02f));
        }

        [Test]
        public void MarketBaseRate_FromPolicyRate()
        {
            var cb = new CentralBank("中央銀行", policyRate: 0.03f);
            Assert.AreEqual(0.03f, MonetaryPolicyRules.MarketBaseRate(cb), 1e-4f);
            Assert.AreEqual(0f, MonetaryPolicyRules.MarketBaseRate(null), 1e-4f);
        }

        // ===== CB-2 マネーサプライとインフレ =====
        [Test]
        public void Inflation_FromQuantityTheory()
        {
            Assert.AreEqual(0.05f, MonetaryPolicyRules.Inflation(0.08f, 0.03f), 1e-4f);   // 撒きすぎ＝インフレ
            Assert.AreEqual(-0.02f, MonetaryPolicyRules.Inflation(0.01f, 0.03f), 1e-4f);  // 絞りすぎ＝デフレ
            Assert.AreEqual(0.05f, MonetaryPolicyRules.MoneyGrowthForTarget(0.02f, 0.03f), 1e-4f);
        }

        // ===== CB-3 公開市場操作（OMO） =====
        [Test]
        public void OpenMarketOperation_AdjustsMoneySupply_AndYield()
        {
            var cb = new CentralBank("中央銀行", moneySupply: 1000f);
            Assert.AreEqual(1200f, MonetaryPolicyRules.OpenMarketOperation(cb, 200f), 1e-3f);  // 買いオペ＝供給↑
            Assert.AreEqual(900f, MonetaryPolicyRules.OpenMarketOperation(cb, -300f), 1e-3f);  // 売りオペ＝吸収↓
            // 買いオペは利回り↓（負）、売りオペは利回り↑（正）、厚みで割る
            Assert.AreEqual(-0.2f, MonetaryPolicyRules.YieldImpact(200f, 1000f), 1e-4f);
            Assert.AreEqual(0.1f, MonetaryPolicyRules.YieldImpact(-100f, 1000f), 1e-4f);
            Assert.AreEqual(0f, MonetaryPolicyRules.YieldImpact(200f, 0f), 1e-4f);
            // QE＝非負の買いオペ
            float before = cb.moneySupply;
            Assert.AreEqual(before + 100f, MonetaryPolicyRules.QuantitativeEasing(cb, 100f), 1e-3f);
        }

        // ===== CB-4 最後の貸し手 =====
        [Test]
        public void LenderOfLastResort_SavesIlliquidButSolvent()
        {
            var fi = new FinancialInstitution("行", capital: 50f, assets: 1000f);
            float loss = 80f; // 自己資本50を超える＝放置なら破綻
            Assert.AreEqual(30f, MonetaryPolicyRules.RequiredLiquidity(fi, loss), 1e-3f); // 80-50
            Assert.AreEqual(30f, MonetaryPolicyRules.EmergencyLiquidity(fi, 30f), 1e-3f);
            Assert.IsTrue(MonetaryPolicyRules.SurvivesWithLiquidity(fi, loss, 30f));   // 穴を埋めれば生存
            Assert.IsFalse(MonetaryPolicyRules.SurvivesWithLiquidity(fi, loss, 20f));  // 不足なら破綻
        }

        // ===== CB-5 独立性 =====
        [Test]
        public void Independence_BlendsTaylorAndPoliticalPressure()
        {
            // 独立＝テイラー則どおり、従属＝政府の望む低金利、中間は混合
            Assert.AreEqual(0.10f, MonetaryPolicyRules.EffectivePolicyRate(0.10f, 0f, 1f), 1e-4f);
            Assert.AreEqual(0f, MonetaryPolicyRules.EffectivePolicyRate(0.10f, 0f, 0f), 1e-4f);
            Assert.AreEqual(0.05f, MonetaryPolicyRules.EffectivePolicyRate(0.10f, 0f, 0.5f), 1e-4f);
            // 低独立性×高圧力＝インフレバイアス、独立した中銀は歪みなし
            Assert.AreEqual(0.4f, MonetaryPolicyRules.InflationBias(0.2f, 0.5f), 1e-4f); // (1-0.2)*0.5
            Assert.AreEqual(0f, MonetaryPolicyRules.InflationBias(1f, 0.5f), 1e-4f);
        }
    }
}
