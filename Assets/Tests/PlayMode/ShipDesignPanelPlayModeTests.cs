using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Ginei.Tests
{
    public class ShipDesignPanelPlayModeTests
    {
        private GameObject host;

        [SetUp]
        public void SetUp()
        {
            StrategySession.Campaign = new CampaignState(new GalaxyMap());
            StrategySession.Campaign.states.Add(new FactionState(Faction.同盟));
        }

        [TearDown]
        public void TearDown()
        {
            if (host != null) Object.DestroyImmediate(host);
            StrategySession.Clear();
        }

        [UnityTest]
        public IEnumerator Panel_LoadsTechnologySlot_RegistersAndRenamesDesign()
        {
            host = new GameObject("ShipDesignPanelTest");
            var panel = host.AddComponent<ShipDesignPanel>();
            ShipDesignPanel.Show();
            yield return null;

            ShipModule gun = ShipDesignCatalog.Modules()[0];
            panel.SetDraftModuleForTest(0, gun);
            StringAssert.Contains("#63DFF2", panel.PerformanceLabelForTest.text,
                "現役設計より向上する性能は水色で示す");
            Assert.IsTrue(panel.RegisterForTest("試験戦艦A"));
            Assert.AreEqual(1, panel.StateForTest.designs.Count);
            Assert.IsTrue(panel.StateForTest.designs[0].active);
            Assert.AreEqual(gun.moduleName, panel.StateForTest.designs[0].modules[0].moduleName);

            Assert.IsTrue(panel.RenameForTest("試験戦艦B"));
            Assert.AreEqual("試験戦艦B", panel.StateForTest.designs[0].designName);
        }

        [UnityTest]
        public IEnumerator Panel_RejectsOverPoweredDraftWithVisibleReason()
        {
            host = new GameObject("ShipDesignPanelInvalidTest");
            var panel = host.AddComponent<ShipDesignPanel>();
            ShipDesignPanel.Show();
            yield return null;

            ShipModule gun = ShipDesignCatalog.Modules()[0];
            for (int i = 0; i < panel.DraftModulesForTest.Length; i++) panel.SetDraftModuleForTest(i, gun);

            Assert.IsFalse(panel.RegisterForTest("過電力設計"));
            StringAssert.Contains("電力", panel.MessageLabelForTest.text);
            Assert.AreEqual(0, panel.StateForTest.designs.Count);
        }
    }
}
