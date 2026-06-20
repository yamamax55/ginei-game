using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 覇王の純ロジック（#覇王・銀河英雄伝説＝ラインハルト・フォン・ローエングラム）。腐敗した旧体制を底辺から覆す常勝の革命児を再現する：
    /// ①<b>各個撃破の電撃戦</b>（敵が合流する前に1つずつ粉砕＝アスターテ。敵が分散しているほど強い）、
    /// ②<b>攻勢インフレ</b>（防御を捨て全軍前進＝戦いが進むほど与効果が増す）、
    /// ③<b>黄金の獅子のカリスマ</b>（味方士気を限界突破させ、若き才・叩き上げの部下を何倍にも引き上げる）、
    /// ④<b>門閥貴族への憎悪</b>（腐敗・貴族体制への特効）、
    /// ⑤<b>キルヒアイスの喪失で暴走</b>（相棒健在なら完璧だが、失うと攻撃激増・防御/精神崩壊）、
    /// ⑥<b>戦いの中でしか生きられぬ</b>（好敵手がいて初めて輝く・平時は不調）、
    /// ⑦<b>短期決戦無敵・持久戦に弱い</b>（若き天才・短命＝泥沼の持久/ゲリラ戦でストレス）。
    /// 専用旗艦ブリュンヒルトは <see cref="SignatureShipRegistry"/>（ラインハルト→ブリュンヒルト）。美貌・姉のペンダントは flavor。
    /// 数式は係数を返すだけで既存窓口（士気/移動/`RisingHero`#立身出世/`DynastyRules`#867腐敗/`ConditionRules`#2306）へ橋渡しする。
    /// 実効値パターン・決定論・test-first。
    /// </summary>
    public static class KaiserRules
    {
        /// <summary>各個撃破の最大上乗せ（敵が完全分散のとき）。</summary>
        public const float DefeatInDetailMax = 0.4f;
        /// <summary>攻勢インフレの最大上乗せ（戦い終盤）。</summary>
        public const float OffensiveEscalationMax = 0.3f;
        /// <summary>黄金の獅子の士気限界突破倍率。</summary>
        public const float CharismaMorale = 1.3f;
        /// <summary>部下を引き上げる最大上乗せ（高才の部下ほど大）。</summary>
        public const float SubordinateAmplifyMax = 0.3f;
        /// <summary>門閥貴族（腐敗・旧体制）への特効倍率。</summary>
        public const float AntiEstablishment = 1.25f;
        /// <summary>暴走時の与効果倍率（相棒喪失）。</summary>
        public const float BerserkAttack = 1.5f;
        /// <summary>暴走時の被ダメ倍率（防御/精神崩壊）。</summary>
        public const float BerserkDamageTaken = 1.4f;
        /// <summary>好敵手がいるときの倍率（本領）。</summary>
        public const float RivalPresent = 1.15f;
        /// <summary>好敵手がいないときの倍率（退屈で不調）。</summary>
        public const float RivalAbsent = 0.9f;
        /// <summary>持久戦/ゲリラ戦での倍率（短期決戦無敵だが泥沼に弱い）。</summary>
        public const float AttritionPenalty = 0.75f;

        /// <summary>各個撃破の与効果倍率。敵の集結度 enemyConcentration(0..1) が低い（分散）ほど大。並は1.0。</summary>
        public static float DefeatInDetailFactor(bool isKaiser, float enemyConcentration)
            => isKaiser ? Mathf.Lerp(1f + DefeatInDetailMax, 1f, Mathf.Clamp01(enemyConcentration)) : 1f;

        /// <summary>攻勢インフレの与効果倍率。戦闘進行 battleProgress(0..1) とともに増す。並は1.0。</summary>
        public static float OffensiveEscalationFactor(bool isKaiser, float battleProgress)
            => isKaiser ? 1f + Mathf.Clamp01(battleProgress) * OffensiveEscalationMax : 1f;

        /// <summary>黄金の獅子＝味方士気の限界突破倍率（並は1.0）。</summary>
        public static float CharismaMoraleFactor(bool isKaiser)
            => isKaiser ? CharismaMorale : 1f;

        /// <summary>部下を引き上げる倍率（高才の部下ほど大＝若き天才/叩き上げが輝く・並は1.0）。</summary>
        public static float SubordinateAmplificationFactor(bool isKaiser, int subordinateAbility)
            => isKaiser ? 1f + Mathf.Clamp(subordinateAbility, 0, 100) / 100f * SubordinateAmplifyMax : 1f;

        /// <summary>門閥貴族（腐敗・旧体制）への特効倍率（対象相手のみ・並は1.0）。</summary>
        public static float AntiEstablishmentBonus(bool isKaiser, bool vsCorruptAristocracy)
            => (isKaiser && vsCorruptAristocracy) ? AntiEstablishment : 1f;

        /// <summary>暴走しているか（覇王かつ相棒〔キルヒアイス〕喪失）。良心を失い箍が外れる。</summary>
        public static bool IsBerserk(bool isKaiser, bool partnerLost)
            => isKaiser && partnerLost;

        /// <summary>暴走時の与効果倍率（攻撃激増・非暴走/並は1.0）。</summary>
        public static float BerserkAttackFactor(bool berserk)
            => berserk ? BerserkAttack : 1f;

        /// <summary>暴走時の被ダメ倍率（防御/精神崩壊で脆い・非暴走/並は1.0）。</summary>
        public static float BerserkDamageTakenFactor(bool berserk)
            => berserk ? BerserkDamageTaken : 1f;

        /// <summary>好敵手の有無による倍率（覇王はヤンのような好敵手がいて輝く・いないと退屈で不調・並は1.0）。</summary>
        public static float RivalPresenceFactor(bool isKaiser, bool worthyRivalExists)
            => isKaiser ? (worthyRivalExists ? RivalPresent : RivalAbsent) : 1f;

        /// <summary>持久戦/ゲリラ戦の倍率（短期決戦無敵だが泥沼の持久戦に弱い・並は1.0）。</summary>
        public static float AttritionPenaltyFactor(bool isKaiser, bool prolonged)
            => (isKaiser && prolonged) ? AttritionPenalty : 1f;
    }
}
