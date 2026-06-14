using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>国債（#161/#185 配線）の純ロジック検証＝額面の債務同期・財政悪化で信用リスク↑価格↓利回り↑。</summary>
    public class SovereignBondRulesTests
    {
        [Test]
        public void Ensure_CreatesBondWithCouponAndIsIdempotent()
        {
            Bond b = SovereignBondRules.Ensure(null, Faction.帝国);
            Assert.IsNotNull(b);
            Assert.AreEqual(Faction.帝国, b.issuer);
            Assert.AreEqual(SovereignBondRules.DefaultCouponRate, b.couponRate, 1e-5f);

            Bond again = SovereignBondRules.Ensure(b, Faction.帝国);
            Assert.AreSame(b, again);
        }

        [Test]
        public void TickYear_SyncsFaceValueToDebt()
        {
            Bond b = SovereignBondRules.Ensure(null, Faction.帝国);
            var fs = new FiscalState(revenue: 100f, baseExpenditure: 100f, debt: 500f);
            SovereignBondRules.TickYear(b, fs, economy: 400f, dt: 1f);
            Assert.AreEqual(500f, b.faceValue, 1e-3f, "額面残高＝累積債務に同期");
        }

        [Test]
        public void TickYear_WorseFinancesRaiseRiskAndLowerPrice()
        {
            // 健全：債務0。
            Bond healthy = SovereignBondRules.Ensure(null, Faction.帝国);
            var fsHealthy = new FiscalState(revenue: 100f, baseExpenditure: 100f, debt: 0f);
            SovereignBondRules.TickYear(healthy, fsHealthy, economy: 400f, dt: 1f);

            // 不健全：巨額債務。
            Bond risky = SovereignBondRules.Ensure(null, Faction.同盟);
            var fsRisky = new FiscalState(revenue: 100f, baseExpenditure: 100f, debt: 2000f);
            SovereignBondRules.TickYear(risky, fsRisky, economy: 400f, dt: 1f);

            Assert.Greater(risky.defaultRisk, healthy.defaultRisk, "財政悪化でデフォルトリスク↑");
            Assert.Less(risky.price, healthy.price, "財政悪化で国債価格↓（利回り↑）");
            Assert.Greater(BondMarketRules.CurrentYield(risky), BondMarketRules.CurrentYield(healthy), "利回りは逆相関で↑");
        }

        [Test]
        public void TickYear_NullSafe()
        {
            Assert.DoesNotThrow(() => SovereignBondRules.TickYear(null, new FiscalState(), 100f, 1f));
            Assert.DoesNotThrow(() => SovereignBondRules.TickYear(new Bond(), null, 100f, 1f));
            var b = SovereignBondRules.Ensure(null, Faction.帝国);
            float price0 = b.price;
            SovereignBondRules.TickYear(b, new FiscalState(), 100f, 0f); // dt=0 は何もしない
            Assert.AreEqual(price0, b.price, 1e-5f);
        }
    }
}
