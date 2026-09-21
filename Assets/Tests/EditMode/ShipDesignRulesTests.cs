using Ginei;
using NUnit.Framework;

namespace Ginei.Tests
{
    public class ShipDesignRulesTests
    {
        static HullSpec Hull() => new HullSpec(80f, 10f, 4, "標準艦体");
        static ShipModule Engine() => new ShipModule(ModuleType.機関, 10f, -30f, 20f, 20f, "標準機関");
        static ShipModule Gun() => new ShipModule(ModuleType.主砲, 15f, 12f, 30f, 25f, "標準主砲");

        [Test]
        public void 登録すると同じ艦種の旧設計を退役させ新設計を現役化する()
        {
            var state = new ShipDesignState();
            Assert.IsTrue(ShipDesignRules.TryRegister(state, "A型", ShipClass.戦艦, Hull(), new[] { Engine(), Gun() }, out ShipDesign a, out _));
            Assert.IsTrue(ShipDesignRules.TryRegister(state, "B型", ShipClass.戦艦, Hull(), new[] { Engine(), Gun() }, out ShipDesign b, out _));

            Assert.IsFalse(a.active);
            Assert.IsTrue(b.active);
            Assert.AreSame(b, ShipDesignRules.ActiveFor(state, ShipClass.戦艦));
            Assert.AreEqual(3, state.nextDesignId);
        }

        [Test]
        public void 不正設計と同名設計は登録しない()
        {
            var state = new ShipDesignState();
            Assert.IsFalse(ShipDesignRules.TryRegister(state, "過電型", ShipClass.巡航艦,
                new HullSpec(100f, 0f, 2), new[] { Gun() }, out _, out string powerReason));
            StringAssert.Contains("電力", powerReason);

            Assert.IsTrue(ShipDesignRules.TryRegister(state, "標準型", ShipClass.巡航艦, Hull(), new[] { Engine(), Gun() }, out _, out _));
            Assert.IsFalse(ShipDesignRules.TryRegister(state, "標準型", ShipClass.駆逐艦, Hull(), new[] { Engine(), Gun() }, out _, out string duplicateReason));
            StringAssert.Contains("同名", duplicateReason);
            Assert.AreEqual(1, state.designs.Count);
        }

        [Test]
        public void 改名と性能倍率を返す()
        {
            var state = new ShipDesignState();
            ShipDesignRules.TryRegister(state, "旧名", ShipClass.戦艦, Hull(), new[] { Engine(), Gun() }, out ShipDesign design, out _);
            Assert.IsTrue(ShipDesignRules.TryRename(state, design.id, "新名", out _));
            Assert.AreEqual("新名", design.designName);

            ShipDesignPerformance p = ShipDesignRules.PerformanceOf(design);
            Assert.Greater(p.firepower, 1f);
            Assert.Greater(p.mobility, 1f);
            Assert.AreEqual(1f, p.durability, 1e-4f);
        }

        [Test]
        public void 保存復元し旧セーブは空台帳になる()
        {
            var campaign = new CampaignState(new GalaxyMap());
            var faction = new FactionState(Faction.同盟);
            campaign.states.Add(faction);
            ShipDesignRules.TryRegister(faction.shipDesigns, "同盟巡航艦I", ShipClass.巡航艦,
                Hull(), new[] { Engine(), Gun() }, out ShipDesign original, out _);

            CampaignState restored = CampaignSerializer.FromJson(CampaignSerializer.ToJson(campaign));
            ShipDesign loaded = ShipDesignRules.ActiveFor(restored.states[0].shipDesigns, ShipClass.巡航艦);
            Assert.NotNull(loaded);
            Assert.AreEqual(original.id, loaded.id);
            Assert.AreEqual("同盟巡航艦I", loaded.designName);
            Assert.AreEqual(2, restored.states[0].shipDesigns.nextDesignId);

            var legacy = CampaignSerializer.ToSaveData(campaign);
            legacy.states[0].hasShipDesigns = false;
            legacy.states[0].shipDesigns = null;
            CampaignState oldRestored = CampaignSerializer.FromSaveData(legacy);
            Assert.NotNull(oldRestored.states[0].shipDesigns);
            Assert.AreEqual(0, oldRestored.states[0].shipDesigns.designs.Count);
        }
    }
}
