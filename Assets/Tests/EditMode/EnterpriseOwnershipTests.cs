using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 企業のネームド化と法人所有（#1022/#2063/#2070）を固定する：企業が資産/株式/土地を所有できる（AssetOwnerKind.企業）、
    /// 株式は国家・個人・企業が所有可能、企業所有の地代/配当は企業の資本へ、惑星の私有持分は個人＋企業。
    /// </summary>
    public class EnterpriseOwnershipTests
    {
        [SetUp]
        public void Clear()
        {
            PropertyDeedRegistry.Clear();
            FinancialHoldingRegistry.Clear();
            NamedAssetRegistry.Clear();
        }

        [Test]
        public void AssetOwnerKind_HasEnterprise_BackwardCompatOrdinals()
        {
            Assert.AreEqual(0, (int)AssetOwnerKind.人物);
            Assert.AreEqual(1, (int)AssetOwnerKind.国家);
            Assert.AreEqual(2, (int)AssetOwnerKind.企業); // 末尾追加＝直列化後方互換
        }

        [Test]
        public void Enterprise_HasNamedId()
        {
            var e = new Enterprise(Faction.帝国, SystemType.工業, 100f, name: "帝国重工") { id = 42 };
            Assert.AreEqual(42, e.id);
            Assert.AreEqual("帝国重工", e.name);
        }

        // ===== 企業がネームド資産・株式・土地を所有できる（法人所有） =====
        [Test]
        public void Enterprise_OwnsNamedAsset()
        {
            var a = new NamedAsset(0, "本社ビル", NamedAssetCategory.邸宅) { ownerKind = AssetOwnerKind.企業, ownerEnterpriseId = 42 };
            NamedAssetRegistry.Register(a);
            Assert.IsTrue(a.IsEnterpriseOwned);
            Assert.AreEqual("E:42", a.OwnerKey);
            Assert.AreEqual(1, NamedAssetRegistry.OwnedByEnterprise(42).Count);
            Assert.AreEqual(0, NamedAssetRegistry.OwnedByEnterprise(99).Count);
        }

        [Test]
        public void Enterprise_OwnsStock_AlongsideNationAndPerson()
        {
            // 株式（FinancialHolding 株式）を 国家・個人・企業 が同じ原資産に持てる
            var byNation = new FinancialHolding(0, FinancialInstrument.株式, "中央造船")
            { ownerKind = AssetOwnerKind.国家, ownerFaction = Faction.帝国, underlyingId = 7, units = 100f, unitPrice = 10f, incomePerUnit = 0.5f };
            var byPerson = new FinancialHolding(0, FinancialInstrument.株式, "中央造船")
            { ownerKind = AssetOwnerKind.人物, ownerPersonId = 3, underlyingId = 7, units = 50f, unitPrice = 10f, incomePerUnit = 0.5f };
            var byFirm = new FinancialHolding(0, FinancialInstrument.株式, "中央造船")
            { ownerKind = AssetOwnerKind.企業, ownerEnterpriseId = 42, underlyingId = 7, units = 30f, unitPrice = 10f, incomePerUnit = 0.5f };
            FinancialHoldingRegistry.Register(byNation);
            FinancialHoldingRegistry.Register(byPerson);
            FinancialHoldingRegistry.Register(byFirm);

            Assert.AreEqual(1, FinancialHoldingRegistry.OwnedByFaction(Faction.帝国).Count);
            Assert.AreEqual(1, FinancialHoldingRegistry.OwnedByPerson(3).Count);
            Assert.AreEqual(1, FinancialHoldingRegistry.OwnedByEnterprise(42).Count);
            Assert.AreEqual("E:42", byFirm.OwnerKey);
            Assert.AreEqual(3, FinancialHoldingRegistry.HoldingsOfUnderlying(7).Count); // 同一銘柄を3主体が保有
        }

        [Test]
        public void Enterprise_OwnsLandDeed()
        {
            var d = new PropertyDeed(0, 5, 0.3f, 1000f) { ownerKind = AssetOwnerKind.企業, ownerEnterpriseId = 42, rentRate = 0.04f };
            PropertyDeedRegistry.Register(d);
            Assert.IsTrue(d.IsEnterpriseOwned);
            Assert.AreEqual("E:42", d.OwnerKey);
            Assert.AreEqual(1, PropertyDeedRegistry.OwnedByEnterprise(42).Count);
        }

        // ===== 惑星の土地：私有＝個人＋企業 =====
        [Test]
        public void PlanetLand_PrivateShareIncludesEnterprise()
        {
            PropertyDeedRegistry.Register(new PropertyDeed(0, 5, 0.2f, 1000f) { ownerKind = AssetOwnerKind.人物, ownerPersonId = 1 });
            PropertyDeedRegistry.Register(new PropertyDeed(0, 5, 0.3f, 1000f) { ownerKind = AssetOwnerKind.企業, ownerEnterpriseId = 42 });
            Assert.AreEqual(0.2f, PlanetLandRules.PersonShare(5), 1e-4f);
            Assert.AreEqual(0.3f, PlanetLandRules.EnterpriseShare(5), 1e-4f);
            Assert.AreEqual(0.5f, PlanetLandRules.PrivateShare(5), 1e-4f); // 個人＋企業
            Assert.AreEqual(0.5f, PlanetLandRules.StateShare(5), 1e-4f);
        }

        [Test]
        public void PlanetLand_StateOwnsResidualOverPrivateIncludingEnterprise()
        {
            PropertyDeedRegistry.Register(new PropertyDeed(0, 5, 0.2f, 1000f) { ownerKind = AssetOwnerKind.人物, ownerPersonId = 1 });
            PropertyDeedRegistry.Register(new PropertyDeed(0, 5, 0.3f, 1000f) { ownerKind = AssetOwnerKind.企業, ownerEnterpriseId = 42 });
            PropertyDeed state = PlanetLandRules.EnsureStateDeed(5, Faction.帝国, 1000f, canPrivatize: true);
            Assert.AreEqual(0.5f, state.share, 1e-4f); // 1 − (個人0.2＋企業0.3)
            Assert.IsTrue(PlanetLandRules.IsFullyOwned(5));
        }

        [Test]
        public void PlanetLand_CommunismNationalizesEnterpriseLand()
        {
            PropertyDeedRegistry.Register(new PropertyDeed(0, 5, 0.3f, 1000f) { ownerKind = AssetOwnerKind.企業, ownerEnterpriseId = 42 });
            PlanetLandRules.EnsureStateDeed(5, Faction.帝国, 1000f, canPrivatize: false);
            Assert.AreEqual(0f, PlanetLandRules.EnterpriseShare(5), 1e-4f); // 企業の土地も国有化
            Assert.AreEqual(1f, PlanetLandRules.StateShare(5), 1e-4f);
            foreach (var d in PropertyDeedRegistry.DeedsOnSystem(5))
                Assert.IsTrue(d.IsFactionOwned);
        }

        // ===== 企業所有の収益は企業の資本へ =====
        [Test]
        public void EnterpriseOwnedIncome_GoesToEnterpriseCapital()
        {
            var firm = new Enterprise(Faction.帝国, SystemType.工業, 100f, capital: 1000f) { id = 42 };
            FinancialHoldingRegistry.Register(new FinancialHolding(0, FinancialInstrument.株式, "中央造船")
            { ownerKind = AssetOwnerKind.企業, ownerEnterpriseId = 42, underlyingId = 7, units = 100f, unitPrice = 10f, incomePerUnit = 0.5f }); // 配当 50
            PropertyDeedRegistry.Register(new PropertyDeed(0, 5, 0.5f, 2000f) { ownerKind = AssetOwnerKind.企業, ownerEnterpriseId = 42, rentRate = 0.04f }); // 地代 2000×0.5×0.04=40

            Assert.AreEqual(90f, NamedFinancialTickRules.EnterpriseAnnualIncome(42), 1e-3f); // 50＋40
            NamedFinancialTickRules.TickYear(null, id => id == 42 ? firm : null);
            Assert.AreEqual(1090f, firm.capital, 1e-3f); // 資本に再投資
        }

        [Test]
        public void TickYear_BackwardCompat_NullEnterpriseResolverIsSafe()
        {
            FinancialHoldingRegistry.Register(new FinancialHolding(0, FinancialInstrument.株式, "中央造船")
            { ownerKind = AssetOwnerKind.企業, ownerEnterpriseId = 42, units = 100f, unitPrice = 10f, incomePerUnit = 0.5f });
            Assert.DoesNotThrow(() => NamedFinancialTickRules.TickYear(null)); // resolveEnterprise 省略でも安全
        }
    }
}
