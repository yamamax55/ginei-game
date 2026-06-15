using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 副産物グルット（連産の従産物が供給過剰で価格暴落）の調整係数（#1113）。
    /// </summary>
    public readonly struct ByproductGlutParams
    {
        /// <summary>供給過剰度→価格の効き（大きいほど暴落が急＝需給比の値崩れ弾力性）。</summary>
        public readonly float collapseElasticity;
        /// <summary>価格下限（基準1.0に対する底＝二束三文でもゼロにはしない）。</summary>
        public readonly float minPriceRatio;
        /// <summary>貯蔵できない従産物の単位あたり廃棄コスト係数（負の価値＝捨てるのに金がかかる）。</summary>
        public readonly float disposalCostRate;
        /// <summary>貯蔵できる従産物の廃棄コスト軽減倍率（0..1＝在庫に積めるぶん安く済む）。</summary>
        public readonly float storableRelief;
        /// <summary>従産物の有効利用の効き（川下産業規模→過剰の価値転換の強さ）。</summary>
        public readonly float valorizationGain;

        public ByproductGlutParams(float collapseElasticity, float minPriceRatio,
            float disposalCostRate, float storableRelief, float valorizationGain)
        {
            this.collapseElasticity = Mathf.Max(0f, collapseElasticity);
            this.minPriceRatio = Mathf.Clamp01(minPriceRatio);
            this.disposalCostRate = Mathf.Max(0f, disposalCostRate);
            this.storableRelief = Mathf.Clamp01(storableRelief);
            this.valorizationGain = Mathf.Max(0f, valorizationGain);
        }

        /// <summary>
        /// 既定＝暴落弾力性1・価格下限0.1倍（二束三文）・廃棄コスト率0.5・貯蔵で廃棄1割まで軽減（軽減倍率0.1）・有効利用ゲイン0.8。
        /// </summary>
        public static ByproductGlutParams Default =>
            new ByproductGlutParams(1f, 0.1f, 0.5f, 0.1f, 0.8f);
    }

    /// <summary>
    /// 副産物グルット（連産×市場）の純ロジック（#1113・唯一の窓口）。
    /// <see cref="CoupledProductionRules"/>（連産＝1工程が固定比で複数財を同時産出。主産物需要で従産物が強制的に湧く
    /// ＝<see cref="CoupledProductionRules.ForcedByproduct"/>）が吐いた従産物が、市場で需要を超えて溢れ（供給過剰＝グルット）、
    /// 価格が暴落する道筋を式にする。「主産物を作れば従産物が溢れる＝捌けない従産物は主産物の足を引っ張る」
    /// （<see cref="PrimaryProductionDrag"/>＝連産の負債）が核。
    /// <see cref="CoupledProductionRules"/>（生産の結合＝固定比同時産出）とは分担が別＝こちらは<b>その従産物の市場処理</b>
    /// （供給過剰・価格暴落・廃棄・有効利用・主産物への足枷）を扱う。
    /// <see cref="MarketRules"/>（単一財の需給均衡価格）とも別＝連産で<b>強制的に</b>湧く過剰に特化した値崩れと負の価値を初めて式にする。
    /// 乱数なし・決定論。純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class ByproductGlutRules
    {
        /// <summary>
        /// 主産物生産に固定比で連れ出される従産物の供給量（連産の宿命）。
        /// <see cref="CoupledProductionRules.ForcedByproduct"/> の従産物量を受ける想定＝主産物生産×従産物比。
        /// 主産物を作るほど従産物は望むと望まざるとに関わらず湧く（供給過剰の起点）。負はクランプ。
        /// </summary>
        public static float ByproductSupply(float primaryProduction, float byproductRatio)
        {
            float prod = Mathf.Max(0f, primaryProduction);
            float ratio = Mathf.Max(0f, byproductRatio);
            return prod * ratio;
        }

        /// <summary>
        /// 供給過剰度（0..1）＝需要を超えて捌けない在庫の割合。
        /// 供給≤需要なら0（全部捌ける＝グルットなし）、需要0で供給ありなら1（全量だぶつき）。
        /// グルット＝(供給−需要)/供給。負・ゼロ除算はクランプ。
        /// </summary>
        public static float GlutSeverity(float byproductSupply, float byproductDemand)
        {
            float supply = Mathf.Max(0f, byproductSupply);
            float demand = Mathf.Max(0f, byproductDemand);
            if (supply <= 0f) return 0f; // 供給なし＝過剰なし
            float excess = supply - demand;
            if (excess <= 0f) return 0f; // 需要が呑む＝過剰なし
            return Mathf.Clamp01(excess / supply);
        }

        /// <summary>
        /// 価格暴落後の価格倍率（0..1＝基準価格に対する割合）。供給過剰ほど値崩れ。
        /// 価格＝(1−グルット)^elasticity を minPriceRatio で下支え＝需要の数倍出れば二束三文（下限へ張り付く）。
        /// グルット0で1.0（暴落なし）、グルット1でほぼ下限。
        /// </summary>
        public static float PriceCollapse(float glutSeverity, ByproductGlutParams p)
        {
            float glut = Mathf.Clamp01(glutSeverity);
            float price = Mathf.Pow(1f - glut, p.collapseElasticity);
            return Mathf.Clamp(price, p.minPriceRatio, 1f);
        }

        public static float PriceCollapse(float glutSeverity)
            => PriceCollapse(glutSeverity, ByproductGlutParams.Default);

        /// <summary>
        /// 廃棄コスト（負の価値＝捌けない従産物を捨てるのにかかる金）。供給過剰ほど嵩む。
        /// 貯蔵できる（storable=true）なら storableRelief 倍まで軽減（在庫に積めるぶん安く済む）。
        /// 貯蔵できない従産物（廃ガス・スラグ等）は満額の廃棄コストがかかる＝負の価値。
        /// コスト＝グルット×disposalCostRate×(貯蔵可なら storableRelief / 不可なら1)。
        /// </summary>
        public static float DisposalCost(float glutSeverity, bool storable, ByproductGlutParams p)
        {
            float glut = Mathf.Clamp01(glutSeverity);
            float relief = storable ? p.storableRelief : 1f;
            return glut * p.disposalCostRate * relief;
        }

        public static float DisposalCost(float glutSeverity, bool storable)
            => DisposalCost(glutSeverity, storable, ByproductGlutParams.Default);

        /// <summary>
        /// 従産物の有効利用（0..1＝過剰のうち価値に転じた割合）。
        /// 従産物を原料にする川下産業（downstreamCapacity 0..1＝その規模）があれば、だぶついた従産物が価値に転じる
        /// （コークス→化学・重油→発電型）。川下が大きいほどグルットを吸収＝価値化。
        /// 有効利用＝グルット×downstreamCapacity×valorizationGain（0..1にクランプ）。
        /// </summary>
        public static float ByproductValorization(float glutSeverity, float downstreamCapacity, ByproductGlutParams p)
        {
            float glut = Mathf.Clamp01(glutSeverity);
            float cap = Mathf.Clamp01(downstreamCapacity);
            return Mathf.Clamp01(glut * cap * p.valorizationGain);
        }

        public static float ByproductValorization(float glutSeverity, float downstreamCapacity)
            => ByproductValorization(glutSeverity, downstreamCapacity, ByproductGlutParams.Default);

        /// <summary>
        /// 主産物への足枷（連産の負債）＝従産物の処理コストが主産物の採算をどれだけ削るか（採算比 0..1）。
        /// 「捌けない従産物は主産物の足を引っ張る」＝従産物の廃棄コストが主産物の利幅（primaryMargin）を蝕む。
        /// 採算＝Clamp01(1 − 廃棄コスト/利幅)。利幅が薄いほど足枷が重く（廃棄コスト>利幅で0＝連産が赤字に転落）、
        /// 利幅0以下は採算ゼロ。1.0＝足枷なし（健全）。
        /// </summary>
        public static float PrimaryProductionDrag(float disposalCost, float primaryMargin)
        {
            float cost = Mathf.Max(0f, disposalCost);
            float margin = primaryMargin;
            if (margin <= 0f) return 0f; // 利幅なし＝採算ゼロ
            return Mathf.Clamp01(1f - cost / margin);
        }
    }
}
