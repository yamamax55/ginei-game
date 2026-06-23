using NUnit.Framework;
using UnityEngine;

namespace Ginei.Tests
{
    /// <summary>戦力投射（PowerProjectionRules）の純ロジック検証。既定 Params の具体値で期待値を固定。</summary>
    public class PowerProjectionRulesTests
    {
        const float Eps = 1e-4f;

        [Test]
        public void ProjectableForce_TotalTimesReadiness()
        {
            // 1000 × 0.8 = 800
            Assert.AreEqual(800f, PowerProjectionRules.ProjectableForce(1000f, 0.8f), Eps);
        }

        [Test]
        public void ProjectableForce_ClampsInputs()
        {
            // 負の総戦力は0・即応>1は1へ
            Assert.AreEqual(0f, PowerProjectionRules.ProjectableForce(-500f, 0.5f), Eps);
            Assert.AreEqual(1000f, PowerProjectionRules.ProjectableForce(1000f, 2f), Eps);
        }

        [Test]
        public void DistanceAttenuation_HyperbolicFalloff()
        {
            // 800/(1 + 2×0.5) = 800/2 = 400・距離0は減衰なし
            Assert.AreEqual(400f, PowerProjectionRules.DistanceAttenuation(800f, 2f), Eps);
            Assert.AreEqual(800f, PowerProjectionRules.DistanceAttenuation(800f, 0f), Eps);
        }

        [Test]
        public void SupplyLineStrain_DistanceOverCapacity()
        {
            // 2/4 = 0.5
            Assert.AreEqual(0.5f, PowerProjectionRules.SupplyLineStrain(2f, 4f), Eps);
            // 兵站皆無＝最大負荷（既定 maxStrain=1.0）
            Assert.AreEqual(1f, PowerProjectionRules.SupplyLineStrain(5f, 0f), Eps);
            // 過大な比は maxStrain で頭打ち
            Assert.AreEqual(1f, PowerProjectionRules.SupplyLineStrain(100f, 1f), Eps);
        }

        [Test]
        public void SustainedPresence_ReducedByStrain()
        {
            // 400 × (1 - 0.5) = 200
            Assert.AreEqual(200f, PowerProjectionRules.SustainedPresence(400f, 0.5f), Eps);
            // 補給途絶（負荷1.0）で維持戦力ゼロ
            Assert.AreEqual(0f, PowerProjectionRules.SustainedPresence(400f, 1f), Eps);
        }

        [Test]
        public void ProjectionDuration_ScaledByHomelandSupport()
        {
            // 200 × 0.5 × 1.0 = 100
            Assert.AreEqual(100f, PowerProjectionRules.ProjectionDuration(200f, 0.5f), Eps);
            // 本国支援ゼロで持続時間ゼロ
            Assert.AreEqual(0f, PowerProjectionRules.ProjectionDuration(200f, 0f), Eps);
        }

        [Test]
        public void DeterrentEffect_RequiresPerception()
        {
            // 200 × 0.5 × 1.0 = 100
            Assert.AreEqual(100f, PowerProjectionRules.DeterrentEffect(200f, 0.5f), Eps);
            // 相手が認識しなければ抑止ゼロ
            Assert.AreEqual(0f, PowerProjectionRules.DeterrentEffect(200f, 0f), Eps);
        }

        [Test]
        public void OverextensionRisk_StrainTimesVulnerability()
        {
            // 0.5 × 0.4 × 1.0 = 0.2
            Assert.AreEqual(0.2f, PowerProjectionRules.OverextensionRisk(0.5f, 0.4f), Eps);
            // 0..1 にクランプ
            Assert.AreEqual(1f, PowerProjectionRules.OverextensionRisk(2f, 2f), Eps);
        }

        [Test]
        public void ProjectionValue_BenefitMinusRisk()
        {
            // presence=0.8, deterrent=0.5 → 便益0.4・risk0.2 → 0.2
            Assert.AreEqual(0.2f, PowerProjectionRules.ProjectionValue(0.8f, 0.5f, 0.2f), Eps);
            // 便益薄く危険大なら負値（撤収の合図）
            Assert.AreEqual(-0.7f, PowerProjectionRules.ProjectionValue(0.2f, 0.5f, 0.8f), Eps);
            // 符号付き出力は -1..1
            Assert.AreEqual(-1f, PowerProjectionRules.ProjectionValue(0f, 0f, 5f), Eps);
        }

        [Test]
        public void Narrative_OverextendedExpeditionTurnsNegative()
        {
            // 物語：本国から遠く（距離8）兵站の細い（能力2）遠征。総戦力1000・遠征即応0.6。
            var p = PowerProjectionParams.Default;

            float projectable = PowerProjectionRules.ProjectableForce(1000f, 0.6f, p); // 600
            Assert.AreEqual(600f, projectable, Eps);

            float arrived = PowerProjectionRules.DistanceAttenuation(projectable, 8f, p); // 600/(1+8×0.5)=600/5=120
            Assert.AreEqual(120f, arrived, Eps);

            float strain = PowerProjectionRules.SupplyLineStrain(8f, 2f, p); // 8/2=4 → maxStrain で1.0に頭打ち
            Assert.AreEqual(1f, strain, Eps);

            float presence = PowerProjectionRules.SustainedPresence(arrived, strain, p); // 120×(1-1)=0
            Assert.AreEqual(0f, presence, Eps);

            // 補給線が崩壊し現地に戦力を維持できない＝持続も抑止も成立しない
            float duration = PowerProjectionRules.ProjectionDuration(presence, 0.9f, p);
            Assert.AreEqual(0f, duration, Eps);
            float deterrent = PowerProjectionRules.DeterrentEffect(presence, 0.8f, p);
            Assert.AreEqual(0f, deterrent, Eps);

            // 補給逼迫(1.0)＋本国の手薄(0.7) → 過剰投射リスク0.7
            float risk = PowerProjectionRules.OverextensionRisk(strain, 0.7f, p);
            Assert.AreEqual(0.7f, risk, Eps);

            // 便益ゼロ−リスク0.7 → 負の価値＝この遠征は割に合わない
            float value = PowerProjectionRules.ProjectionValue(presence, deterrent, risk, p);
            Assert.AreEqual(-0.7f, value, Eps);
            Assert.Less(value, 0f);
        }
    }
}
