using System.Collections.Generic;
using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// DisclosureRules の穴埋めテスト（DisclosureLedgerTests と非重複）：
    /// null 安全・複数前提の部分充足・condition 既定・Progress の境界/null 混在・
    /// 開示不可時に効果が発火しないことを固定する。
    /// </summary>
    public class DisclosureRulesExtraTests
    {
        /// <summary>null 引数は全 API で安全に false/既定を返す（例外を投げない）。</summary>
        [Test]
        public void NullArguments_AreSafe()
        {
            var state = new DisclosureState();
            var entry = new DisclosureEntry("x", "t", "b");

            // entry/state が null なら前提・開示判定は false
            Assert.IsFalse(DisclosureRules.PrerequisitesMet(null, state));
            Assert.IsFalse(DisclosureRules.PrerequisitesMet(entry, null));
            Assert.IsFalse(DisclosureRules.CanReveal(null, state, null));
            Assert.IsFalse(DisclosureRules.CanReveal(entry, null, null));
            // ConditionMet は entry null で false
            Assert.IsFalse(DisclosureRules.ConditionMet(null, null));
            // TryReveal は開示不能なら false（副作用なし）
            Assert.IsFalse(DisclosureRules.TryReveal(null, state, null));
            Assert.AreEqual(0, state.Count);
        }

        /// <summary>複数前提のうち1つでも未開示なら開示不可。全て揃って初めて可。</summary>
        [Test]
        public void PrerequisitesMet_RequiresAllPrereqs()
        {
            var state = new DisclosureState();
            var entry = new DisclosureEntry("z", "t", "b").Requires("a", "b");

            state.Reveal("a"); // 片方だけ
            Assert.IsFalse(DisclosureRules.PrerequisitesMet(entry, state));
            Assert.IsFalse(DisclosureRules.CanReveal(entry, state, null));

            state.Reveal("b"); // 両方揃う
            Assert.IsTrue(DisclosureRules.PrerequisitesMet(entry, state));
            Assert.IsTrue(DisclosureRules.CanReveal(entry, state, null));
        }

        /// <summary>condition 未設定（null）は常に真として扱われる＝前提だけで通る。</summary>
        [Test]
        public void ConditionMet_NullConditionIsAlwaysTrue()
        {
            var entry = new DisclosureEntry("c", "t", "b"); // When 未設定
            Assert.IsTrue(DisclosureRules.ConditionMet(entry, null));
            Assert.IsTrue(DisclosureRules.ConditionMet(entry, new EventContext(Faction.帝国)));
        }

        /// <summary>前提不足で CanReveal が偽なら TryReveal は記録も効果発火もしない（冪等な失敗）。</summary>
        [Test]
        public void TryReveal_DoesNothingWhenPrereqMissing()
        {
            int fired = 0;
            var state = new DisclosureState();
            var entry = new DisclosureEntry("e", "t", "b").Requires("a").OnReveal(ctx => fired++);

            Assert.IsFalse(DisclosureRules.TryReveal(entry, state, null)); // 前提 a 未開示
            Assert.IsFalse(state.IsRevealed("e"));
            Assert.AreEqual(0, fired); // 効果は発火しない
        }

        /// <summary>Progress の境界：空/null リスト・null state は 0、リスト中の null 要素は分母に数え分子から除外。</summary>
        [Test]
        public void Progress_EdgeCases()
        {
            var state = new DisclosureState();
            // 空・null 入力は 0
            Assert.AreEqual(0f, DisclosureRules.Progress(new List<DisclosureEntry>(), state), 1e-4f);
            Assert.AreEqual(0f, DisclosureRules.Progress(null, state), 1e-4f);

            var a = new DisclosureEntry("a", "A", "b");
            var list = new List<DisclosureEntry> { a, null }; // null 要素は分母に含むが分子に数えない
            Assert.AreEqual(0f, DisclosureRules.Progress(list, state), 1e-4f);     // state null でない・未開示
            Assert.AreEqual(0f, DisclosureRules.Progress(list, null), 1e-4f);      // state null は 0

            state.Reveal("a");
            // 開示済み a は分子1、分母は2（null 含む）＝0.5
            Assert.AreEqual(0.5f, DisclosureRules.Progress(list, state), 1e-4f);
        }
    }
}
