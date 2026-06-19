using UnityEngine;

namespace Ginei
{
    /// <summary>供給反応（価格弾力性）の調整係数。</summary>
    public readonly struct SupplyResponseParams
    {
        /// <summary>価格弾力性（価格比の産出への効き＝大きいほど供給が価格に強く反応する）。</summary>
        public readonly float elasticity;
        /// <summary>産出倍率の下限（安値でも稼働を絞りきらない＝固定費・操業継続の床）。</summary>
        public readonly float minFactor;
        /// <summary>産出倍率の上限（高値でも能力の天井で青天井にしない＝設備の限界）。</summary>
        public readonly float maxFactor;
        /// <summary>利鞘から増産へ寄せる感応度（限界利益が薄いほど増産しない＝利潤動機）。</summary>
        public readonly float marginGain;

        public SupplyResponseParams(float elasticity, float minFactor, float maxFactor, float marginGain)
        {
            this.elasticity = Mathf.Max(0f, elasticity);
            this.minFactor = Mathf.Max(0f, minFactor);
            this.maxFactor = Mathf.Max(this.minFactor, maxFactor); // 上限は下限以上
            this.marginGain = Mathf.Max(0f, marginGain);
        }

        /// <summary>既定＝弾力性0.5・下限0.5倍/上限2倍（価格2倍で増産・半値で減産だが操業は止めない）・利鞘感応度1。</summary>
        public static SupplyResponseParams Default => new SupplyResponseParams(0.5f, 0.5f, 2f, 1f);
    }

    /// <summary>
    /// 供給反応の純ロジック（供給曲線＝価格弾力性of供給・経済フィードバックの欠けた環）。
    /// 既存の市場（<see cref="MarketRules"/>＝需給→価格）と生産（<see cref="ResourceProductionRules"/>＝類型→産出／
    /// <see cref="ManufacturerRules"/>＝投入→産出）は <b>価格→生産</b> の環が繋がっていない＝価格が上がっても
    /// 生産者は増産せず、暴落しても減産しない（供給が価格に無反応）。本ルールは <b>価格シグナル→産出倍率</b> を返す
    /// 単一の窓口＝価格が基準より高ければ増産（利潤を追う）・低ければ減産（赤字を避ける）。倍率は呼び手が既存の
    /// 産出（`ResourceProductionRules.Produce` の factor / `ManufacturerRules` の targetOutput）に掛けて適用する
    /// （実効値パターン＝基準の産出率は書き換えない）。これで <b>高値→増産→供給増→価格低下</b> の負帰還が閉じ、
    /// グラットや払底が動的に自己修正する。需要側の所得弾力性（<see cref="ConsumptionDemandRules"/>）の供給側の対。
    /// 価格は <see cref="MarketRules"/> へ・利潤の核は <see cref="ManufacturerRules"/> へ委譲し二重実装しない。
    /// 乱数なし・決定論・有界。純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class SupplyResponseRules
    {
        /// <summary>産出倍率の絶対床（どんな入力でもこれ未満にはしない＝完全停止を避ける安全弁）。</summary>
        public const float HardMinFactor = 0f;

        /// <summary>
        /// 価格比＝現在価格/基準価格（1で均衡＝増減なし）。基準価格0以下は1（中立）。負の現在価格は0へクランプ。
        /// </summary>
        public static float PriceRatio(float price, float basePrice)
        {
            if (basePrice <= 0f) return 1f;
            return Mathf.Max(0f, price) / basePrice;
        }

        /// <summary>
        /// 価格シグナルに対する産出倍率（供給曲線）。価格比^弾力性で増減し、上下限でクランプ。
        /// 価格比1（均衡）で必ず1.0＝後方互換（価格情報が無い／均衡なら従来の産出のまま）。
        /// 価格比&gt;1（高値）で増産（&gt;1.0、上限 maxFactor まで）・価格比&lt;1（安値）で減産（&lt;1.0、下限 minFactor まで）。
        /// </summary>
        public static float OutputFactor(float price, float basePrice, SupplyResponseParams p)
        {
            float ratio = PriceRatio(price, basePrice);
            // 弾力性0なら価格無反応＝常に1.0（後方互換）。
            float raw = p.elasticity <= 0f ? 1f : Mathf.Pow(ratio, p.elasticity);
            return Mathf.Clamp(raw, Mathf.Max(HardMinFactor, p.minFactor), p.maxFactor);
        }

        /// <summary>
        /// 利潤動機を加味した産出倍率＝価格シグナルの増産ぶんを「単位あたり利鞘」の薄さで割り引く。
        /// 利鞘（価格−単位費用）が薄い／赤字なら高値でも増産しない（限界利益＝<see cref="ManufacturerRules.UnitProfit"/> へ委譲）。
        /// 価格≤費用（赤字）なら増産は完全に消え減産のみ残る＝損を増やさない。価格0以下は増産動機なし。
        /// </summary>
        public static float OutputFactorWithMargin(float price, float basePrice, float unitCost, SupplyResponseParams p)
        {
            float baseFactor = OutputFactor(price, basePrice, p);
            // 増産ぶん（1超）だけを利鞘で割り引く。減産（1未満）はそのまま（赤字回避は強める方向）。
            if (baseFactor <= 1f) return baseFactor;

            float pr = Mathf.Max(0f, price);
            float profit = ManufacturerRules.UnitProfit(pr, unitCost); // 価格−単位費用
            // 利鞘比（0..1）＝利鞘/価格。価格0や赤字なら増産動機なし。
            float marginRatio = pr <= 0f ? 0f : Mathf.Clamp01(profit / pr);
            float damp = Mathf.Clamp01(marginRatio * p.marginGain);
            // 増産ぶん(baseFactor−1)を利鞘で割り引いて1へ寄せる。
            return 1f + (baseFactor - 1f) * damp;
        }

        /// <summary>
        /// 市場（<see cref="Market"/>）の現在価格と基準価格から産出倍率を直接返す簡便版（呼び手の合成を1行に）。
        /// market が null なら1.0（中立＝従来どおり）。
        /// </summary>
        public static float OutputFactorFor(Market market, float basePrice, SupplyResponseParams p)
            => market == null ? 1f : OutputFactor(market.price, basePrice, p);
    }
}
