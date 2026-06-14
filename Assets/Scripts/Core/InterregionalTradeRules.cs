using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 勢力間／星系間 交易の薄いオーケストレータ（純ロジック・唯一の合成窓口）。
    /// 既存の未配線 Core（<see cref="SpatialArbitrageRules"/>＝空間裁定／<see cref="TransportCostRules"/>＝輸送コスト／
    /// <see cref="TradeRules"/>＝対外交易の利得／<see cref="TradingHouseRules"/>＝商社／<see cref="MerchantCreditRules"/>＝商人の信用）
    /// と単一市場（<see cref="MarketRules"/>＝需給価格・<see cref="Market"/>）を合成し、二つの市場（地域）の間に交易を流す。
    /// 価格が高い地域へ供給が流れ価格差が縮む＝一物一価へ向かう（遠隔地交易）。ただし輸送コストが利鞘を超えると
    /// 交易は成立しない（割に合わない）。数式は既存ルールへ委譲＝二重実装しない。状態は引数で受ける（自前保管庫を持たない）。
    /// 乱数なし・決定論。純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class InterregionalTradeRules
    {
        /// <summary>
        /// 二市場間の交易量（裁定の流量）＝価格差（裁定の源泉）から算出する。安い側（exporter＝輸出元）から
        /// 高い側（importer＝輸入先）へ財が流れる。輸送コストを超える価格差が無ければ交易は成立せず0
        /// （<see cref="SpatialArbitrageRules.IsArbitrageViable"/> ＝遠隔地交易の条件）。
        /// 流量は輸送コスト控除後の利鞘（<see cref="SpatialArbitrageRules.ArbitrageProfit"/> の利幅）に
        /// 比例し、輸出元の供給と輸入先の需要の小さい方（取引可能量）でクランプ＝供給も需要も無い取引はできない。
        /// </summary>
        public static float TradeFlow(Market exporter, Market importer, float transportCost)
        {
            if (exporter == null || importer == null) return 0f;

            // exporter=安い側／importer=高い側を価格で判定（高い側へ流れる）。
            float buyPrice = Mathf.Min(exporter.price, importer.price);
            float sellPrice = Mathf.Max(exporter.price, importer.price);
            float gap = SpatialArbitrageRules.PriceGap(buyPrice, sellPrice); // 高−安（負は0）

            // 輸送コストを超える価格差が無ければ割に合わない＝交易しない。
            if (!SpatialArbitrageRules.IsArbitrageViable(gap, transportCost)) return 0f;

            // 輸送コスト控除後の利幅（利鞘）。
            float margin = gap - Mathf.Max(0f, transportCost);
            if (margin <= 0f) return 0f;

            // 取引可能量＝高い側（輸入先）の需要と安い側（輸出元）の供給の小さい方。
            float tradable = Mathf.Min(Mathf.Max(0f, exporter.supply), Mathf.Max(0f, importer.demand));
            if (tradable <= 0f) return 0f;

            // 利幅が大きいほど多く流れる（利鞘で正規化）＝裁定の流量。価格差を上限に取引可能量でクランプ。
            float intensity = Mathf.Clamp01(margin / gap);
            return tradable * intensity;
        }

        /// <summary>
        /// 1tick の二地域交易（破壊的・総量保存的）。<see cref="TradeFlow"/> ぶんの財を安い側（輸出元）から
        /// 高い側（輸入先）へ移し（輸出元の供給を減らし輸入先の供給を増やす＝総供給は保存）、両市場の価格を
        /// <see cref="MarketRules.ClearingPrice"/> で需給から引き直す＝安い方は供給が減って上がり高い方は供給が増えて下がる
        /// ＝価格差が縮む（一物一価へ）。輸送コストが利鞘を超える（交易ゼロ）なら何もしない。
        /// 価格基準（basePrice）は各市場の現価格を採る（<see cref="MarketRules.Tick"/> 単体版に倣う近似）。
        /// </summary>
        public static void TickPair(Market a, Market b, float transportCost, float dt, MarketRules.MarketParams p)
        {
            if (a == null || b == null || dt <= 0f) return;

            float flowRate = TradeFlow(SourceOf(a, b), SinkOf(a, b), transportCost);
            if (flowRate <= 0f) return;

            // この tick で動かす量（dt 比例＝timeScale 追従）。移せる量を超えない。
            Market src = SourceOf(a, b); // 安い側（輸出元）
            Market dst = SinkOf(a, b);   // 高い側（輸入先）
            float move = Mathf.Min(flowRate * Mathf.Max(0f, dt), Mathf.Max(0f, src.supply));
            if (move <= 0f) return;

            // 総量保存：輸出元の供給を減らし輸入先の供給を増やす（財が移動する）。
            src.supply = Mathf.Max(0f, src.supply - move);
            dst.supply = Mathf.Max(0f, dst.supply + move);

            // 価格を需給から引き直す＝供給が減った安い側は上がり供給が増えた高い側は下がる（価格差が縮む）。
            src.price = MarketRules.ClearingPrice(src.supply, src.demand, Mathf.Max(0f, src.price), p);
            dst.price = MarketRules.ClearingPrice(dst.supply, dst.demand, Mathf.Max(0f, dst.price), p);
        }

        /// <summary>既定パラメータ版の二地域交易 tick。</summary>
        public static void TickPair(Market a, Market b, float transportCost, float dt)
            => TickPair(a, b, transportCost, dt, MarketRules.MarketParams.Default);

        /// <summary>二市場のうち安い側（輸出元＝供給を出す）。同値なら a を採る（決定論）。</summary>
        private static Market SourceOf(Market a, Market b) => a.price <= b.price ? a : b;

        /// <summary>二市場のうち高い側（輸入先＝財を受ける）。同値なら b を採る（決定論）。</summary>
        private static Market SinkOf(Market a, Market b) => a.price <= b.price ? b : a;
    }
}
