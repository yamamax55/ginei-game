using NUnit.Framework;
using UnityEngine;

namespace Ginei.Tests
{
    /// <summary>
    /// 船団護衛（<see cref="ConvoyDefenseRules"/>）の純ロジックテスト。既定 <see cref="ConvoyDefenseParams.Default"/> で
    /// 期待値を固定（密度0.1底上げ・被覆指数0.5・被覆上限0.95・鈍重化0.4・速度下限0.4）。Pow/除算箇所のみ許容差を緩める。
    /// </summary>
    public class ConvoyDefenseRulesTests
    {
        const float Eps = 1e-4f;
        const float PowEps = 1e-3f;

        [Test]
        public void EscortDensity_IsEscortPerSizePlusFloor()
        {
            // 60/120 + 0.1（自衛底上げ） = 0.6
            Assert.AreEqual(0.6f, ConvoyDefenseRules.EscortDensity(60f, 120f), Eps);
        }

        [Test]
        public void EscortDensity_ZeroConvoyIsZero()
        {
            Assert.AreEqual(0f, ConvoyDefenseRules.EscortDensity(50f, 0f), Eps);
        }

        [Test]
        public void ScreenCoverage_ThinsWithDispersion()
        {
            // pow(4,0.5)/(1+3) = 2/4 = 0.5
            Assert.AreEqual(0.5f, ConvoyDefenseRules.ScreenCoverage(4f, 3f), PowEps);
        }

        [Test]
        public void ScreenCoverage_ClampedToMax()
        {
            // sqrt(100)/1 = 10 → 上限0.95
            Assert.AreEqual(0.95f, ConvoyDefenseRules.ScreenCoverage(100f, 0f), Eps);
        }

        [Test]
        public void RaiderRepulse_RatioOfCoverageToThreat()
        {
            // 0.8/(0.8+0.2) = 0.8
            Assert.AreEqual(0.8f, ConvoyDefenseRules.RaiderRepulse(0.8f, 0.2f), Eps);
        }

        [Test]
        public void LossesIfRaided_CappedAtConvoySize()
        {
            // 200*(1-0) = 200 → 規模120で頭打ち（壊滅以上は出さない）
            Assert.AreEqual(120f, ConvoyDefenseRules.LossesIfRaided(200f, 0f, 120f), Eps);
        }

        [Test]
        public void ConvoySpeed_HeavyEscortHitsFloor()
        {
            // 1/(1+0.4*100) → 下限0.4 → 10*0.4 = 4
            Assert.AreEqual(4f, ConvoyDefenseRules.ConvoySpeed(100f, 10f), Eps);
        }

        [Test]
        public void DispersalVsConcentration_HighThreatLeansConcentration()
        {
            // 分散はマイナス、集結はプラス
            Assert.AreEqual(-0.6f, ConvoyDefenseRules.DispersalVsConcentration(0.2f, 0f), Eps);
            // (0.9-0.5)*2 + (10/20)*0.2 = 0.8 + 0.1 = 0.9
            Assert.AreEqual(0.9f, ConvoyDefenseRules.DispersalVsConcentration(0.9f, 10f), Eps);
        }

        [Test]
        public void EscortAttrition_ConcentratesOnDefenders()
        {
            // 30^2/(30+60) = 900/90 = 10
            Assert.AreEqual(10f, ConvoyDefenseRules.EscortAttrition(30f, 60f), Eps);
        }

        [Test]
        public void IsConvoySafe_TrueWhenRepulseAboveThreshold()
        {
            Assert.IsTrue(ConvoyDefenseRules.IsConvoySafe(0.8f, 0.2f, 0.5f));
            Assert.IsFalse(ConvoyDefenseRules.IsConvoySafe(0.05f, 2f, 0.5f));
        }

        [Test]
        public void Narrative_DenseEscortRepelsThinGetsEatenHeavySlows()
        {
            // 同じ船団規模・同じ襲撃に対し、密な護衛と薄い護衛を比べる。
            const float size = 120f;
            const float raider = 20f;

            float denseDensity = ConvoyDefenseRules.EscortDensity(60f, size); // 0.6
            float thinDensity = ConvoyDefenseRules.EscortDensity(5f, size);   // 0.0417+0.1

            float denseCoverage = ConvoyDefenseRules.ScreenCoverage(denseDensity, 0f);
            float thinCoverage = ConvoyDefenseRules.ScreenCoverage(thinDensity, 0f);

            // 密な護衛のほうが傘が厚い。
            Assert.Greater(denseCoverage, thinCoverage);

            float denseLosses = ConvoyDefenseRules.LossesIfRaided(raider, denseCoverage, size);
            float thinLosses = ConvoyDefenseRules.LossesIfRaided(raider, thinCoverage, size);

            // 薄い護衛のほうが食われる（損害大）。
            Assert.Greater(thinLosses, denseLosses);

            // 密な護衛は襲撃を撥ね返し安全、薄い護衛は危険。
            Assert.IsTrue(ConvoyDefenseRules.IsConvoySafe(denseCoverage, raider * 0.01f));
            Assert.IsFalse(ConvoyDefenseRules.IsConvoySafe(thinCoverage, raider));

            // だが固めすぎると鈍重＝護衛が重いほど遅い（トレードオフ）。
            float lightSpeed = ConvoyDefenseRules.ConvoySpeed(2f, 10f);
            float heavySpeed = ConvoyDefenseRules.ConvoySpeed(20f, 10f);
            Assert.Greater(lightSpeed, heavySpeed);
        }
    }
}
