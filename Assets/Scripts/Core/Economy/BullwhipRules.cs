using UnityEngine;

namespace Ginei
{
    /// <summary>ブルウィップ効果の調整係数（#1114）。</summary>
    public readonly struct BullwhipParams
    {
        /// <summary>安全在庫1段ぶんが上乗せする増幅の重み（在庫調整が変動を膨らませる強さ）。</summary>
        public readonly float safetyStockWeight;
        /// <summary>リードタイム1単位ぶんが上乗せする増幅の重み（待ち時間が長いほど予測で過剰反応する）。</summary>
        public readonly float leadTimeWeight;
        /// <summary>まとめ発注1単位ぶんが上乗せする発注変動の重み（バッチ化で発注がギザつく）。</summary>
        public readonly float batchingWeight;

        public BullwhipParams(float safetyStockWeight, float leadTimeWeight, float batchingWeight)
        {
            this.safetyStockWeight = Mathf.Max(0f, safetyStockWeight);
            this.leadTimeWeight = Mathf.Max(0f, leadTimeWeight);
            this.batchingWeight = Mathf.Max(0f, batchingWeight);
        }

        /// <summary>既定＝安全在庫重み0.5／リードタイム重み0.25／バッチ重み0.5。</summary>
        public static BullwhipParams Default => new BullwhipParams(0.5f, 0.25f, 0.5f);
    }

    /// <summary>
    /// ブルウィップ効果の純ロジック（#1114・唯一の窓口）。多段サプライチェーンで、末端の小さな需要変動が
    /// 各段の在庫調整・予測・まとめ発注を経て<b>上流（原料）へ行くほど指数的に増幅</b>される＝鞭のしなり。
    /// 鞭の先（末端）より根元（上流）が大きく振れる＝1段あたりの増幅率（<see cref="AmplificationPerStage"/>）が
    /// 段数ぶん累乗されて上流の変動（<see cref="UpstreamVariance"/>）になる。発注の変動性（<see cref="OrderVariability"/>）は
    /// 予測誤差とバッチ化で増し、上流の在庫は過剰在庫と欠品を繰り返して振動する（<see cref="InventorySwing"/>）。
    /// <b>情報共有（末端需要を上流が直接見る）と発注平準化が鞭を抑える</b>（<see cref="MitigationByInfoSharing"/>／
    /// <see cref="MitigationBySmoothing"/>）＝可視化が増幅を止める。
    /// <see cref="ChainFragilityRules"/>（#1112・遮断の上下流カスケード＝止まると伝播）とは別＝こちらは<b>変動の増幅</b>。
    /// 各段のバッファ運用は IntermediateBufferRules（中間在庫の積み増し/取り崩し）、所要量の段階展開は
    /// MrpRules（所要計算）が扱う＝本ルールは需要変動が上流へ膨らむ増幅に特化。
    /// 乱数なし・決定論。純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class BullwhipRules
    {
        /// <summary>
        /// 1段あたりの変動増幅率（≥1＝在庫調整が変動を膨らませる）。
        /// 末端の需要変動 demandVariance（観測される需要のばらつき）に対し、各段が安全在庫
        /// <paramref name="safetyStockFactor"/>（0..1＝在庫調整の積極度）とリードタイム <paramref name="leadTime"/>
        /// （長いほど予測で過剰反応）ぶん発注を上乗せする＝<b>変動はそのまま下流へ返さず膨らんで上流へ渡る</b>。
        /// demandVariance は方向（大きいほど膨らみやすい兆候）として弱く効かせ、戻り値は 1 以上（増幅＝1で素通り）。
        /// </summary>
        public static float AmplificationPerStage(float demandVariance, float safetyStockFactor, float leadTime, BullwhipParams p)
        {
            float variance = Mathf.Max(0f, demandVariance);
            float safety = Mathf.Clamp01(safetyStockFactor);
            float lead = Mathf.Max(0f, leadTime);
            // 在庫調整＝安全在庫×重み、予測の過剰反応＝リードタイム×重み。変動が大きいほどわずかに増幅を後押し。
            float varianceNudge = variance / (variance + 1f);          // 0..1（飽和）＝大変動ほど膨らみやすい
            float gain = safety * p.safetyStockWeight + lead * p.leadTimeWeight * (0.5f + 0.5f * varianceNudge);
            return 1f + Mathf.Max(0f, gain);                            // 1以上＝増幅（1で素通り）
        }

        public static float AmplificationPerStage(float demandVariance, float safetyStockFactor, float leadTime)
            => AmplificationPerStage(demandVariance, safetyStockFactor, leadTime, BullwhipParams.Default);

        /// <summary>
        /// N段上流での需要変動（末端の変動が段数を経て<b>指数的に増幅</b>＝鞭の先より根元が大きく振れる）。
        /// 末端変動 endDemandVariance に、1段あたりの増幅率 amplificationPerStage（<see cref="AmplificationPerStage"/>）を
        /// stages 段ぶん累乗して掛ける＝段数で指数的に膨らむ。増幅率1（素通り）なら段数によらず末端のまま、stages0なら末端そのもの。
        /// </summary>
        public static float UpstreamVariance(float endDemandVariance, int stages, float amplificationPerStage)
        {
            float endVar = Mathf.Max(0f, endDemandVariance);
            int n = Mathf.Max(0, stages);
            float amp = Mathf.Max(1f, amplificationPerStage);          // 1未満＝減衰は起きない（鞭は膨らむ）
            return endVar * Mathf.Pow(amp, n);                         // 指数的増幅
        }

        /// <summary>
        /// 発注の変動性（予測誤差とまとめ発注が変動を増す）。
        /// 実需変動 actualDemandVariance に、予測誤差 forecastError（0..1＝外れるほど発注が振れる）と
        /// バッチ化 batchingFactor（まとめ発注の単位＝大きいほど発注がギザつく）ぶん上乗せする＝
        /// <b>正しく読めずまとめて頼むほど発注は実需より大きく揺れる</b>。
        /// </summary>
        public static float OrderVariability(float actualDemandVariance, float forecastError, float batchingFactor, BullwhipParams p)
        {
            float actual = Mathf.Max(0f, actualDemandVariance);
            float error = Mathf.Clamp01(forecastError);
            float batch = Mathf.Max(0f, batchingFactor);
            // 予測誤差は実需変動に比例して上乗せ、バッチ化は発注のギザつきとして加算。
            float inflate = 1f + error + batch * p.batchingWeight;
            return actual * inflate;                                   // 実需≦発注変動（誤差0・バッチ0で等しい）
        }

        public static float OrderVariability(float actualDemandVariance, float forecastError, float batchingFactor)
            => OrderVariability(actualDemandVariance, forecastError, batchingFactor, BullwhipParams.Default);

        /// <summary>
        /// 上流の在庫の振れ（過剰在庫と欠品を繰り返す＝振動の振幅）。
        /// 上流変動 upstreamVariance（<see cref="UpstreamVariance"/>）が大きいほど在庫が大きく上下する＝
        /// 膨らんだ変動を在庫で受けると過剰と欠品を往復する。負入力は0クランプ、振幅は変動に比例。
        /// </summary>
        public static float InventorySwing(float upstreamVariance)
        {
            return Mathf.Max(0f, upstreamVariance);
        }

        /// <summary>
        /// 情報共有による緩和（<b>末端需要を上流が直接見れば増幅が止まる＝可視化が鞭を抑える</b>）。
        /// 基礎ブルウィップ baseBullwhip（緩和前の上流変動）を、情報共有度 informationSharing（0..1＝
        /// 末端需要の見える化）ぶん引き下げる＝共有1で末端変動そのもの（増幅消滅に漸近）まで縮む。
        /// 共有0なら無緩和（基礎のまま）。
        /// </summary>
        public static float MitigationByInfoSharing(float baseBullwhip, float informationSharing)
        {
            float baseB = Mathf.Max(0f, baseBullwhip);
            float share = Mathf.Clamp01(informationSharing);
            return baseB * (1f - share);                              // 共有1で増幅ぶんが消える
        }

        /// <summary>
        /// 発注平準化による緩和（まとめ発注をならして鞭の振れを抑える）。
        /// 基礎ブルウィップ baseBullwhip を、発注平準化度 orderSmoothing（0..1＝発注の均し）ぶん引き下げる。
        /// 情報共有が増幅の源（予測）を断つのに対し、平準化は発注側の振れ（バッチ）をならす＝
        /// 完全平準化（1）でも実需の変動分は残しうるよう <see cref="SmoothingFloor"/> までしか縮まない。
        /// </summary>
        public static float MitigationBySmoothing(float baseBullwhip, float orderSmoothing)
        {
            float baseB = Mathf.Max(0f, baseBullwhip);
            float smooth = Mathf.Clamp01(orderSmoothing);
            float reduction = smooth * (1f - SmoothingFloor);        // 最大で (1-floor) ぶん削る
            return baseB * (1f - reduction);                         // 平準化1で floor 倍まで
        }

        /// <summary>発注平準化で残る最低割合（実需の変動はならしても消えない＝0.2）。</summary>
        public const float SmoothingFloor = 0.2f;
    }
}
