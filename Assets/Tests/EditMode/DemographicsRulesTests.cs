using NUnit.Framework;
using Ginei;
using DP = Ginei.DemographicsRules.DemographicsParams;
using VR = Ginei.DemographicsRules.VitalRates;

namespace Ginei.Tests
{
    /// <summary>
    /// 人口動態（LIFE-3 #153）を固定する：従属人口指数、ボーナス/オーナス局面の判定、産出係数（±）、
    /// コホートの時間更新（出生・加齢・高齢死亡）。
    /// </summary>
    public class DemographicsRulesTests
    {
        [Test]
        public void DependencyRatio_DependentsOverWorking()
        {
            var p = new Population(youth: 20, working: 100, elderly: 10);
            Assert.AreEqual(0.3f, DemographicsRules.DependencyRatio(p), 1e-4f); // (20+10)/100
        }

        [Test]
        public void Phase_BonusWhenWorkingThick()
        {
            var bonus = new Population(20, 100, 10);   // 従属0.3 < 0.5
            var onus = new Population(30, 100, 50);    // 従属0.8 > 0.7
            var neutral = new Population(30, 100, 30);  // 従属0.6

            Assert.AreEqual(PopulationPhase.人口ボーナス, DemographicsRules.Phase(bonus, DP.Default));
            Assert.AreEqual(PopulationPhase.人口オーナス, DemographicsRules.Phase(onus, DP.Default));
            Assert.AreEqual(PopulationPhase.中立, DemographicsRules.Phase(neutral, DP.Default));
        }

        [Test]
        public void OutputFactor_SwingsByPhase()
        {
            var bonus = new Population(20, 100, 10);
            var onus = new Population(30, 100, 50);
            Assert.AreEqual(1.2f, DemographicsRules.OutputFactor(bonus, DP.Default), 1e-4f); // +20%
            Assert.AreEqual(0.8f, DemographicsRules.OutputFactor(onus, DP.Default), 1e-4f);  // -20%
        }

        [Test]
        public void Tick_AgesCohorts()
        {
            var p = new Population(youth: 150, working: 100, elderly: 0);
            float beforeWorking = p.working;
            DemographicsRules.Tick(p, VR.Default);
            // 年少→生産年齢の流入で生産年齢が増える（出生も加わる）
            Assert.Greater(p.working, beforeWorking);
            Assert.Greater(p.elderly, 0f); // 生産年齢の一部が高齢へ
        }

        [Test]
        public void Tick_ElderlyShrinkWithHighMortality()
        {
            var p = new Population(youth: 0, working: 0, elderly: 100);
            DemographicsRules.Tick(p, VR.Default); // 高齢死亡8%
            Assert.AreEqual(92f, p.elderly, 1e-3f);
        }

        [Test]
        public void DependencyRatio_NoWorkingHandledSafely()
        {
            Assert.AreEqual(0f, DemographicsRules.DependencyRatio(new Population(0, 0, 0)), 1e-4f);
            Assert.Greater(DemographicsRules.DependencyRatio(new Population(10, 0, 10)), 100f);
        }
    }
}
