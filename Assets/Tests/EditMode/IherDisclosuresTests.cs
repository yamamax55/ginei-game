using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 世界観の開示連鎖（IHER-5 #2401『星を継ぐもの』着想）を固定する：証拠が確証になると起点が開き、
    /// 前提を満たすたびに 先行文明→失われた惑星→移民→孤独 が連鎖開示される。
    /// 確証前は何も開かないこと、前提が揃う前に後段が漏れて開かないことも担保する。
    /// 既存の <see cref="DisclosureLedger"/> テストに倣い、<see cref="DisclosureLedger"/> へ登録して
    /// <see cref="EventContext"/> 付きで <c>Evaluate</c> する。
    /// </summary>
    public class IherDisclosuresTests
    {
        // 実効信頼性が閾値を超える確証級の証拠群（2分野で収束＝倍率付き）。
        private static EvidenceBody ConclusiveBody()
        {
            var body = new EvidenceBody();
            body.fragments.Add(new EvidenceFragment("先行文明の遺構", ResearchField.社会, 0.8f, 1f));
            body.fragments.Add(new EvidenceFragment("年代測定の異常", ResearchField.情報, 0.6f, 1f));
            return body;
        }

        // 確証に届かない弱い証拠（1分野・低信頼）。
        private static EvidenceBody WeakBody()
        {
            var body = new EvidenceBody();
            body.fragments.Add(new EvidenceFragment("噂", ResearchField.社会, 0.1f, 1f));
            return body;
        }

        private static DisclosureLedger BuildLedger()
        {
            var ledger = new DisclosureLedger();
            foreach (var e in IherDisclosures.All())
                ledger.Register(e);
            return ledger;
        }

        [Test]
        public void NoReveal_BeforeEvidenceIsConclusive()
        {
            var ledger = BuildLedger();
            var ctx = new EventContext(Faction.同盟, payload: WeakBody());

            Assert.AreEqual(0, ledger.Evaluate(ctx).Count); // 確証未達＝起点すら開かない
            Assert.IsFalse(ledger.IsRevealed(IherDisclosures.Precursor));
            Assert.IsFalse(ledger.IsRevealed(IherDisclosures.LostPlanet));
            Assert.IsFalse(ledger.IsRevealed(IherDisclosures.Migration));
            Assert.IsFalse(ledger.IsRevealed(IherDisclosures.Solitude));
        }

        [Test]
        public void Chain_RevealsInOrder_WhenEvidenceIsConclusive()
        {
            var ledger = BuildLedger();
            var ctx = new EventContext(Faction.同盟, payload: ConclusiveBody());

            var newly = ledger.Evaluate(ctx);

            // 起点が開いたら前提が次々満ち、4段すべてが1回の評価で連鎖開示される。
            Assert.AreEqual(4, newly.Count);
            Assert.IsTrue(ledger.IsRevealed(IherDisclosures.Precursor));
            Assert.IsTrue(ledger.IsRevealed(IherDisclosures.LostPlanet));
            Assert.IsTrue(ledger.IsRevealed(IherDisclosures.Migration));
            Assert.IsTrue(ledger.IsRevealed(IherDisclosures.Solitude));
            Assert.AreEqual(1f, ledger.Progress(), 1e-4f);

            // 連鎖の順序（前提が先に開いている）を id の出現順で固定。
            Assert.AreEqual(IherDisclosures.Precursor, newly[0].id);
            Assert.AreEqual(IherDisclosures.LostPlanet, newly[1].id);
            Assert.AreEqual(IherDisclosures.Migration, newly[2].id);
            Assert.AreEqual(IherDisclosures.Solitude, newly[3].id);
        }

        [Test]
        public void Downstream_DoesNotReveal_WithoutPrerequisite()
        {
            // 起点(Precursor)だけ先に開示済みにしておき、確証ありで評価。
            var ledger = BuildLedger();
            ledger.State.Reveal(IherDisclosures.Precursor);
            ledger.State.Reveal(IherDisclosures.LostPlanet);
            // Migration の前提は Precursor＋LostPlanet なので開く。Solitude は Migration が前提。
            var ctx = new EventContext(Faction.同盟, payload: ConclusiveBody());

            // この時点で Migration はまだ未開示＝Solitude は前提未充足で開けない。
            var migrationEntry = ledger.Get(IherDisclosures.Solitude);
            Assert.IsFalse(DisclosureRules.PrerequisitesMet(migrationEntry, ledger.State));

            var newly = ledger.Evaluate(ctx);
            // Migration→Solitude が連鎖で開く（Precursor/LostPlanet は既開示で再開示されない）。
            Assert.AreEqual(2, newly.Count);
            Assert.AreEqual(IherDisclosures.Migration, newly[0].id);
            Assert.AreEqual(IherDisclosures.Solitude, newly[1].id);
        }
    }
}
