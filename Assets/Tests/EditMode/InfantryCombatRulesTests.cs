using NUnit.Framework;
using UnityEngine;

namespace Ginei.Tests
{
    public class InfantryCombatRulesTests
    {
        const float Eps = 1e-4f;

        [Test]
        public void Firepower_QualityRaisesFire()
        {
            // 質0＝頭数のみ100、質1＝160。武器の質が火力に乗る。
            Assert.AreEqual(100f, InfantryCombatRules.Firepower(0f, 100f), Eps);
            Assert.AreEqual(160f, InfantryCombatRules.Firepower(1f, 100f), Eps);
        }

        [Test]
        public void CoverUtilization_TerrainTimesFieldcraft()
        {
            // 良地形×高い野戦技術＝満点の遮蔽活用。1*(0.5+0.5*1)=1。
            Assert.AreEqual(1f, InfantryCombatRules.CoverUtilization(1f, 1f), Eps);
            // 技術ゼロは活用が半減＝0.8*(0.5)=0.4。
            Assert.AreEqual(0.4f, InfantryCombatRules.CoverUtilization(0.8f, 0f), Eps);
        }

        [Test]
        public void SuppressiveFire_AmmoDependent()
        {
            // 弾薬満タンは火力フル＝100。弾切れは半減＝50。
            Assert.AreEqual(100f, InfantryCombatRules.SuppressiveFire(100f, 1f), Eps);
            Assert.AreEqual(50f, InfantryCombatRules.SuppressiveFire(100f, 0f), Eps);
        }

        [Test]
        public void FireAndMovement_ManeuverAdvances()
        {
            // 機動ゼロでも制圧の基礎ぶんは働く＝100*0.5の補正で100。
            Assert.AreEqual(100f, InfantryCombatRules.FireAndMovement(100f, 0f), Eps);
            // 機動全開で前進力が増す＝100*(0.5+0.5*2)=150。
            Assert.AreEqual(150f, InfantryCombatRules.FireAndMovement(100f, 1f), Eps);
        }

        [Test]
        public void CloseAssault_MoraleAndSkill()
        {
            // 高士気×高技術は飽和して1。
            Assert.AreEqual(1f, InfantryCombatRules.CloseAssault(1f, 1f), Eps);
            // 中士気・技術ゼロ＝0.5*(0.5+0.5*1)=0.5。技術なしでも士気の基礎ぶんは戦える。
            Assert.AreEqual(0.5f, InfantryCombatRules.CloseAssault(0.5f, 0f), Eps);
        }

        [Test]
        public void SquadCohesion_TrainingAndNco()
        {
            // 訓練・統率とも満点で結束1。
            Assert.AreEqual(1f, InfantryCombatRules.SquadCohesion(1f, 1f), Eps);
            // 中庸＝0.5*(0.5+0.5*1.5)=0.625。
            Assert.AreEqual(0.625f, InfantryCombatRules.SquadCohesion(0.5f, 0.5f), Eps);
        }

        [Test]
        public void CombatAttrition_ExposureDriven()
        {
            // 遮蔽ゼロは敵火力に晒される＝2/(1+2)=0.6667。
            Assert.AreEqual(2f / 3f, InfantryCombatRules.CombatAttrition(2f, 0f), Eps);
            // 完全遮蔽なら損耗ゼロ。
            Assert.AreEqual(0f, InfantryCombatRules.CombatAttrition(2f, 1f), Eps);
        }

        [Test]
        public void InfantryEffectiveness_CompositesAndClamps()
        {
            // 火力運動3→飽和0.75、結束1、損耗0 ＝ 0.75。
            Assert.AreEqual(0.75f, InfantryCombatRules.InfantryEffectiveness(3f, 1f, 0f), Eps);
            // 損耗が結束を食う＝0.75*0.5-0.25=0.125。
            Assert.AreEqual(0.125f, InfantryCombatRules.InfantryEffectiveness(3f, 0.5f, 0.25f), Eps);
        }

        [Test]
        public void Clamps_HandleOutOfRangeAndNegativeInputs()
        {
            // 入力は全て clamp＝負や範囲外でも破綻しない。
            Assert.AreEqual(0f, InfantryCombatRules.Firepower(0.5f, -10f), Eps);
            Assert.AreEqual(0f, InfantryCombatRules.CoverUtilization(-1f, 2f), Eps);
            float eff = InfantryCombatRules.InfantryEffectiveness(-5f, 2f, -1f);
            Assert.GreaterOrEqual(eff, 0f);
            Assert.LessOrEqual(eff, 1f);
        }

        [Test]
        public void Narrative_ArmoredGrenadiersHoldTheLine()
        {
            // 物語：装甲擲弾兵の防御戦。
            // 良地形を野戦技術で活かし、弾薬潤沢な制圧射撃と分隊結束で持ちこたえる中隊と、
            // 開けた地で弾切れ・結束も低い中隊。前者の歩兵戦闘力が明確に上回る。
            var p = InfantryCombatParams.Default;

            // 精鋭：良遮蔽・潤沢弾薬・高訓練。
            float coverA = InfantryCombatRules.CoverUtilization(0.9f, 0.9f, p);
            float fireA = InfantryCombatRules.Firepower(0.8f, 120f, p);
            float suppA = InfantryCombatRules.SuppressiveFire(fireA, 0.9f, p);
            float fmA = InfantryCombatRules.FireAndMovement(suppA, 0.7f, p);
            float cohA = InfantryCombatRules.SquadCohesion(0.9f, 0.9f, p);
            float attrA = InfantryCombatRules.CombatAttrition(150f, coverA, p);
            float effA = InfantryCombatRules.InfantryEffectiveness(fmA, cohA, attrA, p);

            // 損耗（遮蔽外）・弾切れ・低結束。
            float coverB = InfantryCombatRules.CoverUtilization(0.1f, 0.2f, p);
            float fireB = InfantryCombatRules.Firepower(0.3f, 120f, p);
            float suppB = InfantryCombatRules.SuppressiveFire(fireB, 0.1f, p);
            float fmB = InfantryCombatRules.FireAndMovement(suppB, 0.2f, p);
            float cohB = InfantryCombatRules.SquadCohesion(0.3f, 0.2f, p);
            float attrB = InfantryCombatRules.CombatAttrition(150f, coverB, p);
            float effB = InfantryCombatRules.InfantryEffectiveness(fmB, cohB, attrB, p);

            Assert.Greater(effA, effB);
            Assert.GreaterOrEqual(effA, 0f);
            Assert.LessOrEqual(effA, 1f);
            Assert.GreaterOrEqual(effB, 0f);
            // 遮蔽を捨てた中隊は損耗で押し潰され、ほぼ無力化される。
            Assert.Less(effB, 0.1f);
        }
    }
}
