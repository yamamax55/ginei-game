using System.Collections.Generic;
using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 提督の特技・パッシブスキル → 会戦の戦闘係数（攻撃/防御/機動の倍率）への薄い橋渡し（#特技・#137-140）。
    /// 数式は持たず既存窓口へ委譲し二重実装しない：
    /// ・特技（<see cref="Talent"/>/<see cref="TalentDef"/>）→ <see cref="TalentRules.ToAdmiralSkills"/> で <see cref="AdmiralSkill"/> へ写す。
    /// ・修正子→倍率の合算は <see cref="AdmiralSkillRules.EffectiveMultiplier"/> が担う（条件成立分のみ・1.0 中心）。
    /// ・倍率の積み上げ合成は <see cref="ModifierStack"/>（#106）に揃える。
    /// 返り値は決定論・bounded（1.0 中心＝特技/スキルが無い、無能で扱えないなら 1.0）。基準値非破壊（実効値パターン）。
    /// 入力は <see cref="AdmiralData"/> でなくスキル群／能力値を受ける（Game 側が AdmiralData から読んで渡す）。test-first。
    /// </summary>
    public static class AdmiralCombatBonusRules
    {
        /// <summary>修正子が無いときの倍率（等倍）。<see cref="AdmiralSkillRules.NeutralMultiplier"/> に揃える。</summary>
        public const float Neutral = AdmiralSkillRules.NeutralMultiplier; // 1.0

        // ── スキル群（既に AdmiralSkill 化済み）から倍率を解く ──────────────────────

        /// <summary>攻撃倍率（攻撃倍率スキルのうち条件成立分の積・1.0 中心）。null/該当なしは 1.0。</summary>
        public static float AttackFactor(IList<AdmiralSkill> skills, AdmiralSkillRules.Context ctx)
            => AdmiralSkillRules.EffectiveMultiplier(skills, SkillEffectType.攻撃倍率, ctx);

        /// <summary>防御倍率（防御倍率スキルのうち条件成立分の積・1.0 中心）。null/該当なしは 1.0。</summary>
        public static float DefenseFactor(IList<AdmiralSkill> skills, AdmiralSkillRules.Context ctx)
            => AdmiralSkillRules.EffectiveMultiplier(skills, SkillEffectType.防御倍率, ctx);

        /// <summary>機動倍率（機動倍率スキルのうち条件成立分の積・1.0 中心）。null/該当なしは 1.0。</summary>
        public static float MobilityFactor(IList<AdmiralSkill> skills, AdmiralSkillRules.Context ctx)
            => AdmiralSkillRules.EffectiveMultiplier(skills, SkillEffectType.機動倍率, ctx);

        /// <summary>状況既定（常時スキルのみ効く）のオーバーロード。</summary>
        public static float AttackFactor(IList<AdmiralSkill> skills)
            => AttackFactor(skills, AdmiralSkillRules.Context.Default);

        /// <summary>状況既定（常時スキルのみ効く）のオーバーロード。</summary>
        public static float DefenseFactor(IList<AdmiralSkill> skills)
            => DefenseFactor(skills, AdmiralSkillRules.Context.Default);

        /// <summary>状況既定（常時スキルのみ効く）のオーバーロード。</summary>
        public static float MobilityFactor(IList<AdmiralSkill> skills)
            => MobilityFactor(skills, AdmiralSkillRules.Context.Default);

        // ── 提督（特技＋能力）から直接 倍率を解く窓口 ──────────────────────────────

        /// <summary>
        /// 提督の所持特技を <see cref="TalentRules.ToAdmiralSkills"/> で <see cref="AdmiralSkill"/> 群へ写し、
        /// 効果種別ごとの倍率（条件成立分の積・1.0 中心）を一括算出。素養ゲート未達の特技は効かない（1.0）。
        /// </summary>
        public static CombatFactors FromAdmiral(AdmiralData admiral, AdmiralSkillRules.Context ctx)
        {
            List<AdmiralSkill> skills = (admiral != null)
                ? TalentRules.ToAdmiralSkills(admiral.talents, admiral)
                : null;
            return new CombatFactors(
                AttackFactor(skills, ctx),
                DefenseFactor(skills, ctx),
                MobilityFactor(skills, ctx));
        }

        /// <summary>状況既定（常時のみ）の <see cref="FromAdmiral(AdmiralData, AdmiralSkillRules.Context)"/>。</summary>
        public static CombatFactors FromAdmiral(AdmiralData admiral)
            => FromAdmiral(admiral, AdmiralSkillRules.Context.Default);

        /// <summary>提督の特技＋追加のパッシブスキル群を合わせて倍率を解く（両方の修正子を積む）。</summary>
        public static CombatFactors FromAdmiral(AdmiralData admiral, IList<AdmiralSkill> extraSkills, AdmiralSkillRules.Context ctx)
        {
            var skills = new List<AdmiralSkill>();
            if (admiral != null)
            {
                List<AdmiralSkill> fromTalents = TalentRules.ToAdmiralSkills(admiral.talents, admiral);
                if (fromTalents != null) skills.AddRange(fromTalents);
            }
            if (extraSkills != null) skills.AddRange(extraSkills);
            return new CombatFactors(
                AttackFactor(skills, ctx),
                DefenseFactor(skills, ctx),
                MobilityFactor(skills, ctx));
        }

        // ── ModifierStack(#106) への積み込み ──────────────────────────────────────

        /// <summary>攻撃倍率を既存スタックへ積む（基準値非破壊＝合成値のみ更新）。</summary>
        public static void ApplyAttack(ref ModifierStack stack, IList<AdmiralSkill> skills, AdmiralSkillRules.Context ctx)
            => stack.Mul(AttackFactor(skills, ctx));

        /// <summary>防御倍率を既存スタックへ積む。</summary>
        public static void ApplyDefense(ref ModifierStack stack, IList<AdmiralSkill> skills, AdmiralSkillRules.Context ctx)
            => stack.Mul(DefenseFactor(skills, ctx));

        /// <summary>機動倍率を既存スタックへ積む。</summary>
        public static void ApplyMobility(ref ModifierStack stack, IList<AdmiralSkill> skills, AdmiralSkillRules.Context ctx)
            => stack.Mul(MobilityFactor(skills, ctx));

        /// <summary>攻撃/防御/機動の倍率セット（1.0 中心・bounded・値型＝ヒープ確保なし）。</summary>
        public readonly struct CombatFactors
        {
            /// <summary>攻撃倍率（1.0 中心）。</summary>
            public readonly float attack;

            /// <summary>防御倍率（1.0 中心）。</summary>
            public readonly float defense;

            /// <summary>機動倍率（1.0 中心）。</summary>
            public readonly float mobility;

            public CombatFactors(float attack, float defense, float mobility)
            {
                this.attack = attack;
                this.defense = defense;
                this.mobility = mobility;
            }

            /// <summary>全て等倍（修正子なし）の既定。</summary>
            public static CombatFactors Identity => new CombatFactors(Neutral, Neutral, Neutral);
        }
    }
}
