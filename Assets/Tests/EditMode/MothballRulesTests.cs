using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// モスボールを固定する：保管中の維持費は大幅減・再就役は保管期間に比例して遅く高くなる・
    /// 保管状態は保守を怠ると朽ち保守すれば緩やか・朽ちた艦は再就役直後の実効戦力が落ちる。
    /// 「平時の節約は有事の遅れで払う」とクランプを担保。
    /// </summary>
    public class MothballRulesTests
    {
        private static readonly MothballParams P = MothballParams.Default; // 維持費0.2/基礎時間10/時間増0.2/整備基礎0.1/整備増0.01/劣化0.02/保守時0.25/下限0.3

        [Test]
        public void MothballedUpkeepFactor_FarBelowActive()
        {
            // 保管中の維持費は現役の2割＝眠らせる動機
            Assert.AreEqual(0.2f, MothballRules.MothballedUpkeepFactor(P), 1e-4f);
            Assert.Less(MothballRules.MothballedUpkeepFactor(P), 1f);
        }

        [Test]
        public void ReactivationTime_GrowsWithStoredDuration()
        {
            Assert.AreEqual(10f, MothballRules.ReactivationTime(0f, P), 1e-4f);  // 眠ってすぐでも基礎時間
            Assert.AreEqual(20f, MothballRules.ReactivationTime(50f, P), 1e-4f); // 10＋50×0.2＝長く眠るほど遅い
            Assert.AreEqual(10f, MothballRules.ReactivationTime(-5f, P), 1e-4f); // 負期間はクランプ
        }

        [Test]
        public void ReactivationCost_ScalesWithStrengthAndDuration()
        {
            Assert.AreEqual(10f, MothballRules.ReactivationCost(100f, 0f, P), 1e-4f);  // 100×0.1
            Assert.AreEqual(60f, MothballRules.ReactivationCost(100f, 50f, P), 1e-4f); // 100×(0.1+50×0.01)
            Assert.AreEqual(0f, MothballRules.ReactivationCost(-10f, 50f, P), 1e-5f);  // 負兵力はクランプ
        }

        [Test]
        public void ConditionDecayTick_NeglectRotsMaintenanceSlows()
        {
            // 保守ゼロ：0.02×10dt＝0.2 朽ちる
            Assert.AreEqual(0.8f, MothballRules.ConditionDecayTick(1f, 0f, 10f, P), 1e-4f);
            // 保守満額：劣化は0.25倍＝0.05 しか減らない
            Assert.AreEqual(0.95f, MothballRules.ConditionDecayTick(1f, 1f, 10f, P), 1e-4f);
            // 保守半分：倍率 Lerp(1,0.25,0.5)=0.625 → 0.125 減
            Assert.AreEqual(0.875f, MothballRules.ConditionDecayTick(1f, 0.5f, 10f, P), 1e-4f);
        }

        [Test]
        public void ConditionDecayTick_ClampsAtZero()
        {
            Assert.AreEqual(0f, MothballRules.ConditionDecayTick(0.1f, 0f, 10f, P), 1e-5f); // 底で止まる
            Assert.AreEqual(1f, MothballRules.ConditionDecayTick(5f, 1f, 0f, P), 1e-5f);    // 状態は1にクランプ・dt0は不変
        }

        [Test]
        public void ReactivatedEffectiveness_ProportionalToCondition()
        {
            Assert.AreEqual(1f, MothballRules.ReactivatedEffectiveness(1f, P), 1e-4f);    // 手入れ満点＝満額
            Assert.AreEqual(0.65f, MothballRules.ReactivatedEffectiveness(0.5f, P), 1e-4f);
            Assert.AreEqual(0.3f, MothballRules.ReactivatedEffectiveness(0f, P), 1e-4f);  // 朽ちても下限は出る
            Assert.AreEqual(1f, MothballRules.ReactivatedEffectiveness(2f, P), 1e-4f);    // 過剰入力はクランプ
        }

        [Test]
        public void SavingsNowCostLater_NeglectedFleetIsSlowAndWeak()
        {
            // 平時の節約（保守ゼロで50単位時間眠らせる）→ 有事の遅れと弱体で払う
            float condition = 1f;
            for (int i = 0; i < 50; i++) condition = MothballRules.ConditionDecayTick(condition, 0f, 1f, P);
            Assert.AreEqual(0f, condition, 1e-4f); // 50×0.02＝完全に朽ちる
            Assert.Greater(MothballRules.ReactivationTime(50f, P), MothballRules.ReactivationTime(0f, P));   // 起こすのが遅い
            Assert.Greater(MothballRules.ReactivationCost(100f, 50f, P), MothballRules.ReactivationCost(100f, 0f, P)); // 高い
            Assert.AreEqual(0.3f, MothballRules.ReactivatedEffectiveness(condition, P), 1e-4f); // 出ても弱い
        }
    }
}
