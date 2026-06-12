using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// バランスシート銀行（#1976 BANK・<see cref="BankRules"/> 拡張）を固定する：バランスシート(BANK-1)、自己資本比率(BANK-2)、
    /// 信用乗数と中央銀行(BANK-3)、銀行の収益(BANK-4)、流動性と取り付け(BANK-5)。既存 BankRulesTests は不変。
    /// </summary>
    public class BankBalanceSheetTests
    {
        // ===== BANK-1 バランスシート =====
        [Test]
        public void BalanceSheet_AssetsEquityInsolvency()
        {
            var b = new Bank(1000f, 800f) { reserves = 200f, securities = 100f, nonPerformingLoans = 40f };
            Assert.AreEqual(1100f, BankRules.TotalAssets(b), 1e-3f);  // 200+800+100
            Assert.AreEqual(100f, BankRules.Equity(b), 1e-3f);        // 1100−1000
            Assert.IsFalse(BankRules.IsInsolventBySheet(b));
            // 資産が負債を割れば債務超過
            var bad = new Bank(1000f, 700f) { reserves = 50f };       // 資産750−預金1000=−250
            Assert.AreEqual(-250f, BankRules.Equity(bad), 1e-3f);
            Assert.IsTrue(BankRules.IsInsolventBySheet(bad));
        }

        // ===== BANK-2 自己資本比率 =====
        [Test]
        public void CapitalAdequacy_BisRule()
        {
            var well = new Bank(1000f, 800f) { reserves = 200f, securities = 100f }; // 自己資本100
            var weak = new Bank(1000f, 800f) { reserves = 150f, securities = 100f }; // 自己資本50
            float lw = BankRules.DefaultLoanRiskWeight, sw = BankRules.DefaultSecuritiesRiskWeight;
            Assert.AreEqual(820f, BankRules.RiskWeightedAssets(well, lw, sw), 1e-3f); // 800×1+100×0.2
            Assert.AreEqual(100f / 820f, BankRules.CapitalAdequacyRatio(well, lw, sw), 1e-4f);
            Assert.IsTrue(BankRules.MeetsCapitalRequirement(well, BankRules.MinCapitalRatio, lw, sw));   // 0.122≥0.08
            Assert.IsFalse(BankRules.MeetsCapitalRequirement(weak, BankRules.MinCapitalRatio, lw, sw));  // 0.061<0.08
        }

        // ===== BANK-3 信用乗数と中央銀行 =====
        [Test]
        public void MoneyMultiplier_AndCentralBank()
        {
            Assert.AreEqual(10f, BankRules.MoneyMultiplier(0.1f), 1e-3f);
            Assert.AreEqual(5f, BankRules.MoneyMultiplier(0.2f), 1e-3f);
            var cb = new CentralBank("中央銀行", reserveRequirement: 0.1f);
            Assert.AreEqual(10f, BankRules.MoneyMultiplierFromCentralBank(cb), 1e-3f);
            // 既存 CreditCreation との整合：預金×(乗数−1)
            float created = BankRules.CreditCreation(100f, 0.1f, BankRules.BankParams.Default);
            Assert.AreEqual(100f * (BankRules.MoneyMultiplier(0.1f) - 1f), created, 1e-3f); // =900
        }

        // ===== BANK-4 銀行の収益 =====
        [Test]
        public void Profit_NetInterestMinusLoanLoss()
        {
            var b = new Bank(1000f, 800f) { nonPerformingLoans = 40f };
            Assert.AreEqual(30f, BankRules.NetInterestIncome(b, 0.05f, 0.01f), 1e-3f); // 800×0.05−1000×0.01
            Assert.AreEqual(20f, BankRules.LoanLossProvision(b, 0.5f), 1e-3f);         // 40×0.5
            Assert.AreEqual(10f, BankRules.BankProfit(b, 0.05f, 0.01f, 0.5f), 1e-3f);  // 30−20
        }

        // ===== BANK-5 流動性と取り付け =====
        [Test]
        public void Liquidity_AndWithdrawalCoverage()
        {
            var b = new Bank(1000f, 800f) { reserves = 150f };
            Assert.AreEqual(0.15f, BankRules.LiquidityRatio(b), 1e-4f);          // 150/1000
            Assert.IsTrue(BankRules.CanCoverWithdrawal(b, 0.1f));               // 150≥100
            Assert.IsFalse(BankRules.CanCoverWithdrawal(b, 0.2f));              // 150<200
        }
    }
}
