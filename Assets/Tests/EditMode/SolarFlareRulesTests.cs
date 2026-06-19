using NUnit.Framework;
using UnityEngine;

namespace Ginei.Tests
{
    /// <summary>
    /// 恒星フレア／宇宙嵐ロジックの EditMode テスト。既定 Params 期待値を手計算で固定。
    /// </summary>
    public class SolarFlareRulesTests
    {
        private const float Eps = 1e-4f;

        [Test]
        public void FlareIntensity_ActivityAndProximityWeightedSum()
        {
            // 0.8*0.7 + 0.6*0.5 = 0.56 + 0.30 = 0.86
            Assert.AreEqual(0.86f, SolarFlareRules.FlareIntensity(0.8f, 0.6f), Eps);
        }

        [Test]
        public void FlareIntensity_ClampsToOne()
        {
            // 1*0.7 + 1*0.5 = 1.2 → 1.0
            Assert.AreEqual(1f, SolarFlareRules.FlareIntensity(1f, 1f), Eps);
            // 入力 clamp：負/超過でも崩れない
            Assert.AreEqual(0f, SolarFlareRules.FlareIntensity(-5f, -5f), Eps);
        }

        [Test]
        public void WarningTime_DetectionAndDistanceScaleSeconds()
        {
            // 120 * (0.5*0.6 + 0.4*0.5) = 120 * 0.5 = 60
            Assert.AreEqual(60f, SolarFlareRules.WarningTime(0.5f, 0.4f), Eps);
            // 探知/距離が最大なら上限へ（0.6+0.5=1.1→clamp1.0）
            Assert.AreEqual(120f, SolarFlareRules.WarningTime(1f, 1f), Eps);
            // 全く探知できなければ猶予ゼロ＝不意打ち
            Assert.AreEqual(0f, SolarFlareRules.WarningTime(0f, 0f), Eps);
        }

        [Test]
        public void EmpDisruption_ScalesWithIntensityAndInverseHardening()
        {
            // 0.86 * 0.9 * (1-0.3) = 0.774 * 0.7 = 0.5418
            Assert.AreEqual(0.5418f, SolarFlareRules.EmpDisruption(0.86f, 0.3f), Eps);
            // 完全耐性なら障害ゼロ
            Assert.AreEqual(0f, SolarFlareRules.EmpDisruption(1f, 1f), Eps);
        }

        [Test]
        public void CommunicationBlackout_ProportionalToIntensity()
        {
            // 0.86 * 0.8 = 0.688
            Assert.AreEqual(0.688f, SolarFlareRules.CommunicationBlackout(0.86f), Eps);
        }

        [Test]
        public void ShelterMitigation_CoverReducesEffectiveDisruption()
        {
            // 0.8 * (1 - 0.5*0.8) = 0.8 * 0.6 = 0.48
            Assert.AreEqual(0.48f, SolarFlareRules.ShelterMitigation(0.5f, 0.8f), Eps);
            // 遮蔽なしなら素のフレア強度のまま
            Assert.AreEqual(0.8f, SolarFlareRules.ShelterMitigation(0f, 0.8f), Eps);
        }

        [Test]
        public void FlareExploitation_PositiveWhenEnemyMoreVulnerable()
        {
            // own 固い(0.8)・enemy 脆い(0.2)：adv = 0.8 - 0.2 = 0.6
            // 0.5 * 0.6 * 0.8 = 0.24
            Assert.AreEqual(0.24f, SolarFlareRules.FlareExploitation(0.5f, 0.8f, 0.2f), Eps);
        }

        [Test]
        public void FlareExploitation_NegativeWhenSelfMoreVulnerable_AndSymmetricZero()
        {
            // own 脆い(0.2)・enemy 固い(0.8)：adv = 0.2 - 0.8 = -0.6 → -0.24
            Assert.AreEqual(-0.24f, SolarFlareRules.FlareExploitation(0.5f, 0.2f, 0.8f), Eps);
            // 耐性が等しければ好機なし
            Assert.AreEqual(0f, SolarFlareRules.FlareExploitation(0.5f, 0.5f, 0.5f), Eps);
        }

        [Test]
        public void StormDuration_ProportionalToIntensity()
        {
            // 90 * 0.86 = 77.4
            Assert.AreEqual(77.4f, SolarFlareRules.StormDuration(0.86f), Eps);
            Assert.AreEqual(0f, SolarFlareRules.StormDuration(0f), Eps);
        }

        [Test]
        public void IsSystemsCritical_ThresholdAtDefault()
        {
            // 既定しきい値 0.6
            Assert.IsFalse(SolarFlareRules.IsSystemsCritical(0.5418f));
            Assert.IsTrue(SolarFlareRules.IsSystemsCritical(0.7f));
            // 明示しきい値委譲
            Assert.IsTrue(SolarFlareRules.IsSystemsCritical(0.5f, 0.5f));
        }

        [Test]
        public void Narrative_AmbushThroughTheStorm()
        {
            // 活動的な恒星のすぐ傍で会戦＝強いフレアが立つ。
            float intensity = SolarFlareRules.FlareIntensity(0.9f, 0.8f);
            // 0.9*0.7 + 0.8*0.5 = 0.63 + 0.40 = 1.03 → 1.0
            Assert.AreEqual(1f, intensity, Eps);

            // 探知が貧弱で距離も近い＝ほぼ不意打ち（猶予が短い）。
            float warning = SolarFlareRules.WarningTime(0.1f, 0.1f);
            // 120 * (0.1*0.6 + 0.1*0.5) = 120 * 0.11 = 13.2
            Assert.AreEqual(13.2f, warning, Eps);

            // 耐性の低い敵は電磁パルスで麻痺し、自軍は遮蔽の陰で被害を抑える。
            float enemyEmp = SolarFlareRules.EmpDisruption(intensity, 0.1f); // 1*0.9*0.9 = 0.81
            Assert.AreEqual(0.81f, enemyEmp, Eps);
            Assert.IsTrue(SolarFlareRules.IsSystemsCritical(enemyEmp), "敵システムは危機的");

            float ownEffective = SolarFlareRules.ShelterMitigation(0.9f, intensity); // 1*(1-0.9*0.8)=0.28
            Assert.AreEqual(0.28f, ownEffective, Eps);

            // 敵が脆く(0.1)自軍が固い(0.8)＝フレアを衝く好機（正）。
            float chance = SolarFlareRules.FlareExploitation(enemyEmp, 0.8f, 0.1f);
            // adv = (1-0.1) - (1-0.8) = 0.9 - 0.2 = 0.7 → 0.81*0.7*0.8 = 0.4536
            Assert.AreEqual(0.4536f, chance, Eps);
            Assert.Greater(chance, 0f, "嵐に紛れた奇襲は有利");

            // 嵐は強度に比例してしばらく続く。
            float storm = SolarFlareRules.StormDuration(intensity); // 90*1 = 90
            Assert.AreEqual(90f, storm, Eps);
        }
    }
}
