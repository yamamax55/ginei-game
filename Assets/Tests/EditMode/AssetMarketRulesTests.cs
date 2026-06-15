using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 財産の自動取引（資産市場・#2056・<see cref="AssetMarketRules"/>）を固定する：財産特性ごとの目標投資比率と
    /// 目標へ近づける売買額（投資型は買い・浪費型は売り・貯金型は控えめ・現金/投資残高で頭打ち）。
    /// </summary>
    public class AssetMarketRulesTests
    {
        [Test]
        public void TargetInvestmentRatio_ByTrait()
        {
            Assert.Greater(AssetMarketRules.TargetInvestmentRatio(FinancialTrait.投資), AssetMarketRules.TargetInvestmentRatio(FinancialTrait.貯金));
            Assert.AreEqual(0f, AssetMarketRules.TargetInvestmentRatio(FinancialTrait.浪費), 1e-4f); // 浪費は投資しない
        }

        [Test]
        public void Rebalance_InvestorBuysWhenBelowTarget()
        {
            // 投資型・現金1000・投資0 → 総1000×0.6=600 を投資へ＝+600（現金内）
            float r = AssetMarketRules.RebalanceAmount(FinancialTrait.投資, 1000f, 0f);
            Assert.AreEqual(600f, r, 1e-3f);
        }

        [Test]
        public void Rebalance_BuyCappedByLiquidCash()
        {
            // 目標は高いが現金が少なければ現金まで（投資型・現金100・投資0 → 目標60だが…実際 min(60,100)=60）。
            // 現金が目標より少ないケース：現金50・投資0 → 目標 (50)*0.6=30、min(30,50)=30
            Assert.AreEqual(30f, AssetMarketRules.RebalanceAmount(FinancialTrait.投資, 50f, 0f), 1e-3f);
        }

        [Test]
        public void Rebalance_SpenderSellsHoldings()
        {
            // 浪費型・現金0・投資500 → 目標0、差=−500、売り（投資残高まで）＝−500
            Assert.AreEqual(-500f, AssetMarketRules.RebalanceAmount(FinancialTrait.浪費, 0f, 500f), 1e-3f);
        }

        [Test]
        public void Rebalance_SellCappedByInvested()
        {
            // 売りは投資残高を超えない（浪費・投資100 → 最大 −100）
            Assert.AreEqual(-100f, AssetMarketRules.RebalanceAmount(FinancialTrait.浪費, 0f, 100f), 1e-3f);
        }

        [Test]
        public void Rebalance_SaverStaysNearTarget()
        {
            // 貯金型・現金850・投資150 → 総1000×0.15=150＝既に目標＝0
            Assert.AreEqual(0f, AssetMarketRules.RebalanceAmount(FinancialTrait.貯金, 850f, 150f), 1e-3f);
        }

        [Test]
        public void AnnualRebalance_ThrottlesTowardTarget()
        {
            // 1年では差の半分だけ（投資型・現金1000・投資0 → +600 の半分=+300）
            Assert.AreEqual(300f, AssetMarketRules.AnnualRebalance(FinancialTrait.投資, 1000f, 0f), 1e-3f);
        }

        [Test]
        public void Rebalance_AlreadyAtTarget_NoTrade()
        {
            // 投資型・現金400・投資600 → 総1000×0.6=600＝目標＝差0
            Assert.AreEqual(0f, AssetMarketRules.RebalanceAmount(FinancialTrait.投資, 400f, 600f), 1e-3f);
        }
    }
}
