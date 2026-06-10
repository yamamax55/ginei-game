using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 株式市場の評価・収束（CAP-1 #185・純ロジック test-first・唯一の窓口）。
    /// 適正株価＝収益×市場心理倍率（強気で割高・弱気で割安）。配当利回り＝配当/株価。
    /// 暴落リスク＝弱気かつ割高（バブル）で高まる。株価は <see cref="Tick"/> で適正株価へ滑らかに収束。
    /// 少数の量で株価が創発する＝タイクン化回避（建設マイクロ・通貨ツリーは持たない）。
    /// 調整値は <see cref="StockParams"/> に集約（既定 <see cref="StockParams.Default"/>）。
    /// </summary>
    public static class StockMarketRules
    {
        /// <summary>株式市場の調整値（PER・心理倍率の幅・上下限・収束速度・暴落係数）。</summary>
        public readonly struct StockParams
        {
            /// <summary>基準株価収益率（PER＝収益1あたりの基準株価。心理中立0.5での倍率）。</summary>
            public readonly float basePer;
            /// <summary>心理倍率の振れ幅（心理0で 1-swing 倍・1で 1+swing 倍。0..1想定）。</summary>
            public readonly float sentimentSwing;
            /// <summary>株価下限（収益×basePer に対する倍率＝下落の底）。</summary>
            public readonly float minPriceRatio;
            /// <summary>株価上限（収益×basePer に対する倍率＝高騰の天井）。</summary>
            public readonly float maxPriceRatio;
            /// <summary>株価が適正へ寄る速さ（/戦略秒）。</summary>
            public readonly float convergeSpeed;
            /// <summary>割高度→暴落リスクの効き（大きいほどバブルで崩れやすい）。</summary>
            public readonly float crashSensitivity;

            public StockParams(float basePer, float sentimentSwing, float minPriceRatio, float maxPriceRatio, float convergeSpeed, float crashSensitivity)
            {
                this.basePer = Mathf.Max(0f, basePer);
                this.sentimentSwing = Mathf.Clamp01(sentimentSwing);
                this.minPriceRatio = Mathf.Max(0f, minPriceRatio);
                this.maxPriceRatio = Mathf.Max(minPriceRatio, maxPriceRatio); // 上限は下限以上
                this.convergeSpeed = Mathf.Max(0f, convergeSpeed);
                this.crashSensitivity = Mathf.Max(0f, crashSensitivity);
            }

            /// <summary>
            /// 既定＝PER15・心理倍率±0.5（弱気0.5倍/強気1.5倍）・下限0.25倍/上限4倍・収束速度2・暴落係数1。
            /// </summary>
            public static StockParams Default => new StockParams(15f, 0.5f, 0.25f, 4f, 2f, 1f);
        }

        /// <summary>
        /// 収益と市場心理から適正株価を算出する純関数（CAP-1 #185）。
        /// 適正株価＝収益×basePer×心理倍率（心理0.5で中立1.0・0で 1-swing・1で 1+swing）。
        /// 収益×basePer×(min..max) でクランプ。収益0は株価0（無価値）。
        /// </summary>
        public static float FairPrice(float earnings, float sentiment, StockParams p)
        {
            float e = Mathf.Max(0f, earnings);
            if (e <= 0f) return 0f; // 収益なし＝無価値

            float baseValue = e * p.basePer;
            float lo = baseValue * p.minPriceRatio;
            float hi = baseValue * p.maxPriceRatio;

            float s = Mathf.Clamp01(sentiment);
            float sentimentFactor = 1f + (s - 0.5f) * 2f * p.sentimentSwing; // 0→1-swing / 0.5→1 / 1→1+swing
            float price = baseValue * sentimentFactor;
            return Mathf.Clamp(price, lo, hi);
        }

        /// <summary>
        /// 配当利回りを算出する純関数（CAP-1 #185）＝配当/株価。株価0は利回り0（評価不能＝中立）。
        /// </summary>
        public static float DividendYield(float dividend, float sharePrice)
        {
            float sp = Mathf.Max(0f, sharePrice);
            if (sp <= 0f) return 0f; // 株価なし＝評価不能
            float d = Mathf.Max(0f, dividend);
            return d / sp;
        }

        /// <summary>
        /// 暴落リスク(0..1)を算出する純関数（CAP-1 #185）：弱気(低 sentiment)かつ割高(overvaluation>0)で高まる。
        /// overvaluation＝(現在株価/適正株価 − 1) を想定（正＝割高＝バブル）。
        /// リスク＝割高度×crashSensitivity×弱気度((1−sentiment))。割安・強気は崩れにくい。0..1でクランプ。
        /// </summary>
        public static float CrashRisk(float sentiment, float overvaluation, StockParams p)
        {
            float over = Mathf.Max(0f, overvaluation); // 割安(負)は暴落要因にしない
            float bearishness = 1f - Mathf.Clamp01(sentiment); // 弱気ほど崩れやすい
            return Mathf.Clamp01(over * p.crashSensitivity * bearishness);
        }

        /// <summary>
        /// 1tick の株価更新（CAP-1 #185）：現在株価を適正株価（収益×心理）へ滑らかに収束させる
        /// （MoveTowards 風・dt 比例＝timeScale 追従）。null・dt&lt;=0 は無変化・無例外。
        /// </summary>
        public static void Tick(Company c, StockParams p, float dt)
        {
            if (c == null || dt <= 0f) return;
            float target = FairPrice(c.earnings, c.sentiment, p);
            float step = Mathf.Abs(target - c.sharePrice) * p.convergeSpeed * dt;
            c.sharePrice = Mathf.MoveTowards(c.sharePrice, target, step);
            c.sharePrice = Mathf.Max(0f, c.sharePrice);
        }
    }
}
