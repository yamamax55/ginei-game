using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 積み替えハブ（CNTR-2 #1612）を固定する：処理量の律速（能力と需要の最小律）・規模の経済（逓減つき）・
    /// 周辺コスト低減（距離減衰）・産出倍率への波及・混雑による効率逓減（飽和）・ハブ重力（正のフィードバック）。
    /// 「荷が集まるほど効率↑→さらに集まるが混雑で頭打ち」を担保する。
    /// </summary>
    public class TransshipmentRulesTests
    {
        [Test]
        public void HubThroughput_IsLimitedBySmallerOfCapacityOrDemand()
        {
            // 能力か需要の小さい方が律速（リービッヒの最小律）。
            Assert.AreEqual(0.3f, TransshipmentRules.HubThroughput(0.6f, 0.3f), 1e-4f); // 需要律速
            Assert.AreEqual(0.2f, TransshipmentRules.HubThroughput(0.2f, 0.9f), 1e-4f); // 能力律速
            Assert.AreEqual(0f, TransshipmentRules.HubThroughput(-1f, 0.5f), 1e-4f);   // クランプ
        }

        [Test]
        public void ScaleEconomyFactor_DecaysWithRoot()
        {
            // 既定強さ0.5・√で逓減：throughput=1で最大0.5、0.25で0.25、0で0。
            Assert.AreEqual(0.5f, TransshipmentRules.ScaleEconomyFactor(1f), 1e-4f);
            Assert.AreEqual(0.25f, TransshipmentRules.ScaleEconomyFactor(0.25f), 1e-4f);
            Assert.AreEqual(0f, TransshipmentRules.ScaleEconomyFactor(0f), 1e-4f);
        }

        [Test]
        public void NeighborCostReduction_DecaysWithDistance()
        {
            // 既定減衰0.4：ハブ直下(距離0)で能力満額、距離=falloffで半減、遠いほど薄れる。
            Assert.AreEqual(1f, TransshipmentRules.NeighborCostReduction(1f, 0f), 1e-4f);
            Assert.AreEqual(0.5f, TransshipmentRules.NeighborCostReduction(1f, 0.4f), 1e-4f);
            // 能力が低ければ比例して薄い。
            Assert.AreEqual(0.25f, TransshipmentRules.NeighborCostReduction(0.5f, 0.4f), 1e-4f);
        }

        [Test]
        public void OutputBoost_PropagatesCostReductionToOutput()
        {
            // 既定波及0.5：低減0で等倍1.0、低減0.5で1.25、低減1で1.5。常に≥1.0。
            Assert.AreEqual(1f, TransshipmentRules.OutputBoost(0f), 1e-4f);
            Assert.AreEqual(1.25f, TransshipmentRules.OutputBoost(0.5f), 1e-4f);
            Assert.AreEqual(1.5f, TransshipmentRules.OutputBoost(1f), 1e-4f);
        }

        [Test]
        public void CongestionPenalty_KicksInPastThreshold()
        {
            // 既定閾値0.8・強さ1.0：負荷0.7は閾値内で無ペナルティ、0.9は超過0.1ぶん効率0.9へ低下。
            Assert.AreEqual(1f, TransshipmentRules.CongestionPenalty(0.7f, 1f), 1e-4f);
            Assert.AreEqual(0.9f, TransshipmentRules.CongestionPenalty(0.9f, 1f), 1e-4f);
            // 能力ゼロに荷が来れば即パンク（効率0）。
            Assert.AreEqual(0f, TransshipmentRules.CongestionPenalty(0.5f, 0f), 1e-4f);
        }

        [Test]
        public void HubGravityTick_GrowsWithTrafficButSaturates()
        {
            // 正のフィードバック：荷(流量1)が集まるほど能力が育つ。
            // 能力0.2＋余地(0.8-0.2)=0.6×ゲイン0.6×流量1×dt1 = +0.36 → 0.56。
            Assert.AreEqual(0.56f, TransshipmentRules.HubGravityTick(0.2f, 1f, 1f), 1e-4f);
            // 混雑閾値0.8に達した能力は頭打ち＝余地0で伸びない（飽和）。
            Assert.AreEqual(0.8f, TransshipmentRules.HubGravityTick(0.8f, 1f, 1f), 1e-4f);
            // 荷が無ければ育たない。
            Assert.AreEqual(0.3f, TransshipmentRules.HubGravityTick(0.3f, 0f, 1f), 1e-4f);
        }

        [Test]
        public void HubViability_RequiresBenefitToExceedCost()
        {
            // 能力0.8・流量0.8 → throughput0.8、便益0.8×0.5×√0.8 ≈ 0.358。
            Assert.IsTrue(TransshipmentRules.HubViability(0.8f, 0.2f, 0.8f));  // 便益>コスト
            Assert.IsFalse(TransshipmentRules.HubViability(0.8f, 0.5f, 0.8f)); // 便益<コスト
            // 荷が無ければ便益0＝引き合わない。
            Assert.IsFalse(TransshipmentRules.HubViability(1f, 0.01f, 0f));
        }

        [Test]
        public void OptimalHubCapacity_SizesToStayBelowCongestion()
        {
            // 需要を混雑閾値の負荷に収める能力＝demand/threshold。需要0.4・閾値0.8 → 0.5。
            Assert.AreEqual(0.5f, TransshipmentRules.OptimalHubCapacity(0.4f, 0.8f), 1e-4f);
            // 厳しい閾値(0.5)ほど大きな能力が要る：0.4/0.5=0.8。
            Assert.AreEqual(0.8f, TransshipmentRules.OptimalHubCapacity(0.4f, 0.5f), 1e-4f);
            // 高需要は能力上限1へクランプ。
            Assert.AreEqual(1f, TransshipmentRules.OptimalHubCapacity(0.9f, 0.5f), 1e-4f);
        }

        [Test]
        public void Params_ClampInvalidValues()
        {
            // ctor クランプ：規模の経済→0..1、減衰→下限0.01、波及→非負、閾値→0..1、混雑強さ→非負、重力→非負。
            var p = new TransshipmentParams(2f, -5f, -1f, 3f, -2f, -1f);
            Assert.AreEqual(1f, p.scaleEconomyStrength, 1e-4f);
            Assert.AreEqual(0.01f, p.neighborFalloff, 1e-4f);
            Assert.AreEqual(0f, p.outputElasticity, 1e-4f);
            Assert.AreEqual(1f, p.congestionThreshold, 1e-4f);
            Assert.AreEqual(0f, p.congestionStrength, 1e-4f);
            Assert.AreEqual(0f, p.gravityGain, 1e-4f);
        }
    }
}
