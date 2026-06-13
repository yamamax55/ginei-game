using System.Collections.Generic;
using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 債券市場システム基盤（#161/#185・純ロジック・唯一の窓口）。株式（出資 <see cref="StockMarketSystemRules"/>）と対の<b>借入の市場</b>。
    /// <b>必要利回り＝市場金利＋信用スプレッド</b>（リスクが高いほど上乗せ）、<b>適正価格＝表面利率/必要利回り</b>（額面比＝金利と逆相関）、
    /// <b>現在利回り＝表面利率/価格</b>。発行で資本調達（国債→国庫#163/社債→企業資本）、市場集計（総債務/平均利回り）。test-first。
    /// </summary>
    public static class BondMarketRules
    {
        /// <summary>債券市場の調整値。</summary>
        public readonly struct BondParams
        {
            /// <summary>信用リスク→利回り上乗せ係数（リスク1で +spreadFactor の利回り）。</summary>
            public readonly float spreadFactor;
            /// <summary>価格下限（額面比）。</summary>
            public readonly float minPriceRatio;
            /// <summary>価格上限（額面比）。</summary>
            public readonly float maxPriceRatio;
            /// <summary>価格が適正へ寄る速さ（/戦略秒）。</summary>
            public readonly float convergeSpeed;

            public BondParams(float spreadFactor, float minPriceRatio, float maxPriceRatio, float convergeSpeed)
            {
                this.spreadFactor = Mathf.Max(0f, spreadFactor);
                this.minPriceRatio = Mathf.Max(0f, minPriceRatio);
                this.maxPriceRatio = Mathf.Max(minPriceRatio, maxPriceRatio);
                this.convergeSpeed = Mathf.Max(0f, convergeSpeed);
            }

            /// <summary>既定＝リスク1で+10%利回り・価格0.1〜2.0倍・収束速度2。</summary>
            public static BondParams Default => new BondParams(0.1f, 0.1f, 2f, 2f);
        }

        private const float MinYield = 1e-4f; // ゼロ除算防止（利回りの下限）

        /// <summary>信用スプレッド＝信用リスク×係数（リスクプレミアム）。</summary>
        public static float CreditSpread(float defaultRisk, BondParams p) => Mathf.Clamp01(defaultRisk) * p.spreadFactor;

        /// <summary>必要利回り＝市場金利＋信用スプレッド（最低 <see cref="MinYield"/>）。</summary>
        public static float RequiredYield(float marketRate, float defaultRisk, BondParams p)
            => Mathf.Max(MinYield, Mathf.Max(0f, marketRate) + CreditSpread(defaultRisk, p));

        /// <summary>
        /// 適正価格（額面比）＝表面利率/必要利回り（永久債近似）。市場金利・信用が表面利率と等しければ額面(1.0)、
        /// 金利/リスクが高いほど価格↓（逆相関）。下限/上限でクランプ。
        /// </summary>
        public static float FairPrice(Bond b, float marketRate, BondParams p)
        {
            if (b == null) return 0f;
            float reqYield = RequiredYield(marketRate, b.defaultRisk, p);
            float price = Mathf.Max(0f, b.couponRate) / reqYield;
            return Mathf.Clamp(price, p.minPriceRatio, p.maxPriceRatio);
        }

        /// <summary>現在利回り＝表面利率/価格（価格が下がるほど利回りが上がる＝逆相関）。価格0は0。</summary>
        public static float CurrentYield(Bond b)
            => b == null || b.price <= 0f ? 0f : Mathf.Max(0f, b.couponRate) / b.price;

        /// <summary>時価＝額面残高×価格（額面比）。</summary>
        public static float MarketValue(Bond b) => b == null ? 0f : Mathf.Max(0f, b.faceValue) * Mathf.Max(0f, b.price);

        /// <summary>1tick：価格を適正へ滑らかに収束させる（金利/信用の変化で価格が動く）。</summary>
        public static void Tick(Bond b, float marketRate, BondParams p, float dt)
        {
            if (b == null || dt <= 0f) return;
            float target = FairPrice(b, marketRate, p);
            float step = Mathf.Abs(target - b.price) * p.convergeSpeed * dt;
            b.price = Mathf.Max(0f, Mathf.MoveTowards(b.price, target, step));
        }

        /// <summary>
        /// 起債（発行）：額面 <paramref name="additionalFace"/> を発行し、調達額（=額面×現在価格）を返す＝借入で資本調達。
        /// 国債なら国庫（#163・債務増）/社債なら企業資本へ回す（行き先は呼び出し側）。額面残高が増える。
        /// </summary>
        public static float Issue(Bond b, float additionalFace)
        {
            if (b == null || additionalFace <= 0f) return 0f;
            float raised = additionalFace * Mathf.Max(0f, b.price);
            b.faceValue = Mathf.Max(0f, b.faceValue + additionalFace);
            return raised;
        }

        /// <summary>市場全体の総債務（時価合計）。</summary>
        public static float TotalDebt(IReadOnlyList<Bond> bonds)
        {
            if (bonds == null) return 0f;
            float sum = 0f;
            for (int i = 0; i < bonds.Count; i++) sum += MarketValue(bonds[i]);
            return sum;
        }

        /// <summary>市場全体の平均利回り（債券なし=0）。利回り曲線の水準＝市場の借入コスト。</summary>
        public static float AverageYield(IReadOnlyList<Bond> bonds)
        {
            if (bonds == null || bonds.Count == 0) return 0f;
            float sum = 0f; int n = 0;
            for (int i = 0; i < bonds.Count; i++)
                if (bonds[i] != null) { sum += CurrentYield(bonds[i]); n++; }
            return n == 0 ? 0f : sum / n;
        }
    }
}
