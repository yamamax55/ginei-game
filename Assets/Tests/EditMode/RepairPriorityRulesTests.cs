using NUnit.Framework;
using UnityEngine;

namespace Ginei.Tests
{
    /// <summary>損傷艦トリアージ（修理優先配分）の純ロジック検証。既定 Params で期待値固定。</summary>
    public class RepairPriorityRulesTests
    {
        const float Eps = 1e-4f;

        [Test]
        public void RepairTime_ScalesWithDamageAndValue()
        {
            // 10 * 0.5 * (1 + 0.5*2) = 5 * 2 = 10
            Assert.AreEqual(10f, RepairPriorityRules.RepairTime(0.5f, 2f), Eps);
            // 価値0でも基準時間ぶんは要る：10 * 1 * 1 = 10
            Assert.AreEqual(10f, RepairPriorityRules.RepairTime(1f, 0f), Eps);
            // 大型艦ほど長い
            Assert.Greater(RepairPriorityRules.RepairTime(0.5f, 4f), RepairPriorityRules.RepairTime(0.5f, 1f));
        }

        [Test]
        public void TriageScore_FavorsValuableAndQuick()
        {
            // 4 / (6 + 1)
            Assert.AreEqual(0.5714286f, RepairPriorityRules.TriageScore(0.3f, 4f, 6f), Eps);
            // 未損傷はスコア0（修理不要）
            Assert.AreEqual(0f, RepairPriorityRules.TriageScore(0f, 4f, 6f), Eps);
            // 同価値なら早く戻せる方が高い
            Assert.Greater(RepairPriorityRules.TriageScore(0.3f, 4f, 2f),
                           RepairPriorityRules.TriageScore(0.3f, 4f, 8f));
        }

        [Test]
        public void BeyondEconomicRepair_ScrapsHeavyDamage()
        {
            // 0.9 * (1 + 0.1*1) = 0.99 >= 0.85
            Assert.IsTrue(RepairPriorityRules.BeyondEconomicRepair(0.9f, 1f));
            // 0.5 * 1.1 = 0.55 < 0.85
            Assert.IsFalse(RepairPriorityRules.BeyondEconomicRepair(0.5f, 1f));
        }

        [Test]
        public void QuickReturnVsFullRepair_SignedDecision()
        {
            // 逼迫＋軽傷 → 応急(+)：0.9*0.8 - 0.1*0.2 = 0.72 - 0.02 = 0.70
            Assert.AreEqual(0.70f, RepairPriorityRules.QuickReturnVsFullRepair(0.2f, 0.9f), Eps);
            // 平穏＋重傷 → 完全(-)：0.1*0.1 - 0.9*0.9 = 0.01 - 0.81 = -0.80
            Assert.AreEqual(-0.80f, RepairPriorityRules.QuickReturnVsFullRepair(0.9f, 0.1f), Eps);
        }

        [Test]
        public void DockUtilization_RatioAndFullOnZeroCapacity()
        {
            Assert.AreEqual(0.75f, RepairPriorityRules.DockUtilization(3f, 4f), Eps);
            // 容量0は満杯扱い
            Assert.AreEqual(1f, RepairPriorityRules.DockUtilization(1f, 0f), Eps);
        }

        [Test]
        public void FleetReconstitution_ClampedToDamagedSize()
        {
            // throughput*dt = 100 だが損傷規模30で頭打ち
            Assert.AreEqual(30f, RepairPriorityRules.FleetReconstitution(50f, 30f, 2f), Eps);
            // throughput*dt = 20 < 30
            Assert.AreEqual(20f, RepairPriorityRules.FleetReconstitution(10f, 30f, 2f), Eps);
        }

        [Test]
        public void CannibalizationGain_PartsAccelerateRepair()
        {
            // 1 + clamp01(2/4) = 1.5
            Assert.AreEqual(1.5f, RepairPriorityRules.CannibalizationGain(2f, 4f), Eps);
            // 1 + clamp01(8/4=2) = 2（頭打ち）
            Assert.AreEqual(2f, RepairPriorityRules.CannibalizationGain(8f, 4f), Eps);
            // 修理対象が無ければ部品の使い道なし
            Assert.AreEqual(1f, RepairPriorityRules.CannibalizationGain(1f, 0f), Eps);
        }

        [Test]
        public void IsShipWorthRepairing_ThresholdGate()
        {
            Assert.IsTrue(RepairPriorityRules.IsShipWorthRepairing(0.5f, 0.3f));
            Assert.IsFalse(RepairPriorityRules.IsShipWorthRepairing(0.2f, 0.3f));
        }

        [Test]
        public void Story_TriageAfterBattle()
        {
            // 会戦後：満身創痍の戦艦と、かすり傷の巡洋艦が工作艦に列をなす。
            // 大破した戦艦(損傷0.95・高価値3)＝直すより新造が安い → 廃棄。
            bool battleshipScrapped = RepairPriorityRules.BeyondEconomicRepair(0.95f, 3f);
            Assert.IsTrue(battleshipScrapped, "大破した主力艦は経済的全損で廃棄されるべき");

            // 廃棄戦艦から部品取り（共食い）して残る修理を加速する。
            float gain = RepairPriorityRules.CannibalizationGain(1f, 2f);
            Assert.Greater(gain, 1f, "廃棄艦の部品取りで修理は速まる");

            // 軽傷の巡洋艦(損傷0.3・価値1)＝短時間で戦線復帰でき優先度が立つ。
            float t = RepairPriorityRules.RepairTime(0.3f, 1f);          // 10*0.3*1.5 = 4.5
            Assert.AreEqual(4.5f, t, Eps);
            float score = RepairPriorityRules.TriageScore(0.3f, 1f, t);  // 1/(4.5+1) = 0.181818
            Assert.AreEqual(0.181818f, score, Eps);
            Assert.IsTrue(RepairPriorityRules.IsShipWorthRepairing(score, 0.1f),
                "軽傷艦は早期復帰でき修理する価値がある");

            // 前線が逼迫していれば完全修理を待たず応急で戻す（+側）。
            float decision = RepairPriorityRules.QuickReturnVsFullRepair(0.3f, 0.9f);
            Assert.Greater(decision, 0f, "逼迫した軽傷艦は応急修理で早く戻す");
        }
    }
}
