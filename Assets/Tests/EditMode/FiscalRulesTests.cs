using NUnit.Framework;
using Ginei;
using FP = Ginei.FiscalRules.FiscalParams;

namespace Ginei.Tests
{
    /// <summary>
    /// 財政・経済（#163 EPIC・#161/#162）を固定する：PB・金利のリスクプレミアム・国債の増減・債務スパイラル・財政健全度/為替、
    /// 税収と社会保障費（オーナス連動）・再分配の政治帰結。すべて純ロジック。
    /// </summary>
    public class FiscalRulesTests
    {
        private const float Economy = 1000f;

        // ===== #161 財政 =====

        [Test]
        public void PrimaryBalance_RevenueMinusExpenditure()
        {
            var s = new FiscalState(150f, 100f);
            Assert.AreEqual(50f, FiscalRules.PrimaryBalance(s), 1e-4f);
        }

        [Test]
        public void InterestRate_RisesWithDebtRatio()
        {
            var p = FP.Default; // safe 0.6, slope 0.1
            var low = new FiscalState(0, 0, debt: 300f);  // ratio 0.3 < 0.6 → 基準のみ
            var high = new FiscalState(0, 0, debt: 1600f); // ratio 1.6 → +（1.6-0.6)*0.1=0.1
            Assert.AreEqual(0.02f, FiscalRules.InterestRate(low, Economy, p), 1e-4f);
            Assert.AreEqual(0.12f, FiscalRules.InterestRate(high, Economy, p), 1e-4f);
        }

        [Test]
        public void Tick_DeficitGrowsDebt_SurplusShrinks()
        {
            var p = FP.Default;
            var deficit = new FiscalState(100f, 120f); // PB -20、債務0→利払い0
            FiscalRules.Tick(deficit, Economy, 1f, p);
            Assert.AreEqual(20f, deficit.debt, 1e-3f); // 赤字を国債で埋める

            var surplus = new FiscalState(150f, 100f, debt: 200f); // PB 50、利払い=200*0.02=4 → 収支+46
            FiscalRules.Tick(surplus, Economy, 1f, p);
            Assert.AreEqual(154f, surplus.debt, 1e-3f); // 黒字で減債
        }

        [Test]
        public void IsDebtSpiral_WhenInterestExceedsPrimarySurplus()
        {
            var p = FP.Default;
            var spiral = new FiscalState(100f, 90f, debt: 1600f); // PB10、利払い=1600*0.12=192 → PB<利払い・高債務
            Assert.IsTrue(FiscalRules.IsDebtSpiral(spiral, Economy, p));

            var healthy = new FiscalState(150f, 100f, debt: 200f); // PB50、利払い4、低債務
            Assert.IsFalse(FiscalRules.IsDebtSpiral(healthy, Economy, p));
        }

        [Test]
        public void DebtSpiral_CompoundsOverTicks()
        {
            var p = FP.Default;
            var s = new FiscalState(100f, 90f, debt: 1600f); // 小黒字PBだが高債務＝利払いが上回る
            float before = s.debt;
            for (int i = 0; i < 5; i++) FiscalRules.Tick(s, Economy, 1f, p);
            Assert.Greater(s.debt, before); // 複利で膨らむ
        }

        [Test]
        public void FiscalHealthFactor_LerpsSafeToCrisis()
        {
            var p = FP.Default; // safe0.6 crisis2.0
            Assert.AreEqual(1f, FiscalRules.FiscalHealthFactor(new FiscalState(0, 0, 300f), Economy, p), 1e-4f);  // 0.3
            Assert.AreEqual(0f, FiscalRules.FiscalHealthFactor(new FiscalState(0, 0, 2000f), Economy, p), 1e-4f); // 2.0
            Assert.AreEqual(0.5f, FiscalRules.FiscalHealthFactor(new FiscalState(0, 0, 1300f), Economy, p), 1e-4f); // 1.3
        }

        [Test]
        public void ExchangeRate_DepreciatesWithDebt()
        {
            var p = FP.Default;
            float strong = FiscalRules.ExchangeRateFactor(new FiscalState(0, 0, 300f), Economy, p);
            float weak = FiscalRules.ExchangeRateFactor(new FiscalState(0, 0, 2000f), Economy, p);
            Assert.Greater(strong, weak);
            Assert.AreEqual(0.5f, weak, 1e-4f); // 危機で通貨半値
        }

        // ===== #162 税・社会保障 =====

        [Test]
        public void TaxRevenue_BaseTimesRate()
        {
            Assert.AreEqual(30f, FiscalRules.TaxRevenue(taxBase: 100f, taxRate: 0.3f), 1e-4f);
        }

        [Test]
        public void WelfareCost_RisesWithDependents_OnusLink()
        {
            var p = FP.Default;
            float few = FiscalRules.WelfareCost(dependents: 100f, welfareLevel: 0.5f, p);
            float many = FiscalRules.WelfareCost(dependents: 300f, welfareLevel: 0.5f, p); // 高齢化＝扶養増
            Assert.AreEqual(50f, few, 1e-4f);
            Assert.Greater(many, few); // 人口オーナスで社会保障費が増える
        }

        [Test]
        public void Redistribution_TaxPenaltyAndWelfareHope_Monotonic()
        {
            Assert.Greater(FiscalRules.TaxBurdenPenalty(0.8f), FiscalRules.TaxBurdenPenalty(0.2f));
            Assert.Greater(FiscalRules.WelfareHopeBonus(0.8f), FiscalRules.WelfareHopeBonus(0.2f));
        }

        [Test]
        public void RevenueAndExpenditure_Assemble()
        {
            var p = FP.Default;
            float rev = FiscalRules.Revenue(taxBase: 100f, taxRate: 0.3f, tradeIncome: 20f); // 30+20
            float exp = FiscalRules.Expenditure(military: 30f, admin: 10f, dependents: 50f, welfareLevel: 0.4f, p); // 30+10+20
            Assert.AreEqual(50f, rev, 1e-4f);
            Assert.AreEqual(60f, exp, 1e-4f);
        }
    }
}
