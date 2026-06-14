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

        // ===== CurrencyRules.CrossRate（為替クロスレート） =====

        [Test]
        public void CrossRate_EqualStrengthIsParity()
        {
            var a = new CurrencyState { exchangeRate = 1f };
            var b = new CurrencyState { exchangeRate = 1f };
            Assert.AreEqual(1f, CurrencyRules.CrossRate(a, b), 1e-5f);
        }

        [Test]
        public void CrossRate_StrongBaseBuysMoreQuote()
        {
            var strong = new CurrencyState { exchangeRate = 1.2f };
            var weak = new CurrencyState { exchangeRate = 0.8f };
            Assert.AreEqual(1.5f, CurrencyRules.CrossRate(strong, weak), 1e-4f);   // 1.2/0.8
            Assert.AreEqual(1f / 1.5f, CurrencyRules.CrossRate(weak, strong), 1e-4f); // 逆は逆数
        }

        [Test]
        public void CrossRate_EdgeCases()
        {
            Assert.AreEqual(1f, CurrencyRules.CrossRate(null, new CurrencyState()), 1e-5f);
            Assert.AreEqual(0f, CurrencyRules.CrossRate(new CurrencyState { exchangeRate = 1f }, new CurrencyState { exchangeRate = 0f }), 1e-5f);
        }

        // ===== 基軸通貨（#ReserveCurrencyRules 配線） =====

        [Test]
        public void TickReserve_HighSharesRaiseStatusAndYieldSeigniorage()
        {
            var c = new CurrencyState(); // reserveStatus=0 から
            float income = CurrencyRules.TickReserve(c, tradeShare: 1f, militaryHegemony: 1f, trustInIssuer: 1f, globalTradeVolume: 1000f, dt: 1f);
            Assert.Greater(c.reserveStatus, 0f, "高い交易/軍事/信認で基軸度が上がる");
            Assert.GreaterOrEqual(income, 0f);
            Assert.AreEqual(ReserveCurrencyRules.SeigniorageIncome(c.reserveStatus, 1000f), income, 1e-4f, "発行益は ReserveCurrencyRules へ委譲");
        }

        [Test]
        public void TickReserve_NullSafe()
        {
            Assert.AreEqual(0f, CurrencyRules.TickReserve(null, 1f, 1f, 1f, 1000f, 1f), 1e-5f);
        }

        // ===== 通貨改鋳（#CoinageRules 配線） =====

        [Test]
        public void TickCoinage_DebasementErodesTrustAndMints()
        {
            var c = new CurrencyState { silverContent = 1f, publicTrust = 1f, moneySupply = 1000f };
            float minted = CurrencyRules.TickCoinage(c, targetSilverContent: 0.5f, dt: 1f);
            Assert.AreEqual(0.5f, c.silverContent, 1e-4f, "品位は目標へ下がる");
            Assert.Less(c.publicTrust, 1f, "品位低下で信認が崩れる");
            Assert.Greater(minted, 0f, "品位を抜いたぶんが改鋳益");
        }

        [Test]
        public void TickCoinage_PureCoinageMintsNothing()
        {
            var c = new CurrencyState { silverContent = 1f, publicTrust = 1f, moneySupply = 1000f };
            float minted = CurrencyRules.TickCoinage(c, targetSilverContent: 1f, dt: 1f);
            Assert.AreEqual(0f, minted, 1e-4f, "純度維持なら改鋳益なし");
            Assert.GreaterOrEqual(c.publicTrust, 1f - 1e-4f, "信認は崩れない");
        }

        [Test]
        public void RealValueFactor_LowTrustReducesValue()
        {
            var trusted = new CurrencyState { publicTrust = 1f };
            var distrusted = new CurrencyState { publicTrust = 0.3f };
            Assert.Less(CurrencyRules.RealValueFactor(distrusted), CurrencyRules.RealValueFactor(trusted),
                "信認が低いほど実質購買力が下がる（額面通りに通らない）");
            Assert.AreEqual(1f, CurrencyRules.RealValueFactor(null), 1e-5f);
        }

        // ===== 市場価格→物価（#179↔物価 コストプッシュ） =====

        [Test]
        public void TickYear_MarketPressureAddsInflation()
        {
            var baseC = new CurrencyState();
            var pressC = new CurrencyState();
            var fs = new FiscalState(revenue: 100f, baseExpenditure: 100f, debt: 0f); // 赤字0
            float economy = 200f;

            CurrencyRules.TickYear(baseC, fs, economy, 1f, marketPressure: 0f);
            CurrencyRules.TickYear(pressC, fs, economy, 1f, marketPressure: 0.2f);

            Assert.Greater(pressC.inflationRate, baseC.inflationRate, "市場の逼迫はコストプッシュでインフレを上げる");
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
