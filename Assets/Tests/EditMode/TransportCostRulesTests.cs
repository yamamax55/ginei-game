using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// TransportCostRules（CNTR-1 #1611・回廊ごとの輸送コスト係数と加重一体化）の検証。
    /// 既定 TransportCostParams（割引0.7・割増1.0・感度2・しきい値0.6）で期待値を固定する。
    /// </summary>
    public class TransportCostRulesTests
    {
        const float Eps = 1e-4f;

        /// <summary>規格化が進み混雑が低い回廊ほど安い：std=1/cong=0 は割引、std=0/cong=1 は割増。</summary>
        [Test]
        public void CorridorCost_StandardizationCheapens_CongestionRaises()
        {
            // 基準：distance=1, std=0, cong=0 → 1*1*1 = 1.0
            Assert.AreEqual(1.0f, TransportCostRules.CorridorCost(1f, 0f, 0f), Eps);
            // 規格化満点：1*(1-0.7)*1 = 0.3
            Assert.AreEqual(0.3f, TransportCostRules.CorridorCost(1f, 1f, 0f), Eps);
            // 混雑満点：1*1*(1+1.0) = 2.0
            Assert.AreEqual(2.0f, TransportCostRules.CorridorCost(1f, 0f, 1f), Eps);
            // 規格化満点かつ混雑満点：1*0.3*2.0 = 0.6
            Assert.AreEqual(0.6f, TransportCostRules.CorridorCost(1f, 1f, 1f), Eps);
        }

        /// <summary>入力は0..1へクランプされる（負・1超を丸める）。</summary>
        [Test]
        public void CorridorCost_ClampsInputs()
        {
            // distance=-1→0 でコスト0
            Assert.AreEqual(0f, TransportCostRules.CorridorCost(-1f, 0.5f, 0.5f), Eps);
            // std=2→1, cong=-1→0, distance=1 → 1*0.3*1 = 0.3
            Assert.AreEqual(0.3f, TransportCostRules.CorridorCost(1f, 2f, -1f), Eps);
        }

        /// <summary>安い回廊は実効的に近い：感度2でコストの二乗が実効距離。</summary>
        [Test]
        public void EffectiveDistance_CheapIsNear()
        {
            // cost=0.5 → 0.5^2 = 0.25
            Assert.AreEqual(0.25f, TransportCostRules.EffectiveDistance(0.5f), Eps);
            // cost=1 → 1
            Assert.AreEqual(1.0f, TransportCostRules.EffectiveDistance(1f), Eps);
            // cost=0 → 0（自由に運べる＝距離ゼロ）
            Assert.AreEqual(0f, TransportCostRules.EffectiveDistance(0f), Eps);
        }

        /// <summary>連結重みはコストが低いほど高い（0..1）。コスト0で重み1。</summary>
        [Test]
        public void ConnectionWeight_LowerCostHigherWeight()
        {
            // cost=0 → 1 - 0^2 = 1
            Assert.AreEqual(1.0f, TransportCostRules.ConnectionWeight(0f), Eps);
            // cost=0.5 → 1 - 0.25 = 0.75
            Assert.AreEqual(0.75f, TransportCostRules.ConnectionWeight(0.5f), Eps);
            // cost=1 → 1 - 1 = 0
            Assert.AreEqual(0f, TransportCostRules.ConnectionWeight(1f), Eps);
            // cost>1 はクランプで0
            Assert.AreEqual(0f, TransportCostRules.ConnectionWeight(2f), Eps);
        }

        /// <summary>加重一体化度＝回廊重みの平均。安い回廊が多いほど一体化が強い。空・0本は0。</summary>
        [Test]
        public void WeightedCohesion_AveragesWeights()
        {
            // 重み総和 1.5、回廊2本 → 0.75
            Assert.AreEqual(0.75f, TransportCostRules.WeightedCohesion(1.5f, 2), Eps);
            // 回廊0本は0（運ぶ回廊が無い）
            Assert.AreEqual(0f, TransportCostRules.WeightedCohesion(1.5f, 0), Eps);
            // 平均1超はクランプで1
            Assert.AreEqual(1.0f, TransportCostRules.WeightedCohesion(5f, 2), Eps);
        }

        /// <summary>経路コスト合計：手書きループで加算。null/空は0、負は丸める。</summary>
        [Test]
        public void CostToTraverse_SumsAndIsNullSafe()
        {
            Assert.AreEqual(0f, TransportCostRules.CostToTraverse(null), Eps);
            Assert.AreEqual(0f, TransportCostRules.CostToTraverse(new float[0]), Eps);
            // 0.3 + 0.5 + 0.2 = 1.0
            Assert.AreEqual(1.0f, TransportCostRules.CostToTraverse(new[] { 0.3f, 0.5f, 0.2f }), Eps);
            // 負は0扱い：-1 + 0.5 = 0.5
            Assert.AreEqual(0.5f, TransportCostRules.CostToTraverse(new[] { -1f, 0.5f }), Eps);
        }

        /// <summary>コンテナ化の低減率：規格化が進むほどコストが下がる。</summary>
        [Test]
        public void ContainerizationGain_StandardizationLowersCost()
        {
            // before=1, std=1 → after=0.3、低減率=(1-0.3)/1 = 0.7
            Assert.AreEqual(0.7f, TransportCostRules.ContainerizationGain(1f, 1f), Eps);
            // std=0 は低減なし
            Assert.AreEqual(0f, TransportCostRules.ContainerizationGain(1f, 0f), Eps);
            // before=0 は0（運ぶコストが無い）
            Assert.AreEqual(0f, TransportCostRules.ContainerizationGain(0f, 1f), Eps);
        }

        /// <summary>チョークプレミアムと採算判定：迂回路が少ないほど上乗せ大・しきい値以下で経済的。</summary>
        [Test]
        public void ChokepointPremium_AndIsEconomical()
        {
            // 迂回路0本：cost*1/(1+0) = cost
            Assert.AreEqual(0.6f, TransportCostRules.ChokepointPremium(0.6f, 0), Eps);
            // 迂回路3本：0.6*1/4 = 0.15
            Assert.AreEqual(0.15f, TransportCostRules.ChokepointPremium(0.6f, 3), Eps);
            // 既定しきい値0.6：0.5は経済的、0.7は不経済
            Assert.IsTrue(TransportCostRules.IsEconomical(0.5f));
            Assert.IsFalse(TransportCostRules.IsEconomical(0.7f));
            Assert.IsTrue(TransportCostRules.IsEconomical(0.6f));
        }
    }
}
