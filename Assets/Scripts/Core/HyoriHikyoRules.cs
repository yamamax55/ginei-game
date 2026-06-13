using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 表裏比興の純ロジック（#表裏比興・史実＝真田昌幸）。豊臣秀吉が真田昌幸を「表裏比興の者」と評した梟雄性を再現する。
    /// <b>発動条件＝主家滅亡</b>（昌幸は主家・武田家の滅亡〔1582〕後に独立勢力として覚醒した）。覚醒すると：
    /// ①寡兵で大軍を翻弄（上田合戦で徳川の大軍を寡兵で二度撃退）＝劣勢ほど与効果が増す、
    /// ②大国の間を自在に変節して生き残る（武田→織田→北条→徳川→上杉→豊臣…）＝忠誠に縛られぬ日和見の寝返り、
    /// ③外交・調略で大国を手玉に取る（天正壬午の乱）、④敗れても滅びを免れる生存力。
    /// 主家が健在な間は並の提督（潜在）。実効値パターン・決定論・test-first。既存窓口（`AllegianceDriftRules`#2312/
    /// `LoyaltyRules`#817/`BattleWithdrawalRules`/外交#189）へ橋渡しする係数・可否を返すだけ。
    /// </summary>
    public static class HyoriHikyoRules
    {
        /// <summary>劣勢で与効果に乗る最大上乗せ（寡兵で大軍を翻弄＝上田合戦）。</summary>
        public const float MaxUnderdogBonus = 0.5f;
        /// <summary>劣勢倍率の傾き（兵力比の不利1点あたり）。</summary>
        public const float UnderdogScale = 0.25f;
        /// <summary>外交・調略の最大冴え（情報100で）。</summary>
        public const float MaxGuile = 0.5f;
        /// <summary>覚醒時の基礎生存率（敗れても滅びを免れる）。</summary>
        public const float BaseSurvival = 0.5f;
        /// <summary>生存率への能力寄与（統率・情報それぞれ最大このぶん）。</summary>
        public const float SurvivalAbilityWeight = 0.25f;

        /// <summary>
        /// 覚醒しているか。<b>表裏比興の者であり、かつ主家が滅亡している</b>ときのみ真価を発揮する（発動条件＝主家滅亡）。
        /// 主家健在なら潜在（false）。
        /// </summary>
        public static bool IsActive(bool isHyoriHikyo, bool lordHouseFallen)
            => isHyoriHikyo && lordHouseFallen;

        /// <summary>
        /// 劣勢倍率（自軍&lt;敵軍ほど与効果が増す・有利時は1.0）。上田合戦＝寡兵で大軍を翻弄。
        /// 兵力比（敵/自）が1で1.0、不利が大きいほど 1+<see cref="MaxUnderdogBonus"/> までクランプ。
        /// </summary>
        public static float UnderdogFactor(float ownStrength, float enemyStrength)
        {
            float own = Mathf.Max(1f, ownStrength);
            float ratio = Mathf.Max(0f, enemyStrength) / own;        // 敵/自
            float disadvantage = Mathf.Max(0f, ratio - 1f);          // 不利ぶんだけ
            return Mathf.Clamp(1f + disadvantage * UnderdogScale, 1f, 1f + MaxUnderdogBonus);
        }

        /// <summary>覚醒時のみ劣勢倍率を返す（非覚醒は1.0）。会戦の与効果に乗る想定。</summary>
        public static float CombatFactor(bool active, float ownStrength, float enemyStrength)
            => active ? UnderdogFactor(ownStrength, enemyStrength) : 1f;

        /// <summary>
        /// 日和見の変節が許されるか（覚醒時＝忠誠に縛られず生存のため自在に寝返る）。
        /// `AllegianceDriftRules`/`LoyaltyRules` が通常の忠誠ゲートを上書きする入口。
        /// </summary>
        public static bool CanOpportunisticallyDefect(bool active) => active;

        /// <summary>外交・調略の冴え（覚醒時に情報能力で大国を手玉に取る・非覚醒は1.0）。</summary>
        public static float DiplomaticGuileFactor(bool active, int intelligence)
            => active ? 1f + Mathf.Clamp(intelligence, 0, 100) / 100f * MaxGuile : 1f;

        /// <summary>
        /// 覚醒時に敗れても滅びを免れる生存率（統率・情報で上がる・0..1）。非覚醒は0（特別な生存なし）。
        /// 真田は何度も滅亡を回避した＝この生存力。
        /// </summary>
        public static float SurvivalChance(bool active, int leadership, int intelligence)
        {
            if (!active) return 0f;
            float c = BaseSurvival
                + Mathf.Clamp(leadership, 0, 100) / 100f * SurvivalAbilityWeight
                + Mathf.Clamp(intelligence, 0, 100) / 100f * SurvivalAbilityWeight;
            return Mathf.Clamp01(c);
        }

        /// <summary>敗北時に滅びを免れたか（roll∈[0,1) を注入・決定論）。</summary>
        public static bool EvadesDestruction(bool active, int leadership, int intelligence, float roll)
            => roll < SurvivalChance(active, leadership, intelligence);
    }
}
