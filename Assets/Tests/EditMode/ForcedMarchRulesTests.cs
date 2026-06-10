using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 強行軍を固定する：速度倍率は強度に線形・疲労は強行で積み休息で抜ける（回復は蓄積より遅い）・
    /// 到着直後の戦闘倍率低下・閾値超過×強度の落伍・回復所要時間。回復/落伍の境界とクランプを担保。
    /// </summary>
    public class ForcedMarchRulesTests
    {
        private static readonly ForcedMarchParams P = ForcedMarchParams.Default; // 速度1.5/蓄積0.1/回復0.05/戦闘減0.5/落伍閾値0.6/落伍幅0.1

        [Test]
        public void SpeedFactor_LinearWithIntensity_Clamped()
        {
            Assert.AreEqual(1f, ForcedMarchRules.SpeedFactor(0f, P), 1e-4f);     // 通常行軍＝等速
            Assert.AreEqual(1.25f, ForcedMarchRules.SpeedFactor(0.5f, P), 1e-4f);
            Assert.AreEqual(1.5f, ForcedMarchRules.SpeedFactor(1f, P), 1e-4f);   // 全力強行＝上限
            Assert.AreEqual(1.5f, ForcedMarchRules.SpeedFactor(2f, P), 1e-4f);   // 入力クランプ
            Assert.AreEqual(1f, ForcedMarchRules.SpeedFactor(-1f, P), 1e-4f);
        }

        [Test]
        public void FatigueTick_AccumulatesWithIntensity_ClampedToOne()
        {
            // 全力強行：0.1×dt5＝0.5、半分の強度なら半分しか積まない
            Assert.AreEqual(0.5f, ForcedMarchRules.FatigueTick(0f, 1f, 5f, P), 1e-4f);
            Assert.AreEqual(0.25f, ForcedMarchRules.FatigueTick(0f, 0.5f, 5f, P), 1e-4f);
            // 上限1で頭打ち
            Assert.AreEqual(1f, ForcedMarchRules.FatigueTick(0.9f, 1f, 5f, P), 1e-4f);
        }

        [Test]
        public void FatigueTick_RecoversAtRest_SlowerThanGain_FloorZero()
        {
            // 休息：0.05×dt4＝0.2 抜ける（蓄積0.1の半分＝返済は倍かかる）
            Assert.AreEqual(0.3f, ForcedMarchRules.FatigueTick(0.5f, 0f, 4f, P), 1e-4f);
            // 下限0で止まる
            Assert.AreEqual(0f, ForcedMarchRules.FatigueTick(0.1f, 0f, 10f, P), 1e-4f);
        }

        [Test]
        public void CombatPenalty_FreshIsFull_ExhaustedIsHalved()
        {
            Assert.AreEqual(1f, ForcedMarchRules.CombatPenalty(0f, P), 1e-4f);    // 万全
            Assert.AreEqual(0.75f, ForcedMarchRules.CombatPenalty(0.5f, P), 1e-4f);
            Assert.AreEqual(0.5f, ForcedMarchRules.CombatPenalty(1f, P), 1e-4f);  // 疲労困憊＝半減
        }

        [Test]
        public void StragglerRatio_ZeroBelowThreshold_ScalesWithExcessAndIntensity()
        {
            // 閾値0.6以下は落伍なし＝無理が利く安全圏
            Assert.AreEqual(0f, ForcedMarchRules.StragglerRatio(0.6f, 1f, P), 1e-5f);
            Assert.AreEqual(0f, ForcedMarchRules.StragglerRatio(0.3f, 1f, P), 1e-5f);
            // 超過分×強度×幅：(0.8−0.6)/0.4×1×0.1＝0.05、強度半分で0.025
            Assert.AreEqual(0.05f, ForcedMarchRules.StragglerRatio(0.8f, 1f, P), 1e-4f);
            Assert.AreEqual(0.025f, ForcedMarchRules.StragglerRatio(0.8f, 0.5f, P), 1e-4f);
            // 疲労最大×全力＝落伍幅いっぱい
            Assert.AreEqual(0.1f, ForcedMarchRules.StragglerRatio(1f, 1f, P), 1e-4f);
        }

        [Test]
        public void StragglerRatio_NoLossAtRest()
        {
            // 休息中は疲れていても落伍しない（落伍は「疲れた軍を駆けさせる」ときだけ）
            Assert.AreEqual(0f, ForcedMarchRules.StragglerRatio(1f, 0f, P), 1e-5f);
        }

        [Test]
        public void RecoveryTime_FatigueOverRate_InfinityWhenNoRecovery()
        {
            Assert.AreEqual(20f, ForcedMarchRules.RecoveryTime(1f, P), 1e-4f);  // 1/0.05
            Assert.AreEqual(10f, ForcedMarchRules.RecoveryTime(0.5f, P), 1e-4f);
            Assert.AreEqual(0f, ForcedMarchRules.RecoveryTime(0f, P), 1e-5f);   // 万全＝0
            var noRecover = new ForcedMarchParams(1.5f, 0.1f, 0f, 0.5f, 0.6f, 0.1f);
            Assert.IsTrue(float.IsPositiveInfinity(ForcedMarchRules.RecoveryTime(0.5f, noRecover))); // 休めない軍は戻らない
        }
    }
}
