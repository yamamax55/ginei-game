using NUnit.Framework;
using UnityEngine;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 二重過程（System1 直感 / System2 熟慮）を固定する：直感の速さ、熟慮の正確さ（認知負荷で目減り）、
    /// スタイルの配合、判断速度、複雑度に応じた正確さ、時間切迫の System1 寄せ、直感の系統的誤り、
    /// 速さ正確さのトレードオフ、直感型判定。境界を担保。
    /// </summary>
    public class DualProcessRulesTests
    {
        private static readonly DualProcessParams P = DualProcessParams.Default;
        // 直感基準速0.6/直感速度増0.4/熟慮遅延0.5/負荷罰0.5/直感基礎誤り0.1/不慣れ誤り増0.5/切迫寄せ0.7

        [Test]
        public void System1Speed_FasterWhenIntuitive()
        {
            // 直感ゼロ＝基準速度0.6
            Assert.AreEqual(0.6f, DualProcessRules.System1Speed(0f, P), 1e-4f);
            // 直感1.0＝0.6 + 1×0.4 = 1.0
            Assert.AreEqual(1f, DualProcessRules.System1Speed(1f, P), 1e-4f);
            // 直感0.5＝0.6 + 0.5×0.4 = 0.8
            Assert.AreEqual(0.8f, DualProcessRules.System1Speed(0.5f, P), 1e-4f);
        }

        [Test]
        public void System2Accuracy_DropsUnderLoad()
        {
            // 熟慮0.8・負荷ゼロ＝0.8（そのまま正確）
            Assert.AreEqual(0.8f, DualProcessRules.System2Accuracy(0.8f, 0f, P), 1e-4f);
            // 熟慮0.8・負荷1.0＝0.8×(1 − 0.5) = 0.4（熟慮しきれない）
            Assert.AreEqual(0.4f, DualProcessRules.System2Accuracy(0.8f, 1f, P), 1e-4f);
            // 熟慮1.0・負荷0.5＝1×(1 − 0.25) = 0.75
            Assert.AreEqual(0.75f, DualProcessRules.System2Accuracy(1f, 0.5f, P), 1e-4f);
        }

        [Test]
        public void ProcessBlend_ReturnsStyle()
        {
            Assert.AreEqual(0.3f, DualProcessRules.ProcessBlend(0.3f), 1e-4f);
            Assert.AreEqual(0f, DualProcessRules.ProcessBlend(-0.5f), 1e-4f); // クランプ
            Assert.AreEqual(1f, DualProcessRules.ProcessBlend(2f), 1e-4f);
        }

        [Test]
        public void DecisionSpeed_SlowerWhenDeliberative()
        {
            // 直感的（スタイル0）＝0.6×1 = 0.6（速い）
            Assert.AreEqual(0.6f, DualProcessRules.DecisionSpeed(0f, P), 1e-4f);
            // 熟慮的（スタイル1）＝0.6×(1 − 0.5) = 0.3（遅い）
            Assert.AreEqual(0.3f, DualProcessRules.DecisionSpeed(1f, P), 1e-4f);
            // 中間＝0.6×0.75 = 0.45
            Assert.AreEqual(0.45f, DualProcessRules.DecisionSpeed(0.5f, P), 1e-4f);
        }

        [Test]
        public void DecisionAccuracy_ComplexNeedsDeliberation()
        {
            // 単純な問題（複雑度0）＝直感で十分＝1 − 基礎誤り0.1 = 0.9（スタイルによらない）
            Assert.AreEqual(0.9f, DualProcessRules.DecisionAccuracy(0f, 0f, P), 1e-4f);
            Assert.AreEqual(0.9f, DualProcessRules.DecisionAccuracy(1f, 0f, P), 1e-4f);
            // 複雑な問題（複雑度1）＝スタイルが支配。直感型(0.2)は外す
            Assert.AreEqual(0.2f, DualProcessRules.DecisionAccuracy(0.2f, 1f, P), 1e-4f);
            // 複雑な問題でも熟慮型(0.8)は正確
            Assert.AreEqual(0.8f, DualProcessRules.DecisionAccuracy(0.8f, 1f, P), 1e-4f);
            // 中庸＝Lerp(0.9, 0.5, 0.5) = 0.7
            Assert.AreEqual(0.7f, DualProcessRules.DecisionAccuracy(0.5f, 0.5f, P), 1e-4f);
        }

        [Test]
        public void TimePressureShift_PullsTowardSystem1()
        {
            // 切迫なし＝スタイルそのまま0.8
            Assert.AreEqual(0.8f, DualProcessRules.TimePressureShift(0.8f, 0f, P), 1e-4f);
            // 切迫1.0＝0.8×(1 − 0.7) = 0.24（直感寄りへ大きく引かれる）
            Assert.AreEqual(0.24f, DualProcessRules.TimePressureShift(0.8f, 1f, P), 1e-4f);
            // 切迫0.5＝0.8×(1 − 0.35) = 0.52
            Assert.AreEqual(0.52f, DualProcessRules.TimePressureShift(0.8f, 0.5f, P), 1e-4f);
        }

        [Test]
        public void IntuitionErrorRate_HigherWhenUnfamiliar()
        {
            // 慣れた問題（familiarity1）＝基礎誤り0.1のみ
            Assert.AreEqual(0.1f, DualProcessRules.IntuitionErrorRate(1f, 1f, P), 1e-4f);
            // 完全に不慣れ＝0.1 + 1×1×0.5 = 0.6（直感が外す）
            Assert.AreEqual(0.6f, DualProcessRules.IntuitionErrorRate(1f, 0f, P), 1e-4f);
            // 直感0.5・不慣れ0.5＝0.1 + 0.5×0.5×0.5 = 0.225
            Assert.AreEqual(0.225f, DualProcessRules.IntuitionErrorRate(0.5f, 0.5f, P), 1e-4f);
        }

        [Test]
        public void SpeedAccuracyTradeoff_SignReflectsBalance()
        {
            // 速い(0.8)が正確(0.4)を上回る＝正（速いが粗い寄り）
            Assert.AreEqual(0.4f, DualProcessRules.SpeedAccuracyTradeoff(0.8f, 0.4f), 1e-4f);
            // 遅い(0.3)が正確(0.75)に劣る＝負（遅いが正確寄り）
            Assert.AreEqual(-0.45f, DualProcessRules.SpeedAccuracyTradeoff(0.3f, 0.75f), 1e-4f);
            // 均衡＝0
            Assert.AreEqual(0f, DualProcessRules.SpeedAccuracyTradeoff(0.5f, 0.5f), 1e-4f);
        }

        [Test]
        public void IsIntuitiveDecider_BelowThreshold()
        {
            Assert.IsTrue(DualProcessRules.IsIntuitiveDecider(0.3f, 0.5f));
            Assert.IsFalse(DualProcessRules.IsIntuitiveDecider(0.7f, 0.5f));
        }

        // 物語テスト：単純な問題なら直感型の提督が速く正確に裁くが、複雑な問題では熟慮型に正確さで劣る。
        // そして時間切迫は両者を直感へ寄せ、熟慮型ですら速いが粗い判断へ追い込まれる。
        [Test]
        public void Narrative_IntuitiveWinsSimpleDeliberativeWinsComplex()
        {
            float intuitive = 0.15f;     // 直感型の提督
            float deliberative = 0.85f;  // 熟慮型の提督

            // 直感型は速い、熟慮型は遅い
            Assert.Greater(DualProcessRules.DecisionSpeed(intuitive, P),
                           DualProcessRules.DecisionSpeed(deliberative, P));

            // 単純な問題（複雑度0.1）では正確さに大差なし＝直感で十分（むしろ直感型が速度ぶん有利）
            float simpleIntu = DualProcessRules.DecisionAccuracy(intuitive, 0.1f, P);
            float simpleDelib = DualProcessRules.DecisionAccuracy(deliberative, 0.1f, P);
            Assert.That(Mathf.Abs(simpleIntu - simpleDelib), Is.LessThan(0.1f));

            // 複雑な問題（複雑度0.9）では熟慮型が正確さで勝る
            float hardIntu = DualProcessRules.DecisionAccuracy(intuitive, 0.9f, P);
            float hardDelib = DualProcessRules.DecisionAccuracy(deliberative, 0.9f, P);
            Assert.Greater(hardDelib, hardIntu);

            // 時間切迫は熟慮型を直感側へ引きずり込む（実効スタイルが下がる）
            float shifted = DualProcessRules.TimePressureShift(deliberative, 0.8f, P);
            Assert.Less(shifted, deliberative);
            // 切迫下では速いが粗い＝複雑問題の正確さも目減りする
            Assert.Less(DualProcessRules.DecisionAccuracy(shifted, 0.9f, P), hardDelib);
        }
    }
}
