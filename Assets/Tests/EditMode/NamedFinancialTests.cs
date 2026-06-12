using System.Collections.Generic;
using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// ネームド金融資産・不動産（#2070）：保有(NFIN-1/2)/評価・紙くず化(NFIN-3)/不動産(NFIN-4)/地代・細分化(NFIN-5)/Tick(NFIN-6)。
    /// </summary>
    public class NamedFinancialTests
    {
        [SetUp]
        public void Reset()
        {
            FinancialHoldingRegistry.Clear();
            PropertyDeedRegistry.Clear();
        }

        // --- NFIN-1/2/3 金融資産（時価・配当・紙くず化） ---
        [Test]
        public void Financial_ValueIncomeWorthless()
        {
            var h = FinancialHoldingRegistry.Register(new FinancialHolding(0, FinancialInstrument.株式, "帝国重工")
            {
                ownerKind = AssetOwnerKind.人物, ownerPersonId = 1, units = 100f, unitPrice = 10f, incomePerUnit = 0.5f, bookCost = 900f
            });
            Assert.Greater(h.id, 0);
            Assert.AreEqual(1000f, FinancialAssetRules.MarketValue(h), 1e-3f);
            Assert.AreEqual(50f, FinancialAssetRules.AnnualIncome(h), 1e-3f);   // 配当金
            Assert.AreEqual(0.05f, FinancialAssetRules.Yield(h), 1e-4f);
            Assert.AreEqual(100f, FinancialAssetRules.UnrealizedPnL(h), 1e-3f); // 含み益
            Assert.IsFalse(FinancialAssetRules.IsWorthless(h));
            Assert.AreEqual(1000f, FinancialHoldingRegistry.TotalMarketValueOfPerson(1), 1e-3f);

            // 紙くず化＝時価0・配当も0
            FinancialAssetRules.MarkToMarket(h, 0f, 0f);
            Assert.IsTrue(FinancialAssetRules.IsWorthless(h));
            Assert.AreEqual(0f, FinancialAssetRules.MarketValue(h), 1e-3f);
            Assert.AreEqual(0f, FinancialAssetRules.AnnualIncome(h), 1e-3f);
        }

        // --- NFIN-4/5 不動産（地代・評価・細分化） ---
        [Test]
        public void Property_RentValueFragmentation()
        {
            var d = new PropertyDeed(1, 5, 0.2f, 3000f) { rentRate = 0.04f };
            Assert.AreEqual(600f, PropertyValuationRules.DeedValue(d), 1e-3f);
            Assert.AreEqual(24f, PropertyValuationRules.RentIncome(d), 1e-3f);   // 地代
            Assert.AreEqual(3150f, PropertyValuationRules.ValueAfterYear(3000f, 0.05f), 1e-3f);

            // 細分化（分地相続）：持分0.6 を3人へ等分→各0.2、惑星の権利証が1→3枚
            Assert.AreEqual(0.2f, PropertyFragmentationRules.SplitShares(0.6f, 3), 1e-4f);
            var deed = PropertyDeedRegistry.Register(new PropertyDeed(0, 5, 0.6f, 1000f) { ownerKind = AssetOwnerKind.人物, ownerPersonId = 1, rentRate = 0.03f });
            var parts = PropertyFragmentationRules.FragmentOnInheritance(deed, new List<int> { 2, 3, 4 });
            Assert.AreEqual(3, parts.Count);
            Assert.AreEqual(3, PropertyDeedRegistry.CountDeedsOnSystem(5));      // 細分化度
            Assert.AreEqual(3, PropertyFragmentationRules.FragmentationIndex(5));
            Assert.AreEqual(0.6f, PropertyDeedRegistry.TotalShareOnSystem(5), 1e-4f); // 総持分は保存
            Assert.IsNull(PropertyDeedRegistry.Get(deed.id));                   // 元の権利証は除去
            Assert.AreEqual(0.2f, parts[0].share, 1e-4f);

            // 相続人不在は細分化しない（元の deed が残る）
            var solo = PropertyDeedRegistry.Register(new PropertyDeed(0, 9, 0.5f, 1000f) { ownerPersonId = 1 });
            var none = PropertyFragmentationRules.FragmentOnInheritance(solo, new List<int>());
            Assert.AreEqual(0, none.Count);
            Assert.AreEqual(1, PropertyDeedRegistry.CountDeedsOnSystem(9));

            // 買い集め（統合）：細分化の逆
            var merged = PropertyFragmentationRules.Consolidate(5, 7);
            Assert.AreEqual(1, PropertyDeedRegistry.CountDeedsOnSystem(5));
            Assert.AreEqual(0.6f, merged.share, 1e-4f);
            Assert.AreEqual(7, merged.ownerPersonId);
        }

        // --- NFIN-6 Tick（配当+地代→wealth） ---
        [Test]
        public void Tick_AccruesDividendAndRent()
        {
            var p = new Person { id = 7, wealth = 0f };
            FinancialHoldingRegistry.Register(new FinancialHolding(0, FinancialInstrument.株式, "株")
            { ownerKind = AssetOwnerKind.人物, ownerPersonId = 7, units = 100f, unitPrice = 10f, incomePerUnit = 0.5f }); // 配当50
            PropertyDeedRegistry.Register(new PropertyDeed(0, 3, 0.2f, 3000f)
            { ownerKind = AssetOwnerKind.人物, ownerPersonId = 7, rentRate = 0.04f }); // 地代24
            NamedFinancialTickRules.TickYear(id => id == 7 ? p : null);
            Assert.AreEqual(74f, p.wealth, 1e-3f); // 50 + 24

            // 国家の収益集計
            FinancialHoldingRegistry.Register(new FinancialHolding(0, FinancialInstrument.債券, "国債")
            { ownerKind = AssetOwnerKind.国家, ownerFaction = Faction.帝国, units = 500f, unitPrice = 100f, incomePerUnit = 3f }); // クーポン1500
            Assert.AreEqual(1500f, NamedFinancialTickRules.FactionAnnualIncome(Faction.帝国), 1e-3f);

            Assert.DoesNotThrow(() => NamedFinancialTickRules.TickYear(_ => null));
        }
    }
}
