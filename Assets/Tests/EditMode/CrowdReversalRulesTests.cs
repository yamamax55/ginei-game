using NUnit.Framework;
using UnityEngine;
using Ginei;

namespace Ginei.Tests
{
    public class CrowdReversalRulesTests
    {
        const float Eps = 1e-4f;
        const float PowEps = 1e-3f;

        [Test]
        public void 群衆強度が高いほど感情が移ろいやすい()
        {
            float low = CrowdReversalRules.EmotionalVolatility(0.2f);
            float high = CrowdReversalRules.EmotionalVolatility(0.9f);
            Assert.Less(low, high);
            // Pow(0.9,1.5)*0.9 ≈ 0.76843
            Assert.AreEqual(0.76843f, high, PowEps);
            Assert.AreEqual(0f, CrowdReversalRules.EmotionalVolatility(0f), Eps);
            Assert.AreEqual(0.9f, CrowdReversalRules.EmotionalVolatility(1f), PowEps);
        }

        [Test]
        public void 反転確率はきっかけと易変性の加重和に相乗を足す()
        {
            // prob=0.5*0.6+0.8*0.4=0.62; synergy=0.4; 0.62+0.4*0.38=0.772
            float prob = CrowdReversalRules.ReversalProbability(0.8f, 0.5f);
            Assert.AreEqual(0.772f, prob, Eps);
            // 低易変×小きっかけは低確率
            float calm = CrowdReversalRules.ReversalProbability(0.1f, 0.1f);
            Assert.AreEqual(0.109f, calm, Eps);
            Assert.Less(calm, prob);
        }

        [Test]
        public void 反転振幅は強度と現在の振れ幅で大きくなる()
        {
            // baseSwing=0.5+0.5*0.8=0.9; (0.5+1.0)=1.5; *1.0=1.35
            float big = CrowdReversalRules.ReversalAmplitude(1.0f, 0.8f);
            Assert.AreEqual(1.35f, big, Eps);
            // baseSwing=0.5; (0.5+0.2)=0.7; =0.35
            float small = CrowdReversalRules.ReversalAmplitude(0.2f, 0.0f);
            Assert.AreEqual(0.35f, small, Eps);
            Assert.Less(small, big);
        }

        [Test]
        public void TriggerReversalはrollで決定論的に反転する()
        {
            Assert.IsTrue(CrowdReversalRules.TriggerReversal(0.7f, 0.5f));
            Assert.IsFalse(CrowdReversalRules.TriggerReversal(0.3f, 0.5f));
            // 境界（roll==prob）は反転しない
            Assert.IsFalse(CrowdReversalRules.TriggerReversal(0.5f, 0.5f));
        }

        [Test]
        public void FlipEmotionは歓喜を恐慌へ符号反転する()
        {
            // 歓喜0.8 → 振幅1.0で -0.2（恐慌側へ）
            Assert.AreEqual(-0.2f, CrowdReversalRules.FlipEmotion(0.8f, 1.0f), Eps);
            // 恐慌-0.6 → 振幅1.0で +0.4（歓喜側へ）
            Assert.AreEqual(0.4f, CrowdReversalRules.FlipEmotion(-0.6f, 1.0f), Eps);
            // 中立0は崩落側（負）へ
            Assert.AreEqual(-0.5f, CrowdReversalRules.FlipEmotion(0.0f, 0.5f), Eps);
            // 大振幅でも-1..1にクランプ
            Assert.AreEqual(-1f, CrowdReversalRules.FlipEmotion(0.5f, 5f), Eps);
        }

        [Test]
        public void 崇拝が高いほど英雄は生贄になりやすい()
        {
            Assert.AreEqual(0.45f, CrowdReversalRules.HeroToScapegoat(0.9f, 0.5f), Eps);
            // 崇拝されていなければ標的化も小さい
            Assert.Less(
                CrowdReversalRules.HeroToScapegoat(0.2f, 0.5f),
                CrowdReversalRules.HeroToScapegoat(0.9f, 0.5f));
            // 失敗がなければ標的化なし
            Assert.AreEqual(0f, CrowdReversalRules.HeroToScapegoat(0.9f, 0f), Eps);
        }

        [Test]
        public void 歓喜とショックが大きいほど恐慌が深い()
        {
            Assert.AreEqual(0.48f, CrowdReversalRules.EuphoriaToPanic(0.8f, 0.6f), Eps);
            Assert.AreEqual(0f, CrowdReversalRules.EuphoriaToPanic(0f, 1f), Eps);
            Assert.Less(
                CrowdReversalRules.EuphoriaToPanic(0.3f, 0.6f),
                CrowdReversalRules.EuphoriaToPanic(0.8f, 0.6f));
        }

        [Test]
        public void 反転を繰り返すと振幅が消耗する()
        {
            // factor=1-0.2*3=0.4
            Assert.AreEqual(0.4f, CrowdReversalRules.ReversalDamping(1.0f, 3), Eps);
            Assert.AreEqual(1.0f, CrowdReversalRules.ReversalDamping(1.0f, 0), Eps);
            // 5回以上で消耗し切る
            Assert.AreEqual(0f, CrowdReversalRules.ReversalDamping(1.0f, 5), Eps);
            Assert.Greater(
                CrowdReversalRules.ReversalDamping(1.0f, 1),
                CrowdReversalRules.ReversalDamping(1.0f, 3));
        }

        [Test]
        public void IsReversingは反転局面を判定する()
        {
            Assert.IsTrue(CrowdReversalRules.IsReversing(0.6f));   // 既定しきい値0.5以上
            Assert.IsFalse(CrowdReversalRules.IsReversing(0.4f));
            Assert.IsTrue(CrowdReversalRules.IsReversing(0.5f, 0.5f)); // 境界は含む
        }

        [Test]
        public void 物語_高強度の群衆は小さなきっかけで歓喜から恐慌へ一斉反転し英雄が生贄になるが繰り返すと消耗する()
        {
            var p = CrowdReversalParams.Default;

            // 高強度の群衆は感情が移ろいやすい
            float volHigh = CrowdReversalRules.EmotionalVolatility(0.95f, p);
            float volLow = CrowdReversalRules.EmotionalVolatility(0.2f, p);
            Assert.Greater(volHigh, volLow);

            // 高易変なら小さなきっかけでも反転確率が高い
            float smallTrigger = 0.3f;
            float probHigh = CrowdReversalRules.ReversalProbability(volHigh, smallTrigger, p);
            float probLow = CrowdReversalRules.ReversalProbability(volLow, smallTrigger, p);
            Assert.Greater(probHigh, probLow);
            Assert.IsTrue(CrowdReversalRules.IsReversing(probHigh, p.reversalThreshold));

            // 決定論：きっかけで実際に反転（roll が確率を下回る）
            Assert.IsTrue(CrowdReversalRules.TriggerReversal(probHigh, 0.1f));

            // 勝利の歓喜が敗北の恐慌へ転じる：歓喜+0.9 が反転で負（恐慌）へ
            float euphoria = 0.9f;
            float amp = CrowdReversalRules.ReversalAmplitude(0.95f, euphoria, p);
            float flipped = CrowdReversalRules.FlipEmotion(euphoria, amp);
            Assert.Less(flipped, 0f, "歓喜が恐慌へ反転する");

            // 昨日の英雄が今日の生贄に
            float scapegoat = CrowdReversalRules.HeroToScapegoat(0.9f, smallTrigger);
            Assert.Greater(scapegoat, 0f);

            // しかし反転を繰り返すと群衆は消耗し、振幅が減衰する
            float ampFresh = CrowdReversalRules.ReversalDamping(amp, 0, p);
            float ampWorn = CrowdReversalRules.ReversalDamping(amp, 4, p);
            Assert.Less(ampWorn, ampFresh, "反転を繰り返すと消耗して振幅が減る");
        }
    }
}
