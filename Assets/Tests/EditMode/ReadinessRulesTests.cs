using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 即応態勢を固定する：警戒を上げるほど維持費が増え・出撃が速く・奇襲に強い。持続可能水準を超えた
    /// 警戒は疲労を積み、疲労は実効警戒を緩める＝常時最高警戒は不可能（メリハリの管理）。
    /// 入力クランプ・既定値の具体数値を担保。
    /// </summary>
    public class ReadinessRulesTests
    {
        private static readonly ReadinessParams P = ReadinessParams.Default;
        // 休暇維持費0.6/最高警戒2.0・最大遅延60・疲労率0.02・回復率0.04・持続可能警戒0.5

        [Test]
        public void UpkeepFactor_RestIsCheap_FullAlertIsExpensive()
        {
            Assert.AreEqual(0.6f, ReadinessRules.UpkeepFactor(0f, P), 1e-5f);   // 休暇＝安い
            Assert.AreEqual(2f, ReadinessRules.UpkeepFactor(1f, P), 1e-5f);     // 最高警戒＝高い
            Assert.AreEqual(1.3f, ReadinessRules.UpkeepFactor(0.5f, P), 1e-5f); // 中間＝線形補間
            // 範囲外入力はクランプ
            Assert.AreEqual(0.6f, ReadinessRules.UpkeepFactor(-1f, P), 1e-5f);
            Assert.AreEqual(2f, ReadinessRules.UpkeepFactor(2f, P), 1e-5f);
        }

        [Test]
        public void ResponseDelay_VacationFleetLagsBehind()
        {
            Assert.AreEqual(60f, ReadinessRules.ResponseDelay(0f, P), 1e-5f);   // 休暇中＝最大遅延
            Assert.AreEqual(0f, ReadinessRules.ResponseDelay(1f, P), 1e-5f);    // 最高警戒＝即応
            Assert.AreEqual(30f, ReadinessRules.ResponseDelay(0.5f, P), 1e-5f); // 中間
            Assert.AreEqual(60f, ReadinessRules.ResponseDelay(-5f, P), 1e-5f);  // クランプ
        }

        [Test]
        public void SurpriseVulnerability_ComplementOfAlert()
        {
            Assert.AreEqual(1f, ReadinessRules.SurpriseVulnerability(0f), 1e-5f);    // 休暇中＝丸裸
            Assert.AreEqual(0f, ReadinessRules.SurpriseVulnerability(1f), 1e-5f);    // 完全警戒＝奇襲不能
            Assert.AreEqual(0.7f, ReadinessRules.SurpriseVulnerability(0.3f), 1e-5f);
            Assert.AreEqual(0f, ReadinessRules.SurpriseVulnerability(9f), 1e-5f);    // クランプ
        }

        [Test]
        public void FatigueTick_HighAlertWearsDown_RestRecovers()
        {
            // 最高警戒：超過0.5×0.02×10 = +0.1
            Assert.AreEqual(0.1f, ReadinessRules.FatigueTick(0f, 1f, 10f, P), 1e-5f);
            // 休暇：不足0.5×0.04×10 = −0.2
            Assert.AreEqual(0.3f, ReadinessRules.FatigueTick(0.5f, 0f, 10f, P), 1e-5f);
            // 持続可能水準ちょうど＝釣り合い（増減なし）
            Assert.AreEqual(0.4f, ReadinessRules.FatigueTick(0.4f, 0.5f, 100f, P), 1e-5f);
            // 0..1 にクランプ
            Assert.AreEqual(1f, ReadinessRules.FatigueTick(0.95f, 1f, 999f, P), 1e-5f);
            Assert.AreEqual(0f, ReadinessRules.FatigueTick(0.05f, 0f, 999f, P), 1e-5f);
            // 負の dt は進めない
            Assert.AreEqual(0.5f, ReadinessRules.FatigueTick(0.5f, 1f, -10f, P), 1e-5f);
        }

        [Test]
        public void EffectiveAlert_FatigueLoosensTheBowstring()
        {
            Assert.AreEqual(1f, ReadinessRules.EffectiveAlert(1f, 0f), 1e-5f);    // 万全
            Assert.AreEqual(0.5f, ReadinessRules.EffectiveAlert(1f, 0.5f), 1e-5f); // 疲労で半減
            Assert.AreEqual(0f, ReadinessRules.EffectiveAlert(1f, 1f), 1e-5f);    // 疲弊＝見えていない
            Assert.AreEqual(0.4f, ReadinessRules.EffectiveAlert(0.8f, 0.5f), 1e-5f);
        }

        [Test]
        public void PermanentFullAlert_IsImpossible()
        {
            // 最高警戒を張り続けると疲労が満ち、実効警戒は休暇以下に落ちる＝メリハリの管理が要る
            float fatigue = 0f;
            for (int i = 0; i < 20; i++) fatigue = ReadinessRules.FatigueTick(fatigue, 1f, 10f, P);
            Assert.AreEqual(1f, fatigue, 1e-5f); // +0.01/秒＝100秒で疲労満タン（200秒経過時点では確実に1）
            float effective = ReadinessRules.EffectiveAlert(1f, fatigue);
            Assert.Less(effective, ReadinessRules.SustainableAlert(P)); // 名目最高でも持続可能水準を割る
        }

        [Test]
        public void Params_CtorClampsAndDefault()
        {
            Assert.AreEqual(0.5f, ReadinessRules.SustainableAlert(P), 1e-5f);
            // 不正値はクランプされる
            var q = new ReadinessParams(-1f, 0.5f, -10f, -1f, -1f, 2f);
            Assert.AreEqual(0f, q.restUpkeepFactor, 1e-5f);
            Assert.AreEqual(1f, q.fullAlertUpkeepFactor, 1e-5f); // 1未満は1へ
            Assert.AreEqual(0f, q.maxResponseDelay, 1e-5f);
            Assert.AreEqual(0f, q.fatigueRate, 1e-5f);
            Assert.AreEqual(0f, q.recoveryRate, 1e-5f);
            Assert.AreEqual(1f, q.sustainableAlert, 1e-5f);
        }
    }
}
