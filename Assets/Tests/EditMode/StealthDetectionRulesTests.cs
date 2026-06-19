using NUnit.Framework;
using UnityEngine;

namespace Ginei.Tests
{
    /// <summary>ステルスと探知（隠密艦の被探知と発見）の純ロジック検証（#隠密艦）。既定Params期待値を手計算で固定。</summary>
    public class StealthDetectionRulesTests
    {
        const float Eps = 1e-4f;
        const float PowEps = 1e-3f;

        [Test]
        public void Signature_LowersBySppingAndEmission()
        {
            // 0.8 * (1-0.5) * (1-0.5) = 0.2
            float sig = StealthDetectionRules.Signature(0.8f, 0.5f, 0.5f);
            Assert.AreEqual(0.2f, sig, Eps);

            // 完璧な形状で0へ
            Assert.AreEqual(0f, StealthDetectionRules.Signature(1f, 1f, 0f), Eps);
        }

        [Test]
        public void DetectionProbability_FallsWithDistance()
        {
            // 0.5 * 0.8 * 1 / (1 + 1*1) = 0.4/2 = 0.2
            float p = StealthDetectionRules.DetectionProbability(0.5f, 0.8f, 1f);
            Assert.AreEqual(0.2f, p, Eps);

            // 近距離（距離0）ほど高い：0.5*0.8/1 = 0.4
            float near = StealthDetectionRules.DetectionProbability(0.5f, 0.8f, 0f);
            Assert.AreEqual(0.4f, near, Eps);
            Assert.Greater(near, p);
        }

        [Test]
        public void MultiBandDetection_AnyBandFinds()
        {
            // 1 - (1-0.5)(1-0.3)(1-0.2) = 1 - 0.5*0.7*0.8 = 1 - 0.28 = 0.72
            float d = StealthDetectionRules.MultiBandDetection(0.5f, 0.3f, 0.2f);
            Assert.AreEqual(0.72f, d, Eps);

            // 1帯域でも完璧なら1
            Assert.AreEqual(1f, StealthDetectionRules.MultiBandDetection(1f, 0f, 0f), Eps);
        }

        [Test]
        public void StealthBreak_FireAndManeuverExpose()
        {
            // breakWeight=0.5: fire=0.5, maneuver=0.5; 1-(0.5)(0.5)=0.75
            float b = StealthDetectionRules.StealthBreak(1f, 1f);
            Assert.AreEqual(0.75f, b, Eps);

            // 何もしなければ破れない
            Assert.AreEqual(0f, StealthDetectionRules.StealthBreak(0f, 0f), Eps);
        }

        [Test]
        public void CumulativeDetection_GrowsWithTime()
        {
            // cumWeight=0.5: instant=0.2; 1-(0.8)^2 = 1-0.64 = 0.36
            float c = StealthDetectionRules.CumulativeDetection(0.4f, 2f);
            Assert.AreEqual(0.36f, c, PowEps);

            // 観察時間0なら未探知
            Assert.AreEqual(0f, StealthDetectionRules.CumulativeDetection(0.4f, 0f), PowEps);
        }

        [Test]
        public void AmbushSurprise_LowSignatureUnwaryEnemy()
        {
            // (1-0.2)(1-0.3) = 0.8*0.7 = 0.56
            float s = StealthDetectionRules.AmbushSurprise(0.2f, 0.3f);
            Assert.AreEqual(0.56f, s, Eps);

            // 露呈済み（シグネチャ1）なら奇襲不成立
            Assert.AreEqual(0f, StealthDetectionRules.AmbushSurprise(1f, 0f), Eps);
        }

        [Test]
        public void CounterStealthTech_SensorVsStealthGeneration()
        {
            // 0.6 / (0.6+0.4) = 0.6
            float adv = StealthDetectionRules.CounterStealthTech(0.6f, 0.4f);
            Assert.AreEqual(0.6f, adv, Eps);

            // 双方ゼロ＝互角0.5
            Assert.AreEqual(0.5f, StealthDetectionRules.CounterStealthTech(0f, 0f), Eps);
        }

        [Test]
        public void IsDetected_DeterministicRoll()
        {
            Assert.IsTrue(StealthDetectionRules.IsDetected(0.5f, 0.3f));  // roll<prob
            Assert.IsFalse(StealthDetectionRules.IsDetected(0.5f, 0.7f)); // roll>=prob
            Assert.IsFalse(StealthDetectionRules.IsDetected(0.5f, 0.5f)); // 境界は未探知
        }

        [Test]
        public void InputsAreClampedAndSafe()
        {
            // 範囲外入力でも 0..1 に収まる
            float sig = StealthDetectionRules.Signature(5f, -1f, 2f);
            Assert.GreaterOrEqual(sig, 0f);
            Assert.LessOrEqual(sig, 1f);

            float p = StealthDetectionRules.DetectionProbability(2f, 2f, -5f);
            Assert.GreaterOrEqual(p, 0f);
            Assert.LessOrEqual(p, 1f);

            float c = StealthDetectionRules.CumulativeDetection(2f, -3f);
            Assert.AreEqual(0f, c, PowEps); // 負の観察時間はclampで0
        }

        [Test]
        public void Story_StealthRaiderSneaksThenExposesItself()
        {
            var pp = StealthDetectionParams.Default;

            // ステルス巡洋艦：基本シグネチャ高めだが形状と放射管理で被探知性を抑える
            float sig = StealthDetectionRules.Signature(0.9f, 0.6f, 0.5f, pp); // 0.9*0.4*0.5 = 0.18
            Assert.AreEqual(0.18f, sig, Eps);

            // 油断した敵への奇襲は成立しやすい
            float ambush = StealthDetectionRules.AmbushSurprise(sig, 0.2f, pp);
            Assert.Greater(ambush, 0.6f);

            // 遠距離では各帯域の瞬間探知は低い
            float optical = StealthDetectionRules.DetectionProbability(sig, 0.7f, 4f, pp);
            float band = StealthDetectionRules.MultiBandDetection(optical, optical * 0.5f, optical * 0.3f, pp);
            Assert.Less(band, 0.2f);

            // しかし発砲・急機動でステルスが破れると一気に露呈する
            float broken = StealthDetectionRules.StealthBreak(1f, 1f, pp);
            Assert.Greater(broken, band);

            // 長く観察され続ければ累積で見つかる
            float cumulative = StealthDetectionRules.CumulativeDetection(band, 10f, pp);
            Assert.Greater(cumulative, band);
        }
    }
}
