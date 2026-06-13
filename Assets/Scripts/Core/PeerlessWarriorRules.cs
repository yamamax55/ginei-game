using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 日本一の兵の純ロジック（#日本一の兵・史実＝真田幸村〔信繁〕）。父・昌幸の「表裏比興」（生存の梟雄）とは対照的に、
    /// <b>純粋無比の武勇</b>を再現する。三本柱：
    /// ①<b>とにかく強い</b>（「日本一の兵」＝常時の武勇ボーナス）、
    /// ②<b>真田丸の堅守</b>（大坂冬の陣＝寡兵の防衛で大軍を撃退＝守勢かつ劣勢ほど被ダメ激減）、
    /// ③<b>決死の突撃</b>（大坂夏の陣＝家康本陣へ斬り込み＝兵力が削れ窮地ほど与効果が跳ね上がる死兵）。
    /// 数式は係数を返すだけで `CombatModifiers`#106・`FleetStrength.TakeDamage` 等の既存窓口へ橋渡しする。
    /// 実効値パターン（基準非破壊）・test-first。
    /// </summary>
    public static class PeerlessWarriorRules
    {
        /// <summary>常時の武勇ボーナス（とにかく強い・日本一の兵）。</summary>
        public const float ValorBonus = 0.15f;
        /// <summary>真田丸の基礎被ダメ軽減（守勢で固める）。</summary>
        public const float FortifiedBase = 0.85f;
        /// <summary>劣勢1段あたりの追加軽減（寡兵で大軍を退ける）。</summary>
        public const float FortifiedScale = 0.25f;
        /// <summary>被ダメ軽減の下限（どれだけ寡兵でも崩れぬわけではない）。</summary>
        public const float MinFortified = 0.5f;
        /// <summary>決死の突撃の最大上乗せ（兵力ゼロ近傍の死兵）。</summary>
        public const float MaxDeathCharge = 0.5f;

        /// <summary>常時の与効果倍率（とにかく強い）。並は1.0。</summary>
        public static float ValorFactor(bool isPeerless)
            => isPeerless ? 1f + ValorBonus : 1f;

        /// <summary>
        /// 真田丸の堅守＝被ダメ倍率（&lt;1で堅い）。守勢(fortified)かつ劣勢ほど被ダメが減る。
        /// 非該当（並・守勢でない）は1.0。寡兵で大軍を撃退した冬の陣。
        /// </summary>
        public static float FortifiedDamageTakenFactor(bool isPeerless, bool fortified, float ownStrength, float enemyStrength)
        {
            if (!isPeerless || !fortified) return 1f;
            float own = Mathf.Max(1f, ownStrength);
            float disadvantage = Mathf.Max(0f, Mathf.Max(0f, enemyStrength) / own - 1f);
            return Mathf.Clamp(FortifiedBase - disadvantage * FortifiedScale, MinFortified, 1f);
        }

        /// <summary>
        /// 決死の突撃＝与効果倍率。残存兵力比 ownHpRatio(0..1) が小さい（窮地）ほど苛烈になる死兵。
        /// 満身（1.0）で上乗せ無し・壊滅寸前（0）で最大。並は1.0。家康本陣へ斬り込んだ夏の陣。
        /// </summary>
        public static float DeathChargeFactor(bool isPeerless, float ownHpRatio)
        {
            if (!isPeerless) return 1f;
            float desperation = 1f - Mathf.Clamp01(ownHpRatio);
            return 1f + desperation * MaxDeathCharge;
        }
    }
}
