using System;
using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 競争入札の1件の見積（#1005・純データ）。サプライヤーがRFQに応じて出す札。
    /// 価格・品質・納期を持つ＝最安が常に最良とは限らない（総合評価で選ぶ余地）。
    /// </summary>
    [Serializable]
    public class Bid
    {
        /// <summary>入札者（サプライヤー）の識別子。</summary>
        public int bidderId;
        /// <summary>提示価格（安いほど買い手有利。負にはならない）。</summary>
        public float price;
        /// <summary>品質（0..1。高いほど良い）。</summary>
        public float quality;
        /// <summary>納期（戦略秒。短いほど良い。負にはならない）。</summary>
        public float leadTime;

        public Bid() { }

        public Bid(int bidderId, float price, float quality, float leadTime)
        {
            this.bidderId = bidderId;
            this.price = Mathf.Max(0f, price);
            this.quality = Mathf.Clamp01(quality);
            this.leadTime = Mathf.Max(0f, leadTime);
        }
    }

    /// <summary>
    /// ソーシング・競争見積・逆オークション（#1005・純ロジック test-first・唯一の窓口）。
    /// RFQ（見積依頼）→複数サプライヤーの入札→落札という離散的な価格発見をモデル化する。
    /// 競争入札は価格を発見する＝入札者が多いほど買い手が得をする（<see cref="CompetitionIntensity"/>）が、
    /// 談合（<see cref="CollusionRisk"/>＝価格カルテル）がそれを殺す。
    /// 役割分担：<see cref="MarketRules"/>（連続市場の需給均衡）とは別＝こちらは離散入札／
    /// <c>SupplierRatingRules</c>（サプライヤー評価・同Wave並行）が入札者の信頼度を、
    /// <c>ProductionOrderRules</c>（発注）が落札後の生産発注を担う。談合の兆候は
    /// <c>FinancialAnomalyRules</c>（財務異常検知）と接続できる（価格の不自然な揃いを渡す）。
    /// 調整値は <see cref="SourcingAuctionParams"/> に集約（既定 <see cref="SourcingAuctionParams.Default"/>）。
    /// </summary>
    public static class SourcingAuctionRules
    {
        /// <summary>逆オークションの調整値（総合評価の重み・競争効果・談合検出の閾値）。</summary>
        public readonly struct SourcingAuctionParams
        {
            /// <summary>総合評価における価格の重み（安さ重視）。</summary>
            public readonly float priceWeight;
            /// <summary>総合評価における品質の重み。</summary>
            public readonly float qualityWeight;
            /// <summary>総合評価における納期（速さ）の重み。</summary>
            public readonly float leadTimeWeight;
            /// <summary>納期の評価基準（この秒数で速さ評価が0＝これ以上遅い納期はゼロ点）。</summary>
            public readonly float leadTimeReference;
            /// <summary>競争効果の係数（入札者増加が価格をどれだけ押し下げるか）。</summary>
            public readonly float competitionFactor;
            /// <summary>談合とみなす価格ばらつき（変動係数）の上限（これ未満で揃いすぎ＝疑い）。</summary>
            public readonly float collusionDispersion;

            public SourcingAuctionParams(float priceWeight, float qualityWeight, float leadTimeWeight,
                                         float leadTimeReference, float competitionFactor, float collusionDispersion)
            {
                this.priceWeight = Mathf.Max(0f, priceWeight);
                this.qualityWeight = Mathf.Max(0f, qualityWeight);
                this.leadTimeWeight = Mathf.Max(0f, leadTimeWeight);
                this.leadTimeReference = Mathf.Max(0f, leadTimeReference);
                this.competitionFactor = Mathf.Max(0f, competitionFactor);
                this.collusionDispersion = Mathf.Max(0f, collusionDispersion);
            }

            /// <summary>
            /// 既定＝価格重み0.5・品質0.3・納期0.2（安さ最優先だが品質/納期も効く）／納期基準100秒／
            /// 競争係数0.1（入札者+1ごとに約10%ぶん買い手有利へ寄る・逓減）／談合分散0.05（札がほぼ揃う＝疑い）。
            /// </summary>
            public static SourcingAuctionParams Default =>
                new SourcingAuctionParams(0.5f, 0.3f, 0.2f, 100f, 0.1f, 0.05f);
        }

        /// <summary>
        /// 最安入札を返す純粋価格競争＝逆オークションの基本（#1005）。最も安い札の落札。
        /// 同価格は先着（配列の前方）を採る。空/nullは null（不落＝札なし）。
        /// </summary>
        public static Bid LowestBid(Bid[] bids)
        {
            if (bids == null || bids.Length == 0) return null;
            Bid best = null;
            for (int i = 0; i < bids.Length; i++)
            {
                Bid b = bids[i];
                if (b == null) continue;
                if (best == null || b.price < best.price)
                    best = b;
            }
            return best;
        }

        /// <summary>
        /// 総合評価の最良入札を返す（#1005）。価格だけでなく品質・納期を重み付け＝最安≠最良。
        /// 各札のスコア＝価格(安いほど高得点・最高/価格で正規化)×priceWeight＋品質×qualityWeight＋速さ×leadTimeWeight。
        /// スコア最大を選ぶ（同点は先着）。空/nullは null。
        /// </summary>
        public static Bid BestValueBid(Bid[] bids, SourcingAuctionParams p)
        {
            if (bids == null || bids.Length == 0) return null;

            // 価格正規化の基準＝有効札の最安価格（最安を1点とし、高い札ほど減点）。
            float minPrice = -1f;
            for (int i = 0; i < bids.Length; i++)
            {
                Bid b = bids[i];
                if (b == null) continue;
                if (minPrice < 0f || b.price < minPrice) minPrice = b.price;
            }
            if (minPrice < 0f) return null; // 有効札なし

            Bid best = null;
            float bestScore = float.NegativeInfinity;
            for (int i = 0; i < bids.Length; i++)
            {
                Bid b = bids[i];
                if (b == null) continue;
                float score = ValueScore(b, minPrice, p);
                if (best == null || score > bestScore)
                {
                    best = b;
                    bestScore = score;
                }
            }
            return best;
        }

        /// <summary>
        /// 1札の総合評価スコア（#1005・0..1合成）。<paramref name="minPrice"/> は基準となる最安価格。
        /// 価格点＝minPrice/price（最安で1・倍額で0.5）／品質点＝quality／速さ点＝1−leadTime/基準（下限0）。
        /// </summary>
        public static float ValueScore(Bid bid, float minPrice, SourcingAuctionParams p)
        {
            if (bid == null) return 0f;
            float mp = Mathf.Max(0f, minPrice);

            // 価格点：最安を1点に、高い札ほど逓減（price 0 は満点扱い＝タダ）。
            float priceScore = bid.price <= 0f ? 1f : Mathf.Clamp01(mp / bid.price);

            // 速さ点：納期が基準以上なら0、即納で1。
            float speedScore = p.leadTimeReference <= 0f
                ? 1f
                : Mathf.Clamp01(1f - bid.leadTime / p.leadTimeReference);

            float total = priceScore * p.priceWeight
                        + bid.quality * p.qualityWeight
                        + speedScore * p.leadTimeWeight;

            float weightSum = p.priceWeight + p.qualityWeight + p.leadTimeWeight;
            if (weightSum <= 0f) return 0f;
            return Mathf.Clamp01(total / weightSum);
        }

        /// <summary>
        /// 競争の激しさ（#1005・0..1）。入札者が多いほど価格が下がる＝買い手有利の度合い。
        /// 1社（独占）で0、増えるほど1へ逓減的に近づく：1−1/(1+competitionFactor×(n−1))。
        /// 0社は0（札なし＝競争なし）。「入札者が多いほど買い手が得をする」を式に出す。
        /// </summary>
        public static float CompetitionIntensity(int bidderCount)
        {
            return CompetitionIntensity(bidderCount, SourcingAuctionParams.Default);
        }

        /// <summary>競争の激しさ（#1005・係数指定版）。1−1/(1+competitionFactor×(n−1))。</summary>
        public static float CompetitionIntensity(int bidderCount, SourcingAuctionParams p)
        {
            int n = Mathf.Max(0, bidderCount);
            if (n <= 1) return 0f; // 0社=札なし／1社=独占＝競争ゼロ
            float extra = p.competitionFactor * (n - 1);
            return Mathf.Clamp01(1f - 1f / (1f + extra));
        }

        /// <summary>
        /// 発見された市場価格（#1005・価格発見）。入札の分布から実勢を読む＝有効札の平均価格。
        /// 競争入札は離散的に「いくらが妥当か」を発見する。空/nullは0（不明）。
        /// </summary>
        public static float PriceDiscovery(Bid[] bids)
        {
            if (bids == null || bids.Length == 0) return 0f;
            float sum = 0f;
            int count = 0;
            for (int i = 0; i < bids.Length; i++)
            {
                Bid b = bids[i];
                if (b == null) continue;
                sum += b.price;
                count++;
            }
            return count == 0 ? 0f : sum / count;
        }

        /// <summary>
        /// 談合の兆候（#1005・0..1）。入札価格が不自然に揃う＝価格カルテルの疑い。
        /// 価格の変動係数（標準偏差/平均）が小さいほど談合度が高い：1−CV/collusionDispersion でクランプ。
        /// 競争が健全なら札はばらつく（CVが大きい）＝談合度0。札2件未満は0（比較不能）。
        /// 検出した談合度は <c>FinancialAnomalyRules</c>（財務異常検知）へ渡せる。
        /// </summary>
        public static float CollusionRisk(Bid[] bids, SourcingAuctionParams p)
        {
            if (bids == null) return 0f;

            // 有効札の平均と分散（手書きループ・LINQ不可）。
            float sum = 0f;
            int count = 0;
            for (int i = 0; i < bids.Length; i++)
            {
                Bid b = bids[i];
                if (b == null) continue;
                sum += b.price;
                count++;
            }
            if (count < 2) return 0f; // 比較不能
            float mean = sum / count;
            if (mean <= 0f) return 0f; // 全札0＝価格情報なし

            float sqSum = 0f;
            for (int i = 0; i < bids.Length; i++)
            {
                Bid b = bids[i];
                if (b == null) continue;
                float d = b.price - mean;
                sqSum += d * d;
            }
            float variance = sqSum / count;
            float cv = Mathf.Sqrt(variance) / mean; // 変動係数（ばらつきの相対値）

            if (p.collusionDispersion <= 0f) return 0f;
            // ばらつきが閾値未満なら揃いすぎ＝談合度↑（CV0で最大1・閾値でゼロ）。
            return Mathf.Clamp01(1f - cv / p.collusionDispersion);
        }

        /// <summary>
        /// 最低落札価格（予定価格）の充足（#1005）。予定価格を上回る入札しかなければ不調＝不落。
        /// 最安札の価格が <paramref name="reservePrice"/> 以下なら成立（true）。
        /// 札なし（lowestBid=null）は不成立。reservePrice≤0は上限なし扱い＝札があれば成立。
        /// </summary>
        public static bool ReservePriceMet(Bid lowestBid, float reservePrice)
        {
            if (lowestBid == null) return false;
            if (reservePrice <= 0f) return true; // 予定価格なし＝制約なし
            return lowestBid.price <= reservePrice;
        }
    }
}
