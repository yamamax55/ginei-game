using System.Collections.Generic;
using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 開示エンジン（FND-4 #495・秘史開示）を固定する：前提＋条件のゲート、開示時効果、連鎖開示（不動点）、
    /// 進捗、サンプル（断片→真相→エンディング解放）の通し。`GameEvent` と共有の `EventContext` を使う。
    /// </summary>
    public class DisclosureLedgerTests
    {
        // ===== DisclosureRules =====

        [Test]
        public void CanReveal_RequiresPrereqAndCondition()
        {
            var state = new DisclosureState();
            var entry = new DisclosureEntry("b", "t", "x").Requires("a").When(ctx => true);

            Assert.IsFalse(DisclosureRules.CanReveal(entry, state, null)); // 前提 a 未開示
            state.Reveal("a");
            Assert.IsTrue(DisclosureRules.CanReveal(entry, state, null));  // 前提充足＋条件真
        }

        [Test]
        public void ConditionGatesReveal()
        {
            var state = new DisclosureState();
            var entry = new DisclosureEntry("c", "t", "x").When(ctx => ctx != null && ctx.faction == Faction.同盟);
            Assert.IsFalse(DisclosureRules.CanReveal(entry, state, new EventContext(Faction.帝国)));
            Assert.IsTrue(DisclosureRules.CanReveal(entry, state, new EventContext(Faction.同盟)));
        }

        [Test]
        public void TryReveal_MarksAndFiresEffectOnce()
        {
            int fired = 0;
            var state = new DisclosureState();
            var entry = new DisclosureEntry("e", "t", "x").OnReveal(ctx => fired++);

            Assert.IsTrue(DisclosureRules.TryReveal(entry, state, null));
            Assert.IsTrue(state.IsRevealed("e"));
            Assert.AreEqual(1, fired);
            Assert.IsFalse(DisclosureRules.TryReveal(entry, state, null)); // 二度は開示しない
            Assert.AreEqual(1, fired);
        }

        // ===== DisclosureLedger 連鎖 =====

        [Test]
        public void Evaluate_RevealsChainToFixedPoint()
        {
            var ledger = new DisclosureLedger();
            ledger.Register(new DisclosureEntry("c", "C", "x").Requires("b")); // 逆順登録でも
            ledger.Register(new DisclosureEntry("b", "B", "x").Requires("a"));
            ledger.Register(new DisclosureEntry("a", "A", "x"));               // 無条件＝起点

            var newly = ledger.Evaluate(null);
            Assert.AreEqual(3, newly.Count);          // a→b→c が1回の評価で連鎖開示
            Assert.IsTrue(ledger.IsRevealed("a"));
            Assert.IsTrue(ledger.IsRevealed("b"));
            Assert.IsTrue(ledger.IsRevealed("c"));
            Assert.AreEqual(1f, ledger.Progress(), 1e-4f);
        }

        [Test]
        public void Evaluate_StopsAtUnmetCondition_ResumesLater()
        {
            bool gateOpen = false;
            var ledger = new DisclosureLedger();
            ledger.Register(new DisclosureEntry("a", "A", "x"));
            ledger.Register(new DisclosureEntry("b", "B", "x").Requires("a").When(ctx => gateOpen));

            var first = ledger.Evaluate(null);
            Assert.AreEqual(1, first.Count);  // a のみ（b は条件未成立で止まる）
            Assert.IsFalse(ledger.IsRevealed("b"));

            gateOpen = true;
            var second = ledger.Evaluate(null);
            Assert.AreEqual(1, second.Count); // 後から b が開く
            Assert.IsTrue(ledger.IsRevealed("b"));
        }

        [Test]
        public void Progress_TracksRevealedFraction()
        {
            var ledger = new DisclosureLedger();
            ledger.Register(new DisclosureEntry("a", "A", "x"));
            ledger.Register(new DisclosureEntry("b", "B", "x").When(ctx => false)); // 開かない
            ledger.Evaluate(null);
            Assert.AreEqual(0.5f, ledger.Progress(), 1e-4f);
        }

        // ===== サンプル End-to-End（FND-4 完了像） =====

        [Test]
        public void Sample_FragmentToEnding_UnlocksViaChainAndEffect()
        {
            var ledger = new DisclosureLedger();
            ledger.Register(SampleDisclosures.SecretFragment());
            ledger.Register(SampleDisclosures.AncientTruth());
            ledger.Register(SampleDisclosures.EndingUnlock());

            var chronicle = new SampleDisclosures.Chronicle(); // fragmentFound=false
            var ctx = new EventContext(Faction.帝国, payload: chronicle);

            // 断片の条件が未成立 → 何も開かない
            Assert.AreEqual(0, ledger.Evaluate(ctx).Count);

            // 断片を発見 → 断片→真相→エンディングまで1回の評価で連鎖開示、効果でエンディング解放
            chronicle.fragmentFound = true;
            var newly = ledger.Evaluate(ctx);
            Assert.AreEqual(3, newly.Count);
            Assert.IsTrue(ledger.IsRevealed(SampleDisclosures.Ending));
            Assert.IsTrue(chronicle.endingUnlocked); // 開示時効果でエンディング解放
            Assert.AreEqual(1f, ledger.Progress(), 1e-4f);
        }
    }
}
