using NUnit.Framework;
using UnityEngine;

namespace Ginei.Tests
{
    /// <summary>迂回機動（JOM-4・#1353）の純ロジックを既定 Params で固定する。</summary>
    public class TurningMovementRulesTests
    {
        const float Eps = 1e-4f;

        [Test]
        public void DetourCost_ExcessOverDirectIsWeighted()
        {
            // dir=100, det=150 → excess=0.5, raw=0.5/1.5=0.33333, *0.5=0.16667
            Assert.AreEqual(0.16667f, TurningMovementRules.DetourCost(100f, 150f), Eps);
        }

        [Test]
        public void DetourCost_DetourNotLongerThanDirectIsZero()
        {
            Assert.AreEqual(0f, TurningMovementRules.DetourCost(100f, 100f), Eps);
            Assert.AreEqual(0f, TurningMovementRules.DetourCost(0f, 50f), Eps); // 正面距離0は0
        }

        [Test]
        public void LineOfCommunicationThreat_ProductOfReachAndExposure()
        {
            Assert.AreEqual(0.48f, TurningMovementRules.LineOfCommunicationThreat(0.8f, 0.6f), Eps);
            Assert.AreEqual(0f, TurningMovementRules.LineOfCommunicationThreat(0f, 0.6f), Eps);
        }

        [Test]
        public void DislodgementPressure_HigherDependencyAmplifies()
        {
            // dep=1 → depFactor=1.0, *0.5=0.5／dep=0 → depFactor=0.4, *0.5=0.2
            Assert.AreEqual(0.5f, TurningMovementRules.DislodgementPressure(0.5f, 1.0f), Eps);
            Assert.AreEqual(0.2f, TurningMovementRules.DislodgementPressure(0.5f, 0.0f), Eps);
        }

        [Test]
        public void TurningAdvantage_ThreatDiscountedByCost()
        {
            // 0.6 - 0.5*0.2 = 0.5
            Assert.AreEqual(0.5f, TurningMovementRules.TurningAdvantage(0.6f, 0.2f), Eps);
        }

        [Test]
        public void ExposureDuringDetour_ScreenForceMitigates()
        {
            // det=3 → raw=0.75, *(1-0.5)=0.375
            Assert.AreEqual(0.375f, TurningMovementRules.ExposureDuringDetour(3f, 0.5f), Eps);
            // 掩護満点で脆弱性ゼロ
            Assert.AreEqual(0f, TurningMovementRules.ExposureDuringDetour(3f, 1.0f), Eps);
        }

        [Test]
        public void WideVsCloseTurning_DeeperWithForceScoresHigher()
        {
            // det=3 → raw=0.75, wide=0.75^(1/1.5), *force=1.0
            float expected = Mathf.Pow(0.75f, 1f / 1.5f);
            Assert.AreEqual(expected, TurningMovementRules.WideVsCloseTurning(3f, 1.0f), 1e-3f);
            // 戦力ゼロなら大回りに耐えられない
            Assert.AreEqual(0f, TurningMovementRules.WideVsCloseTurning(3f, 0f), Eps);
        }

        [Test]
        public void IsTurningMovementViable_AdvantageOverThreshold()
        {
            Assert.IsTrue(TurningMovementRules.IsTurningMovementViable(0.5f));   // 0.5 >= 0.4
            Assert.IsFalse(TurningMovementRules.IsTurningMovementViable(0.3f));  // 0.3 < 0.4
        }

        [Test]
        public void Story_TurningThreatensLineButFastEnemyCountersInTime()
        {
            // 深く回り込んで敵後背（手薄）の連絡線を脅かす＝高い脅威
            float threat = TurningMovementRules.LineOfCommunicationThreat(0.9f, 0.7f); // 0.63
            // 補給線に依存する敵を陣地から引きずり出す圧力が立つ
            float pressure = TurningMovementRules.DislodgementPressure(threat, 0.9f);
            Assert.Greater(pressure, 0.5f);

            // だが敵が機動的で、迂回に時間がかかると反作用が大きい＝迂回が間に合わない
            float fastEnemy = TurningMovementRules.EnemyCounterReaction(0.9f, 0.8f); // 0.72
            float slowEnemy = TurningMovementRules.EnemyCounterReaction(0.2f, 0.8f); // 0.16
            Assert.Greater(fastEnemy, slowEnemy);
            Assert.Greater(fastEnemy, 0.5f);

            // 遠回りが過大だと利得が削られ、実行に踏み切れない
            float bigDetourCost = TurningMovementRules.DetourCost(100f, 400f); // excess=3, raw=0.75, *0.5=0.375
            float advantage = TurningMovementRules.TurningAdvantage(threat, bigDetourCost);
            Assert.Less(advantage, threat);
        }
    }
}
