using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 政府の財政＝<b>予算と執行で1年が回るか</b>を検証する（#161/#162/#163 の結合）。
    /// 歳入(税)→予算編成(配分)→執行(歳出)→帰結(出資度/債務/利払い)→翌年 の輪が Core 部品で閉じることを固定し、
    /// あわせて<b>足りない機能をあぶり出す</b>（空予算なら執行0＝年が回らない／FiscalState を勢力ごとに持たないと
    /// 債務が繰り越されない、を pin する）。GalaxyView の配線ギャップは docs/fiscal-year-cycle-gap-analysis.md 参照。
    /// </summary>
    public class FiscalYearCycleTests
    {
        private const float Economy = 1000f; // 課税ベース（人口×係数×安定度の代理）

        // ===== ① 歳入：税率→税収 =====

        [Test]
        public void Revenue_FromTaxRateAndBase()
        {
            Assert.AreEqual(300f, FiscalRules.TaxRevenue(Economy, 0.3f), 1e-3f);
            Assert.AreEqual(0f, FiscalRules.TaxRevenue(Economy, 0f), 1e-3f);     // 無税＝歳入0
            Assert.AreEqual(Economy, FiscalRules.TaxRevenue(Economy, 1f), 1e-3f); // 全額（極端）
        }

        // ===== ② 予算編成：歳入内で配分し、超過は緊縮 =====

        [Test]
        public void Budget_AllocateWithinRevenue_AndAusterityCap()
        {
            float revenue = FiscalRules.TaxRevenue(Economy, 0.3f); // 300
            var b = new NationalBudget();
            BudgetRules.AllocateByWeights(b, revenue, new float[] { 2, 1, 2, 1, 1, 1 }); // 軍/建艦/内政/社保/研究/外交
            Assert.AreEqual(revenue, BudgetRules.Total(b), 1e-2f);
            Assert.IsTrue(BudgetRules.IsBalanced(b, revenue));            // 歳出=歳入＝均衡
            Assert.AreEqual(75f, BudgetRules.Get(b, BudgetCategory.軍事), 1e-2f); // 300×2/8

            // 歳入が減ったら緊縮（歳入まで比例縮小・シェア保存）
            Assert.IsTrue(BudgetRules.CapToRevenue(b, 250f));
            Assert.AreEqual(250f, BudgetRules.Total(b), 1e-2f);
            Assert.IsFalse(BudgetRules.IsDeficit(b, 300f));
        }

        // ===== ③ 執行：出資度が実効に効く（満額1・過剰>1・不足<1） =====

        [Test]
        public void Execution_FundingFactorsDriveEffectiveOutputs()
        {
            var b = new NationalBudget();
            BudgetRules.Set(b, BudgetCategory.軍事, 100f);
            BudgetRules.Set(b, BudgetCategory.内政, 100f);
            BudgetRules.Set(b, BudgetCategory.社会保障, 100f);

            // 軍事即応：need=100→1.0、need=50→2.0(上限)、need=200→0.5
            Assert.AreEqual(1f, BudgetRules.MilitaryReadinessFactor(b, 100f), 1e-3f);
            Assert.AreEqual(2f, BudgetRules.MilitaryReadinessFactor(b, 50f), 1e-3f);
            Assert.AreEqual(0.5f, BudgetRules.MilitaryReadinessFactor(b, 200f), 1e-3f);

            // 内政の安定度加点：満額0・過剰+・不足−
            Assert.AreEqual(0f, BudgetRules.AdministrationStabilityBonus(b, 100f), 1e-3f);
            Assert.Greater(BudgetRules.AdministrationStabilityBonus(b, 50f), 0f);
            Assert.Less(BudgetRules.AdministrationStabilityBonus(b, 200f), 0f);

            // 社会保障の希望加点：満額0・過剰+
            Assert.AreEqual(0f, BudgetRules.WelfareHopeBonus(b, 100f), 1e-3f);
            Assert.Greater(BudgetRules.WelfareHopeBonus(b, 50f), 0f);
        }

        // ===== ④ 帰結：黒字は減債・赤字は増債（債務が翌年へ繰り越す） =====

        [Test]
        public void Surplus_PaysDownDebt()
        {
            var s = new FiscalState(revenue: 300f, baseExpenditure: 250f, debt: 100f);
            var p = FiscalRules.FiscalParams.Default;
            Assert.AreEqual(50f, FiscalRules.PrimaryBalance(s), 1e-3f);
            FiscalRules.Tick(s, Economy, 1f, p);
            Assert.Less(s.debt, 100f);                 // 黒字→減債
            Assert.AreEqual(52f, s.debt, 1e-3f);       // 100 −(PB50−利払い2)
        }

        [Test]
        public void Deficit_AccumulatesDebt_AndCompoundsAcrossYears()
        {
            var s = new FiscalState(revenue: 300f, baseExpenditure: 400f, debt: 0f); // 毎年100の赤字
            var p = FiscalRules.FiscalParams.Default;
            float prev = s.debt;
            bool spiralSeen = false;
            for (int year = 0; year < 12; year++)
            {
                FiscalRules.Tick(s, Economy, 1f, p);
                Assert.Greater(s.debt, prev);          // 赤字が続く限り債務は毎年増える（繰り越し＋利払い複利）
                prev = s.debt;
                if (FiscalRules.IsDebtSpiral(s, Economy, p)) spiralSeen = true;
            }
            Assert.IsTrue(spiralSeen, "持続赤字でも債務スパイラルに入らなかった");
            Assert.Greater(s.debt, 1000f);             // 12年で大きく膨張
        }

        // ===== ⑤ 統合：歳入→予算→執行→債務 で1年が回り、複数年で状態が連続する =====

        [Test]
        public void FullFiscalYear_Closes_AndCarriesOverAcrossYears()
        {
            var p = FiscalRules.FiscalParams.Default;
            float taxRate = 0.3f;
            var budget = new NationalBudget();
            // やや積極財政（歳入300に対し歳出350＝赤字50）
            BudgetRules.AllocateByWeights(budget, 350f, new float[] { 3, 1, 2, 1, 1, 1 });

            var s = new FiscalState();
            float treasury = 0f;
            float debtY1 = 0f;

            for (int year = 1; year <= 3; year++)
            {
                // 歳入
                float revenue = FiscalRules.TaxRevenue(Economy, taxRate); // 300
                // 執行（現金収支＝歳入−歳出総額）
                treasury += revenue - BudgetRules.Total(budget);
                // 財政（PB/債務）：予算総額を歳出へ反映し1年Tick
                s.revenue = revenue;
                BudgetRules.ApplyToFiscalState(budget, s); // baseExpenditure = 350
                FiscalRules.Tick(s, Economy, 1f, p);
                if (year == 1) debtY1 = s.debt;
            }

            // 年が回って状態が連続：赤字なので現金は目減りし、形式債務は毎年積み上がる
            Assert.Less(treasury, 0f, "赤字なのに現金が減っていない＝執行が効いていない");
            Assert.AreEqual(-150f, treasury, 1e-2f);        // (300−350)×3
            Assert.Greater(s.debt, debtY1, "債務が翌年へ繰り越し・増加していない");
            Assert.Greater(s.debt, 0f);
        }

        // ===== ⑥ ギャップの pin：空予算なら執行0＝年が回らない（デモの現状） =====

        [Test]
        public void Gap_EmptyBudget_MeansNoExecution()
        {
            // FactionState.budget は既定で空＝Total 0。デモは配分ロジックが無いためここから動かない。
            var empty = new NationalBudget();
            Assert.AreEqual(0f, BudgetRules.Total(empty), 1e-6f);            // 歳出0
            Assert.AreEqual(300f, BudgetRules.Balance(empty, 300f), 1e-3f); // 歳入丸ごと残る＝何も執行されない
            // ＝歳入は貯まるが「予算と執行」が成立せず、債務/出資度の帰結も生まれない（1年が回らない）。
        }

        // ===== ⑦ 配線：CampaignRules.TickFiscalYear が予算→歳出→債務を勢力ごとに回す（G4） =====

        [Test]
        public void TickFiscalYear_WiresBudgetToDebt_PerFaction()
        {
            var s = new FactionState(Faction.帝国);
            s.taxRate = 0f;                                // 歳入0（EconomyBase に依存せず赤字を作る）
            BudgetRules.Set(s.budget, BudgetCategory.軍事, 100f); // 歳出100
            var c = new CampaignState();
            c.states.Add(s);

            CampaignRules.TickFiscalYear(c, 1f);

            Assert.AreEqual(100f, s.fiscal.baseExpenditure, 1e-3f); // 予算総額→歳出に反映（ApplyToFiscalState）
            Assert.AreEqual(0f, s.fiscal.revenue, 1e-3f);          // 税率0＝歳入0
            Assert.AreEqual(100f, s.fiscal.debt, 1e-3f);           // 赤字100→国債100（翌年へ繰り越し）
        }

        [Test]
        public void TickFiscalYear_EmptyBudget_NoDeficit_NoDebt()
        {
            var s = new FactionState(Faction.同盟);
            s.taxRate = 0f; // 歳入0・歳出0（空予算）
            var c = new CampaignState();
            c.states.Add(s);

            CampaignRules.TickFiscalYear(c, 1f);
            Assert.AreEqual(0f, s.fiscal.debt, 1e-3f); // 歳出0＝赤字なし＝債務増えない（後方互換）
        }
    }
}
