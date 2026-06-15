using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 純資産の横断集計（#2056/#2063/#2070・<see cref="NetWorthRules"/>）を固定する：
    /// ネームド資産＋金融持分（時価）＋土地（評価）＋現金（呼び側）を所有者別に合算。
    /// </summary>
    public class NetWorthRulesTests
    {
        [SetUp]
        public void Clear()
        {
            NamedAssetRegistry.Clear();
            FinancialHoldingRegistry.Clear();
            PropertyDeedRegistry.Clear();
        }

        [Test]
        public void NetWorth_SumsLedgersPlusLiquid_ForPerson()
        {
            NamedAssetRegistry.Register(new NamedAsset(0, "名画", NamedAssetCategory.美術品) { ownerKind = AssetOwnerKind.人物, ownerPersonId = 1, value = 300f });
            FinancialHoldingRegistry.Register(new FinancialHolding(0, FinancialInstrument.株式, "重工") { ownerKind = AssetOwnerKind.人物, ownerPersonId = 1, units = 10f, unitPrice = 20f }); // 時価200
            PropertyDeedRegistry.Register(new PropertyDeed(0, 5, 0.5f, 1000f) { ownerKind = AssetOwnerKind.人物, ownerPersonId = 1 }); // 500

            var p = OwnerRef.Person(1);
            Assert.AreEqual(300f, NetWorthRules.NamedAssetValue(p), 1e-3f);
            Assert.AreEqual(200f, NetWorthRules.HoldingsValue(p), 1e-3f);
            Assert.AreEqual(500f, NetWorthRules.LandValue(p), 1e-3f);
            Assert.AreEqual(1000f, NetWorthRules.AssetValue(p), 1e-3f);
            Assert.AreEqual(1400f, NetWorthRules.NetWorth(p, 400f), 1e-3f); // ＋現金400
        }

        [Test]
        public void NetWorth_IsolatesByOwner()
        {
            FinancialHoldingRegistry.Register(new FinancialHolding(0, FinancialInstrument.株式, "重工") { ownerKind = AssetOwnerKind.企業, ownerEnterpriseId = 42, units = 100f, unitPrice = 10f }); // 1000
            PropertyDeedRegistry.Register(new PropertyDeed(0, 5, 1f, 2000f) { ownerKind = AssetOwnerKind.国家, ownerFaction = Faction.帝国 }); // 2000

            Assert.AreEqual(1000f, NetWorthRules.AssetValue(OwnerRef.Enterprise(42)), 1e-3f);
            Assert.AreEqual(2000f, NetWorthRules.AssetValue(OwnerRef.State(Faction.帝国)), 1e-3f);
            Assert.AreEqual(0f, NetWorthRules.AssetValue(OwnerRef.Person(1)), 1e-3f); // 別所有者は0
            Assert.AreEqual(0f, NetWorthRules.AssetValue(OwnerRef.State(Faction.同盟)), 1e-3f);
        }

        [Test]
        public void NetWorth_EmptyOwnerIsLiquidOnly()
        {
            Assert.AreEqual(250f, NetWorthRules.NetWorth(OwnerRef.Person(7), 250f), 1e-3f);
            Assert.AreEqual(0f, NetWorthRules.AssetValue(OwnerRef.Person(7)), 1e-3f);
        }
    }
}
