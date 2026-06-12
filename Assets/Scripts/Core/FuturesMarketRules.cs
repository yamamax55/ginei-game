using System.Collections.Generic;
using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 先物市場システム（#1933 FUTR・純ロジック・唯一の窓口）。株式（出資）・債券（借入）に続く三つ目の金融基盤＝<b>価格の予約</b>。
    /// FUTR-1 先物価格（現物×保有コスト）・ベーシス・コンタンゴ/バックワーデーション／FUTR-2 ヘッジ（現物を先物で相殺）／
    /// FUTR-3 投機（評価損益・想定元本・レバレッジ）／FUTR-4 証拠金と清算（追証・強制清算）／FUTR-5 市場集計（建玉・平均ベーシス）。
    /// 原資産価格は市場(#179)/株価(#185)/資源(#93/#178)から渡す。少数集約（タイクン化回避）。test-first。
    /// </summary>
    public static class FuturesMarketRules
    {
        // ===== FUTR-1 先物価格・ベーシス =====

        /// <summary>適正先物価格＝現物×(1+保有コスト率×残存期間)。金利/保管で先物が現物より高い＝コンタンゴの素。</summary>
        public static float FairPrice(float spot, float carryRate, float timeToMaturity)
            => Mathf.Max(0f, spot) * (1f + carryRate * Mathf.Max(0f, timeToMaturity));

        /// <summary>ベーシス＝先物−現物（正＝順ザヤ/コンタンゴ・負＝逆ザヤ/バックワーデーション）。</summary>
        public static float Basis(float futures, float spot) => futures - spot;

        /// <summary>コンタンゴ（順ザヤ＝先物>現物）。</summary>
        public static bool IsContango(float futures, float spot) => futures > spot;

        /// <summary>バックワーデーション（逆ザヤ＝先物&lt;現物＝品薄シグナル）。</summary>
        public static bool IsBackwardation(float futures, float spot) => futures < spot;

        // ===== FUTR-3 評価損益・想定元本・レバレッジ =====

        /// <summary>評価損益(mark-to-market)＝(現値−約定価格)×数量×方向（ロング+1/ショート−1）。</summary>
        public static float ProfitLoss(FuturesContract c, float currentPrice)
        {
            if (c == null) return 0f;
            float dir = c.isLong ? 1f : -1f;
            return (currentPrice - c.contractPrice) * Mathf.Max(0f, c.quantity) * dir;
        }

        /// <summary>想定元本＝価格×数量（ポジションの規模）。</summary>
        public static float Notional(FuturesContract c, float price)
            => c == null ? 0f : Mathf.Max(0f, price) * Mathf.Max(0f, c.quantity);

        /// <summary>レバレッジ＝想定元本/証拠金（小資金で大ポジション）。証拠金0は0。</summary>
        public static float Leverage(float notional, float margin) => margin <= 0f ? 0f : notional / margin;

        // ===== FUTR-2 ヘッジ =====

        /// <summary>
        /// 正味エクスポージャ＝現物数量＋先物の方向×数量。現物ロング(+)をショート先物(−)で相殺すると0に近づく＝価格リスクが減る。
        /// </summary>
        public static float NetExposure(float spotQuantity, FuturesContract c)
        {
            if (c == null) return spotQuantity;
            float futuresDir = c.isLong ? 1f : -1f;
            return spotQuantity + futuresDir * Mathf.Max(0f, c.quantity);
        }

        /// <summary>ヘッジ比率（0..1）＝相殺された価格リスクの割合（1＝完全ヘッジ）。現物0なら0。</summary>
        public static float HedgeRatio(float spotQuantity, FuturesContract c)
        {
            float abs = Mathf.Abs(spotQuantity);
            if (abs < 1e-6f) return 0f;
            return Mathf.Clamp01(1f - Mathf.Abs(NetExposure(spotQuantity, c)) / abs);
        }

        // ===== FUTR-4 証拠金と清算 =====

        /// <summary>必要証拠金＝想定元本×証拠金率（レバレッジの逆数ぶん）。</summary>
        public static float RequiredMargin(float notional, float marginRate)
            => Mathf.Max(0f, notional) * Mathf.Clamp01(marginRate);

        /// <summary>有効証拠金＝預託証拠金＋評価損益。</summary>
        public static float Equity(FuturesContract c, float currentPrice)
            => (c == null ? 0f : c.margin) + ProfitLoss(c, currentPrice);

        /// <summary>追証/強制清算判定＝有効証拠金が維持証拠金（想定元本×維持率）を割ったか。過剰投機の破綻。</summary>
        public static bool IsMarginCall(FuturesContract c, float currentPrice, float maintenanceRate)
        {
            if (c == null) return false;
            float maintenance = Notional(c, currentPrice) * Mathf.Clamp01(maintenanceRate);
            return Equity(c, currentPrice) < maintenance;
        }

        // ===== FUTR-5 市場集計 =====

        /// <summary>建玉合計（市場の総ポジション量）。</summary>
        public static float OpenInterest(IReadOnlyList<FuturesContract> contracts)
        {
            if (contracts == null) return 0f;
            float sum = 0f;
            for (int i = 0; i < contracts.Count; i++)
                if (contracts[i] != null) sum += Mathf.Max(0f, contracts[i].quantity);
            return sum;
        }

        /// <summary>平均ベーシス（約定先物価格−現物の平均）。正＝市場はコンタンゴ・負＝バックワーデーション（品薄）。</summary>
        public static float AverageBasis(IReadOnlyList<FuturesContract> contracts, float spot)
        {
            if (contracts == null || contracts.Count == 0) return 0f;
            float sum = 0f; int n = 0;
            for (int i = 0; i < contracts.Count; i++)
                if (contracts[i] != null) { sum += Basis(contracts[i].contractPrice, spot); n++; }
            return n == 0 ? 0f : sum / n;
        }
    }
}
