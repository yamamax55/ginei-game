using NUnit.Framework;
using UnityEngine;

namespace Ginei.Tests
{
    /// <summary>CommunicationRelayRules（通信中継網＝リレー）の純ロジック検証。既定 Params で期待値を固定。</summary>
    public class CommunicationRelayRulesTests
    {
        const float Eps = 1e-4f;
        const float PowEps = 1e-3f;

        [Test]
        public void RelayCoverage_NodesTimesSpacing()
        {
            // 1 * 5 * 4 = 20
            Assert.AreEqual(20f, CommunicationRelayRules.RelayCoverage(5, 4f), Eps);
            // ノード0なら到達0
            Assert.AreEqual(0f, CommunicationRelayRules.RelayCoverage(0, 4f), Eps);
        }

        [Test]
        public void SignalAmplification_GainOverGainPlusDegradation()
        {
            // 3 / (3 + 1) = 0.75
            Assert.AreEqual(0.75f, CommunicationRelayRules.SignalAmplification(3f, 1f), Eps);
            // 両者0なら0（ゼロ割回避）
            Assert.AreEqual(0f, CommunicationRelayRules.SignalAmplification(0f, 0f), Eps);
        }

        [Test]
        public void AccumulatedLatency_HopsTimesPerHop()
        {
            // 1 * 6 * 0.5 = 3
            Assert.AreEqual(3f, CommunicationRelayRules.AccumulatedLatency(6, 0.5f), Eps);
            // 中継0なら遅延0
            Assert.AreEqual(0f, CommunicationRelayRules.AccumulatedLatency(0, 0.5f), Eps);
        }

        [Test]
        public void RelayVulnerability_ExposureTimesReach()
        {
            // clamp01(1 * 0.8 * 0.5) = 0.4
            Assert.AreEqual(0.4f, CommunicationRelayRules.RelayVulnerability(0.8f, 0.5f), Eps);
            // 敵が届かなければ脆弱性0
            Assert.AreEqual(0f, CommunicationRelayRules.RelayVulnerability(0.9f, 0f), Eps);
        }

        [Test]
        public void ReroutingCapacity_AltTimesMesh()
        {
            // clamp01(1 * 0.5 * 0.6) = 0.3
            Assert.AreEqual(0.3f, CommunicationRelayRules.ReroutingCapacity(0.5f, 0.6f), Eps);
            // 過大入力は clamp（2,2 → 1*1=1）
            Assert.AreEqual(1f, CommunicationRelayRules.ReroutingCapacity(2f, 2f), Eps);
        }

        [Test]
        public void NetworkSurvivability_RerouteTimesOneMinusVuln()
        {
            // clamp01(0.8 * (1 - 0.25)) = 0.8 * 0.75 = 0.6
            Assert.AreEqual(0.6f, CommunicationRelayRules.NetworkSurvivability(0.8f, 0.25f), Eps);
            // 完全に脆弱なら生存性0
            Assert.AreEqual(0f, CommunicationRelayRules.NetworkSurvivability(1f, 1f), Eps);
        }

        [Test]
        public void MaintenanceCost_NodesTimesTempo()
        {
            // 1 * 10 * 1.5 = 15
            Assert.AreEqual(15f, CommunicationRelayRules.MaintenanceCost(10, 1.5f), Eps);
            // 中継0なら維持コスト0
            Assert.AreEqual(0f, CommunicationRelayRules.MaintenanceCost(0, 1.5f), Eps);
        }

        [Test]
        public void RelayNetworkReach_ProductOfThree()
        {
            // clamp01(0.8 * 0.75 * 0.6) = 0.36
            Assert.AreEqual(0.36f, CommunicationRelayRules.RelayNetworkReach(0.8f, 0.75f, 0.6f), Eps);
            // どれか0なら実効到達0
            Assert.AreEqual(0f, CommunicationRelayRules.RelayNetworkReach(0.8f, 0f, 0.6f), Eps);
        }

        [Test]
        public void Story_RelayNetworkUnderEnemyPressureRoutesAround()
        {
            // 物語：前線へ伸びる中継網が敵の襲撃に晒されるが、密な網状の冗長で迂回して通信を保つ。
            // 露出した中継ノードは脆い：露出0.9・敵到達0.7 → clamp01(0.9*0.7)=0.63
            float vuln = CommunicationRelayRules.RelayVulnerability(0.9f, 0.7f);
            Assert.AreEqual(0.63f, vuln, Eps);

            // だが代替中継が豊富で網が密：迂回能力 clamp01(0.9*0.8)=0.72
            float reroute = CommunicationRelayRules.ReroutingCapacity(0.9f, 0.8f);
            Assert.AreEqual(0.72f, reroute, Eps);

            // 生存性＝迂回 × (1-脆弱)：0.72 * (1-0.63) = 0.72 * 0.37 = 0.2664
            float survive = CommunicationRelayRules.NetworkSurvivability(reroute, vuln);
            Assert.AreEqual(0.2664f, survive, Eps);

            // 信号は増幅が減衰に勝つ：4/(4+1)=0.8
            float signal = CommunicationRelayRules.SignalAmplification(4f, 1f);
            Assert.AreEqual(0.8f, signal, Eps);

            // 実効到達＝到達0.9 × 信号0.8 × 生存0.2664 = 0.191808
            float reach = CommunicationRelayRules.RelayNetworkReach(0.9f, signal, survive);
            Assert.AreEqual(0.191808f, reach, Eps);

            // 迂回が無いと脆さがそのまま響き、生存性は崩れる
            float fragileSurvive = CommunicationRelayRules.NetworkSurvivability(0f, vuln);
            Assert.AreEqual(0f, fragileSurvive, Eps);
            Assert.Less(fragileSurvive, survive);
        }
    }
}
