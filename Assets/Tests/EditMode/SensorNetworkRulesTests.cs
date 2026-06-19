using NUnit.Framework;
using UnityEngine;

namespace Ginei.Tests
{
    /// <summary>SensorNetworkRules（センサー網＝索敵網）の純ロジック検証。既定 Params で期待値を固定。</summary>
    public class SensorNetworkRulesTests
    {
        const float Eps = 1e-4f;
        const float PowEps = 1e-3f;

        [Test]
        public void DetectionRange_PowerTimesSqrtSignature()
        {
            // 1 * 2 * 9^0.5 = 2 * 3 = 6
            Assert.AreEqual(6f, SensorNetworkRules.DetectionRange(2f, 9f), PowEps);
        }

        [Test]
        public void NetworkCoverage_CountUnderSqrt()
        {
            // clamp01(0.1 * 2 * 4^0.5 * 1) = clamp01(0.2 * 2) = 0.4
            Assert.AreEqual(0.4f, SensorNetworkRules.NetworkCoverage(4, 2f, 1f), PowEps);
        }

        [Test]
        public void NetworkCoverage_ClampedToOne()
        {
            // 0.1 * 100 * ... は 1 を超えるので 1 に飽和
            Assert.AreEqual(1f, SensorNetworkRules.NetworkCoverage(9, 100f, 1f), Eps);
        }

        [Test]
        public void Redundancy_MoreSensorsMoreRobust()
        {
            // 1 - 0.5^3 = 1 - 0.125 = 0.875
            Assert.AreEqual(0.875f, SensorNetworkRules.Redundancy(3), PowEps);
            // 0台は冗長性0
            Assert.AreEqual(0f, SensorNetworkRules.Redundancy(0), Eps);
        }

        [Test]
        public void FalseAlarmRate_NoiseTimesSensitivity()
        {
            // 1 * 0.5 * 0.8 = 0.4（感度を上げると誤報が増える）
            Assert.AreEqual(0.4f, SensorNetworkRules.FalseAlarmRate(0.5f, 0.8f), Eps);
        }

        [Test]
        public void CoverageGap_OneMinusCoverage()
        {
            // 1 - 0.4 = 0.6
            Assert.AreEqual(0.6f, SensorNetworkRules.CoverageGap(0.4f), Eps);
        }

        [Test]
        public void DataFusionAccuracy_DiminishingReturns()
        {
            // 1台は個別精度のまま
            Assert.AreEqual(0.5f, SensorNetworkRules.DataFusionAccuracy(1, 0.5f), Eps);
            // 3台: 0.5 + 0.5*(1 - 0.5^2) = 0.5 + 0.5*0.75 = 0.875
            Assert.AreEqual(0.875f, SensorNetworkRules.DataFusionAccuracy(3, 0.5f), PowEps);
        }

        [Test]
        public void CounterDetectionRisk_PowerTimesEsm()
        {
            // 1 * 0.8 * 0.5 = 0.4（active sensor は撃つと晒される）
            Assert.AreEqual(0.4f, SensorNetworkRules.CounterDetectionRisk(0.8f, 0.5f), Eps);
        }

        [Test]
        public void IsTargetDetected_InRangeAndOutOfRange()
        {
            // margin = 1 - 5/10 = 0.5。roll 0.4 で探知、roll 0.6 で取り逃し
            Assert.IsTrue(SensorNetworkRules.IsTargetDetected(10f, 5f, 0.4f));
            Assert.IsFalse(SensorNetworkRules.IsTargetDetected(10f, 5f, 0.6f));
            // 探知距離外は常に false
            Assert.IsFalse(SensorNetworkRules.IsTargetDetected(10f, 15f, 0f));
        }

        [Test]
        public void Story_DenseFusedNetworkSeesIntruderThinGapLetsItSlip()
        {
            var p = SensorNetworkParams.Default;

            // 密な索敵網：多数のセンサーを広く張り、ほぼ全域を覆う
            float denseCoverage = SensorNetworkRules.NetworkCoverage(16, 8f, 1f, p);
            float denseGap = SensorNetworkRules.CoverageGap(denseCoverage, p);
            // 薄い索敵網：少数のセンサーで狭くしか覆えず、穴が大きい
            float thinCoverage = SensorNetworkRules.NetworkCoverage(2, 1f, 0.5f, p);
            float thinGap = SensorNetworkRules.CoverageGap(thinCoverage, p);
            Assert.Greater(thinGap, denseGap, "薄い網のほうが索敵網の穴が大きい");

            // 密な網は冗長で、一部喪失にも強い
            Assert.Greater(SensorNetworkRules.Redundancy(16, p), SensorNetworkRules.Redundancy(2, p));

            // データ統合で密な網の精度は単独センサーを上回る
            float fused = SensorNetworkRules.DataFusionAccuracy(16, 0.5f, p);
            Assert.Greater(fused, 0.5f, "統合で精度が個別を上回る");

            // ただし強く照射するほど被探知リスクが上がる（撃てば見つかる）
            float lowPowerRisk = SensorNetworkRules.CounterDetectionRisk(0.3f, 0.7f, p);
            float highPowerRisk = SensorNetworkRules.CounterDetectionRisk(0.9f, 0.7f, p);
            Assert.Greater(highPowerRisk, lowPowerRisk, "出力を上げると被探知が増える");

            // 探知距離内に入った侵入者は、密な網なら高い精度で捉える（roll 注入の決定論判定）
            float range = SensorNetworkRules.DetectionRange(1f, 4f, p); // 1*2 = 2
            Assert.IsTrue(SensorNetworkRules.IsTargetDetected(range, 0.5f, 0.5f), "近距離は探知");
        }
    }
}
