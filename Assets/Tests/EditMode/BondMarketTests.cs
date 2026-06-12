using System.Collections.Generic;
using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 債券市場システム基盤（<see cref="BondMarketRules"/>）を固定する：信用スプレッド/必要利回り、適正価格は金利と逆相関、
    /// 現在利回りは価格と逆相関、起債で資本調達＋額面増、市場集計（総債務/平均利回り）、Tick収束。
    /// </summary>
    public class BondMarketTests
    {
        private static BondMarketRules.BondParams P => BondMarketRules.BondParams.Default;

        private static Bond B(float coupon = 0.05f, float price = 1f, float risk = 0f, float face = 1000f)
            => new Bond(Faction.同盟, face, coupon, price, risk);

        [Test]
        public void CreditSpread_AndRequiredYield()
        {
            Assert.AreEqual(0.05f, BondMarketRules.CreditSpread(0.5f, P), 1e-4f); // 0.5*0.1
            Assert.AreEqual(0.10f, BondMarketRules.RequiredYield(0.05f, 0.5f, P), 1e-4f); // 0.05市場金利+0.05スプレッド
        }

        [Test]
        public void FairPrice_InverseToRate()
        {
            var b = B(coupon: 0.05f);
            // 市場金利=表面利率＝額面(1.0)、金利が高いほど価格↓（逆相関）
            Assert.AreEqual(1f, BondMarketRules.FairPrice(b, 0.05f, P), 1e-3f);
            Assert.AreEqual(0.5f, BondMarketRules.FairPrice(b, 0.10f, P), 1e-3f); // 0.05/0.10
            Assert.Greater(BondMarketRules.FairPrice(b, 0.025f, P), 1f);          // 低金利＝額面超
            // 信用リスクが高いほど価格↓
            var risky = B(coupon: 0.05f, risk: 0.5f);
            Assert.Less(BondMarketRules.FairPrice(risky, 0.05f, P), BondMarketRules.FairPrice(b, 0.05f, P));
        }

        [Test]
        public void CurrentYield_InverseToPrice()
        {
            Assert.AreEqual(0.05f, BondMarketRules.CurrentYield(B(0.05f, price: 1f)), 1e-4f);   // 額面＝表面利率
            Assert.AreEqual(0.10f, BondMarketRules.CurrentYield(B(0.05f, price: 0.5f)), 1e-4f); // 価格半分＝利回り倍
        }

        [Test]
        public void Issue_RaisesCapital_AndFace()
        {
            var b = B(price: 0.9f, face: 1000f);
            float raised = BondMarketRules.Issue(b, 500f); // 500額面×0.9
            Assert.AreEqual(450f, raised, 1e-3f);
            Assert.AreEqual(1500f, b.faceValue, 1e-3f);    // 額面残高（債務）が増える
        }

        [Test]
        public void Market_TotalDebt_AverageYield()
        {
            var a = B(0.05f, price: 1f, face: 1000f);   // 時価1000・利回り0.05
            var c = B(0.06f, price: 0.5f, face: 2000f); // 時価1000・利回り0.12
            var market = new List<Bond> { a, c };
            Assert.AreEqual(2000f, BondMarketRules.TotalDebt(market), 1e-3f);   // 1000+1000
            Assert.AreEqual((0.05f + 0.12f) / 2f, BondMarketRules.AverageYield(market), 1e-4f);
        }

        [Test]
        public void Tick_ConvergesPrice()
        {
            var b = B(0.05f, price: 1f);
            // 金利が上がる(0.10)→適正0.5へ価格が下がっていく
            BondMarketRules.Tick(b, 0.10f, P, 0.1f);
            Assert.Less(b.price, 1f);
            Assert.AreEqual(0f, BondMarketRules.TotalDebt(null), 1e-4f); // null安全
        }
    }
}
