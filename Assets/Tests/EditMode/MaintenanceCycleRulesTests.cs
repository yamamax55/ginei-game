using NUnit.Framework;
using UnityEngine;

namespace Ginei.Tests
{
    /// <summary>
    /// 整備サイクル（艦の稼働率・平時整備）の純ロジック EditMode テスト。
    /// 既定 Params で期待値を固定（検算済み）。Pow を含む故障率のみ許容を緩める。
    /// </summary>
    public class MaintenanceCycleRulesTests
    {
        const float Eps = 1e-4f;
        const float PowEps = 1e-3f;

        [Test]
        public void WearAccumulation_ScalesWithHoursAndIntensity()
        {
            // wearRate=1 * 50h * 0.5 = 25
            Assert.AreEqual(25f, MaintenanceCycleRules.WearAccumulation(50f, 0.5f), Eps);
        }

        [Test]
        public void WearReduction_SubtractsRepairOverTime()
        {
            // 40 - repairRate10 * effort1 * dt2 = 20
            Assert.AreEqual(20f, MaintenanceCycleRules.WearReduction(40f, 1f, 2f), Eps);
        }

        [Test]
        public void OperationalReadiness_FullWhenNoWearAndZeroWhenWornUnmaintained()
        {
            Assert.AreEqual(1f, MaintenanceCycleRules.OperationalReadiness(0f, 1f), Eps);
            Assert.AreEqual(0f, MaintenanceCycleRules.OperationalReadiness(100f, 0f), Eps);
            // ratio0.5 -> base0.5 + (0.5)*quality0.5 = 0.75
            Assert.AreEqual(0.75f, MaintenanceCycleRules.OperationalReadiness(50f, 0.5f), Eps);
        }

        [Test]
        public void FailureRate_RisesWithWear()
        {
            Assert.AreEqual(0f, MaintenanceCycleRules.FailureRate(0f), PowEps);
            Assert.AreEqual(0.15f, MaintenanceCycleRules.FailureRate(50f), PowEps); // 0.6 * 0.5^2
            Assert.AreEqual(0.6f, MaintenanceCycleRules.FailureRate(100f), PowEps);
        }

        [Test]
        public void MaintenanceDowntime_ShortenedByDockCapacity()
        {
            // base24 * scope1 / capacity2 = 12
            Assert.AreEqual(12f, MaintenanceCycleRules.MaintenanceDowntime(1f, 2f), Eps);
            // base24 * scope0.5 / capacity1 = 12
            Assert.AreEqual(12f, MaintenanceCycleRules.MaintenanceDowntime(0.5f, 1f), Eps);
        }

        [Test]
        public void DeferredMaintenancePenalty_AccumulatesPerSkip()
        {
            Assert.AreEqual(0f, MaintenanceCycleRules.DeferredMaintenancePenalty(0), Eps);
            Assert.AreEqual(0.3f, MaintenanceCycleRules.DeferredMaintenancePenalty(3), Eps);
            // 上限 0.6（既定 maxFailureRate と無関係に Clamp01）。20回でも 1.0 を超えない。
            Assert.AreEqual(1f, MaintenanceCycleRules.DeferredMaintenancePenalty(20), Eps);
        }

        [Test]
        public void FleetAvailability_IsReadinessTimesSize()
        {
            Assert.AreEqual(750f, MaintenanceCycleRules.FleetAvailability(0.75f, 1000f), Eps);
        }

        [Test]
        public void IsCombatServiceable_RespectsThreshold()
        {
            Assert.IsTrue(MaintenanceCycleRules.IsCombatServiceable(0.75f, 0.6f));
            Assert.IsFalse(MaintenanceCycleRules.IsCombatServiceable(0.5f, 0.6f));
        }

        [Test]
        public void Story_NeglectedFleetDegradesUntilUnserviceable()
        {
            var p = MaintenanceCycleParams.Default;

            // 長期作戦で艦が摩耗（80時間・高強度）。
            float wear = MaintenanceCycleRules.WearAccumulation(100f, 0.8f, p); // 1*100*0.8 = 80
            Assert.AreEqual(80f, wear, Eps);

            // 整備を3回先送り＝故障率が積み上がる。
            float failure = MaintenanceCycleRules.FailureRate(wear, p)
                          + MaintenanceCycleRules.DeferredMaintenancePenalty(3, p);
            Assert.Greater(failure, MaintenanceCycleRules.FailureRate(wear, p));

            // 整備皆無では稼働率が落ち、戦闘に出せない。
            float neglectReadiness = MaintenanceCycleRules.OperationalReadiness(wear, 0f, p); // base 1-0.8=0.2
            Assert.AreEqual(0.2f, neglectReadiness, Eps);
            Assert.IsFalse(MaintenanceCycleRules.IsCombatServiceable(neglectReadiness, 0.5f));

            // ドック入り＝戦線を離れて整備し摩耗を削る。
            float downtime = MaintenanceCycleRules.MaintenanceDowntime(1f, 1f, p);
            Assert.Greater(downtime, 0f);
            float reduced = MaintenanceCycleRules.WearReduction(wear, 1f, 8f, p); // 80 - 80 = 0
            Assert.AreEqual(0f, reduced, Eps);

            // 整備後は稼働率が戻り、再び戦力になる。
            float restored = MaintenanceCycleRules.OperationalReadiness(reduced, 1f, p);
            Assert.AreEqual(1f, restored, Eps);
            Assert.IsTrue(MaintenanceCycleRules.IsCombatServiceable(restored, 0.5f));
            // 艦隊全体の実働戦力も全力に戻る。
            Assert.AreEqual(2000f, MaintenanceCycleRules.FleetAvailability(restored, 2000f), Eps);
        }
    }
}
