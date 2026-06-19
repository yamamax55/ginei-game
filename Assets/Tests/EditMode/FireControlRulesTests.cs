using NUnit.Framework;
using UnityEngine;

namespace Ginei.Tests
{
    /// <summary>射撃管制（FCS・照準）純ロジックの EditMode テスト（#射撃管制）。既定 Params の期待値を手計算で固定。</summary>
    public class FireControlRulesTests
    {
        const float Eps = 1e-4f;     // 四則のみ
        const float EpsPow = 1e-3f;  // Pow/Sqrt 箇所（本ロジックは未使用だが許容）

        // 照準解：センサー0.8 × 追尾項(2/(2+2)=0.5) = 0.4
        [Test]
        public void TargetingSolution_SensorTimesTrackingTerm()
        {
            float s = FireControlRules.TargetingSolution(0.8f, 2f);
            Assert.AreEqual(0.4f, s, Eps);
        }

        // 追尾時間0なら解は0、長く追尾すると1へ漸近
        [Test]
        public void TargetingSolution_ZeroTrackingIsZero_LongTrackingApproachesSensor()
        {
            Assert.AreEqual(0f, FireControlRules.TargetingSolution(1f, 0f), Eps);
            float longTrack = FireControlRules.TargetingSolution(1f, 1000f);
            Assert.Greater(longTrack, 0.99f);
            Assert.LessOrEqual(longTrack, 1f);
        }

        // 基礎命中：解0.4 / (1 + 0.01×100=2) = 0.2
        [Test]
        public void BaseHitChance_DistanceDecay()
        {
            float h = FireControlRules.BaseHitChance(0.4f, 100f);
            Assert.AreEqual(0.2f, h, Eps);
            // 距離0なら解そのもの
            Assert.AreEqual(0.4f, FireControlRules.BaseHitChance(0.4f, 0f), Eps);
        }

        // 回避：回避0.5・速度2 → sw=1, speedFactor=0.5, penalty=0.5+0.5×0.5×0.5=0.625
        [Test]
        public void EvasionPenalty_EvasionAndSpeed()
        {
            float pen = FireControlRules.EvasionPenalty(0.5f, 2f);
            Assert.AreEqual(0.625f, pen, Eps);
            // 回避0なら速度があっても0
            Assert.AreEqual(0f, FireControlRules.EvasionPenalty(0f, 100f), Eps);
        }

        // 相対運動：横1×0.7 + 接近1×0.3 = 1.0 → 1/(1+1)=0.5。横の方が重い。
        [Test]
        public void RelativeMotionError_LateralWeightedMoreThanClosing()
        {
            float err = FireControlRules.RelativeMotionError(1f, 1f);
            Assert.AreEqual(0.5f, err, Eps);
            float lateralOnly = FireControlRules.RelativeMotionError(0f, 1f);
            float closingOnly = FireControlRules.RelativeMotionError(1f, 0f);
            Assert.Greater(lateralOnly, closingOnly); // 横切る目標の方が当てにくい
            // 符号は絶対値で吸収
            Assert.AreEqual(err, FireControlRules.RelativeMotionError(-1f, -1f), Eps);
        }

        // 斉射散布：5門・FCS0.5 → (4)×0.05×0.5=0.1 → 0.1/1.1≈0.090909
        [Test]
        public void SalvoSpread_MoreGunsSpreadMore_BetterFcsSpreadLess()
        {
            float spread = FireControlRules.SalvoSpread(5, 0.5f);
            Assert.AreEqual(0.1f / 1.1f, spread, Eps);
            // 1門以下は散布0
            Assert.AreEqual(0f, FireControlRules.SalvoSpread(1, 0f), Eps);
            Assert.AreEqual(0f, FireControlRules.SalvoSpread(0, 0f), Eps);
            // FCS完璧なら散らない
            Assert.AreEqual(0f, FireControlRules.SalvoSpread(8, 1f), Eps);
        }

        // 実効命中：0.5 × (1-0.2) × (1-0.1) = 0.5×0.8×0.9 = 0.36
        [Test]
        public void EffectiveHitChance_MultiplicativeReduction()
        {
            float eff = FireControlRules.EffectiveHitChance(0.5f, 0.2f, 0.1f);
            Assert.AreEqual(0.36f, eff, Eps);
            // 回避1で命中0
            Assert.AreEqual(0f, FireControlRules.EffectiveHitChance(0.9f, 1f, 0f), Eps);
        }

        // 修正射撃：前回外れ0.4 × FCS0.8 × gain0.5 = 0.16
        [Test]
        public void WalkingCorrection_PreviousMissTimesFcs()
        {
            float imp = FireControlRules.WalkingCorrection(0.4f, 0.8f);
            Assert.AreEqual(0.16f, imp, Eps);
            // 外れ0なら改善0
            Assert.AreEqual(0f, FireControlRules.WalkingCorrection(0f, 1f), Eps);
        }

        // 命中判定：決定論（roll 注入）
        [Test]
        public void IsHit_DeterministicRoll()
        {
            Assert.IsTrue(FireControlRules.IsHit(0.5f, 0.49f));  // roll < 命中率
            Assert.IsFalse(FireControlRules.IsHit(0.5f, 0.5f));  // 同値は外れ
            Assert.IsFalse(FireControlRules.IsHit(0f, 0f));      // 命中率0は常に外れ
            Assert.IsTrue(FireControlRules.IsHit(1f, 0.999f));   // 命中率1はほぼ命中
        }

        // 入力 clamp の頑健性（範囲外でも 0..1 に収まる）
        [Test]
        public void Inputs_AreClamped()
        {
            Assert.AreEqual(1f, FireControlRules.TargetingSolution(5f, 1000f), 0.05f);
            Assert.GreaterOrEqual(FireControlRules.BaseHitChance(2f, -10f), 0f);
            Assert.LessOrEqual(FireControlRules.BaseHitChance(2f, -10f), 1f);
            Assert.GreaterOrEqual(FireControlRules.EvasionPenalty(-1f, -5f), 0f);
            Assert.GreaterOrEqual(FireControlRules.SalvoSpread(-3, 2f), 0f);
        }

        // 物語テスト：練度の低い艦が、追尾を続け修正射撃を重ねて命中をもぎ取る
        [Test]
        public void Narrative_TrackingAndWalkingTurnAMissIntoAHit()
        {
            var p = FireControlParams.Default;
            float sensor = 0.7f;

            // 第1射：追尾わずか0.3秒の浅い照準解 → 実効命中は低い
            float sol1 = FireControlRules.TargetingSolution(sensor, 0.3f, p);
            float base1 = FireControlRules.BaseHitChance(sol1, 80f, p);
            float evade = FireControlRules.EvasionPenalty(0.2f, 1f, p);
            float motion = FireControlRules.RelativeMotionError(1f, 1f, p); // ほどほどに横切る
            float eff1 = FireControlRules.EffectiveHitChance(base1, evade, motion, p);
            Assert.IsFalse(FireControlRules.IsHit(eff1, 0.5f)); // この roll では外れる

            // 第1射は外れた → 外れ量 = 1 - 実効命中
            float miss1 = 1f - eff1;

            // 第2射：追尾を3秒続けて照準解が固まる＋修正射撃で命中率上乗せ
            float sol2 = FireControlRules.TargetingSolution(sensor, 3f, p);
            float base2 = FireControlRules.BaseHitChance(sol2, 80f, p);
            float eff2Raw = FireControlRules.EffectiveHitChance(base2, evade, motion, p);
            float walk = FireControlRules.WalkingCorrection(miss1, 0.9f, p); // FCS良好
            float eff2 = Mathf.Clamp01(eff2Raw + walk);

            // 追尾と修正で命中率が改善し、同じ roll でも今度は命中する
            Assert.Greater(eff2, eff1);
            Assert.IsTrue(FireControlRules.IsHit(eff2, 0.5f));
        }
    }
}
