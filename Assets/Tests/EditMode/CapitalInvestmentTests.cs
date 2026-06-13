using System.Collections.Generic;
using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 資本投下の基盤（<see cref="CapitalInvestmentRules"/>）を固定する：限界収益＝投資の魅力度、資本利潤率 r、投下で資本↑、
    /// プールは高リターンへ多く配分（全0なら均等）、DeployAll で実際に投下される。
    /// </summary>
    public class CapitalInvestmentTests
    {
        private static Enterprise Firm(float employees = 100f)
            => new Enterprise(Faction.帝国, SystemType.工業, employees, capital: 1000f, productivity: 1f, wageRate: 1f);

        [Test]
        public void MarginalReturn_AndReturnOnCapital()
        {
            var e = Firm();
            // 限界収益＝100*1*0.0005*1=0.05
            Assert.AreEqual(0.05f, CapitalInvestmentRules.MarginalReturnOnCapital(e, 1f), 1e-5f);
            // r＝利潤(50)/資本(1000)=0.05
            Assert.AreEqual(0.05f, CapitalInvestmentRules.ReturnOnCapital(e, 1f), 1e-4f);
            // 価格が上がると魅力度・r ともに上がる
            Assert.Greater(CapitalInvestmentRules.MarginalReturnOnCapital(e, 2f), CapitalInvestmentRules.MarginalReturnOnCapital(e, 1f));
        }

        [Test]
        public void Invest_RaisesCapital()
        {
            var e = Firm();
            float put = CapitalInvestmentRules.Invest(e, 200f);
            Assert.AreEqual(200f, put, 1e-4f);
            Assert.AreEqual(1200f, e.capital, 1e-4f);
            Assert.AreEqual(0f, CapitalInvestmentRules.Invest(e, -5f)); // 負は投下しない
        }

        [Test]
        public void AllocateByReturn_FlowsToHigherReturn()
        {
            var small = Firm(100f);  // 限界 0.05
            var big = Firm(200f);    // 限界 0.10（雇用多＝資本投下の効果大）
            var alloc = CapitalInvestmentRules.AllocateByReturn(300f, new List<Enterprise> { small, big }, 1f);
            Assert.AreEqual(100f, alloc[0], 1e-3f); // 300*(0.05/0.15)
            Assert.AreEqual(200f, alloc[1], 1e-3f); // 300*(0.10/0.15)＝高リターンへ多く
            Assert.AreEqual(300f, alloc[0] + alloc[1], 1e-3f); // 総額保存
        }

        [Test]
        public void AllocateByReturn_AllZero_SplitsEqually()
        {
            var a = Firm(100f);
            var b = Firm(100f);
            // 価格0＝どこも限界収益0＝均等配分
            var alloc = CapitalInvestmentRules.AllocateByReturn(100f, new List<Enterprise> { a, b }, 0f);
            Assert.AreEqual(50f, alloc[0], 1e-3f);
            Assert.AreEqual(50f, alloc[1], 1e-3f);
        }

        [Test]
        public void DeployAll_InvestsThePool()
        {
            var a = Firm(100f);
            var b = Firm(200f);
            float capBefore = a.capital + b.capital;
            float deployed = CapitalInvestmentRules.DeployAll(300f, new List<Enterprise> { a, b }, 1f);
            Assert.AreEqual(300f, deployed, 1e-3f);
            Assert.AreEqual(capBefore + 300f, a.capital + b.capital, 1e-3f); // 投下した分だけ資本が増える
            Assert.Greater(b.capital, a.capital); // 高リターン先により多く投下
        }
    }
}
