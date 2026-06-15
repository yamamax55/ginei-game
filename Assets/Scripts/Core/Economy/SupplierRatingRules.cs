using UnityEngine;

namespace Ginei
{
    /// <summary>サプライヤー評価の調整係数（#1004）。</summary>
    public readonly struct SupplierRatingParams
    {
        /// <summary>信頼性が成功で漸増する強さ（小さく積む＝築くのは遅い）。</summary>
        public readonly float reliabilityGain;
        /// <summary>信頼性が失敗で減る強さ（大きく落とす＝崩すのは一度＝非対称）。</summary>
        public readonly float reliabilityLoss;
        /// <summary>関係スコアの飽和年数（この年数で関係加点がほぼ満ちる）。</summary>
        public readonly float relationshipSaturationYears;
        /// <summary>トラブル1件あたりの関係スコア減（蓄積）。</summary>
        public readonly float disputePenalty;
        /// <summary>総合評価の納期遵守の重み。</summary>
        public readonly float wOnTime;
        /// <summary>総合評価の品質の重み。</summary>
        public readonly float wQuality;
        /// <summary>総合評価の信頼性の重み（信頼性を最重視＝安さより信頼）。</summary>
        public readonly float wReliability;
        /// <summary>総合評価の関係の重み。</summary>
        public readonly float wRelationship;
        /// <summary>優先選定で価格競争力を見る重み（評価1−これ＝評価優先）。</summary>
        public readonly float priceWeight;

        public SupplierRatingParams(float reliabilityGain, float reliabilityLoss, float relationshipSaturationYears,
            float disputePenalty, float wOnTime, float wQuality, float wReliability, float wRelationship, float priceWeight)
        {
            this.reliabilityGain = Mathf.Max(0f, reliabilityGain);
            this.reliabilityLoss = Mathf.Max(0f, reliabilityLoss);
            this.relationshipSaturationYears = Mathf.Max(1e-4f, relationshipSaturationYears);
            this.disputePenalty = Mathf.Max(0f, disputePenalty);
            this.wOnTime = Mathf.Max(0f, wOnTime);
            this.wQuality = Mathf.Max(0f, wQuality);
            this.wReliability = Mathf.Max(0f, wReliability);
            this.wRelationship = Mathf.Max(0f, wRelationship);
            this.priceWeight = Mathf.Clamp01(priceWeight);
        }

        /// <summary>
        /// 既定＝信頼性成功+0.05/失敗−0.4（築くのは遅く崩すのは一度＝非対称）・関係飽和10年・
        /// トラブル減0.15・総合重み 納期0.2/品質0.25/信頼性0.4/関係0.15（信頼性最重視）・価格重み0.3。
        /// </summary>
        public static SupplierRatingParams Default =>
            new SupplierRatingParams(0.05f, 0.4f, 10f, 0.15f, 0.2f, 0.25f, 0.4f, 0.15f, 0.3f);
    }

    /// <summary>
    /// サプライヤー管理・評価の純ロジック（#1004）。供給元の納入実績（納期遵守・品質・信頼性・関係）を加重評価し、
    /// 発注先選定の判断材料にする。核は「信頼は実績で築かれ一度の裏切りで崩れる＝安さより信頼性」＝信頼性は成功で
    /// わずかに積み失敗で大きく崩れる非対称更新で、総合評価でも信頼性を最重視する（最安が最良とは限らない）。
    /// <para>分担：<see cref="TradeRules"/> は交易（相手国との取引利得）。本クラスは調達先（サプライヤー）の評価。
    /// <c>SupplyContractRules</c>（契約条件・同Wave並行）と <c>SourcingAuctionRules</c>（入札・同Wave並行）とは別＝
    /// こちらは過去実績の評価＝誰に発注するかの判断材料を出す。</para>
    /// 純ロジック（非 MonoBehaviour・乱数なし決定論・test-first）。
    /// </summary>
    public static class SupplierRatingRules
    {
        /// <summary>納期遵守スコア（0..1）＝納期内納入数÷総納入数。納入実績ゼロは中立0.5。</summary>
        public static float OnTimeDeliveryScore(int deliveriesOnTime, int totalDeliveries)
        {
            if (totalDeliveries <= 0) return 0.5f;
            int onTime = Mathf.Clamp(deliveriesOnTime, 0, totalDeliveries);
            return (float)onTime / totalDeliveries;
        }

        /// <summary>品質スコア（0..1）＝不良率(0..1)の裏＝1−不良率。</summary>
        public static float QualityScore(float defectRate)
        {
            return 1f - Mathf.Clamp01(defectRate);
        }

        /// <summary>
        /// 信頼性の更新（0..1）。成功なら reliabilityGain×dt でわずかに漸増、失敗なら reliabilityLoss×dt で大きく減る
        /// ＝築くのに時間がかかり崩すのは一度の非対称。基準値非破壊で新しい信頼性を返す。
        /// </summary>
        public static float ReliabilityTick(float reliability, bool deliverySuccess, float dt, SupplierRatingParams p)
        {
            float r = Mathf.Clamp01(reliability);
            float d = Mathf.Max(0f, dt);
            r += deliverySuccess ? p.reliabilityGain * d : -p.reliabilityLoss * d;
            return Mathf.Clamp01(r);
        }

        public static float ReliabilityTick(float reliability, bool deliverySuccess, float dt)
            => ReliabilityTick(reliability, deliverySuccess, dt, SupplierRatingParams.Default);

        /// <summary>
        /// 関係スコア（0..1）＝長い付き合い（年数の飽和カーブ）からトラブルの蓄積（disputeCount×disputePenalty）を引く。
        /// 長年の取引は厚いが、紛争の積み重ねが関係を蝕む。
        /// </summary>
        public static float RelationshipScore(float yearsOfRelationship, int disputeCount, SupplierRatingParams p)
        {
            float years = Mathf.Max(0f, yearsOfRelationship);
            // 飽和年数で1に漸近する加点（years==saturation で 0.5、長いほど1へ）。
            float tenure = years / (years + p.relationshipSaturationYears);
            float disputes = Mathf.Max(0, disputeCount) * p.disputePenalty;
            return Mathf.Clamp01(tenure - disputes);
        }

        public static float RelationshipScore(float yearsOfRelationship, int disputeCount)
            => RelationshipScore(yearsOfRelationship, disputeCount, SupplierRatingParams.Default);

        /// <summary>
        /// 総合評価（0..1）＝納期遵守・品質・信頼性・関係の加重和（重みは正規化）。信頼性の重みが最大＝安さより信頼性。
        /// 発注先選定（<see cref="PreferredSupplier"/>）の入力。
        /// </summary>
        public static float OverallRating(float onTime, float quality, float reliability, float relationship, SupplierRatingParams p)
        {
            float wSum = p.wOnTime + p.wQuality + p.wReliability + p.wRelationship;
            if (wSum <= 0f) return 0f;
            float weighted =
                p.wOnTime * Mathf.Clamp01(onTime) +
                p.wQuality * Mathf.Clamp01(quality) +
                p.wReliability * Mathf.Clamp01(reliability) +
                p.wRelationship * Mathf.Clamp01(relationship);
            return Mathf.Clamp01(weighted / wSum);
        }

        public static float OverallRating(float onTime, float quality, float reliability, float relationship)
            => OverallRating(onTime, quality, reliability, relationship, SupplierRatingParams.Default);

        /// <summary>
        /// 優先サプライヤーの選択。各候補の総合評価(ratings)と価格競争力(priceCompetitiveness 0..1＝高いほど安い)を
        /// priceWeight で按分した総合点が最大の添字を返す（同点は先勝ち）。最安が必ず選ばれるとは限らない＝
        /// 評価（信頼性）が価格を上回りうる。候補が無ければ −1。
        /// </summary>
        public static int PreferredSupplier(float[] ratings, float[] priceCompetitiveness, SupplierRatingParams p)
        {
            if (ratings == null || ratings.Length == 0) return -1;
            int best = -1;
            float bestScore = float.NegativeInfinity;
            for (int i = 0; i < ratings.Length; i++)
            {
                float rating = Mathf.Clamp01(ratings[i]);
                float price = (priceCompetitiveness != null && i < priceCompetitiveness.Length)
                    ? Mathf.Clamp01(priceCompetitiveness[i]) : 0.5f;
                float score = (1f - p.priceWeight) * rating + p.priceWeight * price;
                if (score > bestScore)
                {
                    bestScore = score;
                    best = i;
                }
            }
            return best;
        }

        public static int PreferredSupplier(float[] ratings, float[] priceCompetitiveness)
            => PreferredSupplier(ratings, priceCompetitiveness, SupplierRatingParams.Default);

        /// <summary>
        /// 乗り換えのリスク（0..1）＝代替先の評価が現行を上回るほど切り替えたいが、切替コスト(switchingCost 0..1)が
        /// 引き止める（ロックイン）。評価差(alt−current)が正でも切替コストぶん割り引かれ、コストが高ければ動けない。
        /// 切り替えるべき度合いを返す（0＝囲い込まれて動けない）。
        /// </summary>
        public static float SwitchingRisk(float currentSupplierRating, float alternativeRating, float switchingCost)
        {
            float gain = Mathf.Clamp01(alternativeRating) - Mathf.Clamp01(currentSupplierRating);
            if (gain <= 0f) return 0f; // 代替が良くないなら乗り換え動機なし
            float cost = Mathf.Clamp01(switchingCost);
            return Mathf.Clamp01(gain * (1f - cost));
        }
    }
}
