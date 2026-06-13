using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 立身出世の純ロジック（#立身出世・史実＝豊臣秀吉）。足軽（木下藤吉郎）から天下人へ駆け上がった史上最高の出世を再現：
    /// ①<b>門地を問わぬ実力本位の出世</b>（低い身分の天井を無視し、有能ほど最速で昇進）、
    /// ②<b>人たらし</b>（人心掌握・調略で味方を増やす＝登用/忠誠の名手）、
    /// ③<b>戦略機動の妙</b>（中国大返し＝戦略移動の速さ・兵糧攻めで戦わずに勝つ）。
    /// 数式は係数を返すだけで、出世#900-905/#155-156・登用#2313・忠誠#2312・戦略移動 等の既存窓口へ橋渡しする。
    /// 実効値パターン（基準非破壊）・決定論・test-first。
    /// </summary>
    public static class RisingHeroRules
    {
        /// <summary>有能さ100での昇進最速化の上乗せ（最大で倍速出世）。</summary>
        public const float MaxPromotionSpeed = 1.0f;
        /// <summary>人たらしの最大上乗せ（統率100で・登用/忠誠/調略に乗る）。</summary>
        public const float MaxCharm = 0.5f;
        /// <summary>戦略機動（中国大返し）の上乗せ。</summary>
        public const float ForcedMarchBonus = 0.5f;

        /// <summary>
        /// 門地・席次の天井を無視するか（立身出世型は実力だけで頂点まで昇る）。
        /// 通常は低born/低席次に昇進の天井があるが（`SeniorityRules`/`MeritRankRules`）、これが true なら撤廃。
        /// </summary>
        public static bool IgnoresPedigreeCeiling(bool isRisingHero) => isRisingHero;

        /// <summary>
        /// 昇進速度の倍率（有能ほど速い＝足軽から最速出世）。ability(0..100・武才/文才/戦功) で加速。並は1.0。
        /// </summary>
        public static float PromotionSpeedFactor(bool isRisingHero, int ability)
            => isRisingHero ? 1f + Mathf.Clamp(ability, 0, 100) / 100f * MaxPromotionSpeed : 1f;

        /// <summary>
        /// 人たらしの倍率（人心掌握・調略）。統率（カリスマ）で上がり、登用#2313 の説得・忠誠#2312・相性#2305 に乗る。並は1.0。
        /// </summary>
        public static float CharmFactor(bool isRisingHero, int leadership)
            => isRisingHero ? 1f + Mathf.Clamp(leadership, 0, 100) / 100f * MaxCharm : 1f;

        /// <summary>戦略機動の倍率（中国大返し＝戦略移動の速さ）。立身出世型は速い・並は1.0。</summary>
        public static float ForcedMarchFactor(bool isRisingHero)
            => isRisingHero ? 1f + ForcedMarchBonus : 1f;
    }
}
