using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 惑星の不動産基盤のロジック（#2019 惑星層・純ロジック・唯一の窓口）。惑星（<see cref="Province"/>）の素地
    /// ＝人口・安定度・統合度・生活水準・経済類型から<b>地価・賃料・地価指数（バブル）</b>を導く土台。
    /// 賃料は<b>魅力（所得 proxy）×人口</b>に連動し（賃料の裏付け）、地価は賃料×評価倍率×地価指数で立つ。
    /// 地価指数は<b>土地の希少性（需要/供給）</b>で年々値上がりし（賃料より速いと割高＝バブル）、金融危機で崩壊する。
    /// 住宅戸数の需給は <see cref="ConsumerDemandRules"/>（簡略版BOM #2098 の住宅品目）へ委譲し二重実装しない。地価変動は <see cref="RealEstateRules"/>（勢力集約）・
    /// <see cref="PropertyValuationRules"/>（権利証 #2070）の入力にもなる。基準フィールドは非破壊（実効値パターン）。test-first。
    /// </summary>
    public static class PlanetRealEstateRules
    {
        /// <summary>調整値（マジックナンバー禁止＝集約）。</summary>
        public readonly struct Params
        {
            public readonly float baseRentPerCapita;   // 1人あたり基準賃料（賃料の素）
            public readonly float valuationMultiple;   // 評価倍率＝地価/賃料の基準（基準の price-to-rent）
            public readonly float appreciation;        // 平時の地価指数の年間上昇（需要圧で増幅）
            public readonly float crashRate;           // 金融危機時の地価指数の下落率（負）
            public readonly float bubbleThreshold;     // 価格/賃料倍率がこれを超えると割高＝バブル
            public readonly float minPriceIndex;       // 地価指数の下限（暴落の底）
            public readonly float maxPriceIndex;       // 地価指数の上限（runaway 回避＝タイクン化防止）

            public Params(float baseRentPerCapita, float valuationMultiple, float appreciation,
                float crashRate, float bubbleThreshold, float minPriceIndex, float maxPriceIndex)
            {
                this.baseRentPerCapita = Mathf.Max(0f, baseRentPerCapita);
                this.valuationMultiple = Mathf.Max(0.01f, valuationMultiple);
                this.appreciation = appreciation;
                this.crashRate = Mathf.Clamp(crashRate, -1f, 0f);
                this.bubbleThreshold = Mathf.Max(0.01f, bubbleThreshold);
                this.minPriceIndex = Mathf.Max(0.01f, minPriceIndex);
                this.maxPriceIndex = Mathf.Max(this.minPriceIndex, maxPriceIndex);
            }

            /// <summary>既定＝1人あたり賃料0.1・評価倍率15・年上昇3%・崩壊−25%・バブル閾値25倍・指数0.2〜5.0。</summary>
            public static Params Default => new Params(0.1f, 15f, 0.03f, -0.25f, 25f, 0.2f, 5f);
        }

        /// <summary>経済類型ごとの住宅供給余地（0..1）＝居住が最大・鉱業/工業は手狭（残りが地価の希少性を生む）。</summary>
        public static float ResidentialCapacity(SystemType type)
        {
            switch (type)
            {
                case SystemType.居住: return 1.0f;
                case SystemType.農業: return 0.7f;
                case SystemType.工業: return 0.5f;
                case SystemType.鉱業: return 0.4f;
                default: return 0.7f;
            }
        }

        /// <summary>居住の魅力（0..1）＝安定度（治安）・生活水準・統合度の重み付き平均（住みよい惑星ほど高い＝所得/需要 proxy）。</summary>
        public static float Desirability(float stability01, float livingStandard, float integration)
        {
            float s = Mathf.Clamp01(stability01);
            float l = Mathf.Clamp01(livingStandard);
            float g = Mathf.Clamp01(integration);
            return Mathf.Clamp01(s * 0.4f + l * 0.4f + g * 0.2f);
        }

        /// <summary>年間賃料水準＝人口×1人あたり賃料×(0.5＋0.5×魅力)（住みよい惑星ほど賃料が高い＝賃料の裏付け）。</summary>
        public static float RentLevel(float population, float desirability, Params p)
            => Mathf.Max(0f, population) * p.baseRentPerCapita * (0.5f + 0.5f * Mathf.Clamp01(desirability));

        /// <summary>土地の希少性圧（0.5..2.0）＝魅力/住宅供給余地（住みたいのに供給が手狭なほど高い＝地価が速く値上がり）。</summary>
        public static float LandDemandPressure(float desirability, float residentialCapacity)
        {
            float cap = Mathf.Clamp(residentialCapacity, 0.01f, 1f);
            return Mathf.Clamp(Mathf.Clamp01(desirability) / cap, 0.5f, 2.0f);
        }

        /// <summary>地価＝賃料×評価倍率×地価指数（賃料の裏付けに割高さの指数が乗る）。非負。</summary>
        public static float LandValue(float rentLevel, float priceIndex, Params p)
            => Mathf.Max(0f, rentLevel) * p.valuationMultiple * Mathf.Max(0f, priceIndex);

        /// <summary>価格/賃料倍率＝地価/賃料（高いほど割高＝バブル指標）。賃料0以下は超大。</summary>
        public static float PriceToRent(PlanetRealEstate re)
        {
            if (re == null) return 0f;
            return re.rentLevel <= 0f ? 999999f : Mathf.Max(0f, re.landValue) / re.rentLevel;
        }

        /// <summary>バブルか＝価格/賃料倍率が閾値超（賃料の裏付けを超えた地価）。</summary>
        public static bool IsBubble(PlanetRealEstate re, Params p)
            => re != null && PriceToRent(re) > p.bubbleThreshold;

        /// <summary>賃料負担率＝1人あたり賃料/賃金指数（高いほど家計を圧迫＝住みづらさ）。賃金0以下は超大。</summary>
        public static float RentToIncome(float rentPerCapita, float wageIndex)
            => wageIndex <= 0f ? 999999f : Mathf.Max(0f, rentPerCapita) / wageIndex;

        /// <summary>住宅が手頃か＝賃料負担率が閾値以下（高負担＝流出/不満の圧）。</summary>
        public static bool IsAffordable(float rentToIncome, float threshold)
            => rentToIncome <= Mathf.Max(0f, threshold);

        /// <summary><see cref="Province.realEstate"/> を冪等生成（賃料/地価の初期値を素地から立てる）。null Province は何もしない。</summary>
        public static PlanetRealEstate Ensure(Province prov, Params p)
        {
            if (prov == null) return null;
            if (prov.realEstate == null)
            {
                var re = new PlanetRealEstate();
                float des = Desirability(prov.stability / 100f, prov.livingStandard, prov.integration);
                re.rentLevel = RentLevel(prov.population, des, p);
                re.priceIndex = 1f;
                re.landValue = LandValue(re.rentLevel, re.priceIndex, p);
                prov.realEstate = re;
            }
            return prov.realEstate;
        }

        /// <summary>
        /// 惑星の不動産を1年ぶん更新（賃料は素地に追従・地価指数は希少性で値上がり/危機で崩壊・地価を再評価・開発を進める）。
        /// crisis＝金融危機（#1939）なら地価指数を <see cref="Params.crashRate"/> で下落（バブル崩壊）。基準フィールドは破壊するが
        /// （これ自体が不動産の状態）、Province の素地（人口/安定度等）は読むだけ＝非破壊。
        /// </summary>
        public static void TickYear(Province prov, Params p, bool crisis)
        {
            if (prov == null) return;
            PlanetRealEstate re = Ensure(prov, p);

            float des = Desirability(prov.stability / 100f, prov.livingStandard, prov.integration);
            // 賃料は素地（人口×魅力＝所得 proxy）に追従。
            re.rentLevel = RentLevel(prov.population, des, p);

            // 地価指数：平時は土地の希少性ぶん値上がり（賃料より速い＝バブルの素）・危機で崩壊。上下限でクランプ（runaway 回避）。
            float pressure = LandDemandPressure(des, ResidentialCapacity(prov.systemType));
            float idx = crisis
                ? re.priceIndex * (1f + p.crashRate)
                : re.priceIndex * (1f + p.appreciation * pressure);
            re.priceIndex = Mathf.Clamp(idx, p.minPriceIndex, p.maxPriceIndex);

            // 地価を再評価。
            re.landValue = LandValue(re.rentLevel, re.priceIndex, p);

            // 開発：需要（魅力）に向けて開発済み割合が年々進む（供給の伸びしろ・上限1）。
            re.developedRatio = Mathf.Clamp01(Mathf.MoveTowards(re.developedRatio, Mathf.Clamp01(des), 0.05f));
        }
    }
}
