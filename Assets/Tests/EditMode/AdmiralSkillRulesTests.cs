using System.Collections.Generic;
using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 提督パッシブスキルの修正子解決を固定する（#137-140・SK-3/#138）：条件成立スキルだけが効き、
    /// 倍率系は積・加算系は和で合成、未成立・null・他タイプは効かない（実効値パターン＝基準非破壊）。
    /// </summary>
    public class AdmiralSkillRulesTests
    {
        static AdmiralSkill Make(SkillEffectType type, float magnitude, SkillCondition condition)
            => new AdmiralSkill { skillName = "test", effectType = type, magnitude = magnitude, condition = condition };

        [Test]
        public void IsActive_AllConditions()
        {
            // 常時は無条件 true
            Assert.IsTrue(AdmiralSkillRules.IsActive(SkillCondition.常時, AdmiralSkillRules.Context.Default));

            var ctx = new AdmiralSkillRules.Context(isOutnumbered: true, inCombat: true, isFlanking: true);
            Assert.IsTrue(AdmiralSkillRules.IsActive(SkillCondition.劣勢時, ctx));
            Assert.IsTrue(AdmiralSkillRules.IsActive(SkillCondition.交戦時, ctx));
            Assert.IsTrue(AdmiralSkillRules.IsActive(SkillCondition.側背面時, ctx));

            // 各条件オフでは成立しない（常時を除く）
            var off = AdmiralSkillRules.Context.Default;
            Assert.IsFalse(AdmiralSkillRules.IsActive(SkillCondition.劣勢時, off));
            Assert.IsFalse(AdmiralSkillRules.IsActive(SkillCondition.交戦時, off));
            Assert.IsFalse(AdmiralSkillRules.IsActive(SkillCondition.側背面時, off));
        }

        [Test]
        public void EffectiveMultiplier_ActiveSkillsProduct()
        {
            var skills = new List<AdmiralSkill>
            {
                Make(SkillEffectType.攻撃倍率, 1.2f, SkillCondition.常時),
                Make(SkillEffectType.攻撃倍率, 1.5f, SkillCondition.側背面時),
            };
            var ctx = new AdmiralSkillRules.Context(false, false, isFlanking: true);
            // 1.2 * 1.5 = 1.8（両方アクティブ＝積）
            Assert.AreEqual(1.8f, AdmiralSkillRules.EffectiveMultiplier(skills, SkillEffectType.攻撃倍率, ctx), 1e-4f);
        }

        [Test]
        public void EffectiveMultiplier_InactiveSkillIgnored()
        {
            var skills = new List<AdmiralSkill>
            {
                Make(SkillEffectType.攻撃倍率, 1.2f, SkillCondition.常時),
                Make(SkillEffectType.攻撃倍率, 1.5f, SkillCondition.側背面時),
            };
            var ctx = AdmiralSkillRules.Context.Default; // 側背面オフ
            // 常時の 1.2 のみ（条件未成立は効かない）
            Assert.AreEqual(1.2f, AdmiralSkillRules.EffectiveMultiplier(skills, SkillEffectType.攻撃倍率, ctx), 1e-4f);
        }

        [Test]
        public void EffectiveMultiplier_OtherTypeIgnored()
        {
            var skills = new List<AdmiralSkill>
            {
                Make(SkillEffectType.防御倍率, 2f, SkillCondition.常時), // 別タイプ
            };
            // 攻撃倍率を問うと該当なし＝等倍
            Assert.AreEqual(AdmiralSkillRules.NeutralMultiplier,
                AdmiralSkillRules.EffectiveMultiplier(skills, SkillEffectType.攻撃倍率, AdmiralSkillRules.Context.Default), 1e-4f);
        }

        [Test]
        public void EffectiveBonus_ActiveSkillsSum()
        {
            var skills = new List<AdmiralSkill>
            {
                Make(SkillEffectType.索敵範囲, 3f, SkillCondition.常時),
                Make(SkillEffectType.索敵範囲, 2f, SkillCondition.交戦時),
                Make(SkillEffectType.士気維持, 5f, SkillCondition.常時), // 別タイプ＝無視
            };
            var ctx = new AdmiralSkillRules.Context(false, inCombat: true, false);
            // 3 + 2 = 5（加算系は和、別タイプは無視）
            Assert.AreEqual(5f, AdmiralSkillRules.EffectiveBonus(skills, SkillEffectType.索敵範囲, ctx), 1e-4f);
        }

        [Test]
        public void EffectiveBonus_InactiveSkillIgnored()
        {
            var skills = new List<AdmiralSkill>
            {
                Make(SkillEffectType.士気維持, 4f, SkillCondition.劣勢時),
            };
            // 劣勢でなければ加算ゼロ
            Assert.AreEqual(AdmiralSkillRules.NeutralBonus,
                AdmiralSkillRules.EffectiveBonus(skills, SkillEffectType.士気維持, AdmiralSkillRules.Context.Default), 1e-4f);
        }

        [Test]
        public void NullAndEmpty_ReturnNeutral()
        {
            // null リスト＝基準値
            Assert.AreEqual(AdmiralSkillRules.NeutralMultiplier,
                AdmiralSkillRules.EffectiveMultiplier(null, SkillEffectType.攻撃倍率, AdmiralSkillRules.Context.Default), 1e-4f);
            Assert.AreEqual(AdmiralSkillRules.NeutralBonus,
                AdmiralSkillRules.EffectiveBonus(null, SkillEffectType.索敵範囲, AdmiralSkillRules.Context.Default), 1e-4f);

            // 空リスト＝基準値
            var empty = new List<AdmiralSkill>();
            Assert.AreEqual(AdmiralSkillRules.NeutralMultiplier,
                AdmiralSkillRules.EffectiveMultiplier(empty, SkillEffectType.防御倍率, AdmiralSkillRules.Context.Default), 1e-4f);
        }

        [Test]
        public void NullSkillEntry_Skipped()
        {
            var skills = new List<AdmiralSkill>
            {
                null,
                Make(SkillEffectType.機動倍率, 1.3f, SkillCondition.常時),
            };
            // null 要素は飛ばし、有効分だけ積む
            Assert.AreEqual(1.3f,
                AdmiralSkillRules.EffectiveMultiplier(skills, SkillEffectType.機動倍率, AdmiralSkillRules.Context.Default), 1e-4f);
        }

        [Test]
        public void IsMultiplicative_Classification()
        {
            Assert.IsTrue(AdmiralSkillRules.IsMultiplicative(SkillEffectType.攻撃倍率));
            Assert.IsTrue(AdmiralSkillRules.IsMultiplicative(SkillEffectType.防御倍率));
            Assert.IsTrue(AdmiralSkillRules.IsMultiplicative(SkillEffectType.機動倍率));
            Assert.IsFalse(AdmiralSkillRules.IsMultiplicative(SkillEffectType.士気維持));
            Assert.IsFalse(AdmiralSkillRules.IsMultiplicative(SkillEffectType.索敵範囲));
        }
    }
}
