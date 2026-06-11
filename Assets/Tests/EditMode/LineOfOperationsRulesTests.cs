using NUnit.Framework;
using UnityEngine;

namespace Ginei.Tests
{
    public class LineOfOperationsRulesTests
    {
        const float Tol = 1e-4f;

        [Test]
        public void LineLength_ScalesDistanceByLengthWeight()
        {
            // 既定 lengthWeight=0.5 → 0.8 * (0.5 + 0.5*0.5) = 0.8 * 0.75 = 0.6
            Assert.AreEqual(0.6f, LineOfOperationsRules.LineLength(0.8f), Tol);
            // 距離0なら長さ0
            Assert.AreEqual(0f, LineOfOperationsRules.LineLength(0f), Tol);
        }

        [Test]
        public void ExposureThreats_AveragesAndScalesByLength()
        {
            // avg(0.6,0.8)=0.7 → *(0.5+0.5*0.6)=0.7*0.8=0.56
            float[] threats = { 0.6f, 0.8f };
            Assert.AreEqual(0.56f, LineOfOperationsRules.ExposureThreats(threats, 0.6f), Tol);
        }

        [Test]
        public void ExposureThreats_NullAndEmptyAreZero()
        {
            Assert.AreEqual(0f, LineOfOperationsRules.ExposureThreats(null, 0.9f), Tol);
            Assert.AreEqual(0f, LineOfOperationsRules.ExposureThreats(new float[0], 0.9f), Tol);
        }

        [Test]
        public void LineVulnerability_WeightedCombineOfLengthAndExposure()
        {
            // (0.6*0.5 + 0.56*0.6)/(0.5+0.6) = (0.3+0.336)/1.1 = 0.578181...
            float v = LineOfOperationsRules.LineVulnerability(0.6f, 0.56f);
            Assert.AreEqual(0.6360f / 1.1f, v, Tol);
        }

        [Test]
        public void SupplyThroughputLoss_TracksVulnerability()
        {
            Assert.AreEqual(0.7f, LineOfOperationsRules.SupplyThroughputLoss(0.7f), Tol);
            Assert.AreEqual(0f, LineOfOperationsRules.SupplyThroughputLoss(0f), Tol);
        }

        [Test]
        public void FlankingRisk_ScreenForceMitigatesUpToCap()
        {
            // mitigation = 0.5*0.7 = 0.35 → 0.8*(1-0.35) = 0.52
            Assert.AreEqual(0.52f, LineOfOperationsRules.FlankingRisk(0.8f, 0.5f), Tol);
            // 掩護ゼロなら晒され度そのまま
            Assert.AreEqual(0.8f, LineOfOperationsRules.FlankingRisk(0.8f, 0f), Tol);
        }

        [Test]
        public void LineSecurityInvestment_LongerLineDilutesGarrison()
        {
            // perUnit = 0.6/(1+0.6)=0.375 → *0.5*2 = 0.375
            float shortLine = LineOfOperationsRules.LineSecurityInvestment(0.6f, 0.6f);
            Assert.AreEqual(0.375f, shortLine, Tol);
            // 同じ守備でも線が長いほど安全度が薄まる（割高）
            float longLine = LineOfOperationsRules.LineSecurityInvestment(0.6f, 1.0f);
            Assert.Less(longLine, shortLine);
        }

        [Test]
        public void SingleVsDoubleLine_ConcentrationVsRedundancy()
        {
            // 単一線＝集中の利 0.8*0.4 = 0.32（正＝単一有利）
            Assert.AreEqual(0.32f, LineOfOperationsRules.SingleVsDoubleLine(1, 0.8f), Tol);
            // 複数線＝conc(0.4*0.4=0.16) - redundancy((1-0.5)*0.5=0.25) = -0.09（負＝複数有利）
            Assert.AreEqual(-0.09f, LineOfOperationsRules.SingleVsDoubleLine(2, 0.8f), Tol);
        }

        [Test]
        public void IsVulnerableLine_ThresholdCheck()
        {
            Assert.IsTrue(LineOfOperationsRules.IsVulnerableLine(0.7f, 0.6f));
            Assert.IsFalse(LineOfOperationsRules.IsVulnerableLine(0.5f, 0.6f));
        }

        [Test]
        public void Narrative_OverextendedLineBleedsSupplyAndScreenDefends()
        {
            // 物語：基地から遠い目標へ作戦線を長く伸ばす
            float length = LineOfOperationsRules.LineLength(0.9f); // 0.9*0.75 = 0.675
            Assert.AreEqual(0.675f, length, Tol);

            // 経路上に敵接触が多い＝晒され度が高い
            float[] threats = { 0.7f, 0.8f, 0.9f };
            float exposure = LineOfOperationsRules.ExposureThreats(threats, length);
            // avg=0.8 → *(0.5+0.5*0.675)=0.8*0.8375=0.67
            Assert.AreEqual(0.67f, exposure, Tol);

            // 長く伸びて晒される作戦線は脆弱
            float vuln = LineOfOperationsRules.LineVulnerability(length, exposure);
            Assert.IsTrue(LineOfOperationsRules.IsVulnerableLine(vuln, 0.6f),
                "長く伸びて敵接触に晒される作戦線は危険な脆弱性に達する");

            // 脆弱な作戦線ほど補給が細る（連絡線を断たれる）
            float loss = LineOfOperationsRules.SupplyThroughputLoss(vuln);
            Assert.Greater(loss, 0.6f, "脆弱な作戦線は補給が大きく細る");

            // 掩護兵力（スクリーン）を厚くすると側面を突かれるリスクが下がる＝守れる
            float riskNoScreen = LineOfOperationsRules.FlankingRisk(exposure, 0f);
            float riskScreened = LineOfOperationsRules.FlankingRisk(exposure, 0.9f);
            Assert.Less(riskScreened, riskNoScreen, "掩護兵力を厚くすると側面突破リスクが下がる");

            // 作戦線沿いの守備で安全度を買える（脆弱性是正の手）
            float security = LineOfOperationsRules.LineSecurityInvestment(0.8f, length);
            Assert.Greater(security, 0f, "守備で安全度を買える");
        }
    }
}
