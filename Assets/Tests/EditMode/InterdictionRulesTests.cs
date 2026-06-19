using NUnit.Framework;
using UnityEngine;

namespace Ginei.Tests
{
    /// <summary>
    /// InterdictionRules（阻止攻撃）の EditMode テスト。既定 Params で期待値固定。
    /// </summary>
    public class InterdictionRulesTests
    {
        const float Eps = 1e-4f;
        // 平方根（ReinforcementDelay）を含む箇所は緩める。
        const float PowEps = 1e-3f;

        [Test]
        public void ChokePointControl_StrengthVsTraffic_Ratio()
        {
            // interdictor 100 ×1.5 = 150, traffic 50 → 150/(150+50)=0.75
            Assert.AreEqual(0.75f, InterdictionRules.ChokePointControl(100f, 50f), Eps);
        }

        [Test]
        public void ChokePointControl_ZeroStrength_NoControl()
        {
            Assert.AreEqual(0f, InterdictionRules.ChokePointControl(0f, 50f), Eps);
        }

        [Test]
        public void SupplyThroughputReduction_LeavesFloorThrough()
        {
            // control 0.75 × (1 - 0.1) = 0.675
            Assert.AreEqual(0.675f, InterdictionRules.SupplyThroughputReduction(0.75f), Eps);
        }

        [Test]
        public void ReinforcementDelay_ScalesWithSqrtSize()
        {
            // control 0.75 × 10 × sqrt(100)=10 → 75
            Assert.AreEqual(75f, InterdictionRules.ReinforcementDelay(0.75f, 100f), PowEps);
        }

        [Test]
        public void FrontlineStarvation_ProportionalToReductionAndDt()
        {
            // 0.675 × 100 × 0.5 × 1 = 33.75
            Assert.AreEqual(33.75f, InterdictionRules.FrontlineStarvation(0.675f, 100f, 1f), Eps);
        }

        [Test]
        public void InterdictionExposure_BalancedRisk()
        {
            // response 50 ×1.0 = 50, position 50 → 50/(50+50)=0.5
            Assert.AreEqual(0.5f, InterdictionRules.InterdictionExposure(50f, 50f), Eps);
        }

        [Test]
        public void CumulativeAttrition_AccumulatesAndClamps()
        {
            // 0.675 × 0.3 × 2 = 0.405
            Assert.AreEqual(0.405f, InterdictionRules.CumulativeAttrition(0.675f, 2f), Eps);
            // 大きな持続は 1 でクランプ
            Assert.AreEqual(1f, InterdictionRules.CumulativeAttrition(0.8f, 100f), Eps);
        }

        [Test]
        public void EscortCounter_HeavyEscortReducesInterdiction()
        {
            // interdictor 100 ×1.5 = 150, escort 150 ×1.0 = 150 → 150/(150+150)=0.5
            Assert.AreEqual(0.5f, InterdictionRules.EscortCounter(100f, 150f), Eps);
            // 護衛なしならほぼ完全に扼せる
            float noEscort = InterdictionRules.EscortCounter(100f, 0f);
            Assert.AreEqual(1f, noEscort, Eps);
        }

        [Test]
        public void IsSupplyLineSevered_AtThreshold()
        {
            // 既定閾値 0.7
            Assert.IsFalse(InterdictionRules.IsSupplyLineSevered(0.675f));
            Assert.IsTrue(InterdictionRules.IsSupplyLineSevered(0.7f));
            Assert.IsTrue(InterdictionRules.IsSupplyLineSevered(0.9f));
        }

        [Test]
        public void Narrative_ChokeStarvesFrontButEscortMakesItHardAndExposesRaider()
        {
            var p = InterdictionRules.InterdictionParams.Default;

            // 阻止部隊が回廊の要所を扼す（戦力 120、流量 40）。
            float control = InterdictionRules.ChokePointControl(120f, 40f, p);
            // 120×1.5=180 → 180/(180+40)=0.81818...
            Assert.AreEqual(180f / 220f, control, Eps);

            // 補給到達が大きく減り、補給線を断った。
            float reduction = InterdictionRules.SupplyThroughputReduction(control, p);
            Assert.Greater(reduction, p.severThreshold);
            Assert.IsTrue(InterdictionRules.IsSupplyLineSevered(reduction, p));

            // 前線は補給不足で弱り、持続すれば累積消耗が積む。
            float starve = InterdictionRules.FrontlineStarvation(reduction, 80f, 2f, p);
            Assert.Greater(starve, 0f);
            float attr = InterdictionRules.CumulativeAttrition(reduction, 4f, p);
            Assert.Greater(attr, 0f);

            // しかし護衛がつくと阻止度が落ちる（護衛ありの方が扼せない）。
            float withEscort = InterdictionRules.EscortCounter(120f, 200f, p);
            float withoutEscort = InterdictionRules.EscortCounter(120f, 0f, p);
            Assert.Less(withEscort, withoutEscort);

            // 後方に深く出ると敵対応で孤立リスクが上がる。
            float lowResponse = InterdictionRules.InterdictionExposure(50f, 20f, p);
            float highResponse = InterdictionRules.InterdictionExposure(50f, 120f, p);
            Assert.Greater(highResponse, lowResponse);
        }
    }
}
