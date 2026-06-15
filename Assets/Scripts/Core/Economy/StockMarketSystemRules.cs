using System.Collections.Generic;
using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 株式市場システム基盤（#185・純ロジック・唯一の窓口）。単一銘柄の評価 <see cref="StockMarketRules"/> を<b>市場全体</b>へ広げる：
    /// <b>実体↔株価の連結</b>（企業の利潤→1株あたり収益EPS・配当）、<b>時価総額/市場指数/市場心理</b>の集約、
    /// <b>増資（公募）＝株式発行で資本調達→企業の生産基盤へ投下</b>（<see cref="CapitalInvestmentRules"/>）。
    /// 個別の板/約定は持たない（少数集約＝タイクン化回避）。test-first。
    /// </summary>
    public static class StockMarketSystemRules
    {
        /// <summary>
        /// 操業企業の利潤から1株あたり収益(EPS)・配当を求めて <see cref="Company"/> に反映（実体↔株価の連結）。
        /// EPS＝利潤/発行株式数、配当＝EPS×配当性向。赤字は収益0（株は無価値方向へ）。
        /// </summary>
        public static void SyncEarnings(Listing l, float price, float payoutRatio)
        {
            if (l == null || l.enterprise == null || l.stock == null) return;
            float profit = Mathf.Max(0f, EnterpriseRules.Profit(l.enterprise, price));
            float shares = Mathf.Max(1f, l.shares);
            l.stock.earnings = profit / shares;
            l.stock.dividend = l.stock.earnings * Mathf.Clamp01(payoutRatio);
        }

        /// <summary>時価総額＝株価×発行株式数。</summary>
        public static float MarketCap(Listing l)
            => l == null || l.stock == null ? 0f : Mathf.Max(0f, l.stock.sharePrice) * Mathf.Max(0f, l.shares);

        /// <summary>市場指数＝全銘柄の時価総額合計（市場全体の規模）。</summary>
        public static float MarketIndex(IReadOnlyList<Listing> listings)
        {
            if (listings == null) return 0f;
            float sum = 0f;
            for (int i = 0; i < listings.Count; i++) sum += MarketCap(listings[i]);
            return sum;
        }

        /// <summary>市場全体の心理＝銘柄の sentiment の平均（市場のムード・銘柄なし=中立0.5）。</summary>
        public static float MarketSentiment(IReadOnlyList<Listing> listings)
        {
            if (listings == null || listings.Count == 0) return 0.5f;
            float sum = 0f; int n = 0;
            for (int i = 0; i < listings.Count; i++)
                if (listings[i] != null && listings[i].stock != null) { sum += Mathf.Clamp01(listings[i].stock.sentiment); n++; }
            return n == 0 ? 0.5f : sum / n;
        }

        /// <summary>
        /// 増資（公募）：<paramref name="newShares"/> 株を発行し、調達額（=新株×現在株価）を企業の生産基盤へ投下する
        /// （<see cref="CapitalInvestmentRules.Invest"/>）。発行株式数が増え希薄化する。調達額を返す。
        /// </summary>
        public static float IssueShares(Listing l, float newShares)
        {
            if (l == null || l.stock == null || l.enterprise == null || newShares <= 0f) return 0f;
            float raised = newShares * Mathf.Max(0f, l.stock.sharePrice);
            l.shares = Mathf.Max(0f, l.shares + newShares);
            CapitalInvestmentRules.Invest(l.enterprise, raised); // 調達資本＝資本投下
            return raised;
        }

        /// <summary>
        /// 1tick：各銘柄の収益を実体から同期し（<see cref="SyncEarnings"/>）、株価を適正へ収束させる（<see cref="StockMarketRules.Tick"/>）。
        /// 基盤の市場ステップ（価格は当面 市場 #179 から渡す単一値・将来は銘柄ごとの sector 価格へ）。
        /// </summary>
        public static void TickMarket(IReadOnlyList<Listing> listings, float price, float payoutRatio,
            StockMarketRules.StockParams p, float dt)
        {
            if (listings == null || dt <= 0f) return;
            for (int i = 0; i < listings.Count; i++)
            {
                Listing l = listings[i];
                if (l == null) continue;
                SyncEarnings(l, price, payoutRatio);
                StockMarketRules.Tick(l.stock, p, dt);
            }
        }
    }
}
