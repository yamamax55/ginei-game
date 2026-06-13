using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 経済制裁を固定する：実効ペナルティ＝強度×(1−抜け穴)×最大幅、自己コストは交易依存比例、
    /// 抜け穴は制裁が強いほど速く広がり（解除で停止）、形骸化判定。クランプを担保。
    /// </summary>
    public class SanctionsRulesTests
    {
        private static readonly SanctionsParams P = SanctionsParams.Default;
        // 最大0.4/自己コスト0.5/抜け穴成長0.02/有効閾値0.1

        [Test]
        public void OutputPenalty_SeverityMinusLeakage()
        {
            Assert.AreEqual(0.4f, SanctionsRules.OutputPenalty(1f, 0f, P), 1e-5f);   // 全力・抜け穴なし＝最大
            Assert.AreEqual(0.2f, SanctionsRules.OutputPenalty(1f, 0.5f, P), 1e-5f); // 抜け穴半開＝半減
            Assert.AreEqual(0f, SanctionsRules.OutputPenalty(1f, 1f, P), 1e-5f);     // 抜け穴全開＝無効
            Assert.AreEqual(0f, SanctionsRules.OutputPenalty(0f, 0f, P), 1e-5f);     // 制裁なし
        }

        [Test]
        public void TargetOutputFactor_Complement()
        {
            Assert.AreEqual(0.6f, SanctionsRules.TargetOutputFactor(1f, 0f, P), 1e-5f);
            Assert.AreEqual(1f, SanctionsRules.TargetOutputFactor(0f, 0f, P), 1e-5f);
        }

        [Test]
        public void SelfCost_ScalesWithTradeDependence()
        {
            // 太い取引相手への全力制裁＝1×1×0.5=0.5 の返り血
            Assert.AreEqual(0.5f, SanctionsRules.SelfCost(1f, 1f, P), 1e-5f);
            // 無縁の相手なら無料
            Assert.AreEqual(0f, SanctionsRules.SelfCost(1f, 0f, P), 1e-5f);
            Assert.AreEqual(0.125f, SanctionsRules.SelfCost(0.5f, 0.5f, P), 1e-5f);
        }

        [Test]
        public void LeakageTick_GrowsWithSeverity_StopsWhenLifted()
        {
            // 全力制裁 dt=10：0.02×1×10=0.2 広がる
            Assert.AreEqual(0.2f, SanctionsRules.LeakageTick(0f, 1f, 10f, P), 1e-5f);
            // 制裁解除＝広がらない
            Assert.AreEqual(0.3f, SanctionsRules.LeakageTick(0.3f, 0f, 10f, P), 1e-5f);
            // 上限1
            Assert.AreEqual(1f, SanctionsRules.LeakageTick(0.95f, 1f, 100f, P), 1e-5f);
        }

        [Test]
        public void IsEffective_ThresholdOnRealPenalty()
        {
            Assert.IsTrue(SanctionsRules.IsEffective(1f, 0f, P));      // 0.4 ≥ 0.1
            Assert.IsTrue(SanctionsRules.IsEffective(1f, 0.75f, P));   // 0.1 ちょうど＝有効
            Assert.IsFalse(SanctionsRules.IsEffective(1f, 0.8f, P));   // 0.08＝形骸化
            Assert.IsFalse(SanctionsRules.IsEffective(0.1f, 0f, P));   // 弱い制裁＝0.04
        }

        [Test]
        public void LongRunStory_SanctionsDecayIntoTheater()
        {
            // 全力制裁を続けると抜け穴が広がり、いずれ形骸化する（決定論のシミュレート）
            float leakage = 0f;
            int ticks = 0;
            while (SanctionsRules.IsEffective(1f, leakage, P) && ticks < 1000)
            {
                leakage = SanctionsRules.LeakageTick(leakage, 1f, 1f, P);
                ticks++;
            }
            Assert.Less(ticks, 1000);                  // 有限時間で形骸化
            Assert.IsFalse(SanctionsRules.IsEffective(1f, leakage, P));
        }
    }
}
