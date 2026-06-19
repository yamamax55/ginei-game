using NUnit.Framework;
using UnityEngine;

namespace Ginei.Tests
{
    public class GarrisonDefenseRulesTests
    {
        const float Eps = 1e-4f;
        const float EpsPow = 1e-3f;

        [Test]
        public void DefensiveStrength_FortificationMultipliesGarrison()
        {
            // 1000 * (1 + 3 * 0.5) = 1000 * 2.5 = 2500
            Assert.AreEqual(2500f, GarrisonDefenseRules.DefensiveStrength(1000f, 3f), Eps);
            // 施設0段なら守備兵そのまま
            Assert.AreEqual(1000f, GarrisonDefenseRules.DefensiveStrength(1000f, 0f), Eps);
        }

        [Test]
        public void SiegeMorale_WeightedAverageOfInputs()
        {
            // 全て満たせば 0.4 + 0.4 + 0.2 = 1.0
            Assert.AreEqual(1.0f, GarrisonDefenseRules.SiegeMorale(1f, 1f, 1f), Eps);
            // 0.5*0.4 + 0.5*0.4 + 1*0.2 = 0.2 + 0.2 + 0.2 = 0.6
            Assert.AreEqual(0.6f, GarrisonDefenseRules.SiegeMorale(0.5f, 0.5f, 1f), Eps);
        }

        [Test]
        public void AttritionUnderSiege_PressureOverDefense()
        {
            // 500 / 2500 * 0.5 = 0.2 * 0.5 = 0.1
            Assert.AreEqual(0.1f, GarrisonDefenseRules.AttritionUnderSiege(500f, 2500f), Eps);
            // 防御力0で圧力ありは最大消耗1.0
            Assert.AreEqual(1.0f, GarrisonDefenseRules.AttritionUnderSiege(500f, 0f), Eps);
            // 圧力0は消耗なし
            Assert.AreEqual(0.0f, GarrisonDefenseRules.AttritionUnderSiege(0f, 2500f), Eps);
        }

        [Test]
        public void ReliefExpectation_NearnessTimesStrength()
        {
            // (1 - 0.25) * 0.8 = 0.75 * 0.8 = 0.6
            Assert.AreEqual(0.6f, GarrisonDefenseRules.ReliefExpectation(0.25f, 0.8f), Eps);
            // 遠方（距離1）は期待0
            Assert.AreEqual(0.0f, GarrisonDefenseRules.ReliefExpectation(1f, 1f), Eps);
        }

        [Test]
        public void SurrenderPressure_WeightedAverageOfInputs()
        {
            // 0.8*0.5 + 0.6*0.3 + 0.5*0.2 = 0.4 + 0.18 + 0.1 = 0.68
            Assert.AreEqual(0.68f, GarrisonDefenseRules.SurrenderPressure(0.8f, 0.6f, 0.5f), Eps);
        }

        [Test]
        public void LastStandResolve_LerpsMoraleTowardResolve()
        {
            // Lerp(0.2, 0.9, 0.5) = 0.55 ＝崩れた士気でも将が引っ張る
            Assert.AreEqual(0.55f, GarrisonDefenseRules.LastStandResolve(0.2f, 0.9f), Eps);
        }

        [Test]
        public void DefenseDuration_SqrtDefenseTimesSupplyMorale()
        {
            // sqrt(2500) * 0.5 * 0.6 * 30 = 50 * 0.5 * 0.6 * 30 = 450
            Assert.AreEqual(450f, GarrisonDefenseRules.DefenseDuration(2500f, 0.5f, 0.6f), EpsPow);
            // 補給0なら即落城
            Assert.AreEqual(0f, GarrisonDefenseRules.DefenseDuration(2500f, 0f, 0.6f), EpsPow);
        }

        [Test]
        public void WillHold_MoraleAtOrAbovePressureHolds()
        {
            Assert.IsTrue(GarrisonDefenseRules.WillHold(0.6f, 0.5f));
            Assert.IsFalse(GarrisonDefenseRules.WillHold(0.4f, 0.5f));
            // 閾値0.2 ぶん上回りが必要なら 0.6 vs 0.5 は崩れる
            Assert.IsFalse(GarrisonDefenseRules.WillHold(0.6f, 0.5f, 0.2f));
        }

        [Test]
        public void NullAndExtremeInputs_AreSafeAndClamped()
        {
            // 負の入力はクランプされ落ちない
            Assert.AreEqual(0f, GarrisonDefenseRules.DefensiveStrength(-100f, -1f), Eps);
            // 1超えの士気/圧力は 0..1 にクランプ
            Assert.AreEqual(1.0f, GarrisonDefenseRules.SiegeMorale(5f, 5f, 5f), Eps);
            Assert.AreEqual(1.0f, GarrisonDefenseRules.SurrenderPressure(5f, 5f, 5f), Eps);
            Assert.AreEqual(1.0f, GarrisonDefenseRules.LastStandResolve(5f, 5f), Eps);
        }

        [Test]
        public void Narrative_ReliefNeverComes_GarrisonStarvesAndSurrenders()
        {
            var p = GarrisonDefenseParams.Default;

            // 堅城に小勢の守備＝施設で防御力を底上げ
            float defense = GarrisonDefenseRules.DefensiveStrength(800f, 4f, p); // 800*(1+4*0.5)=2400
            Assert.AreEqual(2400f, defense, Eps);

            // 救援は遠く戦力も乏しい＝期待は低い
            float relief = GarrisonDefenseRules.ReliefExpectation(0.9f, 0.3f, p); // (1-0.9)*0.3=0.03
            Assert.AreEqual(0.03f, relief, Eps);

            // 序盤＝補給が残り士気は高い、守りきる
            float earlyMorale = GarrisonDefenseRules.SiegeMorale(relief, 0.9f, 0.8f, p);
            float earlyPressure = GarrisonDefenseRules.SurrenderPressure(0.1f, 0.1f, 1f - relief, p);
            Assert.IsTrue(GarrisonDefenseRules.WillHold(earlyMorale, earlyPressure));

            // 終盤＝兵糧が尽き攻囲で痩せ、救援の望みも絶たれる
            float lateAttrition = GarrisonDefenseRules.AttritionUnderSiege(2000f, defense, p);
            float lateMorale = GarrisonDefenseRules.SiegeMorale(relief, 0.05f, 0.4f, p);
            float lateHopeless = 1f - relief;
            float latePressure = GarrisonDefenseRules.SurrenderPressure(lateAttrition, 0.95f, lateHopeless, p);

            // 籠城士気は降伏圧力を下回る＝降伏へ
            Assert.IsFalse(GarrisonDefenseRules.WillHold(lateMorale, latePressure));
            Assert.Less(lateMorale, latePressure);

            // 持ちこたえる期間も序盤より短い（補給と士気が枯れた）
            float earlyDur = GarrisonDefenseRules.DefenseDuration(defense, 0.9f, earlyMorale, p);
            float lateDur = GarrisonDefenseRules.DefenseDuration(defense, 0.05f, lateMorale, p);
            Assert.Less(lateDur, earlyDur);
        }
    }
}
