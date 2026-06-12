using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 企業＝操業する経済アクター（#1022・<see cref="EnterpriseRules"/>）を固定する：生産＝労働×生産性×資本、利潤＝売上−賃金、
    /// 搾取率（剰余価値/賃金 #271）、労働需要は収益性で増減、Tick は利潤を再投資（蓄積 #269）し雇用を供給律速で動かす。
    /// </summary>
    public class EnterpriseTests
    {
        private static Enterprise Firm()
            => new Enterprise(Faction.帝国, SystemType.工業, employees: 100f, capital: 1000f, productivity: 1f, wageRate: 1f);

        [Test]
        public void Output_Revenue_Profit_Exploitation()
        {
            var e = Firm();
            // 資本集約度 1+1000*0.0005=1.5、産出=100*1*1.5=150
            Assert.AreEqual(1.5f, EnterpriseRules.CapitalFactor(e), 1e-4f);
            Assert.AreEqual(150f, EnterpriseRules.Output(e), 1e-3f);
            Assert.AreEqual(150f, EnterpriseRules.Revenue(e, 1f), 1e-3f);
            Assert.AreEqual(100f, EnterpriseRules.WageBill(e), 1e-3f);
            Assert.AreEqual(50f, EnterpriseRules.Profit(e, 1f), 1e-3f);
            // 搾取率＝利潤/賃金＝0.5。価格が上がると搾取率↑（剰余価値が増える）
            Assert.AreEqual(0.5f, EnterpriseRules.ExploitationRate(e, 1f), 1e-3f);
            Assert.Greater(EnterpriseRules.ExploitationRate(e, 2f), EnterpriseRules.ExploitationRate(e, 1f));
        }

        [Test]
        public void LaborDemand_GrowsWhenProfitable_ShrinksWhenNot()
        {
            var e = Firm();
            // 限界収益(1*1.5*price) > 賃金(1) なら雇用拡大、未満なら縮小
            Assert.Greater(EnterpriseRules.LaborDemand(e, 1f), e.employees);   // price1: MRP1.5>1
            Assert.Less(EnterpriseRules.LaborDemand(e, 0.5f), e.employees);    // price0.5: MRP0.75<1
        }

        [Test]
        public void Tick_Profitable_AccumulatesCapital_Hires()
        {
            var e = Firm();
            float profit = EnterpriseRules.Tick(e, 1f, availableLabor: 1000f, dt: 1f);
            Assert.AreEqual(50f, profit, 1e-3f);
            Assert.Greater(e.capital, 1000f);     // 利潤を再投資＝資本蓄積
            Assert.Greater(e.employees, 100f);    // 労働需要(150)へ向けて雇用
            Assert.Less(e.employees, 150f);        // 一度に全部は雇わない
        }

        [Test]
        public void Tick_Loss_NoAccumulation_LaysOff()
        {
            var e = Firm();
            float profit = EnterpriseRules.Tick(e, 0.5f, availableLabor: 1000f, dt: 1f);
            Assert.Less(profit, 0f);
            Assert.AreEqual(1000f, e.capital, 1e-3f); // 赤字は再投資しない＝資本据え置き
            Assert.Less(e.employees, 100f);            // 解雇（縮小）
        }

        [Test]
        public void Tick_HiringLimitedByLaborSupply()
        {
            var e = Firm();
            // 利潤は出る（需要150）が労働供給が5しかない＝雇用は最大105まで
            EnterpriseRules.Tick(e, 1f, availableLabor: 5f, dt: 1f);
            Assert.LessOrEqual(e.employees, 105f + 1e-3f);
            Assert.Greater(e.employees, 100f);
        }
    }
}
