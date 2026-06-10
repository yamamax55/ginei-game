using NUnit.Framework;
using UnityEngine;

namespace Ginei.Tests
{
    /// <summary>有限能力スケジューリング（#987・TOC）の純ロジック検証。</summary>
    public class CapacitySchedulingRulesTests
    {
        /// <summary>ボトルネックは最も能力の低い工程＝鎖は最弱の輪で切れる。</summary>
        [Test]
        public void BottleneckStage_ReturnsSlowestStage()
        {
            float[] caps = { 10f, 4f, 8f, 6f };
            Assert.AreEqual(1, CapacitySchedulingRules.BottleneckStage(caps));
        }

        /// <summary>同点ボトルネックは先頭側（添字の小さい方）。空はインデックス-1。</summary>
        [Test]
        public void BottleneckStage_TieFavorsFirst_EmptyIsMinusOne()
        {
            float[] tie = { 5f, 3f, 3f };
            Assert.AreEqual(1, CapacitySchedulingRules.BottleneckStage(tie));
            Assert.AreEqual(-1, CapacitySchedulingRules.BottleneckStage(new float[0]));
        }

        /// <summary>システムスループットはボトルネックの能力＝全体は最弱工程以上には流れない（TOCの核）。</summary>
        [Test]
        public void SystemThroughput_EqualsBottleneckCapacity()
        {
            float[] caps = { 10f, 4f, 8f, 6f };
            Assert.AreEqual(4f, CapacitySchedulingRules.SystemThroughput(caps), 1e-4f);
            // 非ボトルネックを上げても全体は最弱(4)のまま
            float[] fasterNonBottleneck = { 100f, 4f, 100f, 100f };
            Assert.AreEqual(4f, CapacitySchedulingRules.SystemThroughput(fasterNonBottleneck), 1e-4f);
        }

        /// <summary>投入が処理を超えるとWIPが溜まり、超えなければ溜まらない（ボトルネック手前の山）。</summary>
        [Test]
        public void WipAccumulation_GrowsWhenInflowExceedsCapacity()
        {
            // 投入10・ボトルネック4・dt2秒 → 余剰6×2=12
            Assert.AreEqual(12f, CapacitySchedulingRules.WipAccumulation(10f, 4f, 2f), 1e-4f);
            // 投入が能力以下なら溜まらない
            Assert.AreEqual(0f, CapacitySchedulingRules.WipAccumulation(3f, 4f, 2f), 1e-4f);
        }

        /// <summary>稼働率＝負荷÷能力。ボトルネックは100%・非ボトルネックは遊休（余力は無駄ではない）。</summary>
        [Test]
        public void UtilizationByStage_BottleneckSaturated_OthersIdle()
        {
            float[] caps = { 10f, 4f, 8f };
            float[] loads = { 4f, 4f, 4f }; // システムスループット=4 が全工程を流れる
            float[] util = CapacitySchedulingRules.UtilizationByStage(loads, caps);
            Assert.AreEqual(0.4f, util[0], 1e-4f);  // 速い工程は遊休
            Assert.AreEqual(1.0f, util[1], 1e-4f);  // ボトルネックは100%
            Assert.AreEqual(0.5f, util[2], 1e-4f);
        }

        /// <summary>過負荷は稼働率上限（既定1.0）で頭打ち・能力0は稼働率0。</summary>
        [Test]
        public void UtilizationByStage_ClampsOverloadAndZeroCapacity()
        {
            float[] caps = { 4f, 0f };
            float[] loads = { 10f, 5f };
            float[] util = CapacitySchedulingRules.UtilizationByStage(loads, caps);
            Assert.AreEqual(1.0f, util[0], 1e-4f); // 250%要求でも1.0頭打ち
            Assert.AreEqual(0f, util[1], 1e-4f);   // 能力0は0
        }

        /// <summary>完成時間＝数量÷ボトルネックスループット。スループット0は完成しない（無限大）。</summary>
        [Test]
        public void ScheduleCompletion_QtyOverThroughput()
        {
            Assert.AreEqual(25f, CapacitySchedulingRules.ScheduleCompletion(100f, 4f), 1e-4f);
            Assert.AreEqual(0f, CapacitySchedulingRules.ScheduleCompletion(0f, 4f), 1e-4f);
            Assert.IsTrue(float.IsPositiveInfinity(CapacitySchedulingRules.ScheduleCompletion(100f, 0f)));
        }

        /// <summary>ボトルネック強化の効果は次ボトルネックで頭打ち＝改善は移動する（TOC）。</summary>
        [Test]
        public void BottleneckElevationGain_CappedBySecondBottleneck()
        {
            // 現ボトルネック4・第2ボトルネック6。+10 足しても伸びは6までの差分=2
            Assert.AreEqual(2f, CapacitySchedulingRules.BottleneckElevationGain(4f, 10f, 6f), 1e-4f);
            // 控えめな強化(+1)は次に達しない＝丸ごと効く
            Assert.AreEqual(1f, CapacitySchedulingRules.BottleneckElevationGain(4f, 1f, 6f), 1e-4f);
            // 既定Params値の確認（稼働率上限1.0）
            Assert.AreEqual(1f, CapacitySchedulingParams.Default.maxUtilization, 1e-4f);
        }
    }
}
