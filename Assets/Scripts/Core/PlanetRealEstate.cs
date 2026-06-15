namespace Ginei
{
    /// <summary>
    /// 惑星の不動産基盤（#2019 惑星層・純データ）。惑星（<see cref="Province"/>）ごとの<b>地価・賃料・地価指数・開発済み割合</b>を持つ＝
    /// 勢力集約の不動産会社（<see cref="RealEstateRules"/>）と、ネームドの権利証評価（<see cref="PropertyValuationRules"/> #2070）が
    /// 立つ土台。住宅戸数の需給は <see cref="HousingDemandRules"/>（#2091）が別途担い、ここは<b>土地・物件の価値と賃料水準</b>に特化。
    /// 解決は <see cref="PlanetRealEstateRules"/>(static) が唯一の窓口。<see cref="Province.realEstate"/> に保持（null＝未形成＝後方互換）。
    /// 個別物件 micro は持たない（惑星×集約＝タイクン化/終盤ラグ回避）。
    /// </summary>
    [System.Serializable]
    public class PlanetRealEstate
    {
        /// <summary>地価（惑星の土地・物件の評価総額）。賃料×評価倍率×地価指数で年々更新。</summary>
        public float landValue = 0f;

        /// <summary>年間賃料水準（実効）。人口×魅力（所得proxy）に連動＝賃料の裏付け。</summary>
        public float rentLevel = 0f;

        /// <summary>地価指数（1.0基準）。好況で値上がり・崩壊で下落＝賃料に対する割高さ（バブル）の蓄積。</summary>
        public float priceIndex = 1f;

        /// <summary>開発済み割合（0..1）。残りが開発余地。需要に向けて年々進む（供給の伸びしろ）。</summary>
        public float developedRatio = 0.3f;
    }
}
