using NUnit.Framework;
using UnityEngine;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 艦のライフサイクルを固定する：艦齢×運用で疲労が増え、故障率はバスタブ曲線（就役直後と末期が高い）、
    /// 改修で性能を維持し近代化で技術差ぶん上下し、整備投資は健全な艦ほど延命に効き、維持コストは年々累積、
    /// 残存価値は古い艦ほど目減りし、コストが価値を上回れば経済的寿命を超える。クランプと符号も担保。
    /// </summary>
    public class VesselLifecycleRulesTests
    {
        private static readonly VesselLifecycleParams P = VesselLifecycleParams.Default;
        // 疲労0.02/年・運用重み1・初期故障0.1(減衰0.5)・摩耗0.005(指数2)・整備抑制0.8・改修維持0.2・延命0.5・近代化0.5・維持基礎1・整備費0.5・減価0.1・経済寿命1.5

        [Test]
        public void HullFatigue_GrowsWithYearsAndTempo()
        {
            Assert.AreEqual(0.2f, VesselLifecycleRules.HullFatigue(10f, 0f, P), 1e-4f);   // 10×0.02×(1+0)
            Assert.AreEqual(0.6f, VesselLifecycleRules.HullFatigue(20f, 0.5f, P), 1e-4f); // 0.4×1.5＝高稼働で増幅
            Assert.AreEqual(1f, VesselLifecycleRules.HullFatigue(100f, 1f, P), 1e-4f);    // 4→1にクランプ
            Assert.AreEqual(0f, VesselLifecycleRules.HullFatigue(-5f, 1f, P), 1e-4f);     // 負年数はクランプ
        }

        [Test]
        public void FailureRate_BathtubHighAtBirthAndOldAge()
        {
            // 就役直後＝初期故障0.1（摩耗ゼロ）
            Assert.AreEqual(0.1f, VesselLifecycleRules.FailureRate(0f, 0f, P), 1e-4f);
            // 中盤(2年)＝初期故障消え摩耗わずか 0.005×4＝0.02（谷）
            Assert.AreEqual(0.02f, VesselLifecycleRules.FailureRate(2f, 0f, P), 1e-3f);
            // 末期(10年)＝摩耗0.005×100＝0.5（再び高い）
            Assert.AreEqual(0.5f, VesselLifecycleRules.FailureRate(10f, 0f, P), 1e-3f);
            // U字＝両端が谷より高い
            Assert.Greater(VesselLifecycleRules.FailureRate(0f, 0f, P), VesselLifecycleRules.FailureRate(2f, 0f, P));
            Assert.Greater(VesselLifecycleRules.FailureRate(10f, 0f, P), VesselLifecycleRules.FailureRate(2f, 0f, P));
        }

        [Test]
        public void FailureRate_MaintenanceSuppresses()
        {
            // 10年・整備満額＝0.5×(1-0.8)＝0.1 まで抑える
            Assert.AreEqual(0.1f, VesselLifecycleRules.FailureRate(10f, 1f, P), 1e-3f);
            Assert.Less(VesselLifecycleRules.FailureRate(10f, 1f, P), VesselLifecycleRules.FailureRate(10f, 0f, P));
        }

        [Test]
        public void PerformanceRetention_FatigueDownRefitUp()
        {
            Assert.AreEqual(1f, VesselLifecycleRules.PerformanceRetention(0f, 0f, P), 1e-4f);   // 新品＝満額
            Assert.AreEqual(0.5f, VesselLifecycleRules.PerformanceRetention(0.6f, 0.5f, P), 1e-4f); // 0.4＋0.5×0.2
            Assert.AreEqual(0.2f, VesselLifecycleRules.PerformanceRetention(1f, 1f, P), 1e-4f); // 疲労満でも改修で0.2粘る
        }

        [Test]
        public void OverhaulExtension_HealthyShipsExtendBetter()
        {
            Assert.AreEqual(0.5f, VesselLifecycleRules.OverhaulExtension(1f, 0f, P), 1e-4f);  // 健全＝満額延命
            Assert.AreEqual(0.2f, VesselLifecycleRules.OverhaulExtension(1f, 0.6f, P), 1e-4f); // 0.5×0.4
            Assert.AreEqual(0f, VesselLifecycleRules.OverhaulExtension(1f, 1f, P), 1e-4f);     // 疲労満では効かない
        }

        [Test]
        public void ModernizationGain_SignedByTechGap()
        {
            Assert.AreEqual(0.15f, VesselLifecycleRules.ModernizationGain(0.5f, 0.6f, P), 1e-4f);  // 0.5×0.6×0.5
            Assert.AreEqual(-0.2f, VesselLifecycleRules.ModernizationGain(1f, -0.4f, P), 1e-4f);   // 時代遅れは陳腐化＝負
            Assert.AreEqual(0.5f, VesselLifecycleRules.ModernizationGain(1f, 10f, P), 1e-4f);      // 技術差1にクランプ→0.5
            Assert.AreEqual(0f, VesselLifecycleRules.ModernizationGain(0f, 1f, P), 1e-4f);         // 未改修は得なし
        }

        [Test]
        public void LifecycleCost_AccumulatesWithYearsAndMaintenance()
        {
            Assert.AreEqual(10f, VesselLifecycleRules.LifecycleCost(10f, 0f, P), 1e-4f);  // 10×1
            Assert.AreEqual(28f, VesselLifecycleRules.LifecycleCost(20f, 0.8f, P), 1e-4f); // 20×(1+0.8×0.5)
            Assert.AreEqual(0f, VesselLifecycleRules.LifecycleCost(-5f, 1f, P), 1e-4f);    // 負年数はクランプ
        }

        [Test]
        public void ResidualValue_DepreciatesWithAge()
        {
            Assert.AreEqual(1f, VesselLifecycleRules.ResidualValue(1f, 0f, P), 1e-4f);          // 新品＝満額
            Assert.AreEqual(0.5f / 3f, VesselLifecycleRules.ResidualValue(0.5f, 20f, P), 1e-4f); // 0.5/(1+2)
            Assert.Less(VesselLifecycleRules.ResidualValue(1f, 20f, P), VesselLifecycleRules.ResidualValue(1f, 0f, P));
        }

        [Test]
        public void IsBeyondEconomicLife_CostExceedsValue()
        {
            Assert.IsFalse(VesselLifecycleRules.IsBeyondEconomicLife(0.1f, 1f, 1.5f)); // 安く価値あり＝現役
            Assert.IsTrue(VesselLifecycleRules.IsBeyondEconomicLife(28f, 0.16667f, 1.5f)); // 高コスト低価値＝退役
            Assert.IsTrue(VesselLifecycleRules.IsBeyondEconomicLife(2f, 1f)); // 既定閾値1.5委譲＝2>1.5
        }

        [Test]
        public void AgingWarship_LifeStory_FromCommissionToRetirement()
        {
            // 物語：新造艦が30年運用され、改修で粘りつつ末期に維持コストが価値を食い潰し退役へ。
            // 新造時：疲労なし・故障は初期故障のみ・性能満額・価値満額・経済寿命内
            float youngFatigue = VesselLifecycleRules.HullFatigue(0f, 0.6f, P);
            Assert.AreEqual(0f, youngFatigue, 1e-4f);
            Assert.AreEqual(1f, VesselLifecycleRules.PerformanceRetention(youngFatigue, 0f, P), 1e-4f);
            float youngCost = VesselLifecycleRules.LifecycleCost(0f, 0.6f, P);
            float youngValue = VesselLifecycleRules.ResidualValue(1f, 0f, P);
            Assert.IsFalse(VesselLifecycleRules.IsBeyondEconomicLife(youngCost, youngValue, P.economicLifeThreshold));

            // 老朽時（30年・高稼働）：疲労が進み故障率が跳ね、性能・価値が落ちる
            float oldFatigue = VesselLifecycleRules.HullFatigue(30f, 0.6f, P); // 30×0.02×1.6=0.96
            Assert.Greater(oldFatigue, youngFatigue);
            float oldFailure = VesselLifecycleRules.FailureRate(30f, 0.5f, P);
            float youngFailure = VesselLifecycleRules.FailureRate(0f, 0.5f, P);
            Assert.Greater(oldFailure, youngFailure); // 末期の摩耗故障

            float oldPerf = VesselLifecycleRules.PerformanceRetention(oldFatigue, 0.4f, P);
            float oldValue = VesselLifecycleRules.ResidualValue(oldPerf, 30f, P);
            float oldCost = VesselLifecycleRules.LifecycleCost(30f, 0.8f, P);
            Assert.Greater(oldCost, youngCost);  // 維持コスト累積
            Assert.Less(oldValue, youngValue);   // 価値目減り

            // 維持コストが残存価値を遥かに上回る＝経済的寿命を超え退役判断
            Assert.IsTrue(VesselLifecycleRules.IsBeyondEconomicLife(oldCost, oldValue, P.economicLifeThreshold));
        }
    }
}
