using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 防諜を固定する：摘発tick、摘発率＝努力×浸透度（深い網ほど尻尾を出す）、転向は低忠誠のみ、
    /// 偽情報バイアスは人数比例で上限あり。境界・決定論を担保。
    /// </summary>
    public class CounterIntelligenceRulesTests
    {
        private static readonly CounterIntelParams P = CounterIntelParams.Default;
        // 摘発0.1/転向基礎0.3/偽情報0.15/上限0.6

        [Test]
        public void SweepTick_CutsPenetration()
        {
            Assert.AreEqual(0.4f, CounterIntelligenceRules.SweepTick(0.5f, 1f, 1f, P), 1e-5f);
            Assert.AreEqual(0.5f, CounterIntelligenceRules.SweepTick(0.5f, 0f, 1f, P), 1e-5f); // 努力ゼロ＝放置
            Assert.AreEqual(0f, CounterIntelligenceRules.SweepTick(0.05f, 1f, 1f, P), 1e-5f);  // 下限0
        }

        [Test]
        public void CatchChance_DeepNetsShowTails()
        {
            Assert.AreEqual(1f, CounterIntelligenceRules.CatchChance(1f, 1f), 1e-5f);
            Assert.AreEqual(0.25f, CounterIntelligenceRules.CatchChance(0.5f, 0.5f), 1e-5f);
            Assert.AreEqual(0f, CounterIntelligenceRules.CatchChance(0f, 1f), 1e-5f); // 浸透なし＝捕まえる物がない
        }

        [Test]
        public void CatchesSpy_DeterministicByRoll()
        {
            Assert.IsTrue(CounterIntelligenceRules.CatchesSpy(0.5f, 0.5f, 0.24f));
            Assert.IsFalse(CounterIntelligenceRules.CatchesSpy(0.5f, 0.5f, 0.26f));
        }

        [Test]
        public void TurnChance_LoyalSpiesDontTurn()
        {
            Assert.AreEqual(0.3f, CounterIntelligenceRules.TurnChance(0f, P), 1e-5f);  // 忠誠なし＝基礎満額
            Assert.AreEqual(0f, CounterIntelligenceRules.TurnChance(1f, P), 1e-5f);    // 忠誠で死ぬスパイは転ばない
            Assert.AreEqual(0.15f, CounterIntelligenceRules.TurnChance(0.5f, P), 1e-5f);
        }

        [Test]
        public void DisinformationBias_CappedByAgents()
        {
            Assert.AreEqual(0.15f, CounterIntelligenceRules.DisinformationBias(1, P), 1e-5f);
            Assert.AreEqual(0.45f, CounterIntelligenceRules.DisinformationBias(3, P), 1e-5f);
            Assert.AreEqual(0.6f, CounterIntelligenceRules.DisinformationBias(10, P), 1e-5f); // 上限0.6
            Assert.AreEqual(0f, CounterIntelligenceRules.DisinformationBias(0, P), 1e-5f);
        }
    }
}
