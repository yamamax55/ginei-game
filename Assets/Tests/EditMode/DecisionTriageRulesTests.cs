using System.Collections.Generic;
using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 決裁トリアージ（DESK-2/3 #1630/#1631）を固定する：重大のみ時間停止、重要度別締切、
    /// 締切超で最小化→さらに猶予超でAIが既定選択を機械的に自動解決、重大は最小化も自動解決もしない。
    /// </summary>
    public class DecisionTriageRulesTests
    {
        private static readonly DecisionTriageParams P = DecisionTriageParams.Default; // normal20 / important45 / grace30

        private static PendingDecision Dec(DecisionSeverity sev, int defIdx = 0)
        {
            var d = new PendingDecision(1, "案件", sev, DecisionSource.システム, "eff", defaultChoiceIndex: defIdx);
            d.choices.Add("A"); d.choices.Add("B");
            return d;
        }

        [Test]
        public void PausesClock_OnlyCritical()
        {
            Assert.IsTrue(DecisionTriageRules.PausesClock(DecisionSeverity.重大));
            Assert.IsFalse(DecisionTriageRules.PausesClock(DecisionSeverity.重要));
            Assert.IsFalse(DecisionTriageRules.PausesClock(DecisionSeverity.通常));
            Assert.IsFalse(DecisionTriageRules.PausesClock(DecisionSeverity.情報));
        }

        [Test]
        public void DeadlineFor_BySeverity()
        {
            Assert.AreEqual(20f, DecisionTriageRules.DeadlineFor(DecisionSeverity.通常, P), 1e-4f);
            Assert.AreEqual(45f, DecisionTriageRules.DeadlineFor(DecisionSeverity.重要, P), 1e-4f);
            Assert.IsTrue(float.IsPositiveInfinity(DecisionTriageRules.DeadlineFor(DecisionSeverity.重大, P))); // 人を待つ
        }

        [Test]
        public void ClockShouldStop_WhenActiveCriticalPresent()
        {
            var q = new DecisionQueue();
            q.Enqueue(Dec(DecisionSeverity.通常));
            Assert.IsFalse(DecisionTriageRules.ClockShouldStop(q));

            var crit = Dec(DecisionSeverity.重大);
            q.Enqueue(crit);
            Assert.IsTrue(DecisionTriageRules.ClockShouldStop(q));

            q.Resolve(crit, 0); // 片付ければ時間が流れ出す
            Assert.IsFalse(DecisionTriageRules.ClockShouldStop(q));
        }

        [Test]
        public void Tick_MinimizesAfterDeadline_ThenAutoResolvesAfterGrace()
        {
            var q = new DecisionQueue();
            var d = Dec(DecisionSeverity.通常, defIdx: 1);
            q.Enqueue(d);

            // 締切(20)未満＝そのまま
            var r1 = DecisionTriageRules.Tick(q, 10f, P);
            Assert.AreEqual(0, r1.Count);
            Assert.AreEqual(DecisionStatus.新着, d.status);

            // 締切(20)超＝最小化（elapsed 25・< 20+30）
            var r2 = DecisionTriageRules.Tick(q, 15f, P);
            Assert.AreEqual(0, r2.Count);
            Assert.AreEqual(DecisionStatus.最小化, d.status);

            // 猶予(20+30=50)超＝AIが既定選択を機械的に採択（elapsed 55）
            var r3 = DecisionTriageRules.Tick(q, 30f, P);
            Assert.AreEqual(1, r3.Count);
            Assert.AreSame(d, r3[0]);
            Assert.AreEqual(DecisionStatus.自動解決, d.status);
            Assert.AreEqual(1, d.chosenIndex); // 既定選択(defaultChoiceIndex=1)
        }

        [Test]
        public void Tick_Critical_NeverMinimizedNorAutoResolved()
        {
            var q = new DecisionQueue();
            var crit = Dec(DecisionSeverity.重大);
            q.Enqueue(crit);
            var resolved = DecisionTriageRules.Tick(q, 10000f, P); // どれだけ放置しても
            Assert.AreEqual(0, resolved.Count);
            Assert.AreEqual(DecisionStatus.新着, crit.status); // 重大は人を待つ
            Assert.AreEqual(-1, crit.chosenIndex);
        }

        [Test]
        public void AutoResolvable_Predicate()
        {
            var d = Dec(DecisionSeverity.通常);
            d.elapsed = 49f;
            Assert.IsFalse(DecisionTriageRules.AutoResolvable(d, P)); // 50未満
            d.elapsed = 50f;
            Assert.IsTrue(DecisionTriageRules.AutoResolvable(d, P));  // 締切20+猶予30
            // 重大は対象外
            var crit = Dec(DecisionSeverity.重大); crit.elapsed = 100000f;
            Assert.IsFalse(DecisionTriageRules.AutoResolvable(crit, P));
        }

        [Test]
        public void Tick_NullAndZeroDt_AreSafe()
        {
            Assert.AreEqual(0, DecisionTriageRules.Tick(null, 1f, P).Count);
            var q = new DecisionQueue();
            q.Enqueue(Dec(DecisionSeverity.通常));
            Assert.AreEqual(0, DecisionTriageRules.Tick(q, 0f, P).Count);
        }
    }
}
