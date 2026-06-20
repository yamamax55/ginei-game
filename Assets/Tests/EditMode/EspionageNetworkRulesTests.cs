using NUnit.Framework;
using UnityEngine;

namespace Ginei.Tests
{
    /// <summary>
    /// EspionageNetworkRules（諜報網・HUMINT）の純ロジックテスト。既定 Params で期待値を固定。
    /// 全乗算は 1e-4f、Pow を含む到達範囲のみ 1e-3f に緩める。
    /// </summary>
    public class EspionageNetworkRulesTests
    {
        const float Tol = 1e-4f;
        const float PowTol = 1e-3f;

        [Test]
        public void NetworkReach_CountSaturatesAndQualityBoosts()
        {
            // 飽和工作員数50で countNorm=1, Pow(1,0.5)=1, 質係数=0.5+0.5×0.6=0.8 → 0.8
            Assert.AreEqual(0.8f, EspionageNetworkRules.NetworkReach(50f, 0.6f), PowTol);
            // 工作員12.5で countNorm=0.25, Pow(0.25,0.5)=0.5, 質係数=1.0 → 0.5（収穫逓減＝少数でも一定の網）
            Assert.AreEqual(0.5f, EspionageNetworkRules.NetworkReach(12.5f, 1f), PowTol);
        }

        [Test]
        public void IntelYield_BlockedBySecurity()
        {
            // 到達0.8 × (1 − 防諜0.25) = 0.8 × 0.75 = 0.6
            Assert.AreEqual(0.6f, EspionageNetworkRules.IntelYield(0.8f, 0.25f), Tol);
            // 完璧な秘匿（1.0）なら機密へ届かない
            Assert.AreEqual(0f, EspionageNetworkRules.IntelYield(0.8f, 1f), Tol);
        }

        [Test]
        public void ExposureRisk_GrowsWithReachAndCounterintel()
        {
            // 到達0.8 × 防諜0.5 × 1.0 = 0.4
            Assert.AreEqual(0.4f, EspionageNetworkRules.ExposureRisk(0.8f, 0.5f), Tol);
            // 大きい網ほど目立つ（reach小は露見も小）
            Assert.Less(EspionageNetworkRules.ExposureRisk(0.3f, 0.5f),
                        EspionageNetworkRules.ExposureRisk(0.8f, 0.5f));
        }

        [Test]
        public void NetworkAttrition_LosesAgentsOverTime()
        {
            // 露見0.4 × 摘発率0.5 × dt1 = 0.2
            Assert.AreEqual(0.2f, EspionageNetworkRules.NetworkAttrition(0.4f, 1f), Tol);
            // dt=0 なら損耗なし
            Assert.AreEqual(0f, EspionageNetworkRules.NetworkAttrition(0.4f, 0f), Tol);
        }

        [Test]
        public void DeepCoverValue_FloorPlusEmbeddedness()
        {
            // 下駄0.3 + 0.7×潜入0.5 = 0.65
            Assert.AreEqual(0.65f, EspionageNetworkRules.DeepCoverValue(0.5f), Tol);
            // 潜入度0でも下駄ぶんの価値、深く潜るほど価値増（0.3 < 1.0）
            Assert.AreEqual(0.3f, EspionageNetworkRules.DeepCoverValue(0f), Tol);
            Assert.AreEqual(1f, EspionageNetworkRules.DeepCoverValue(1f), Tol);
        }

        [Test]
        public void NetworkBuildTime_SizeOverRecruitment()
        {
            // 規模20 / 勧誘速度4 = 5
            Assert.AreEqual(5f, EspionageNetworkRules.NetworkBuildTime(20f, 4f), Tol);
            // 勧誘0でもゼロ割せず巨大時間（下限で守る）
            Assert.Greater(EspionageNetworkRules.NetworkBuildTime(20f, 0f), 1000f);
        }

        [Test]
        public void Compromise_ExposureTimesDoubleAgentRisk()
        {
            // 露見0.4 × 寝返り0.5 × 1.0 = 0.2
            Assert.AreEqual(0.2f, EspionageNetworkRules.Compromise(0.4f, 0.5f), Tol);
            // 寝返りが皆無なら汚染されない
            Assert.AreEqual(0f, EspionageNetworkRules.Compromise(0.4f, 0f), Tol);
        }

        [Test]
        public void IsNetworkBlown_ThresholdCrossing()
        {
            // 既定閾値0.6
            Assert.IsTrue(EspionageNetworkRules.IsNetworkBlown(0.6f));
            Assert.IsFalse(EspionageNetworkRules.IsNetworkBlown(0.59f));
            Assert.IsTrue(EspionageNetworkRules.IsNetworkBlown(0.5f, 0.4f));
        }

        [Test]
        public void Story_BuildNetworkGatherSecretsThenRiskExposure()
        {
            // 物語：敵地に精鋭工作員を50人潜入（quality=0.6）させ諜報網を張る。
            // 敵の秘匿は中程度（security=0.25）、敵防諜も中程度（counterintel=0.5）。
            float reach = EspionageNetworkRules.NetworkReach(50f, 0.6f);          // 0.8
            float yield = EspionageNetworkRules.IntelYield(reach, 0.25f);         // 0.6＝機密を多く得る
            Assert.AreEqual(0.6f, yield, PowTol);

            float exposure = EspionageNetworkRules.ExposureRisk(reach, 0.5f);     // 0.4
            // この時点では摘発閾値0.6に届かず網は生きている
            Assert.IsFalse(EspionageNetworkRules.IsNetworkBlown(exposure));

            // 露見ぶん時間とともに工作員を失い、二重スパイ混入で網が汚染されうる
            float attrition = EspionageNetworkRules.NetworkAttrition(exposure, 1f); // 0.2
            Assert.AreEqual(0.2f, attrition, Tol);
            float compromise = EspionageNetworkRules.Compromise(exposure, 0.5f);    // 0.2
            Assert.AreEqual(0.2f, compromise, Tol);

            // 網を拡大して質も上げる（agentCount/quality 増）と収量は増えるが露見も増す＝拡大と秘匿のトレードオフ
            float bigReach = EspionageNetworkRules.NetworkReach(50f, 1f);         // 1.0
            float bigYield = EspionageNetworkRules.IntelYield(bigReach, 0.25f);
            float bigExposure = EspionageNetworkRules.ExposureRisk(bigReach, 0.5f);
            Assert.Greater(bigYield, yield);
            Assert.Greater(bigExposure, exposure);
            // 防諜が厳しい敵地（counterintel=0.8）では大網は摘発される
            float harshExposure = EspionageNetworkRules.ExposureRisk(bigReach, 0.8f); // 0.8
            Assert.IsTrue(EspionageNetworkRules.IsNetworkBlown(harshExposure));

            // 深く潜った工作員は露見しにくく価値が高い
            Assert.Greater(EspionageNetworkRules.DeepCoverValue(0.9f),
                           EspionageNetworkRules.DeepCoverValue(0.1f));
        }
    }
}
