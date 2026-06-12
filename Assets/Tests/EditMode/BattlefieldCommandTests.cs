using System.Collections.Generic;
using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>会戦の臨時指揮（#147 拡張）：後任選定（階級不足でも就ける・先任順）＋臨時台帳（戦闘終了で復帰）。</summary>
    public class BattlefieldCommandTests
    {
        [TearDown]
        public void Cleanup() => ActingCommandLedger.Clear();

        [Test]
        public void Successor_HighestRankThenSeniority_NoRankGate()
        {
            // 階級9が2名（先任 seniority 0 と 1）と階級8が1名 → 最上位9の先任(0)＝id2
            var pool = new List<CommandCandidate>
            {
                new CommandCandidate(1, 8, 0),
                new CommandCandidate(2, 9, 0),
                new CommandCandidate(3, 9, 1),
            };
            Assert.AreEqual(2, BattlefieldCommandRules.SelectActingSuccessor(pool).id);

            // 同階級は先任（seniority小）優先
            var pool2 = new List<CommandCandidate> { new CommandCandidate(1, 9, 2), new CommandCandidate(2, 9, 1) };
            Assert.AreEqual(2, BattlefieldCommandRules.SelectActingSuccessor(pool2).id);

            // 階級不足でも就ける（臨時指揮＝ゲートを問わない）
            var low = new List<CommandCandidate> { new CommandCandidate(5, 5, 0) };
            Assert.AreEqual(5, BattlefieldCommandRules.SelectActingSuccessor(low).id);

            // 候補なし
            Assert.AreEqual(-1, BattlefieldCommandRules.SelectActingSuccessor(new List<CommandCandidate>()).id);
            Assert.IsFalse(BattlefieldCommandRules.HasSuccessor(null));
        }

        [Test]
        public void Ledger_RecordActingAndRevert()
        {
            const string post = "帝国/第1軍団";
            // 開始時：正規指揮官100が就任（臨時ではない）
            ActingCommandLedger.Record(post, 100, 100);
            Assert.AreEqual(100, ActingCommandLedger.ActingFor(post));
            Assert.AreEqual(100, ActingCommandLedger.OriginalFor(post));
            Assert.IsFalse(ActingCommandLedger.IsActing(post));

            // 軍団長戦死→下位200が臨時継承（正規は100のまま）
            ActingCommandLedger.Record(post, 999, 200);
            Assert.AreEqual(200, ActingCommandLedger.ActingFor(post));
            Assert.AreEqual(100, ActingCommandLedger.OriginalFor(post)); // 正規は固定
            Assert.IsTrue(ActingCommandLedger.IsActing(post));
            Assert.AreEqual(1, ActingCommandLedger.Count);

            // 戦闘終了＝臨時指揮を解いて正規人事へ戻す
            ActingCommandLedger.Clear();
            Assert.AreEqual(0, ActingCommandLedger.Count);
            Assert.AreEqual(-1, ActingCommandLedger.ActingFor(post));
        }
    }
}
