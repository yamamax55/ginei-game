using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 武名・名声の純ロジック（ADM-3 #2304・王騎効果）。高名な提督は戦場を「圧」で支配する＝
    /// 敵の士気を削り（威圧）、味方を鼓舞し、寝返りされにくく、徴募/登用の引力になる。
    /// 英雄時代（<see cref="HeroicAgeRules.HeroInfluenceFactor"/>）では武名の効果が増幅する。
    /// 数式は既存窓口（`MoraleShockRules`#2176/`BattleAllegianceRules`#817）へ橋渡しする係数を返す。test-first。
    /// </summary>
    public static class RenownRules
    {
        /// <summary>武名が敵士気を削る最大割合（fame100・平時）。</summary>
        public const float MaxIntimidation = 0.3f;
        /// <summary>武名が味方を鼓舞する最大上乗せ（fame100）。</summary>
        public const float MaxInspiration = 0.2f;
        /// <summary>武名が寝返りを抑える最大割合（fame100）。</summary>
        public const float MaxDefectionResistance = 0.5f;

        /// <summary>
        /// 威圧＝敵士気を削る割合（0..）。武名に比例し英雄時代で増幅（王騎効果）。
        /// 英雄度 heroism は <see cref="HeroicAgeRules"/> の世界英雄度（0=平時）。
        /// </summary>
        public static float IntimidationFactor(int fame, float heroism)
            => Mathf.Clamp01(fame / 100f) * MaxIntimidation * HeroicAgeRules.HeroInfluenceFactor(heroism);

        /// <summary>鼓舞＝味方士気の倍率（1.0..1.2）。武名に比例。</summary>
        public static float InspirationFactor(int fame)
            => 1f + Mathf.Clamp01(fame / 100f) * MaxInspiration;

        /// <summary>寝返り耐性（0..0.5）。武名ある主君の麾下は離反しにくい（#817 の調略浸透を割り引く）。</summary>
        public static float DefectionResistance(int fame)
            => Mathf.Clamp01(fame / 100f) * MaxDefectionResistance;

        /// <summary>徴募/登用の引力（0..1）。名将の旗には人が集う。</summary>
        public static float RecruitmentPull(int fame)
            => Mathf.Clamp01(fame / 100f);

        /// <summary>戦功で武名を獲得（現値＋戦功スケール・0..100クランプ）。大勝ほど名が上がる。</summary>
        public static int Gain(int currentFame, float battleMerit)
            => Mathf.Clamp(currentFame + Mathf.RoundToInt(Mathf.Max(0f, battleMerit)), 0, 100);
    }
}
