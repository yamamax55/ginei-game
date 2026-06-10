using System;
using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 提督パッシブスキルの効果種別（#137-140・SK-1 #138）。
    /// 倍率系（攻撃/防御/機動）は基準値に乗算、加算系（士気維持/索敵範囲）は基準値へ加算する想定。
    /// </summary>
    public enum SkillEffectType
    {
        攻撃倍率,
        防御倍率,
        機動倍率,
        士気維持,
        索敵範囲,
    }

    /// <summary>
    /// スキルの発動条件（パッシブ＝状況依存・SK-3 #140）。
    /// 常時＝無条件、劣勢時＝兵力で劣る、交戦時＝戦闘中、側背面時＝敵の側背面を取っている。
    /// </summary>
    public enum SkillCondition
    {
        常時,
        劣勢時,
        交戦時,
        側背面時,
    }

    /// <summary>
    /// 提督パッシブスキルの純データ（#137-140・SK-1 #138）。基準能力を上書きせず、
    /// <see cref="AdmiralSkillRules"/> が条件成立時にだけ修正子（倍率/加算）として読む（実効値パターン）。
    /// 倍率系は magnitude を乗算修正子（1.0 基準）、加算系は magnitude を加算量として扱う。
    /// </summary>
    [Serializable]
    public class AdmiralSkill
    {
        /// <summary>スキル名（表示用）。</summary>
        public string skillName;

        /// <summary>効果種別（どの能力に効くか）。</summary>
        public SkillEffectType effectType;

        /// <summary>効果量（倍率系＝乗算する倍率／加算系＝加算量）。</summary>
        public float magnitude = 1f;

        /// <summary>発動条件（成立時のみ効果が乗る）。</summary>
        public SkillCondition condition = SkillCondition.常時;
    }
}
