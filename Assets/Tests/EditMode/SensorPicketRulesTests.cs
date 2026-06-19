using NUnit.Framework;
using UnityEngine;

namespace Ginei.Tests
{
    /// <summary>SensorPicketRules（哨戒線・早期警戒）の純ロジック検証。既定 Params で期待値を固定。</summary>
    public class SensorPicketRulesTests
    {
        const float Eps = 1e-4f;
        const float PowEps = 1e-3f;

        [Test]
        public void WarningCoverage_CountUnderSqrt()
        {
            // 1 * 3 * 4^0.5 = 3 * 2 = 6
            Assert.AreEqual(6f, SensorPicketRules.WarningCoverage(4, 3f), PowEps);
        }

        [Test]
        public void DetectionLeadTime_CoverageOverSpeed()
        {
            // 6 / 2 = 3
            Assert.AreEqual(3f, SensorPicketRules.DetectionLeadTime(6f, 2f), Eps);
        }

        [Test]
        public void DetectionLeadTime_ZeroSpeedNoDivByZero()
        {
            Assert.IsFalse(float.IsInfinity(SensorPicketRules.DetectionLeadTime(6f, 0f)));
        }

        [Test]
        public void ReactionPreparation_LeadHalfTimeRaisesReadiness()
        {
            // 0.2 + (1-0.2) * (5/(5+5)) = 0.2 + 0.8*0.5 = 0.6
            Assert.AreEqual(0.6f, SensorPicketRules.ReactionPreparation(5f, 0.2f), Eps);
        }

        [Test]
        public void PicketVulnerability_WiderSpacingIsThinner()
        {
            // 1 * 10/(10+10) * 1 = 0.5
            Assert.AreEqual(0.5f, SensorPicketRules.PicketVulnerability(10f, 1f), Eps);
        }

        [Test]
        public void GapPenetration_SpacingTimesStealth()
        {
            // 10/(10+10) * 0.8 = 0.5 * 0.8 = 0.4
            Assert.AreEqual(0.4f, SensorPicketRules.GapPenetration(10f, 0.8f), Eps);
        }

        [Test]
        public void FalseAlarmRate_QualityAndClutter()
        {
            // (1-0.5) * (0.5*1 + 0.5) = 0.5 * 1.0 = 0.5
            Assert.AreEqual(0.5f, SensorPicketRules.FalseAlarmRate(0.5f, 1f), Eps);
        }

        [Test]
        public void EarlyWarningValue_DiscountedByFalseAlarms()
        {
            // 3 * (1-0.5) = 1.5
            Assert.AreEqual(1.5f, SensorPicketRules.EarlyWarningValue(3f, 0.5f), Eps);
        }

        [Test]
        public void IsScreenPenetrated_ThresholdBoundary()
        {
            Assert.IsFalse(SensorPicketRules.IsScreenPenetrated(0.4f, 0.5f));
            Assert.IsTrue(SensorPicketRules.IsScreenPenetrated(0.6f, 0.5f));
        }

        [Test]
        public void Story_WidePicketLineGivesWarningButIsFragile()
        {
            // 広い哨戒線（間隔8・3隻）と狭い哨戒線（間隔2・3隻）を比べる。
            var p = SensorPicketParams.Default;
            float coverageWide = SensorPicketRules.WarningCoverage(3, 8f, p);
            float coverageNarrow = SensorPicketRules.WarningCoverage(3, 2f, p);

            // 広い線ほど警戒範囲が広く、敵接近の猶予が長く、本隊が態勢を整えられる。
            Assert.Greater(coverageWide, coverageNarrow);
            float leadWide = SensorPicketRules.DetectionLeadTime(coverageWide, 2f);
            float leadNarrow = SensorPicketRules.DetectionLeadTime(coverageNarrow, 2f);
            Assert.Greater(leadWide, leadNarrow);
            Assert.Greater(
                SensorPicketRules.ReactionPreparation(leadWide, 0.2f, p),
                SensorPicketRules.ReactionPreparation(leadNarrow, 0.2f, p));

            // しかし広く薄く張った哨戒艦は脆く、隙を突かれやすい。
            Assert.Greater(
                SensorPicketRules.PicketVulnerability(8f, 0.8f, p),
                SensorPicketRules.PicketVulnerability(2f, 0.8f, p));
            Assert.Greater(
                SensorPicketRules.GapPenetration(8f, 0.6f, p),
                SensorPicketRules.GapPenetration(2f, 0.6f, p));
        }
    }
}
