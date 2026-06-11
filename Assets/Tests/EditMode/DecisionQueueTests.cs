using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 非ブロッキング決裁キュー（DESK-1 #1629）を固定する：積む/活性数/最小化数、最小化↔復帰、
    /// 決裁で効果キーを返し活性から外れる、Front は重要度→経過で最前面を選ぶ、解決済を掃ける。
    /// </summary>
    public class DecisionQueueTests
    {
        private static PendingDecision Dec(int id, DecisionSeverity sev, string effect = "")
            => new PendingDecision(id, "決裁" + id, sev, DecisionSource.システム, effect);

        [Test]
        public void Enqueue_CountsAndActive()
        {
            var q = new DecisionQueue();
            q.Enqueue(Dec(1, DecisionSeverity.通常));
            q.Enqueue(Dec(2, DecisionSeverity.重要));
            q.Enqueue(null); // 無視
            Assert.AreEqual(2, q.Count);
            Assert.AreEqual(2, q.ActiveCount());
        }

        [Test]
        public void MinimizeRestore_TogglesStatus_AndCount()
        {
            var q = new DecisionQueue();
            var d = Dec(1, DecisionSeverity.通常);
            q.Enqueue(d);
            q.Minimize(d);
            Assert.AreEqual(DecisionStatus.最小化, d.status);
            Assert.AreEqual(1, q.MinimizedCount());
            q.Restore(d);
            Assert.AreEqual(DecisionStatus.提示中, d.status);
            Assert.AreEqual(0, q.MinimizedCount());
        }

        [Test]
        public void Restore_ResetsDeadline_SoExpandSticks()
        {
            // 締切を過ぎて最小化された決裁を展開すると、経過がリセットされ即・再最小化されない
            var q = new DecisionQueue();
            var d = Dec(1, DecisionSeverity.通常);
            d.elapsed = 999f; // 締切を大きく超過
            q.Enqueue(d);
            q.Minimize(d);
            Assert.AreEqual(DecisionStatus.最小化, d.status);

            q.Restore(d);
            Assert.AreEqual(DecisionStatus.提示中, d.status);
            Assert.AreEqual(0f, d.elapsed, 1e-4f); // 締切リセット＝展開がちゃんと効く
        }

        [Test]
        public void Resolve_ReturnsEffectKey_AndLeavesActive()
        {
            var q = new DecisionQueue();
            var d = Dec(1, DecisionSeverity.重要, "tax.cut");
            q.Enqueue(d);
            string key = q.Resolve(d, choiceIndex: 1);
            Assert.AreEqual("tax.cut", key);
            Assert.AreEqual(1, d.chosenIndex);
            Assert.AreEqual(DecisionStatus.決裁済, d.status);
            Assert.AreEqual(0, q.ActiveCount()); // 解決済は活性から外れる
        }

        [Test]
        public void Front_PicksHighestSeverity_ThenOldest()
        {
            var q = new DecisionQueue();
            var normal = Dec(1, DecisionSeverity.通常);
            var crit = Dec(2, DecisionSeverity.重大);
            var importantOld = Dec(3, DecisionSeverity.重要); importantOld.elapsed = 50f;
            var importantNew = Dec(4, DecisionSeverity.重要); importantNew.elapsed = 5f;
            q.Enqueue(normal); q.Enqueue(importantOld); q.Enqueue(importantNew); q.Enqueue(crit);

            Assert.AreSame(crit, q.Front()); // 重大が最優先

            q.Resolve(crit, 0); // 重大を片付ける
            Assert.AreSame(importantOld, q.Front()); // 同列(重要)は経過が長い方
        }

        [Test]
        public void Front_NullWhenAllResolved()
        {
            var q = new DecisionQueue();
            var d = Dec(1, DecisionSeverity.通常);
            q.Enqueue(d);
            q.Resolve(d, 0);
            Assert.IsNull(q.Front());
        }

        [Test]
        public void PruneResolved_RemovesDone()
        {
            var q = new DecisionQueue();
            var a = Dec(1, DecisionSeverity.通常);
            var b = Dec(2, DecisionSeverity.重要);
            q.Enqueue(a); q.Enqueue(b);
            q.Resolve(a, 0);
            q.PruneResolved();
            Assert.AreEqual(1, q.Count);
            Assert.AreSame(b, q.items[0]);
        }
    }
}
