using NUnit.Framework;
using UnityEngine;

namespace Ginei.Tests
{
    public class MeyasubakoLorePlayModeTests
    {
        private PetitionLedger savedPetitions;
        private PetitionLedger savedFleetPetitions;
        private GameObject host;

        [SetUp]
        public void SetUp()
        {
            savedPetitions = StrategySession.Petitions;
            savedFleetPetitions = StrategySession.FleetPetitions;
            StrategySession.Petitions = new PetitionLedger();
            StrategySession.FleetPetitions = new PetitionLedger();
        }

        [TearDown]
        public void TearDown()
        {
            if (host != null) Object.DestroyImmediate(host);
            StrategySession.Petitions = savedPetitions;
            StrategySession.FleetPetitions = savedFleetPetitions;
        }

        [Test]
        public void ExecutedAnonymousPetition_EntersDisclosureCodexAndRelaySummary()
        {
            var p = new Petition(0, "辺境を救った建白", Faction.同盟, BoxKind.政治家)
            { drafterId = 0, status = PetitionStatus.執行済 };
            p.hops.Add(101);
            p.hops.Add(102);
            StrategySession.Petitions.Add(p);

            host = new GameObject("GalaxyView_MeyasuLoreQa");
            host.SetActive(false);
            GalaxyView view = host.AddComponent<GalaxyView>();
            view.RunDisclosureTickForQa();

            Assert.IsTrue(view.IsDisclosureRevealed(MeyasubakoDisclosures.Seed));
            Assert.IsTrue(view.IsDisclosureRevealed(MeyasubakoDisclosures.Relay));
            Assert.IsTrue(view.IsDisclosureRevealed(MeyasubakoDisclosures.Legacy));
            StringAssert.Contains("誰が始めたか", view.DisclosureBody(MeyasubakoDisclosures.Legacy));
            StringAssert.Contains("匿名の投書", view.RingiRelaySummary());
            StringAssert.Contains("無名の中継者", view.RingiRelaySummary());
        }
    }
}
