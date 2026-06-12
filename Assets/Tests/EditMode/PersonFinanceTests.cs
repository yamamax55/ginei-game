using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 人物の要求物資・資産（#2056）：特性(PFIN-1)/消費需要(PFIN-2)/可処分配分(PFIN-3)/財産更新(PFIN-4)/効果メリデメ(PFIN-5)/Tick(PFIN-6)。
    /// </summary>
    public class PersonFinanceTests
    {
        // --- PFIN-1 特性パラメータ（配分率は合計1） ---
        [Test]
        public void Trait_RatesSumToOne_AndTradeoffs()
        {
            foreach (FinancialTrait t in new[] { FinancialTrait.貯金, FinancialTrait.投資, FinancialTrait.浪費 })
            {
                float sum = FinanceTraitRules.SaveRate(t) + FinanceTraitRules.InvestRate(t) + FinanceTraitRules.SpendRate(t);
                Assert.AreEqual(1.0f, sum, 1e-4f, $"{t} の配分率合計は1");
            }
            Assert.AreEqual(0.60f, FinanceTraitRules.SaveRate(FinancialTrait.貯金), 1e-4f);
            Assert.AreEqual(0.60f, FinanceTraitRules.InvestRate(FinancialTrait.投資), 1e-4f);
            Assert.AreEqual(0.90f, FinanceTraitRules.SpendRate(FinancialTrait.浪費), 1e-4f);
            // 投資はリスク高、浪費は気前高・破産傾向高
            Assert.AreEqual(0.80f, FinanceTraitRules.RiskExposure(FinancialTrait.投資), 1e-4f);
            Assert.AreEqual(1.00f, FinanceTraitRules.Generosity(FinancialTrait.浪費), 1e-4f);
            Assert.AreEqual(0.20f, FinanceTraitRules.Generosity(FinancialTrait.貯金), 1e-4f);
            Assert.AreEqual(1.00f, FinanceTraitRules.RuinPropensity(FinancialTrait.浪費), 1e-4f);
            Assert.AreEqual(0.10f, FinanceTraitRules.RuinPropensity(FinancialTrait.貯金), 1e-4f);
        }

        // --- PFIN-2 消費需要（階級で増える） ---
        [Test]
        public void Demand_RankRaisesConsumptionNeed()
        {
            Assert.AreEqual(100f, PersonDemandRules.ConsumptionNeed(0, 100f), 1e-3f);
            Assert.AreEqual(180f, PersonDemandRules.ConsumptionNeed(8, 100f), 1e-3f); // 元帥級は見栄が大きい
            Assert.AreEqual(0.5f, PersonDemandRules.LivingStandard(90f, 180f), 1e-4f);
            Assert.AreEqual(1f, PersonDemandRules.LivingStandard(100f, 0f), 1e-4f); // 需要0は満たされ済み
            Assert.AreEqual(1f, PersonDemandRules.LivingStandard(300f, 180f), 1e-4f); // 上限1
        }

        // --- PFIN-3 可処分の配分 ---
        [Test]
        public void Wealth_DisposableAllocation()
        {
            Assert.AreEqual(20f, PersonWealthRules.DisposableIncome(200f, 180f), 1e-3f);
            Assert.AreEqual(0f, PersonWealthRules.DisposableIncome(100f, 200f), 1e-3f); // 赤字は0
            Assert.AreEqual(12f, PersonWealthRules.Saved(20f, FinancialTrait.貯金), 1e-3f);   // 20*0.6
            Assert.AreEqual(12f, PersonWealthRules.Invested(20f, FinancialTrait.投資), 1e-3f); // 20*0.6
            Assert.AreEqual(18f, PersonWealthRules.Spent(20f, FinancialTrait.浪費), 1e-3f);    // 20*0.9
            Assert.AreEqual(10f, PersonWealthRules.InvestmentReturn(100f, 0.1f), 1e-3f);
        }

        // --- PFIN-4 財産更新（特性で増え方が違う） ---
        [Test]
        public void Wealth_AfterYear_TraitDifferences()
        {
            // 貯金：60貯金 + 10投資×1.1 = 71
            Assert.AreEqual(71f, PersonWealthRules.WealthAfterYear(0f, 100f, FinancialTrait.貯金, 0.1f), 1e-3f);
            // 投資（好況）：20貯金 + 60×1.1 = 86
            Assert.AreEqual(86f, PersonWealthRules.WealthAfterYear(0f, 100f, FinancialTrait.投資, 0.1f), 1e-3f);
            // 投資（暴落 -0.5）：20貯金 + 60×0.5 = 50（貯金型71より下＝リスク）
            Assert.AreEqual(50f, PersonWealthRules.WealthAfterYear(0f, 100f, FinancialTrait.投資, -0.5f), 1e-3f);
            // 浪費：5貯金 + 5×1.1 = 10.5（ほぼ貯まらない）
            Assert.AreEqual(10.5f, PersonWealthRules.WealthAfterYear(0f, 100f, FinancialTrait.浪費, 0.1f), 1e-3f);
        }

        // --- PFIN-5 効果（メリット・デメリット） ---
        [Test]
        public void Effect_PopularitySecurityRuin()
        {
            // 浪費は気前で人望↑、貯金はケチで伸びない
            Assert.AreEqual(0.9f, PersonFinanceEffectRules.PopularityDelta(90f, FinancialTrait.浪費, 0.01f), 1e-4f);
            Assert.AreEqual(0.18f, PersonFinanceEffectRules.PopularityDelta(90f, FinancialTrait.貯金, 0.01f), 1e-4f);
            // 安全余裕＝財産/年間需要
            Assert.AreEqual(2.0f, PersonFinanceEffectRules.SecurityYears(180f, 90f), 1e-4f);
            Assert.AreEqual(0f, PersonFinanceEffectRules.SecurityYears(100f, 0f), 1e-4f);
            // 無一文の浪費型は破産リスク最大、厚い財産なら0
            Assert.AreEqual(1.0f, PersonFinanceEffectRules.RuinRisk(0f, 90f, FinancialTrait.浪費), 1e-4f);
            Assert.AreEqual(0f, PersonFinanceEffectRules.RuinRisk(180f, 90f, FinancialTrait.浪費), 1e-4f);
            Assert.IsTrue(PersonFinanceEffectRules.IsBankrupt(0f));
            Assert.IsFalse(PersonFinanceEffectRules.IsBankrupt(50f));
        }

        // --- PFIN-6 Tick（俸給→消費→配分→財産更新） ---
        [Test]
        public void Tick_UpdatesPersonWealth()
        {
            var p = new Person { rankTier = 0, financialTrait = FinancialTrait.貯金, wealth = 0f };
            // need=ConsumptionNeed(0,100)=100、disposable=200-100=100、貯金型 r=0.1 → 71
            PersonFinanceTickRules.TickYear(p, 200f, 0.1f, 100f);
            Assert.AreEqual(71f, p.wealth, 1e-3f);

            // null 安全
            Assert.DoesNotThrow(() => PersonFinanceTickRules.TickYear(null, 100f, 0.1f));
        }
    }
}
