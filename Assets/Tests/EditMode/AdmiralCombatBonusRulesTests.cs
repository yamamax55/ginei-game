using System.Collections.Generic;
using NUnit.Framework;
using Ginei;
using UnityEngine;

namespace Ginei.Tests
{
    /// <summary>
    /// 提督の特技・パッシブスキル → 会戦の戦闘係数（攻撃/防御/機動）の薄い橋渡し。
    /// 数式は TalentRules / AdmiralSkillRules へ委譲＝ここでは合成と bounded（1.0 中心）を担保する。
    /// </summary>
    public class AdmiralCombatBonusRulesTests
    {
        private static AdmiralData Admiral(int attack = 80, int leadership = 80, int defense = 80,
                                           int mobility = 80, int intelligence = 80, int operation = 80)
        {
            var a = ScriptableObject.CreateInstance<AdmiralData>();
            a.attack = attack; a.leadership = leadership; a.defense = defense; a.mobility = mobility;
            a.intelligence = intelligence; a.operation = operation;
            a.staffOfficers = new AdmiralData[0];
            a.talents = new List<Talent>();
            return a;
        }

        [SetUp]
        public void Setup() => TalentCatalog.ResetToDefaults();

        [Test]
        public void NullSkills_AreNeutral()
        {
            var ctx = AdmiralSkillRules.Context.Default;
            Assert.AreEqual(1f, AdmiralCombatBonusRules.AttackFactor(null, ctx), 1e-4f);
            Assert.AreEqual(1f, AdmiralCombatBonusRules.DefenseFactor(null), 1e-4f);
            Assert.AreEqual(1f, AdmiralCombatBonusRules.MobilityFactor(null), 1e-4f);
        }

        [Test]
        public void NullAdmiral_IsIdentity()
        {
            var f = AdmiralCombatBonusRules.FromAdmiral(null);
            Assert.AreEqual(1f, f.attack, 1e-4f);
            Assert.AreEqual(1f, f.defense, 1e-4f);
            Assert.AreEqual(1f, f.mobility, 1e-4f);
        }

        [Test]
        public void AdmiralWithNoTalents_IsNeutral()
        {
            var admiral = Admiral();
            var f = AdmiralCombatBonusRules.FromAdmiral(admiral);
            Assert.AreEqual(1f, f.attack, 1e-4f);
            Assert.AreEqual(1f, f.defense, 1e-4f);
            Assert.AreEqual(1f, f.mobility, 1e-4f);
        }

        [Test]
        public void AttackTalent_RaisesAttackFactorAboveOne()
        {
            // 鬼神＝武勇・攻撃強化・常時。攻撃 80 ＝素養を満たし倍率 > 1.0。
            var admiral = Admiral(attack: 80);
            admiral.talents.Add(new Talent("鬼神", TalentGrade.中));
            var f = AdmiralCombatBonusRules.FromAdmiral(admiral);
            Assert.Greater(f.attack, 1f);
            // 攻撃以外の系統は影響を受けない（1.0）。
            Assert.AreEqual(1f, f.defense, 1e-4f);
            Assert.AreEqual(1f, f.mobility, 1e-4f);
        }

        [Test]
        public void DefenseAndMobilityTalents_RaiseTheirOwnFactors()
        {
            // 鉄壁＝統率・防御強化／疾風＝統率・機動強化（常時）。統率 80 で扱える。
            var admiral = Admiral(leadership: 80);
            admiral.talents.Add(new Talent("鉄壁", TalentGrade.中));
            admiral.talents.Add(new Talent("疾風", TalentGrade.中));
            var f = AdmiralCombatBonusRules.FromAdmiral(admiral);
            Assert.Greater(f.defense, 1f);
            Assert.Greater(f.mobility, 1f);
            Assert.AreEqual(1f, f.attack, 1e-4f); // 攻撃強化の特技は持たない
        }

        [Test]
        public void GateUnmet_IncompetentAdmiral_IsNeutral()
        {
            // 神格の特技は必要素養 90。攻撃 20 では扱えず効果ゼロ＝倍率 1.0。
            var admiral = Admiral(attack: 20);
            admiral.talents.Add(new Talent("鬼神", TalentGrade.神));
            var f = AdmiralCombatBonusRules.FromAdmiral(admiral);
            Assert.AreEqual(1f, f.attack, 1e-4f);
        }

        [Test]
        public void Condition_OutnumberedTalent_OnlyActiveWhenContextMatches()
        {
            // 背水之陣＝武勇・攻撃強化・劣勢時。常時コンテキストでは効かず、劣勢時のみ > 1.0。
            var admiral = Admiral(attack: 80);
            admiral.talents.Add(new Talent("背水", TalentGrade.中));

            var calm = AdmiralCombatBonusRules.FromAdmiral(admiral, AdmiralSkillRules.Context.Default);
            Assert.AreEqual(1f, calm.attack, 1e-4f);

            var outnumbered = AdmiralCombatBonusRules.FromAdmiral(
                admiral, new AdmiralSkillRules.Context(isOutnumbered: true, inCombat: false, isFlanking: false));
            Assert.Greater(outnumbered.attack, 1f);
        }

        [Test]
        public void ApplyAttack_MultipliesIntoModifierStack()
        {
            var admiral = Admiral(attack: 80);
            admiral.talents.Add(new Talent("鬼神", TalentGrade.中));
            List<AdmiralSkill> skills = TalentRules.ToAdmiralSkills(admiral.talents, admiral);
            float expected = AdmiralCombatBonusRules.AttackFactor(skills);

            var stack = ModifierStack.Start();
            AdmiralCombatBonusRules.ApplyAttack(ref stack, skills, AdmiralSkillRules.Context.Default);
            Assert.AreEqual(expected, stack.Value, 1e-4f);
            Assert.Greater(stack.Value, 1f);
        }

        [Test]
        public void Factors_AreBounded_AndDeterministic()
        {
            var admiral = Admiral(attack: 100);
            admiral.talents.Add(new Talent("鬼神", TalentGrade.特));
            var a = AdmiralCombatBonusRules.FromAdmiral(admiral);
            var b = AdmiralCombatBonusRules.FromAdmiral(admiral);
            Assert.AreEqual(a.attack, b.attack, 1e-6f); // 決定論
            // 1.0 中心・有界（暴走しない範囲）。
            Assert.Greater(a.attack, 1f);
            Assert.Less(a.attack, 3f);
        }
    }
}
