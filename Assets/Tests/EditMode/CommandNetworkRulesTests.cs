using NUnit.Framework;
using UnityEngine;

namespace Ginei.Tests
{
    /// <summary>CommandNetworkRules（指揮通信網＝C3I の堅牢性）の純ロジック検証。既定 Params で期待値を固定。</summary>
    public class CommandNetworkRulesTests
    {
        const float Eps = 1e-4f;
        const float PowEps = 1e-3f;

        [Test]
        public void NetworkConnectivity_NodesUnderSqrtTimesDensity()
        {
            // clamp01(0.25 * 4^0.5 * 0.8) = clamp01(0.25 * 2 * 0.8) = 0.4
            Assert.AreEqual(0.4f, CommandNetworkRules.NetworkConnectivity(4, 0.8f), PowEps);
            // ノード0は接続性0
            Assert.AreEqual(0f, CommandNetworkRules.NetworkConnectivity(0, 1f), Eps);
        }

        [Test]
        public void Redundancy_AlternativePathsRaiseRobustness()
        {
            // 0.8 * (1 - 0.5^3) = 0.8 * (1 - 0.125) = 0.8 * 0.875 = 0.7
            Assert.AreEqual(0.7f, CommandNetworkRules.Redundancy(0.8f, 3), PowEps);
            // 代替経路0は冗長性0
            Assert.AreEqual(0f, CommandNetworkRules.Redundancy(0.8f, 0), Eps);
        }

        [Test]
        public void CentralNodeVulnerability_CentralizationIsTheRisk()
        {
            // 既定の集中指数1 では集中度がそのまま脆弱性
            Assert.AreEqual(0.7f, CommandNetworkRules.CentralNodeVulnerability(0.7f), Eps);
            // 完全分散（集中度0）は脆弱性0
            Assert.AreEqual(0f, CommandNetworkRules.CentralNodeVulnerability(0f), Eps);
        }

        [Test]
        public void ResilienceToLoss_RedundancyTimesNotVulnerable()
        {
            // 0.8 * (1 - 0.5) = 0.4
            Assert.AreEqual(0.4f, CommandNetworkRules.ResilienceToLoss(0.8f, 0.5f), Eps);
            // 脆弱性が最大なら耐性0
            Assert.AreEqual(0f, CommandNetworkRules.ResilienceToLoss(0.8f, 1f), Eps);
        }

        [Test]
        public void JammingDegradation_TugOfWarWithConnectivity()
        {
            // 0.6 / (0.6 + 0.6) = 0.5（拮抗）
            Assert.AreEqual(0.5f, CommandNetworkRules.JammingDegradation(0.6f, 0.6f), Eps);
            // 妨害0なら劣化0
            Assert.AreEqual(0f, CommandNetworkRules.JammingDegradation(0f, 0.6f), Eps);
            // 妨害も接続性も0なら劣化0（ゼロ割回避）
            Assert.AreEqual(0f, CommandNetworkRules.JammingDegradation(0f, 0f), Eps);
        }

        [Test]
        public void RecoveryCapacity_RedundancyTimesReserves()
        {
            // 1 * 0.8 * 0.5 = 0.4
            Assert.AreEqual(0.4f, CommandNetworkRules.RecoveryCapacity(0.8f, 0.5f), Eps);
        }

        [Test]
        public void EffectiveCommandReach_ProductOfAllThree()
        {
            // 0.8 * (1 - 0.5) * 0.5 = 0.8 * 0.5 * 0.5 = 0.2
            Assert.AreEqual(0.2f, CommandNetworkRules.EffectiveCommandReach(0.8f, 0.5f, 0.5f), Eps);
            // どれか一つが0なら全体も0
            Assert.AreEqual(0f, CommandNetworkRules.EffectiveCommandReach(0f, 0.5f, 0.5f), Eps);
        }

        [Test]
        public void IsNetworkOperational_ThresholdGate()
        {
            // 既定閾値0.3：以上で機能、未満で分断
            Assert.IsTrue(CommandNetworkRules.IsNetworkOperational(0.3f));
            Assert.IsFalse(CommandNetworkRules.IsNetworkOperational(0.29f));
            // 明示閾値版
            Assert.IsTrue(CommandNetworkRules.IsNetworkOperational(0.5f, 0.5f));
            Assert.IsFalse(CommandNetworkRules.IsNetworkOperational(0.4f, 0.5f));
        }

        [Test]
        public void Story_DistributedNetHoldsUnderJammingCentralizedCollapses()
        {
            var p = CommandNetworkParams.Default;

            // 同じ規模・密度の網。片方は分散（集中度低・代替経路多）、片方は中央集権（集中度高・代替経路少）。
            float connDist = CommandNetworkRules.NetworkConnectivity(9, 0.9f, p); // 0.25*3*0.9 = 0.675
            float connCent = CommandNetworkRules.NetworkConnectivity(9, 0.9f, p);
            Assert.AreEqual(connDist, connCent, Eps, "接続性は同条件なら等しい");

            // 分散網：代替経路が多く冗長で、中枢を撃たれても崩れにくい
            float redDist = CommandNetworkRules.Redundancy(0.9f, 5, p);
            float vulnDist = CommandNetworkRules.CentralNodeVulnerability(0.2f, p);
            float resDist = CommandNetworkRules.ResilienceToLoss(redDist, vulnDist, p);

            // 中央集権網：代替経路が乏しく、中枢への依存度が高くて脆い
            float redCent = CommandNetworkRules.Redundancy(0.9f, 1, p);
            float vulnCent = CommandNetworkRules.CentralNodeVulnerability(0.9f, p);
            float resCent = CommandNetworkRules.ResilienceToLoss(redCent, vulnCent, p);

            Assert.Greater(resDist, resCent, "分散網のほうが中枢喪失への耐性が高い");

            // 敵の電子妨害下：両者は同じ妨害劣化を受ける（接続性が同じため）
            float deg = CommandNetworkRules.JammingDegradation(0.5f, connDist, p);

            // 実効指揮到達：分散網は妨害と中枢喪失の二重苦でも指揮を保つ
            float reachDist = CommandNetworkRules.EffectiveCommandReach(connDist, deg, resDist, p);
            float reachCent = CommandNetworkRules.EffectiveCommandReach(connCent, deg, resCent, p);
            Assert.Greater(reachDist, reachCent, "分散網のほうが実効指揮到達が大きい");

            // 分散網は妨害と中枢喪失の二重苦でも閾値0.2を超えて指揮を保つ
            Assert.IsTrue(CommandNetworkRules.IsNetworkOperational(reachDist, 0.2f),
                "分散網は妨害下でも指揮を保つ");

            // 復旧：冗長な分散網のほうが、技術予備が同じでも立て直しが速い
            float recDist = CommandNetworkRules.RecoveryCapacity(redDist, 0.6f, p);
            float recCent = CommandNetworkRules.RecoveryCapacity(redCent, 0.6f, p);
            Assert.Greater(recDist, recCent, "分散網のほうが復旧能力が高い");
        }
    }
}
