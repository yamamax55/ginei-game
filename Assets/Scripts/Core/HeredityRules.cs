using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 能力の遺伝の純ロジック（結婚と出産システム基盤・<see cref="ChildbirthRules"/> が使う唯一の窓口）。
    /// 子の能力は<b>両親とある程度相関するが、ばらつく</b>＝両親の中間値へ寄りつつ、母集団平均へ回帰し、乱数で散る。
    /// </summary>
    /// <remarks>
    /// <b>倫理ガード：優生学は採らない</b>。
    /// (1) 遺伝率は1未満で必ず<b>平均回帰</b>する＝有能な親同士でも世代を重ねて能力を吊り上げ続けられない（ラチェットが効かない）。
    /// (2) 乱数の散らばりで<b>きょうだいでも能力が違い</b>、低能力の親から高能力児／高能力の親から凡庸児が生まれうる。
    /// (3) 「優秀な配偶子を選ぶ」「劣った子を間引く」といった<b>選別 API は提供しない</b>（最大値を採らない＝中間＋回帰＋乱数のみ）。
    /// ＝能力で人を選別して品種改良することは仕組み上できない。
    /// </remarks>
    public static class HeredityRules
    {
        /// <summary>遺伝の調整値。</summary>
        public readonly struct HeredityParams
        {
            /// <summary>遺伝率 0..1（中間親値が子へ伝わる強さ。1未満＝平均回帰。既定0.5）。</summary>
            public readonly float heritability;
            /// <summary>母集団平均（回帰先＝能力の基準。既定50）。</summary>
            public readonly float populationMean;
            /// <summary>ばらつきの振幅（±この幅まで乱数で散る。既定12）。</summary>
            public readonly float spread;
            /// <summary>能力の上限（下限は0。既定100＝<see cref="AdmiralData.MaxStatValue"/> と同枠）。</summary>
            public readonly float statMax;

            public HeredityParams(float heritability, float populationMean, float spread, float statMax)
            {
                this.heritability = Mathf.Clamp01(heritability);
                this.populationMean = populationMean;
                this.spread = Mathf.Max(0f, spread);
                this.statMax = Mathf.Max(1f, statMax);
            }

            /// <summary>既定＝遺伝率0.5・平均50・ばらつき±12・上限100。</summary>
            public static HeredityParams Default => new HeredityParams(0.5f, 50f, 12f, 100f);
        }

        /// <summary>中間親値（両親の平均）。</summary>
        public static float MidParent(float a, float b) => (a + b) * 0.5f;

        /// <summary>
        /// 子の期待能力（乱数抜き）＝母集団平均＋遺伝率×(中間親値−母集団平均)＝<b>平均回帰</b>。
        /// 遺伝率1で中間親値そのまま、0で母集団平均。<b>両親の最大値は採らない</b>（優生学的な選別をしない）。
        /// </summary>
        public static float ExpectedStat(float parentA, float parentB, HeredityParams p)
        {
            float mid = MidParent(parentA, parentB);
            return p.populationMean + p.heritability * (mid - p.populationMean);
        }

        /// <summary>
        /// 子の能力1つを決める＝<see cref="ExpectedStat"/> に乱数のばらつきを乗せて 0..上限へ丸める（int）。
        /// <paramref name="roll"/>∈[0,1] を対称なノイズ ±spread に写す（0.5で無ノイズ＝期待値）。決定論（roll は呼び出し側が供給）。
        /// </summary>
        public static int InheritStat(int parentA, int parentB, float roll, HeredityParams p)
        {
            float expected = ExpectedStat(parentA, parentB, p);
            float noise = (Mathf.Clamp01(roll) * 2f - 1f) * p.spread; // [0,1] → [-spread, +spread]
            int v = Mathf.RoundToInt(expected + noise);
            return Mathf.Clamp(v, 0, Mathf.RoundToInt(p.statMax));
        }

        public static int InheritStat(int parentA, int parentB, float roll)
            => InheritStat(parentA, parentB, roll, HeredityParams.Default);

        /// <summary>離散特性（性格）の突然変異率の既定（親と無関係な値が出る確率）。</summary>
        public const float DefaultTraitMutation = 0.1f;

        /// <summary>
        /// 財産特性（性格）の遺伝＝<b>どちらかの親からランダムに受け継ぐ</b>（<paramref name="pickRoll"/>&lt;0.5 で親A）。
        /// <paramref name="mutateRoll"/> が突然変異率を下回ると<b>親と無関係な特性</b>になる（多様性）。
        /// 倫理ガード：親の優劣で選ばない（ランダム＋突然変異＝優生学的選別でない）。
        /// </summary>
        public static FinancialTrait InheritFinancialTrait(FinancialTrait a, FinancialTrait b, float pickRoll, float mutateRoll, float mutationChance)
        {
            pickRoll = Mathf.Clamp01(pickRoll);
            if (mutateRoll < mutationChance)
                return (FinancialTrait)Mathf.Clamp(Mathf.FloorToInt(pickRoll * 3f), 0, 2); // 突然変異＝3値から
            return pickRoll < 0.5f ? a : b;
        }

        public static FinancialTrait InheritFinancialTrait(FinancialTrait a, FinancialTrait b, float pickRoll, float mutateRoll)
            => InheritFinancialTrait(a, b, pickRoll, mutateRoll, DefaultTraitMutation);

        /// <summary>劣性遺伝（マスクされた潜在能力）の調整値。</summary>
        public readonly struct RecessiveParams
        {
            /// <summary>子で潜在が発現（開花）する確率（0..1・既定0.08＝まれ）。</summary>
            public readonly float bloomChance;
            /// <summary>潜在の世代減衰（既定0.9＝消えにくく血統に埋もれて残る）。</summary>
            public readonly float carrierDecay;
            /// <summary>開花に値する潜在の下限（これ未満は発現しない・既定75＝開花は明確に高い）。</summary>
            public readonly int minBloom;

            public RecessiveParams(float bloomChance, float carrierDecay, int minBloom)
            {
                this.bloomChance = Mathf.Clamp01(bloomChance);
                this.carrierDecay = Mathf.Clamp01(carrierDecay);
                this.minBloom = Mathf.Clamp(minBloom, 0, 100);
            }

            /// <summary>既定＝発現8%・減衰0.9・開花下限75。</summary>
            public static RecessiveParams Default => new RecessiveParams(0.08f, 0.9f, 75);
        }

        /// <summary>
        /// 子が受け継ぐ<b>マスクされた潜在能力（劣性）</b>＝両親の潜在の高い方を世代減衰して引き継ぎ（埋もれて残る）、
        /// さらに<b>親の高い発現能力が子に出なかったぶん（才能が劣性化）</b>を潜在として格納する。0..100。
        /// </summary>
        public static int InheritRecessiveCarrier(int parentARecessive, int parentBRecessive,
            int parentMaxExpressed, int childMaxExpressed, RecessiveParams r)
        {
            float carried = Mathf.Max(parentARecessive, parentBRecessive) * r.carrierDecay;
            float masked = Mathf.Max(0, parentMaxExpressed - childMaxExpressed); // 親の才能が子に発現せず劣性化
            return Mathf.Clamp(Mathf.RoundToInt(Mathf.Max(carried, masked)), 0, 100);
        }

        public static int InheritRecessiveCarrier(int parentARecessive, int parentBRecessive, int parentMaxExpressed, int childMaxExpressed)
            => InheritRecessiveCarrier(parentARecessive, parentBRecessive, parentMaxExpressed, childMaxExpressed, RecessiveParams.Default);

        /// <summary>潜在が開花する条件か＝潜在が下限以上・発現ロールが確率を下回る・現発現を上回る（劣性が顕性化）。</summary>
        public static bool WouldBloom(int expressed, int carrier, float bloomRoll, RecessiveParams r)
            => carrier >= r.minBloom && Mathf.Clamp01(bloomRoll) < r.bloomChance && carrier > expressed;

        /// <summary>
        /// 劣性能力の発現（開花）＝<see cref="WouldBloom"/> が成立すれば潜在値±ノイズへ跳ね上がる、さもなくば現発現のまま（マスク継続）。
        /// 凡庸な血統からでも突如 名将が生まれる＝乱数で起こり選別できない（優生学NGと整合）。
        /// </summary>
        public static int ExpressRecessive(int expressed, int carrier, float bloomRoll, float noiseRoll, RecessiveParams r, HeredityParams h)
        {
            if (!WouldBloom(expressed, carrier, bloomRoll, r)) return expressed;
            float noise = (Mathf.Clamp01(noiseRoll) * 2f - 1f) * h.spread;
            return Mathf.Clamp(Mathf.RoundToInt(carrier + noise), 0, Mathf.RoundToInt(h.statMax));
        }
    }
}
