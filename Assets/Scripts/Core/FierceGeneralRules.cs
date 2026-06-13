using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 猛将の純ロジック（#猛将・三国志演義＝張飛翼徳）。豪傑にして万人の敵、されど自滅的な欠点を併せ持つ将を再現する：
    /// ①<b>猪突猛進</b>（与効果↑だが隙が大きく被ダメ↑）、
    /// ②<b>長坂の一喝</b>（当陽橋で曹操軍を一喝で退ける＝敵士気を挫く・寡兵で殿を務めるほど強い）、
    /// ③<b>一騎打ちに強い</b>、④<b>正義感にあふれる</b>（大義の戦で味方士気↑）、
    /// ⑤<b>部下に厳しい</b>（配下の士気↓）＋<b>酒癖が悪い</b>（泥酔で能力↓）→ <b>暗殺リスク</b>（張達・范彊に討たれる）。
    /// 桃園の義は徳望/関羽と同じ絆の系統。数式は係数を返すだけで `CombatModifiers`#106・`DuelRules`#2316・
    /// `MoraleShockRules`#2176・`CaptivityRules`#154 等の既存窓口へ橋渡しする。実効値パターン・決定論・test-first。
    /// </summary>
    public static class FierceGeneralRules
    {
        /// <summary>猪突猛進の与効果上乗せ。</summary>
        public const float ChargeAttackBonus = 0.2f;
        /// <summary>猪突の代償＝被ダメ倍率（隙が大きい）。</summary>
        public const float RecklessDamageTaken = 1.25f;
        /// <summary>長坂の一喝で敵士気を挫く割合（通常）。</summary>
        public const float RoarShock = 0.3f;
        /// <summary>殿を単騎で務めるときの一喝（曹操軍を退ける）。</summary>
        public const float RoarShockHoldingAlone = 0.5f;
        /// <summary>一騎打ちの強さ倍率（豪傑・関羽1.5より僅か下）。</summary>
        public const float DuelMight = 1.3f;
        /// <summary>部下に厳しいことによる配下の士気倍率。</summary>
        public const float SubordinateMorale = 0.85f;
        /// <summary>泥酔時の能力倍率。</summary>
        public const float DrunkPenalty = 0.7f;
        /// <summary>素面でも残る暗殺リスク（部下に厳しい）。</summary>
        public const float BaseAssassinationRisk = 0.1f;
        /// <summary>泥酔時の暗殺リスク（張達・范彊に討たれる）。</summary>
        public const float DrunkAssassinationRisk = 0.4f;
        /// <summary>正義の戦での味方士気上乗せ。</summary>
        public const float RighteousMorale = 0.15f;

        /// <summary>猪突猛進の与効果倍率（並は1.0）。</summary>
        public static float ChargeAttackFactor(bool isFierce)
            => isFierce ? 1f + ChargeAttackBonus : 1f;

        /// <summary>猪突の代償＝被ダメ倍率（隙が大きく被弾増・並は1.0）。</summary>
        public static float RecklessDamageTakenFactor(bool isFierce)
            => isFierce ? RecklessDamageTaken : 1f;

        /// <summary>長坂の一喝＝敵士気を挫く割合（単騎で殿を務めるほど大・並は0）。`MoraleShockRules`#2176 へ。</summary>
        public static float IntimidationFactor(bool isFierce, bool holdingAlone)
        {
            if (!isFierce) return 0f;
            return holdingAlone ? RoarShockHoldingAlone : RoarShock;
        }

        /// <summary>一騎打ちの強さ倍率（`DuelRules`#2316 へ・並は1.0）。</summary>
        public static float DuelStrengthFactor(bool isFierce)
            => isFierce ? DuelMight : 1f;

        /// <summary>部下に厳しいことによる配下の士気倍率（並は1.0）。</summary>
        public static float SubordinateMoraleFactor(bool isFierce)
            => isFierce ? SubordinateMorale : 1f;

        /// <summary>泥酔時の能力倍率（酒癖・素面は1.0）。</summary>
        public static float DrunkAbilityFactor(bool drunk)
            => drunk ? DrunkPenalty : 1f;

        /// <summary>暗殺リスク（部下に厳しい＋酒癖＝泥酔で跳ね上がる。並は0）。`CaptivityRules`#154/人事へ。</summary>
        public static float AssassinationRisk(bool isFierce, bool drunk)
        {
            if (!isFierce) return 0f;
            return drunk ? DrunkAssassinationRisk : BaseAssassinationRisk;
        }

        /// <summary>暗殺されるか（roll∈[0,1) を注入・決定論）。</summary>
        public static bool IsAssassinated(bool isFierce, bool drunk, float roll)
            => roll < AssassinationRisk(isFierce, drunk);

        /// <summary>正義感による味方士気の上乗せ（大義の戦のみ・並は0）。</summary>
        public static float RighteousMoraleBonus(bool isFierce, bool justCause)
            => (isFierce && justCause) ? RighteousMorale : 0f;
    }
}
