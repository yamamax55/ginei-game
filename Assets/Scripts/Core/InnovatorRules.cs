using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 革新者の純ロジック（#革新者・史実＝織田信長）。信長の三本柱を再現する：
    /// ①<b>先見性</b>（楽市楽座・兵農分離・既存権威の打破）＝新技術の採用が速く効果が大きい、
    /// ②<b>新技術の積極活用</b>（長篠の鉄砲三段撃ち・鉄甲船）＝敵より新しい技術を持つほど戦場で優位、
    /// ③<b>若き日のうつけ</b>（尾張の大うつけ）＝若い頃は周囲に過小評価されるが、開花して覇者となる。
    /// うつけは「周囲の評価（perceived）」が低いだけで実力（true）は高い＝実効値パターン（基準非破壊）。
    /// 数式は係数を返すだけで、研究#123-127・技術→戦闘・登用#2313 等の既存窓口へ橋渡しする。決定論・test-first。
    /// </summary>
    public static class InnovatorRules
    {
        /// <summary>うつけ（過小評価）が解ける年齢＝開花。これ未満は侮られる。</summary>
        public const int BloomAge = 20;
        /// <summary>うつけ期に周囲が見る能力の割合（実力の半分に見える＝大うつけ）。</summary>
        public const float UtsukePerception = 0.5f;
        /// <summary>先見性による新技術採用の最大加速（情報100で）。</summary>
        public const float MaxForesight = 0.5f;
        /// <summary>新技術優位の技術差1段あたりの上乗せ。</summary>
        public const float NewTechScale = 0.1f;
        /// <summary>新技術優位の最大上乗せ（長篠＝鉄砲の早期大量投入）。</summary>
        public const float MaxNewTechBonus = 0.5f;

        /// <summary>開花したか（革新者は <see cref="BloomAge"/> で開花・並の者は常に開花扱い）。</summary>
        public static bool HasBloomed(bool isInnovator, int age)
            => !isInnovator || age >= BloomAge;

        /// <summary>
        /// 周囲が見る能力（perceived）。革新者の若年期は実力の <see cref="UtsukePerception"/> 倍に侮られる（うつけ）。
        /// <b>実力（true）は変えない</b>＝過小評価は周囲の認識だけ（敵AIの油断・登用の安さ等に使う）。開花後は実力どおり。
        /// </summary>
        public static int PerceivedAbility(int trueAbility, int age, bool isInnovator)
        {
            int t = Mathf.Clamp(trueAbility, 0, 100);
            if (isInnovator && age < BloomAge)
                return Mathf.Clamp(Mathf.RoundToInt(t * UtsukePerception), 0, 100);
            return t;
        }

        /// <summary>うつけと侮られているか（革新者かつ若年）。敵の油断・登用の安さの入口。</summary>
        public static bool IsUnderestimated(bool isInnovator, int age)
            => isInnovator && age < BloomAge;

        /// <summary>
        /// 先見性＝新技術の採用速度・効果の倍率（研究#123-127 の出力等に乗る）。革新者は情報能力で加速、並は1.0。
        /// </summary>
        public static float TechAdoptionFactor(bool isInnovator, int intelligence)
            => isInnovator ? 1f + Mathf.Clamp(intelligence, 0, 100) / 100f * MaxForesight : 1f;

        /// <summary>
        /// 新技術優位の戦闘倍率。革新者が敵より新しい技術段を持つほど与効果が増す（長篠の三段撃ち）。
        /// 技術段は任意スケール（同段=1.0・上回るほど最大 <see cref="MaxNewTechBonus"/> までクランプ）。並は1.0。
        /// </summary>
        public static float NewTechAdvantage(bool isInnovator, int ownTechLevel, int enemyTechLevel)
        {
            if (!isInnovator) return 1f;
            int gap = Mathf.Max(0, ownTechLevel - enemyTechLevel);
            return Mathf.Clamp(1f + gap * NewTechScale, 1f, 1f + MaxNewTechBonus);
        }
    }
}
