using System.Collections.Generic;
using NUnit.Framework;

namespace Ginei.Tests
{
    public class MeyasubakoDisclosuresTests
    {
        private readonly List<Petition> petitions = new List<Petition>();

        private DisclosureLedger Ledger()
        {
            var ledger = new DisclosureLedger();
            ledger.Register(MeyasubakoDisclosures.SeedSown(() => petitions));
            ledger.Register(MeyasubakoDisclosures.UnsungRelay(() => petitions));
            ledger.Register(MeyasubakoDisclosures.AnonymousLegacy(() => petitions));
            return ledger;
        }

        [SetUp]
        public void SetUp() => petitions.Clear();

        [Test]
        public void NoHop_RevealsNothing()
        {
            petitions.Add(new Petition(1, "未伝播", Faction.同盟, BoxKind.政治家));
            DisclosureLedger ledger = Ledger();
            Assert.AreEqual(0, ledger.Evaluate(null).Count);
        }

        [Test]
        public void OneHop_RevealsOnlySeed()
        {
            var p = new Petition(1, "建白", Faction.同盟, BoxKind.政治家);
            p.hops.Add(11);
            petitions.Add(p);
            DisclosureLedger ledger = Ledger();
            ledger.Evaluate(null);
            Assert.IsTrue(ledger.IsRevealed(MeyasubakoDisclosures.Seed));
            Assert.IsFalse(ledger.IsRevealed(MeyasubakoDisclosures.Relay));
        }

        [Test]
        public void AnonymousExecutedRelay_RevealsWholeChain()
        {
            var p = new Petition(1, "実った政策", Faction.同盟, BoxKind.政治家)
            { drafterId = 0, status = PetitionStatus.執行済 };
            p.hops.Add(11);
            p.hops.Add(12);
            petitions.Add(p);

            DisclosureLedger ledger = Ledger();
            List<DisclosureEntry> revealed = ledger.Evaluate(null);
            Assert.AreEqual(3, revealed.Count);
            Assert.IsTrue(ledger.IsRevealed(MeyasubakoDisclosures.Legacy));
            StringAssert.Contains("誰が始めたか", ledger.Get(MeyasubakoDisclosures.Legacy).body);
        }
    }
}
