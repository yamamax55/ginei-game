using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>通貨（#通貨）の純ロジック検証＝名前プールの決定論割り当てと、財政→通貨増発→インフレ／為替の年次進行。</summary>
    public class CurrencyRulesTests
    {
        // ===== CurrencyNames（名前プール） =====

        [Test]
        public void Names_PoolHasExpectedHeadAndCount()
        {
            Assert.AreEqual(11, CurrencyNames.Count);
            Assert.AreEqual("アランド", CurrencyNames.At(0));
            Assert.AreEqual("ドル", CurrencyNames.At(1));
        }

        [Test]
        public void For_IsDeterministicAndDistinctForDemoFactions()
        {
            Assert.AreEqual("アランド", CurrencyNames.For(Faction.帝国)); // 序数0
            Assert.AreEqual("ドル", CurrencyNames.For(Faction.同盟));     // 序数1
            Assert.AreNotEqual(CurrencyNames.For(Faction.帝国), CurrencyNames.For(Faction.同盟));
            Assert.AreEqual(CurrencyNames.For(Faction.帝国), CurrencyNames.For(Faction.帝国)); // 決定論
        }

        [Test]
        public void At_WrapsOutOfRangeIndices()
        {
            Assert.AreEqual(CurrencyNames.At(0), CurrencyNames.At(CurrencyNames.Count));
            Assert.AreEqual(CurrencyNames.At(CurrencyNames.Count - 1), CurrencyNames.At(-1));
        }

        // ===== CurrencyRules.Ensure（用意・冪等） =====

        [Test]
        public void Ensure_AssignsNameOnceAndIsIdempotent()
        {
            CurrencyState c = CurrencyRules.Ensure(null, Faction.同盟);
            Assert.IsNotNull(c);
            Assert.AreEqual("ドル", c.currencyName);

            c.exchangeRate = 0.8f; // 既存状態は保持される
            CurrencyState again = CurrencyRules.Ensure(c, Faction.同盟);
            Assert.AreSame(c, again);
            Assert.AreEqual("ドル", again.currencyName);
            Assert.AreEqual(0.8f, again.exchangeRate, 1e-5f);
        }

        // ===== CurrencyRules.TickYear（赤字→インフレ／黒字→均衡） =====

        [Test]
        public void TickYear_DeficitRaisesPriceLevelAndInflation()
        {
            var c = new CurrencyState();
            // 赤字＝歳出>歳入。経済規模に対し大きめの赤字で通貨増発。
            var fs = new FiscalState(revenue: 100f, baseExpenditure: 200f, debt: 0f);
            float economy = 200f;

            float before = c.priceLevel;
            CurrencyRules.TickYear(c, fs, economy, 1f);

            Assert.Greater(c.inflationRate, 0f, "赤字の貨幣化でインフレ率は正");
            Assert.Greater(c.priceLevel, before, "物価水準は上がる");
            Assert.Greater(c.moneySupply, 1000f, "通貨供給は赤字ぶん増える");
        }

        [Test]
        public void TickYear_BalancedBudgetDoesNotInflate()
        {
            var c = new CurrencyState();
            var fs = new FiscalState(revenue: 150f, baseExpenditure: 150f, debt: 0f); // 均衡＝赤字0
            float economy = 200f;

            CurrencyRules.TickYear(c, fs, economy, 1f);

            // 通貨増発なし＝正のインフレは起きない（実質成長ぶん むしろ僅かにデフレ寄り）。
            Assert.LessOrEqual(c.inflationRate, 1e-4f, "均衡予算では正のインフレは起きない");
            Assert.LessOrEqual(c.priceLevel, 1f + 1e-4f, "物価は上がらない");
            Assert.AreEqual(1000f, c.moneySupply, 1e-4f, "通貨供給は増えない（赤字0）");
        }

        [Test]
        public void TickYear_SetsExchangeRateFromFiscalHealth()
        {
            var c = new CurrencyState();
            var fs = new FiscalState(revenue: 100f, baseExpenditure: 100f, debt: 0f);
            float economy = 200f;

            CurrencyRules.TickYear(c, fs, economy, 1f);
            // 為替係数は FiscalRules.ExchangeRateFactor と一致（委譲・二重実装しない）。
            float expected = FiscalRules.ExchangeRateFactor(fs, economy, FiscalRules.FiscalParams.Default);
            Assert.AreEqual(expected, c.exchangeRate, 1e-4f);
        }

        [Test]
        public void TickYear_NullSafe()
        {
            Assert.DoesNotThrow(() => CurrencyRules.TickYear(null, new FiscalState(), 100f, 1f));
            Assert.DoesNotThrow(() => CurrencyRules.TickYear(new CurrencyState(), null, 100f, 1f));
            var c = new CurrencyState();
            CurrencyRules.TickYear(c, new FiscalState(), 100f, 0f); // dt=0 は何もしない
            Assert.AreEqual(1f, c.priceLevel, 1e-5f);
        }
    }
}
