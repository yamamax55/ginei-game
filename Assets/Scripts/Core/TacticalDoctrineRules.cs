using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 戦術ドクトリン評価の入力コンテキスト（艦隊の現在の戦術状況）。
    /// </summary>
    public readonly struct TacticalContext
    {
        /// <summary>艦隊経験値（練度の基礎。<see cref="VeterancyRules"/> へ委譲）。</summary>
        public readonly float veterancyXp;
        /// <summary>偵察レベル 0..1（0=霧の中・1=完全情報）。</summary>
        public readonly float reconLevel;
        /// <summary>この艦隊が伏兵を仕掛けている（初撃ボーナス側）。</summary>
        public readonly bool sprungAmbush;
        /// <summary>この艦隊が奇襲を受けている（隊形不備・弱体化）。</summary>
        public readonly bool isAmbushVictim;
        /// <summary>奇襲を受けてからの経過時間（<see cref="isAmbushVictim"/> が true の場合のみ有効）。</summary>
        public readonly float timeSinceAmbush;

        public TacticalContext(float veterancyXp, float reconLevel,
            bool sprungAmbush, bool isAmbushVictim, float timeSinceAmbush)
        {
            this.veterancyXp = Mathf.Max(0f, veterancyXp);
            this.reconLevel = Mathf.Clamp01(reconLevel);
            this.sprungAmbush = sprungAmbush;
            this.isAmbushVictim = isAmbushVictim;
            this.timeSinceAmbush = Mathf.Max(0f, timeSinceAmbush);
        }

        /// <summary>中立状態（経験なし・完全情報・伏兵なし・被奇襲なし）。</summary>
        public static TacticalContext Neutral => new TacticalContext(0f, 1f, false, false, 0f);
    }

    /// <summary>
    /// 戦術ドクトリン評価の結果（攻撃・防御倍率と偵察確信度）。
    /// <see cref="TacticalDoctrineRules.Evaluate"/> の戻り値。
    /// </summary>
    public readonly struct TacticalDoctrineResult
    {
        /// <summary>攻撃倍率（練度×伏兵初撃ボーナス）。1.0が通常・奇襲成立で >1。</summary>
        public readonly float attackMultiplier;
        /// <summary>防御倍率（練度×被奇襲隊形不備）。1.0が通常・奇襲被弾直後で <1。</summary>
        public readonly float defenseMultiplier;
        /// <summary>偵察確信度 0..1（霧の濃さ）。AI が敵戦力推定に使う。</summary>
        public readonly float reconConfidence;

        public TacticalDoctrineResult(float attackMultiplier, float defenseMultiplier, float reconConfidence)
        {
            this.attackMultiplier = attackMultiplier;
            this.defenseMultiplier = defenseMultiplier;
            this.reconConfidence = Mathf.Clamp01(reconConfidence);
        }

        /// <summary>倍率1.0×確信度1.0の中立結果。</summary>
        public static TacticalDoctrineResult Neutral => new TacticalDoctrineResult(1f, 1f, 1f);
    }

    /// <summary>
    /// 戦術ドクトリン純ロジックの統合窓口（#playtest サイクル 戦術ドクトリン統合）。
    /// <see cref="AmbushRules"/>・<see cref="VeterancyRules"/>・<see cref="ReconRules"/> を
    /// 単一の <see cref="TacticalDoctrineResult"/> に束ね、<see cref="BattleAiRules"/> や
    /// <see cref="ForceQualityRules"/> と同じ実効値パターンで倍率を返す。
    /// 配線は Game 層の1箇所（BattleAiRules との接点）に留める。test-first。
    /// </summary>
    public static class TacticalDoctrineRules
    {
        /// <summary>倍率の下限（崩壊した艦隊でも完全無力化しない）。</summary>
        public const float MinMultiplier = 0.4f;
        /// <summary>倍率の上限（伏兵×古参でも上限を超えない）。</summary>
        public const float MaxMultiplier = 2.0f;

        /// <summary>
        /// 戦術状況を評価し攻撃倍率・防御倍率・偵察確信度を返す唯一の窓口。
        /// <br/>• 攻撃：<see cref="VeterancyRules.CombatFactor"/> × <see cref="AmbushRules.FirstStrikeFactor"/>
        /// <br/>• 防御：<see cref="VeterancyRules.CombatFactor"/> × <see cref="AmbushRules.VictimCombatFactor"/>
        /// <br/>• 確信度：<see cref="ReconRules.Confidence"/>
        /// </summary>
        public static TacticalDoctrineResult Evaluate(TacticalContext ctx)
        {
            // 練度ボーナス（攻撃・防御の共通ベース）
            float vetFactor = VeterancyRules.CombatFactor(ctx.veterancyXp);

            // 攻撃倍率：伏兵を仕掛けているなら初撃ボーナス
            float firstStrike = AmbushRules.FirstStrikeFactor(ctx.sprungAmbush);
            float attack = Mathf.Clamp(vetFactor * firstStrike, MinMultiplier, MaxMultiplier);

            // 防御倍率：被奇襲中は隊形不備で弱体（時間経過で回復）
            float victimFactor = ctx.isAmbushVictim
                ? AmbushRules.VictimCombatFactor(ctx.timeSinceAmbush)
                : 1f;
            float defense = Mathf.Clamp(vetFactor * victimFactor, MinMultiplier, MaxMultiplier);

            // 偵察確信度（霧の濃さ＝AI の情報精度）
            float confidence = ReconRules.Confidence(ctx.reconLevel);

            return new TacticalDoctrineResult(attack, defense, confidence);
        }

        /// <summary>
        /// BattleAiRules 補助：伏兵を仕掛けるべきか判断する。
        /// 物理的な奇襲成功率（隠蔽×警戒）に AI のスキルゲートを掛けて算出。
        /// roll∈[0,1)。
        /// </summary>
        public static bool ShouldAmbush(float concealment, float alertness, float skill, float roll)
        {
            float chance = AmbushRules.AmbushChance(concealment, alertness) * Mathf.Clamp01(skill);
            return roll < chance;
        }
    }
}
