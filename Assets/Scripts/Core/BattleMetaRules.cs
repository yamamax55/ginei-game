using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 会戦結果のメタ反映（#2260）。既存の <see cref="GrowthRules"/>/<see cref="CaptivityRules"/> を
    /// 会戦の決着・撃墜イベントから呼ぶための橋渡し純ロジック。
    /// <br/>
    /// - 戦果（与ダメ・撃墜数）→ 経験値量の算定（GrowthRules に渡す入力を作る純関数）。<br/>
    /// - 旗艦撃墜時に提督が捕虜/戦死/離脱のいずれかになる判定（CaptivityRules へ委譲）。<br/>
    /// 値・確率はすべて const に集約。乱数は roll(0..1) を引数で受ける決定論設計。test-first。
    /// </summary>
    public static class BattleMetaRules
    {
        // --- 経験値算定の調整値 ---

        /// <summary>与ダメージ 1000 あたりに換算する経験値の基準量。</summary>
        public const float ExperiencePerThousandDamage = 1.0f;

        /// <summary>敵旗艦を1隻撃墜するごとに加算する経験値。</summary>
        public const float ExperiencePerKill = 5.0f;

        /// <summary>勝者側への経験値ボーナス倍率。</summary>
        public const float WinnerExperienceMult = 1.5f;

        /// <summary>敗者側への経験値倍率（苦境での成長）。</summary>
        public const float LoserExperienceMult = 0.8f;

        // --- 提督の運命（撃墜時）の調整値 ---

        /// <summary>
        /// 旗艦撃墜時の捕虜判定で使う包囲フラグ（追われているが完全包囲ではない）。
        /// CaptivityRules.CaptureChance へ渡す encircled 引数の既定値。
        /// </summary>
        public const bool DefaultEncircled = false;

        /// <summary>
        /// 捕虜判定を通過した後、戦死とみなす roll 閾値。
        /// この値未満なら戦死、以上なら離脱（生存）。
        /// </summary>
        public const float KIAThreshold = 0.25f;

        // --- 純関数 ---

        /// <summary>
        /// 戦果（与ダメ・撃墜数・勝敗）から獲得経験値量を算定する
        /// （<see cref="GrowthRules.GainExperience"/> に渡す amount。dt=1 として使う）。
        /// 乱数は引かない（決定論）。
        /// </summary>
        /// <param name="damageDealt">この提督が与えた累計ダメージ。</param>
        /// <param name="kills">この提督が撃墜した敵旗艦数（概算でよい）。</param>
        /// <param name="isWinner">この会戦で勝者側だったか。</param>
        /// <returns>GrowthRules.GainExperience に渡す amount。</returns>
        public static float ExperienceFromBattle(int damageDealt, int kills, bool isWinner)
        {
            float dmgExp = Mathf.Max(0, damageDealt) / 1000f * ExperiencePerThousandDamage;
            float killExp = Mathf.Max(0, kills) * ExperiencePerKill;
            float raw = dmgExp + killExp;
            float mult = isWinner ? WinnerExperienceMult : LoserExperienceMult;
            return Mathf.Max(0f, raw * mult);
        }

        /// <summary>
        /// 旗艦撃墜時の提督の運命を決定する純関数（捕虜/戦死/離脱）。
        /// <paramref name="roll"/> は [0, 1) の一様乱数を外部から注入する（決定論テスト可）。
        /// <list type="bullet">
        /// <item><description>捕虜判定（<see cref="CaptivityRules.IsCaptured"/> が true）→ <see cref="CommanderFate.捕虜"/>。</description></item>
        /// <item><description>それ以外で 1-roll が <see cref="KIAThreshold"/> 未満 → <see cref="CommanderFate.戦死"/>。</description></item>
        /// <item><description>残り → <see cref="CommanderFate.離脱"/>（生存）。</description></item>
        /// </list>
        /// </summary>
        /// <param name="commandFactor">提督の指揮能力係数（0..1。<see cref="CommandFactorFromStat"/> で変換）。</param>
        /// <param name="moraleFactor">部隊の士気係数（FleetMorale.GetMoraleFactor）。</param>
        /// <param name="roll">外部から注入する乱数 [0, 1)。</param>
        public static CommanderFate ResolveCommanderFate(float commandFactor, float moraleFactor, float roll)
        {
            // 捕虜判定を CaptivityRules へ委譲（包囲なし）。
            if (CaptivityRules.IsCaptured(DefaultEncircled, commandFactor, moraleFactor, roll))
                return CommanderFate.捕虜;

            // 残りの確率で戦死 vs 離脱。roll の補数を使い二重の roll を回避する。
            float deathRoll = 1f - roll;
            return (deathRoll < KIAThreshold) ? CommanderFate.戦死 : CommanderFate.離脱;
        }

        /// <summary>
        /// 提督能力値（0..100）を CaptivityRules へ渡す係数 [0, 1] へ変換する。
        /// </summary>
        public static float CommandFactorFromStat(float stat)
            => Mathf.Clamp01(stat / 100f);
    }

    /// <summary>旗艦撃墜時の提督の運命（BattleMetaRules.ResolveCommanderFate の戻り値）。</summary>
    public enum CommanderFate
    {
        /// <summary>敵に捕縛される（CaptivityRules で管理）。</summary>
        捕虜,
        /// <summary>戦死（会戦での戦没）。</summary>
        戦死,
        /// <summary>生還・離脱（戦線から消えるが生存）。</summary>
        離脱
    }
}
