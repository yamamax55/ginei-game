using System;
using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 市場の在庫・需給・価格（M-1 #180・純データ）。少数の量で価格が創発＝タイクン化回避。
    /// 価格は <see cref="MarketRules.Tick"/> が均衡（<see cref="MarketRules.ClearingPrice"/>）へ滑らかに収束させる。
    /// </summary>
    [Serializable]
    public class Market
    {
        /// <summary>取引される財の種別。</summary>
        public GoodType goodType;
        /// <summary>供給量（>需要で価格下落）。負にはならない。</summary>
        public float supply;
        /// <summary>需要量（>供給で価格高騰）。負にはならない。</summary>
        public float demand;
        /// <summary>現在価格（基準価格中心に需給で揺れる。負にはならない）。</summary>
        public float price;

        public Market() { }

        public Market(GoodType goodType, float supply, float demand, float price)
        {
            this.goodType = goodType;
            this.supply = Mathf.Max(0f, supply);
            this.demand = Mathf.Max(0f, demand);
            this.price = Mathf.Max(0f, price);
        }
    }

    /// <summary>
    /// 市場経済の自動均衡（M-1 需給価格＝#180 中心・M-2 経済→支持 #181・純ロジック test-first・唯一の窓口）。
    /// 財・需給・価格の均衡を解く：需要>供給で高騰／供給>需要で下落／供給=需要で基準価格に戻る。
    /// 少数の量で価格が創発する＝タイクン化回避（建設マイクロ・通貨ツリーは持たない）。
    /// 経済（生活水準）が支持／急進化を生む（M-2 #181）＝生活水準→支持の係数を実効値で返す（基準非破壊）。
    /// 調整値は <see cref="MarketParams"/> に集約（既定 <see cref="MarketParams.Default"/>）。
    /// </summary>
    public static class MarketRules
    {
        /// <summary>市場の調整値（価格弾力性・上下限・収束速度・生活水準の係数）。</summary>
        public readonly struct MarketParams
        {
            /// <summary>価格弾力性（需給比の価格への効き＝大きいほど振れる）。</summary>
            public readonly float elasticity;
            /// <summary>価格下限（基準価格に対する倍率＝下落の底）。</summary>
            public readonly float minPriceRatio;
            /// <summary>価格上限（基準価格に対する倍率＝高騰の天井）。</summary>
            public readonly float maxPriceRatio;
            /// <summary>価格が均衡へ寄る速さ（/戦略秒）。</summary>
            public readonly float convergeSpeed;
            /// <summary>生活水準→支持の係数の基準（生活水準0.5で中立0＝この幅で±）。</summary>
            public readonly float supportSwing;

            public MarketParams(float elasticity, float minPriceRatio, float maxPriceRatio, float convergeSpeed, float supportSwing)
            {
                this.elasticity = Mathf.Max(0f, elasticity);
                this.minPriceRatio = Mathf.Max(0f, minPriceRatio);
                this.maxPriceRatio = Mathf.Max(minPriceRatio, maxPriceRatio); // 上限は下限以上
                this.convergeSpeed = Mathf.Max(0f, convergeSpeed);
                this.supportSwing = Mathf.Max(0f, supportSwing);
            }

            /// <summary>
            /// 既定＝弾力性1・下限0.25倍/上限4倍（少量で大きく動くが青天井にしない）・収束速度2・支持振れ幅1。
            /// </summary>
            public static MarketParams Default => new MarketParams(1f, 0.25f, 4f, 2f, 1f);
        }

        /// <summary>
        /// 需給から均衡価格を算出する純関数（M-1 #180）。需要>供給で高騰／供給>需要で下落／均衡で basePrice。
        /// 価格＝basePrice×(需要/供給)^elasticity を上下限（basePrice×min..max）でクランプ。
        /// 供給0は需要があれば上限・需要も0なら basePrice（取引なし＝中立）。少量で価格が創発する。
        /// </summary>
        public static float ClearingPrice(float supply, float demand, float basePrice, MarketParams p)
        {
            float bp = Mathf.Max(0f, basePrice);
            float s = Mathf.Max(0f, supply);
            float d = Mathf.Max(0f, demand);

            float lo = bp * p.minPriceRatio;
            float hi = bp * p.maxPriceRatio;

            // 供給0：需要があれば天井へ張り付く（希少）。需要も0なら取引なし＝基準価格（中立）。
            if (s <= 0f)
                return d > 0f ? hi : bp;

            float ratio = Mathf.Pow(d / s, p.elasticity); // 需給比の弾力性（>1で高騰・<1で下落）
            float price = bp * ratio;
            return Mathf.Clamp(price, lo, hi);
        }

        /// <summary>
        /// 1tick の価格更新（M-1 #180）：現在価格を均衡価格へ滑らかに収束させる（MoveTowards 風・dt 比例＝timeScale 追従）。
        /// 均衡中心（basePrice）は現在価格を採る＝供給=需要なら現価格を保ち、需給差で上下する（Market 単体版・近似）。
        /// 基準価格を固定したい場合は <see cref="Tick(Market, Good, MarketParams, float)"/> を使う。
        /// </summary>
        public static void Tick(Market m, MarketParams p, float dt)
        {
            if (m == null || dt <= 0f) return;
            TickToTarget(m, ClearingPrice(m.supply, m.demand, Mathf.Max(0f, m.price), p), p, dt);
        }

        /// <summary>
        /// 1tick の価格更新（基準価格固定版・M-1 #180）：<paramref name="good"/> の basePrice を均衡中心として
        /// 需給で均衡価格を求め、現在価格をそこへ滑らかに収束させる（供給=需要なら basePrice へ戻る）。
        /// </summary>
        public static void Tick(Market m, Good good, MarketParams p, float dt)
        {
            if (m == null || good == null || dt <= 0f) return;
            TickToTarget(m, ClearingPrice(m.supply, m.demand, good.basePrice, p), p, dt);
        }

        /// <summary>現在価格を target へ MoveTowards で寄せる（移動量は差×収束速度×dt＝指数的に寄る）。</summary>
        private static void TickToTarget(Market m, float target, MarketParams p, float dt)
        {
            float step = Mathf.Abs(target - m.price) * p.convergeSpeed * dt;
            m.price = Mathf.MoveTowards(m.price, target, step);
            m.price = Mathf.Max(0f, m.price);
        }

        /// <summary>
        /// POP消費（consumption）と必要量（need）から生活水準(0..1)を算出する純関数（M-2 #181）。
        /// 必要量を満たせば1、半分なら0.5＝充足率。need≤0は満たされている扱い＝1。
        /// </summary>
        public static float StandardOfLiving(float consumption, float need, MarketParams p)
        {
            float n = Mathf.Max(0f, need);
            if (n <= 0f) return 1f; // 必要なし＝完全充足
            float c = Mathf.Max(0f, consumption);
            return Mathf.Clamp01(c / n);
        }

        /// <summary>
        /// 生活水準→支持/急進化の係数（M-2 #181・経済が革命を生む・実効値パターン＝基準非破壊）。
        /// 生活水準0.5で中立0、1で+supportSwing（豊かさが支持）、0で−supportSwing（窮乏が急進化）。
        /// 返す係数は呼び出し側が支持/反乱圧へ加算する（ここでは基準値を書き換えない）。
        /// </summary>
        public static float SoLToSupport(float standardOfLiving, MarketParams p)
        {
            float sol = Mathf.Clamp01(standardOfLiving);
            return (sol - 0.5f) * 2f * p.supportSwing; // 0→−swing / 0.5→0 / 1→+swing
        }
    }
}
