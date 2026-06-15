using System.Collections.Generic;
using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 提督パッシブスキルの修正子解決の唯一の窓口（#137-140・SK-3 #140 パッシブ／SK-1 #138 データ基盤）。
    /// 基準能力は変えず、条件成立スキルだけを集めて修正子（倍率/加算）を返す（実効値パターン）。
    /// 将来 #106 係数パイプラインに合流する前提で、ここでは修正子を返すだけに留める（Game 型は参照しない）。
    /// </summary>
    public static class AdmiralSkillRules
    {
        /// <summary>倍率系の基準値（修正子なし＝等倍）。</summary>
        public const float NeutralMultiplier = 1f;

        /// <summary>加算系の基準値（修正子なし＝加算ゼロ）。</summary>
        public const float NeutralBonus = 0f;

        /// <summary>
        /// 状況コンテキスト（plain 値）。条件判定はこれだけを見る（Game 型に依存しない）。
        /// </summary>
        public readonly struct Context
        {
            /// <summary>兵力で劣勢か（劣勢時条件で参照）。</summary>
            public readonly bool isOutnumbered;

            /// <summary>交戦中か（交戦時条件で参照）。</summary>
            public readonly bool inCombat;

            /// <summary>敵の側背面を取っているか（側背面時条件で参照）。</summary>
            public readonly bool isFlanking;

            public Context(bool isOutnumbered, bool inCombat, bool isFlanking)
            {
                this.isOutnumbered = isOutnumbered;
                this.inCombat = inCombat;
                this.isFlanking = isFlanking;
            }

            /// <summary>全条件オフの既定（常時スキルのみ効く）。</summary>
            public static Context Default => new Context(false, false, false);
        }

        /// <summary>効果種別が乗算系（攻撃/防御/機動倍率）か。残りは加算系。</summary>
        public static bool IsMultiplicative(SkillEffectType type)
        {
            switch (type)
            {
                case SkillEffectType.攻撃倍率:
                case SkillEffectType.防御倍率:
                case SkillEffectType.機動倍率:
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>発動条件が成立しているか（常時は無条件 true）。</summary>
        public static bool IsActive(SkillCondition condition, Context ctx)
        {
            switch (condition)
            {
                case SkillCondition.常時:
                    return true;
                case SkillCondition.劣勢時:
                    return ctx.isOutnumbered;
                case SkillCondition.交戦時:
                    return ctx.inCombat;
                case SkillCondition.側背面時:
                    return ctx.isFlanking;
                default:
                    return false;
            }
        }

        /// <summary>
        /// 該当タイプの乗算修正子の合算（条件成立スキルの magnitude を積む＝NeutralMultiplier からの積）。
        /// 乗算系タイプ専用（攻撃/防御/機動倍率）。条件未成立・null・他タイプは効かない。
        /// </summary>
        public static float EffectiveMultiplier(IList<AdmiralSkill> skills, SkillEffectType type, Context ctx)
        {
            float mult = NeutralMultiplier;
            if (skills == null)
                return mult;

            for (int i = 0; i < skills.Count; i++)
            {
                AdmiralSkill skill = skills[i];
                if (skill == null || skill.effectType != type)
                    continue;
                if (!IsActive(skill.condition, ctx))
                    continue;
                mult *= skill.magnitude;
            }
            return mult;
        }

        /// <summary>
        /// 該当タイプの加算修正子の合算（条件成立スキルの magnitude を和＝NeutralBonus からの和）。
        /// 加算系タイプ用（士気維持/索敵範囲）。条件未成立・null・他タイプは効かない。
        /// </summary>
        public static float EffectiveBonus(IList<AdmiralSkill> skills, SkillEffectType type, Context ctx)
        {
            float bonus = NeutralBonus;
            if (skills == null)
                return bonus;

            for (int i = 0; i < skills.Count; i++)
            {
                AdmiralSkill skill = skills[i];
                if (skill == null || skill.effectType != type)
                    continue;
                if (!IsActive(skill.condition, ctx))
                    continue;
                bonus += skill.magnitude;
            }
            return bonus;
        }
    }
}
