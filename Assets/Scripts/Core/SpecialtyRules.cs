using UnityEngine;

namespace Ginei
{
    /// <summary>得意戦型（ADM-6 #2307）。提督が真価を発揮する戦闘類型。なし＝得意分野なし（従来動作）。</summary>
    public enum CombatSpecialty { なし, 会戦, 攻城, 機動戦, 防衛, 追撃, 奇襲 }

    /// <summary>会戦の状況類型（得意戦型の一致判定に使う）。</summary>
    public enum BattleSituation { 会戦, 攻城, 機動戦, 防衛, 追撃, 奇襲 }

    /// <summary>
    /// 提督の専門領域・能力の非線形性の純ロジック（ADM-6 #2307）。得意陣形(#104)を「得意戦型」へ一般化し、
    /// 状況一致でボーナス（例：ヤン＝寡兵防衛/機動戦）。加えて90+の「天才の質的飛躍」を線形 `AbilityFactor` に足す
    /// （閾値カーブ・`Talent`#特技の格と整合）。実効値パターン・test-first。
    /// </summary>
    public static class SpecialtyRules
    {
        /// <summary>得意戦型が状況に一致したときの与効果倍率。</summary>
        public const float SpecialtyMatchBonus = 1.15f;
        /// <summary>天才の飛躍が始まる能力しきい値。</summary>
        public const float GeniusThreshold = 90f;
        /// <summary>しきい値超過1点あたりの追加倍率（100で+0.2）。</summary>
        public const float GeniusPerPoint = 0.02f;

        /// <summary>得意戦型が状況に一致するか（なしは常に不一致）。</summary>
        public static bool Matches(CombatSpecialty specialty, BattleSituation situation)
        {
            switch (specialty)
            {
                case CombatSpecialty.会戦:   return situation == BattleSituation.会戦;
                case CombatSpecialty.攻城:   return situation == BattleSituation.攻城;
                case CombatSpecialty.機動戦: return situation == BattleSituation.機動戦;
                case CombatSpecialty.防衛:   return situation == BattleSituation.防衛;
                case CombatSpecialty.追撃:   return situation == BattleSituation.追撃;
                case CombatSpecialty.奇襲:   return situation == BattleSituation.奇襲;
                default: return false; // なし
            }
        }

        /// <summary>得意戦型ボーナス（一致で1.15／不一致・なしで1.0）。</summary>
        public static float SpecialtyBonus(CombatSpecialty specialty, BattleSituation situation)
            => Matches(specialty, situation) ? SpecialtyMatchBonus : 1f;

        /// <summary>
        /// 天才の質的飛躍（90+で線形を超える追加倍率・1.0..1.2）。90未満は1.0＝凡将は線形のまま。
        /// `CombatModifiers.AbilityFactor`（線形）に乗じて使う想定。
        /// </summary>
        public static float GeniusFactor(float effectiveStat)
        {
            float s = Mathf.Clamp(effectiveStat, 0f, 100f);
            if (s <= GeniusThreshold) return 1f;
            return 1f + (s - GeniusThreshold) * GeniusPerPoint;
        }
    }
}
