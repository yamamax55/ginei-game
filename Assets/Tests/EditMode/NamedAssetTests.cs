using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// ネームド資産（#2063）：データ/所有者(NASSET-1)/台帳(NASSET-2)/評価(NASSET-3)/移動(NASSET-4)/効果(NASSET-5)/Tick(NASSET-6)。
    /// </summary>
    public class NamedAssetTests
    {
        [SetUp]
        public void Reset() => NamedAssetRegistry.Clear();

        // --- NASSET-1/2 データ・台帳 ---
        [Test]
        public void Registry_RegisterQueryRemove()
        {
            var a = NamedAssetRegistry.Register(new NamedAsset(0, "獅子泉宮", NamedAssetCategory.宮殿)
            {
                ownerKind = AssetOwnerKind.人物, ownerPersonId = 1, value = 1000f
            });
            Assert.Greater(a.id, 0); // 採番
            Assert.IsTrue(a.IsPersonOwned);
            Assert.AreEqual("P:1", a.OwnerKey);
            Assert.AreSame(a, NamedAssetRegistry.Get(a.id));
            Assert.AreEqual(1, NamedAssetRegistry.OwnedByPerson(1).Count);
            Assert.AreEqual(1000f, NamedAssetRegistry.TotalValueOfPerson(1), 1e-3f);
            Assert.AreEqual(1, NamedAssetRegistry.CountOwnedByPerson(1));
            Assert.IsTrue(NamedAssetRegistry.Remove(a.id));
            Assert.AreEqual(0, NamedAssetRegistry.OwnedByPerson(1).Count);
        }

        // --- NASSET-3 評価（純収益・値上がり・威信） ---
        [Test]
        public void Rules_NetIncomeAppreciationPrestige()
        {
            var a = new NamedAsset(1, "領地", NamedAssetCategory.領地) { value = 1000f, yieldRate = 0.1f, upkeepRate = 0.03f, prestige = 8f };
            Assert.AreEqual(100f, NamedAssetRules.GrossYield(a), 1e-3f);
            Assert.AreEqual(30f, NamedAssetRules.Upkeep(a), 1e-3f);
            Assert.AreEqual(70f, NamedAssetRules.NetAnnualIncome(a), 1e-3f);     // 黒字
            Assert.AreEqual(1050f, NamedAssetRules.ValueAfterYear(1000f, 0.05f), 1e-3f); // 値上がり
            Assert.AreEqual(800f, NamedAssetRules.ValueAfterYear(1000f, -0.2f), 1e-3f);  // 暴落#185
            Assert.AreEqual(0f, NamedAssetRules.ValueAfterYear(50f, -3f), 1e-3f);        // 0でクランプ
            Assert.AreEqual(8f, NamedAssetRules.PrestigeContribution(a), 1e-3f);
            // 維持費が収益を上回る宮殿は赤字
            var palace = new NamedAsset(2, "宮殿", NamedAssetCategory.宮殿) { value = 1000f, yieldRate = 0.02f, upkeepRate = 0.05f };
            Assert.AreEqual(-30f, NamedAssetRules.NetAnnualIncome(palace), 1e-3f);
        }

        // --- NASSET-4 所有者移動（譲渡/相続/没収・ゲート） ---
        [Test]
        public void Transfer_GateAndOwnerChange()
        {
            var a = new NamedAsset(1, "旗艦", NamedAssetCategory.旗艦) { ownerKind = AssetOwnerKind.人物, ownerPersonId = 1 };
            Assert.IsTrue(AssetTransferRules.Inherit(a, 2));
            Assert.AreEqual(2, a.ownerPersonId);
            Assert.IsTrue(AssetTransferRules.Confiscate(a, Faction.帝国));
            Assert.IsTrue(a.IsFactionOwned);
            Assert.AreEqual(Faction.帝国, a.ownerFaction);
            // 称号は譲渡不可＝no-op
            var title = new NamedAsset(2, "ローエングラム伯", NamedAssetCategory.称号) { ownerKind = AssetOwnerKind.人物, ownerPersonId = 5, transferable = false };
            Assert.IsFalse(AssetTransferRules.CanTransfer(title));
            Assert.IsFalse(AssetTransferRules.Inherit(title, 9));
            Assert.AreEqual(5, title.ownerPersonId); // 変わらない
        }

        // --- NASSET-5 効果（収益/威信/総資産・二重計上回避） ---
        [Test]
        public void Effect_AggregateIncomePrestigeNetWorth()
        {
            NamedAssetRegistry.Register(new NamedAsset(0, "領地", NamedAssetCategory.領地) { ownerKind = AssetOwnerKind.人物, ownerPersonId = 1, value = 1000f, yieldRate = 0.1f, upkeepRate = 0.03f, prestige = 8f });
            NamedAssetRegistry.Register(new NamedAsset(0, "邸宅", NamedAssetCategory.邸宅) { ownerKind = AssetOwnerKind.人物, ownerPersonId = 1, value = 1000f, upkeepRate = 0.01f, prestige = 2f });
            NamedAssetRegistry.Register(new NamedAsset(0, "宮殿", NamedAssetCategory.宮殿) { ownerKind = AssetOwnerKind.国家, ownerFaction = Faction.帝国, value = 1000f, yieldRate = 0.1f, prestige = 30f });
            Assert.AreEqual(60f, NamedAssetEffectRules.PersonAnnualIncome(1), 1e-3f);   // 70 + (-10)
            Assert.AreEqual(100f, NamedAssetEffectRules.FactionAnnualIncome(Faction.帝国), 1e-3f);
            Assert.AreEqual(10f, NamedAssetEffectRules.PersonPrestige(1), 1e-3f);       // 8 + 2
            Assert.AreEqual(30f, NamedAssetEffectRules.FactionPrestige(Faction.帝国), 1e-3f);
            // 液状財産#2056（500）＋資産時価（1000+1000）＝総資産
            Assert.AreEqual(2500f, NamedAssetEffectRules.TotalNetWorthOfPerson(1, 500f), 1e-3f);
        }

        // --- NASSET-6 Tick（純収益→wealth・値上がり） ---
        [Test]
        public void Tick_AccruesIncomeAndAppreciates()
        {
            var p = new Person { id = 7, wealth = 0f };
            var a = NamedAssetRegistry.Register(new NamedAsset(0, "領地", NamedAssetCategory.領地)
            {
                ownerKind = AssetOwnerKind.人物, ownerPersonId = 7, value = 1000f, yieldRate = 0.1f, upkeepRate = 0.03f, appreciationRate = 0.05f
            });
            NamedAssetTickRules.TickYear(id => id == 7 ? p : null);
            Assert.AreEqual(70f, p.wealth, 1e-3f);    // 純収益が財産へ
            Assert.AreEqual(1050f, a.value, 1e-3f);   // 時価値上がり
            // 死亡した所有者には流れない（null 解決でも安全）
            Assert.DoesNotThrow(() => NamedAssetTickRules.TickYear(_ => null));
        }
    }
}
