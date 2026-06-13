using System.Collections.Generic;
using NUnit.Framework;
using Ginei;
using UnityEngine;

namespace Ginei.Tests
{
    /// <summary>特技・戦法（本作オリジナル）：格×素養の実効量・素養ゲート・既存システムへの橋渡し。</summary>
    public class TalentRulesTests
    {
        private static AdmiralData Admiral(int attack = 80, int leadership = 80, int intelligence = 80, int operation = 80)
        {
            var a = ScriptableObject.CreateInstance<AdmiralData>();
            a.attack = attack; a.leadership = leadership; a.intelligence = intelligence; a.operation = operation;
            a.staffOfficers = new AdmiralData[0];
            return a;
        }

        [SetUp]
        public void Setup() => TalentCatalog.ResetToDefaults();

        [Test]
        public void GradePotency_And_RequiredStat_Tiers()
        {
            Assert.AreEqual(0.5f, TalentRules.GradePotency(TalentGrade.初), 1e-4f);
            Assert.AreEqual(1.0f, TalentRules.GradePotency(TalentGrade.中), 1e-4f);
            Assert.AreEqual(3.0f, TalentRules.GradePotency(TalentGrade.神), 1e-4f);
            Assert.AreEqual(0f, TalentRules.RequiredStat(TalentGrade.初), 1e-4f);
            Assert.AreEqual(35f, TalentRules.RequiredStat(TalentGrade.中), 1e-4f);
            Assert.AreEqual(90f, TalentRules.RequiredStat(TalentGrade.神), 1e-4f);
        }

        [Test]
        public void AspectScaling_ClampedAroundAbility()
        {
            Assert.AreEqual(1.0f, TalentRules.AspectScaling(50f), 1e-4f);
            Assert.AreEqual(1.3f, TalentRules.AspectScaling(80f), 1e-4f);
            Assert.AreEqual(1.5f, TalentRules.AspectScaling(100f), 1e-4f); // 1.5 でクランプ
            Assert.AreEqual(0.5f, TalentRules.AspectScaling(0f), 1e-4f);   // 0.5 でクランプ
        }

        [Test]
        public void EffectiveMagnitude_ScalesByGradeAndAspect_GatedByRequirement()
        {
            var oni = TalentCatalog.Get("鬼神"); // 武勇・攻撃強化・base0.15・常時
            // 攻撃50・中：0.15×1.0×1.0
            Assert.AreEqual(0.15f, TalentRules.EffectiveMagnitude(oni, TalentGrade.中, Admiral(attack: 50)), 1e-4f);
            // 攻撃100・神：0.15×3.0×1.5＝0.675
            Assert.AreEqual(0.675f, TalentRules.EffectiveMagnitude(oni, TalentGrade.神, Admiral(attack: 100)), 1e-4f);
            // 攻撃80・中：0.15×1.0×1.3＝0.195
            Assert.AreEqual(0.195f, TalentRules.EffectiveMagnitude(oni, TalentGrade.中, Admiral(attack: 80)), 1e-4f);
            // 攻撃80・神：素養80<必要90＝扱えない→0
            Assert.AreEqual(0f, TalentRules.EffectiveMagnitude(oni, TalentGrade.神, Admiral(attack: 80)), 1e-4f);
        }

        [Test]
        public void MeetsRequirement_And_CanWield()
        {
            Assert.IsTrue(TalentRules.MeetsRequirement(TalentAspect.武勇, TalentGrade.特, Admiral(attack: 75)));
            Assert.IsFalse(TalentRules.MeetsRequirement(TalentAspect.武勇, TalentGrade.特, Admiral(attack: 74)));
            Assert.IsTrue(TalentRules.CanWield(new Talent("鬼神", TalentGrade.神), Admiral(attack: 95)));
            Assert.IsFalse(TalentRules.CanWield(new Talent("鬼神", TalentGrade.神), Admiral(attack: 80)));
        }

        [Test]
        public void ToAdmiralSkill_BridgesPassiveCombatTalents()
        {
            // 鬼神（攻撃強化・乗算）→ 攻撃倍率・magnitude=1+割合。
            var s = TalentRules.ToAdmiralSkill(TalentCatalog.Get("鬼神"), TalentGrade.中, Admiral(attack: 80));
            Assert.IsNotNull(s);
            Assert.AreEqual(SkillEffectType.攻撃倍率, s.effectType);
            Assert.AreEqual(SkillCondition.常時, s.condition);
            Assert.AreEqual(1.195f, s.magnitude, 1e-4f);

            // 不動（士気維持・加算・交戦時）→ 士気維持・magnitude=量。
            var fudo = TalentRules.ToAdmiralSkill(TalentCatalog.Get("不動"), TalentGrade.中, Admiral(leadership: 50));
            Assert.IsNotNull(fudo);
            Assert.AreEqual(SkillEffectType.士気維持, fudo.effectType);
            Assert.AreEqual(SkillCondition.交戦時, fudo.condition);
            Assert.AreEqual(12f, fudo.magnitude, 1e-4f);

            // 戦法・マクロは AdmiralSkill へ写らない。
            Assert.IsNull(TalentRules.ToAdmiralSkill(TalentCatalog.Get("火計"), TalentGrade.中, Admiral(intelligence: 80)));
            Assert.IsNull(TalentRules.ToAdmiralSkill(TalentCatalog.Get("兵站"), TalentGrade.中, Admiral(operation: 80)));
        }

        [Test]
        public void ToAdmiralSkills_FeedsAdmiralSkillRulesPipeline()
        {
            var admiral = Admiral(attack: 80, intelligence: 80);
            var talents = new List<Talent> { new Talent("鬼神", TalentGrade.中), new Talent("火計", TalentGrade.中) };
            var skills = TalentRules.ToAdmiralSkills(talents, admiral);
            Assert.AreEqual(1, skills.Count); // 火計（戦法）は写らない＝鬼神のみ

            // 既存の AdmiralSkillRules がそのまま乗算修正子を解く（数式は二重実装しない）。
            float mult = AdmiralSkillRules.EffectiveMultiplier(skills, SkillEffectType.攻撃倍率, AdmiralSkillRules.Context.Default);
            Assert.AreEqual(1.195f, mult, 1e-4f);
        }

        [Test]
        public void EffectChannel_Classification_AndActiveCommandBridge()
        {
            Assert.IsTrue(TalentRules.MapsToAdmiralSkill(TalentEffect.攻撃強化, out var t1));
            Assert.AreEqual(SkillEffectType.攻撃倍率, t1);
            Assert.IsFalse(TalentRules.MapsToAdmiralSkill(TalentEffect.火力集中, out _));

            Assert.IsTrue(TalentRules.IsAdditive(TalentEffect.士気維持));
            Assert.IsFalse(TalentRules.IsAdditive(TalentEffect.攻撃強化));

            Assert.IsTrue(TalentRules.TryGetActiveCommand(TalentEffect.砲撃戦法, out var c1));
            Assert.AreEqual(ActiveCommand.一斉砲撃, c1);
            Assert.IsTrue(TalentRules.TryGetActiveCommand(TalentEffect.突撃戦法, out var c2));
            Assert.AreEqual(ActiveCommand.突撃, c2);
            Assert.IsFalse(TalentRules.TryGetActiveCommand(TalentEffect.鼓舞戦法, out _));

            Assert.IsTrue(TalentRules.IsTacticEffect(TalentEffect.範囲攻撃戦法));
            Assert.IsFalse(TalentRules.IsTacticEffect(TalentEffect.攻撃強化));
        }
    }
}
