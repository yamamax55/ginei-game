using NUnit.Framework;
using UnityEngine;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 反撃（防御から攻勢への切り返し）：攻勢限界到達度・好機・打撃・早すぎ/遅すぎ損失・攻守逆転・追撃移行・転換判定。
    /// 既定 Params（侵攻0.5/補給0.5/早すぎ0.8/遅すぎ0.6）で期待値固定。
    /// </summary>
    public class CounterAttackRulesTests
    {
        [Test]
        public void EnemyCulmination_DepthAndSupply()
        {
            // 0.8*0.5 + 0.6*0.5 = 0.7
            Assert.AreEqual(0.7f, CounterAttackRules.EnemyCulmination(0.8f, 0.6f), 1e-4f);
            Assert.AreEqual(0f, CounterAttackRules.EnemyCulmination(0f, 0f), 1e-4f);     // まだ余裕
            Assert.AreEqual(1f, CounterAttackRules.EnemyCulmination(1f, 1f), 1e-4f);     // 攻勢終末点
        }

        [Test]
        public void CounterTiming_NeedsBothCulminationAndReserve()
        {
            Assert.AreEqual(0.56f, CounterAttackRules.CounterTiming(0.7f, 0.8f), 1e-4f); // 0.7*0.8
            Assert.AreEqual(0f, CounterAttackRules.CounterTiming(0.7f, 0f), 1e-4f);      // 予備なし→好機立たず
            Assert.AreEqual(0f, CounterAttackRules.CounterTiming(0f, 0.9f), 1e-4f);      // 敵未限界→好機立たず
        }

        [Test]
        public void CounterImpact_ProductOfThree()
        {
            // 0.56 * 0.8 * 0.75 = 0.336
            Assert.AreEqual(0.336f, CounterAttackRules.CounterImpact(0.56f, 0.8f, 0.75f), 1e-4f);
            Assert.AreEqual(0f, CounterAttackRules.CounterImpact(0.56f, 0.8f, 0f), 1e-4f); // 敵が疲れていない
        }

        [Test]
        public void PrematureCounterLoss_HighWhenEnemyStillStrong()
        {
            // (1-0.2)*0.8 = 0.64
            Assert.AreEqual(0.64f, CounterAttackRules.PrematureCounterLoss(0.2f), 1e-4f);
            Assert.AreEqual(0f, CounterAttackRules.PrematureCounterLoss(1f), 1e-4f);       // 完全に伸びきった敵なら損なし
        }

        [Test]
        public void TooLateLoss_RisesWithAttrition()
        {
            Assert.AreEqual(0.3f, CounterAttackRules.TooLateLoss(0.5f), 1e-4f);  // 0.5*0.6
            Assert.AreEqual(0f, CounterAttackRules.TooLateLoss(0f), 1e-4f);      // 消耗なし
        }

        [Test]
        public void MomentumReversal_TracksImpact()
        {
            Assert.AreEqual(0.336f, CounterAttackRules.MomentumReversal(0.336f), 1e-4f);
            Assert.AreEqual(1f, CounterAttackRules.MomentumReversal(2f), 1e-4f); // クランプ
        }

        [Test]
        public void PursuitTransition_NeedsReversalAndRout()
        {
            // 0.336 * 0.9 = 0.3024
            Assert.AreEqual(0.3024f, CounterAttackRules.PursuitTransition(0.336f, 0.9f), 1e-4f);
            Assert.AreEqual(0f, CounterAttackRules.PursuitTransition(0.5f, 0f), 1e-4f); // 敵が崩れていない→追撃不可
        }

        [Test]
        public void ShouldCounterattack_Threshold()
        {
            Assert.IsTrue(CounterAttackRules.ShouldCounterattack(0.56f));  // 0.56>=0.5
            Assert.IsTrue(CounterAttackRules.ShouldCounterattack(0.5f));   // 境界
            Assert.IsFalse(CounterAttackRules.ShouldCounterattack(0.4f));  // 好機薄い→受けを続ける
        }

        /// <summary>
        /// 物語：攻めてくる敵を受け止め、攻勢限界で反撃に転じて攻守を逆転し追撃へ移る。
        /// だが同じ予備でも「早すぎ（敵まだ強い）」「遅すぎ（守り磨耗）」は失敗に終わる。
        /// </summary>
        [Test]
        public void Narrative_ReceiveThenCounterAtCulmination()
        {
            // 早すぎ：敵はまだ浅い侵攻・補給も余裕（限界低い）→逆撃して跳ね返される
            float earlyCulm = CounterAttackRules.EnemyCulmination(0.2f, 0.1f); // 0.15
            float earlyTiming = CounterAttackRules.CounterTiming(earlyCulm, 0.9f);
            Assert.IsFalse(CounterAttackRules.ShouldCounterattack(earlyTiming)); // まだ転じない
            float earlyLoss = CounterAttackRules.PrematureCounterLoss(earlyCulm);
            Assert.Greater(earlyLoss, 0.6f); // 早すぎる反撃は大損

            // 攻勢限界：敵が深く伸び補給も逼迫（限界高い）＋予備が整う→好機が立ち反撃へ転じる
            float culm = CounterAttackRules.EnemyCulmination(0.8f, 0.6f); // 0.7
            float timing = CounterAttackRules.CounterTiming(culm, 0.9f);  // 0.63
            Assert.IsTrue(CounterAttackRules.ShouldCounterattack(timing));
            float impact = CounterAttackRules.CounterImpact(timing, 0.8f, 0.8f);
            float reversal = CounterAttackRules.MomentumReversal(impact);
            Assert.Greater(reversal, 0.3f); // 攻守が逆転
            float pursuit = CounterAttackRules.PursuitTransition(reversal, 0.7f);
            Assert.Greater(pursuit, 0f);    // 追撃へ移行
            // 好機の損失は早すぎより小さい（伸びきった敵を叩く）
            Assert.Less(CounterAttackRules.PrematureCounterLoss(culm), earlyLoss);

            // 遅すぎ：好機を逃して受け続け守りが磨り減る→崩壊損失
            float lateLoss = CounterAttackRules.TooLateLoss(0.9f);
            Assert.Greater(lateLoss, 0.5f);
        }
    }
}
