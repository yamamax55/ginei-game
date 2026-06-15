using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 財産の購入・売却システム（#2063/#2070・<see cref="AssetTradeRules"/>／<see cref="FundsAccess"/>）を固定する：
    /// 資金アクセス（人物/国家/企業）・価格評価・ネームド資産の売買・金融持分の売買（部分約定）・決済・ゲート（譲渡不可/自己取引/資金不足）。
    /// </summary>
    public class AssetTradeRulesTests
    {
        private Person alice, bob;
        private Enterprise firm;
        private FactionState empire;
        private FundsAccess Funds()
        {
            return new FundsAccess(
                id => id == 1 ? alice : id == 2 ? bob : null,
                id => id == 42 ? firm : null,
                f => f == Faction.帝国 ? empire : null);
        }

        [SetUp]
        public void Setup()
        {
            NamedAssetRegistry.Clear();
            FinancialHoldingRegistry.Clear();
            alice = new Person { id = 1, wealth = 1000f };
            bob = new Person { id = 2, wealth = 50f };
            firm = new Enterprise(Faction.帝国, SystemType.工業, 100f, capital: 2000f) { id = 42 };
            empire = new FactionState { faction = Faction.帝国, treasury = 5000f };
        }

        // ===== 資金アクセス =====
        [Test]
        public void FundsAccess_GetAddCanAfford_AcrossOwnerKinds()
        {
            var f = Funds();
            Assert.AreEqual(1000f, f.Get(OwnerRef.Person(1)), 1e-3f);
            Assert.AreEqual(2000f, f.Get(OwnerRef.Enterprise(42)), 1e-3f);
            Assert.AreEqual(5000f, f.Get(OwnerRef.State(Faction.帝国)), 1e-3f);
            Assert.IsTrue(f.CanAfford(OwnerRef.Person(1), 800f));
            Assert.IsFalse(f.CanAfford(OwnerRef.Person(2), 800f));
            f.Add(OwnerRef.Person(1), -300f);
            Assert.AreEqual(700f, alice.wealth, 1e-3f);
            f.Add(OwnerRef.State(Faction.帝国), 100f);
            Assert.AreEqual(5100f, empire.treasury, 1e-3f);
            // 解決不能は0／false
            Assert.AreEqual(0f, f.Get(OwnerRef.Person(99)), 1e-3f);
            Assert.IsFalse(f.Add(OwnerRef.Person(99), 10f));
        }

        // ===== 価格 =====
        [Test]
        public void Price_NamedAssetAndHolding()
        {
            var a = new NamedAsset(0, "名画", NamedAssetCategory.美術品) { value = 500f };
            Assert.AreEqual(500f, AssetTradeRules.Price(a), 1e-3f);
            Assert.AreEqual(600f, AssetTradeRules.Price(a, 1.2f), 1e-3f);
            var h = new FinancialHolding(0, FinancialInstrument.株式, "重工") { units = 100f, unitPrice = 10f };
            Assert.AreEqual(300f, AssetTradeRules.Price(h, 30f), 1e-3f);   // 30×10
            Assert.AreEqual(1000f, AssetTradeRules.Price(h, 999f), 1e-3f); // 口数上限100
            Assert.AreEqual(50f, AssetTradeRules.MaxAffordableUnits(OwnerRef.Person(2), 1f, Funds()), 1e-3f); // bob 50 / 単価1
        }

        // ===== ネームド資産の売買 =====
        [Test]
        public void BuyNamedAsset_TransfersOwnershipAndSettles()
        {
            var a = NamedAssetRegistry.Register(new NamedAsset(0, "宝石", NamedAssetCategory.財宝) { value = 400f });
            OwnerRef.State(Faction.帝国).ApplyTo(a); // 売主＝国家
            var f = Funds();
            bool ok = AssetTradeRules.BuyNamedAsset(a, OwnerRef.Person(1), f, out float price);
            Assert.IsTrue(ok);
            Assert.AreEqual(400f, price, 1e-3f);
            Assert.IsTrue(OwnerRef.Person(1).Owns(a));   // 買主のものに
            Assert.AreEqual(600f, alice.wealth, 1e-3f);  // 1000−400
            Assert.AreEqual(5400f, empire.treasury, 1e-3f); // 5000＋400（売主＝国家へ）
        }

        [Test]
        public void BuyNamedAsset_InsufficientFunds_Fails()
        {
            var a = NamedAssetRegistry.Register(new NamedAsset(0, "宮殿", NamedAssetCategory.宮殿) { value = 900f });
            OwnerRef.State(Faction.帝国).ApplyTo(a);
            bool ok = AssetTradeRules.BuyNamedAsset(a, OwnerRef.Person(2), Funds(), out float price); // bob 50 < 900
            Assert.IsFalse(ok);
            Assert.IsTrue(OwnerRef.State(Faction.帝国).Owns(a)); // 所有変わらず
            Assert.AreEqual(50f, bob.wealth, 1e-3f);
        }

        [Test]
        public void BuyNamedAsset_NonTransferable_Fails()
        {
            var title = NamedAssetRegistry.Register(new NamedAsset(0, "公爵位", NamedAssetCategory.称号) { value = 100f, transferable = false });
            OwnerRef.State(Faction.帝国).ApplyTo(title);
            Assert.IsFalse(AssetTradeRules.BuyNamedAsset(title, OwnerRef.Person(1), Funds(), out _)); // 称号は売買不可
        }

        [Test]
        public void BuyNamedAsset_SelfTrade_Fails()
        {
            var a = NamedAssetRegistry.Register(new NamedAsset(0, "邸宅", NamedAssetCategory.邸宅) { value = 100f });
            OwnerRef.Person(1).ApplyTo(a);
            Assert.IsFalse(AssetTradeRules.BuyNamedAsset(a, OwnerRef.Person(1), Funds(), out _)); // 自己取引不可
        }

        // ===== 金融持分の売買 =====
        [Test]
        public void BuyHolding_TransfersUnitsAndSettles()
        {
            var seller = FinancialHoldingRegistry.Register(new FinancialHolding(0, FinancialInstrument.株式, "帝国重工")
            { ownerKind = AssetOwnerKind.国家, ownerFaction = Faction.帝国, underlyingId = 7, units = 100f, unitPrice = 10f, incomePerUnit = 0.5f });
            var f = Funds();
            float price = AssetTradeRules.BuyHolding(seller, OwnerRef.Person(1), 30f, f, out float moved);
            Assert.AreEqual(30f, moved, 1e-3f);
            Assert.AreEqual(300f, price, 1e-3f);          // 30×10
            Assert.AreEqual(70f, seller.units, 1e-3f);    // 売主は減る
            Assert.AreEqual(700f, alice.wealth, 1e-3f);   // 1000−300
            Assert.AreEqual(5300f, empire.treasury, 1e-3f); // 売主＝国家へ
            FinancialHolding bought = AssetTradeRules.FindHolding(7, FinancialInstrument.株式, OwnerRef.Person(1));
            Assert.IsNotNull(bought);
            Assert.AreEqual(30f, bought.units, 1e-3f);
        }

        [Test]
        public void BuyHolding_PartialFill_WhenFundsLimited()
        {
            var seller = FinancialHoldingRegistry.Register(new FinancialHolding(0, FinancialInstrument.株式, "帝国重工")
            { ownerKind = AssetOwnerKind.国家, ownerFaction = Faction.帝国, underlyingId = 7, units = 100f, unitPrice = 10f });
            // bob は 50 しか持たない＝5口だけ買える
            float price = AssetTradeRules.BuyHolding(seller, OwnerRef.Person(2), 30f, Funds(), out float moved);
            Assert.AreEqual(5f, moved, 1e-3f);
            Assert.AreEqual(50f, price, 1e-3f);
            Assert.AreEqual(0f, bob.wealth, 1e-3f);
            Assert.AreEqual(95f, seller.units, 1e-3f);
        }

        [Test]
        public void BuyHolding_SelfTrade_Fails()
        {
            var h = FinancialHoldingRegistry.Register(new FinancialHolding(0, FinancialInstrument.株式, "重工")
            { ownerKind = AssetOwnerKind.人物, ownerPersonId = 1, underlyingId = 7, units = 100f, unitPrice = 10f });
            Assert.AreEqual(0f, AssetTradeRules.BuyHolding(h, OwnerRef.Person(1), 10f, Funds(), out float moved), 1e-3f);
            Assert.AreEqual(0f, moved, 1e-3f);
        }
    }
}
