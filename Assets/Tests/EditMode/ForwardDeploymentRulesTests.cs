using NUnit.Framework;
using UnityEngine;

namespace Ginei.Tests
{
    /// <summary>前方展開の純ロジックのテスト（既定 ForwardDeploymentParams で期待値を固定）。</summary>
    public class ForwardDeploymentRulesTests
    {
        const float Tol = 1e-4f;
        const float PowTol = 1e-3f; // Pow を経由する箇所のみ緩める

        [Test]
        public void ForwardReadiness_ForceTimesAlert()
        {
            // 0.8 * 0.5 * 1(readinessWeight) = 0.4
            Assert.AreEqual(0.4f, ForwardDeploymentRules.ForwardReadiness(0.8f, 0.5f), Tol);
        }

        [Test]
        public void ForwardReadiness_ZeroForceOrAlert_IsZero()
        {
            Assert.AreEqual(0f, ForwardDeploymentRules.ForwardReadiness(0f, 1f), Tol);
            Assert.AreEqual(0f, ForwardDeploymentRules.ForwardReadiness(1f, 0f), Tol);
        }

        [Test]
        public void DeterrentPresence_VisibilityScalesFromMinToFull()
        {
            // visFactor = Lerp(0.3, 1, vis)
            Assert.AreEqual(1.0f, ForwardDeploymentRules.DeterrentPresence(1f, 1f), Tol); // 全可視＝全効果
            Assert.AreEqual(0.3f, ForwardDeploymentRules.DeterrentPresence(1f, 0f), Tol); // 不可視＝最低限
            Assert.AreEqual(0.65f, ForwardDeploymentRules.DeterrentPresence(1f, 0.5f), Tol);
        }

        [Test]
        public void TripwireEffect_SaturatesAtMax()
        {
            // 0.5 * 0.6 = 0.30
            Assert.AreEqual(0.30f, ForwardDeploymentRules.TripwireEffect(0.5f, 0.6f), Tol);
            // 1*1 = 1 だが上限 maxTripwire=0.9 で頭打ち
            Assert.AreEqual(0.9f, ForwardDeploymentRules.TripwireEffect(1f, 1f), Tol);
            // 関与の意思ゼロなら効果なし
            Assert.AreEqual(0f, ForwardDeploymentRules.TripwireEffect(1f, 0f), Tol);
        }

        [Test]
        public void IsolationRisk_GrowsWithForceAndDistance()
        {
            // 0.8 * 0.5 * 1(isolationDistanceWeight) = 0.4
            Assert.AreEqual(0.4f, ForwardDeploymentRules.IsolationRisk(0.8f, 0.5f), Tol);
            // 増援距離ゼロ（隣接）なら孤立リスクなし
            Assert.AreEqual(0f, ForwardDeploymentRules.IsolationRisk(1f, 0f), Tol);
        }

        [Test]
        public void DeploymentCost_BaseCostPlusSustainmentDistance()
        {
            // 本国近傍：2 * (1 + 0*0.5) = 2
            Assert.AreEqual(2f, ForwardDeploymentRules.DeploymentCost(2f, 0f), Tol);
            // 遠距離：2 * (1 + 1*0.5) = 3
            Assert.AreEqual(3f, ForwardDeploymentRules.DeploymentCost(2f, 1f), Tol);
        }

        [Test]
        public void EscalationTension_NonlinearInReaction()
        {
            // raw = 0.8 * 0.5 * 0.8(reactionWeight) = 0.32 ; Pow(0.32, 2) = 0.1024
            Assert.AreEqual(0.1024f, ForwardDeploymentRules.EscalationTension(0.8f, 0.5f), PowTol);
            // 反応ゼロなら緊張なし
            Assert.AreEqual(0f, ForwardDeploymentRules.EscalationTension(1f, 0f), Tol);
        }

        [Test]
        public void RearVulnerability_HighReservesProtectRear()
        {
            // 0.8 * (1 - 0.2) = 0.64
            Assert.AreEqual(0.64f, ForwardDeploymentRules.RearVulnerability(0.8f, 0.2f), Tol);
            // 後方予備が満ちていれば手薄にならない
            Assert.AreEqual(0f, ForwardDeploymentRules.RearVulnerability(1f, 1f), Tol);
        }

        [Test]
        public void ForwardDeploymentValue_DeterrenceMinusCosts()
        {
            // 0.8 - 0.3*0.6(isolationPenaltyWeight) - 0.4*0.5(rearPenaltyWeight)
            //   = 0.8 - 0.18 - 0.2 = 0.42
            Assert.AreEqual(0.42f, ForwardDeploymentRules.ForwardDeploymentValue(0.8f, 0.3f, 0.4f), Tol);
        }

        [Test]
        public void ForwardDeploymentValue_ClampedToSignedRange()
        {
            // 抑止ゼロ・孤立と後方手薄が最大＝負へ振れるが -1 でクランプ
            Assert.AreEqual(-1f, ForwardDeploymentRules.ForwardDeploymentValue(0f, 1f, 1f), Tol);
        }

        [Test]
        public void InputsAreClamped()
        {
            // 負入力・超過入力は内部 clamp で正常域に丸まる
            Assert.AreEqual(0f, ForwardDeploymentRules.ForwardReadiness(-5f, 0.5f), Tol);
            // visibility 超過は 1 扱い＝全効果
            Assert.AreEqual(1f, ForwardDeploymentRules.DeterrentPresence(1f, 5f), Tol);
            // 後方予備の負入力は 0 扱い＝最大の手薄
            Assert.AreEqual(1f, ForwardDeploymentRules.RearVulnerability(1f, -3f), Tol);
        }

        // 物語テスト：前線へ戦力を傾けすぎると、抑止は得るが孤立と後方手薄で割に合わなくなる。
        [Test]
        public void Narrative_OverCommittingForwardErodesValue()
        {
            var p = ForwardDeploymentParams.Default;

            // 慎重な前方展開：可視で抑止が立ち、増援も近く後方予備も厚い
            float prudentReadiness = ForwardDeploymentRules.ForwardReadiness(0.6f, 0.8f, p);
            float prudentDeter = ForwardDeploymentRules.DeterrentPresence(prudentReadiness, 0.9f, p);
            float prudentIsolation = ForwardDeploymentRules.IsolationRisk(0.4f, 0.3f, p);
            float prudentRear = ForwardDeploymentRules.RearVulnerability(0.4f, 0.7f, p);
            float prudentValue = ForwardDeploymentRules.ForwardDeploymentValue(
                prudentDeter, prudentIsolation, prudentRear, p);

            // 過度な前方傾注：突出して増援は遠く後方は空に近い
            float recklessReadiness = ForwardDeploymentRules.ForwardReadiness(0.95f, 0.9f, p);
            float recklessDeter = ForwardDeploymentRules.DeterrentPresence(recklessReadiness, 0.9f, p);
            float recklessIsolation = ForwardDeploymentRules.IsolationRisk(0.95f, 0.95f, p);
            float recklessRear = ForwardDeploymentRules.RearVulnerability(0.95f, 0.05f, p);
            float recklessValue = ForwardDeploymentRules.ForwardDeploymentValue(
                recklessDeter, recklessIsolation, recklessRear, p);

            // 過度な傾注は孤立・後方手薄が増え、慎重な展開より価値が低い
            Assert.Less(recklessValue, prudentValue);
            // 慎重な展開は割に合う（正の価値）
            Assert.Greater(prudentValue, 0f);
        }
    }
}
