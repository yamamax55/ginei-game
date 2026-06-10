using NUnit.Framework;
using UnityEngine;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>サプライヤー評価ロジック（#1004）の EditMode テスト。信頼の非対称更新・最安≠最良を担保。</summary>
    public class SupplierRatingRulesTests
    {
        const float Eps = 1e-4f;

        /// <summary>納期遵守スコア＝納入実績の比率。実績ゼロは中立0.5。</summary>
        [Test]
        public void OnTimeDeliveryScore_IsRatio()
        {
            Assert.AreEqual(0.75f, SupplierRatingRules.OnTimeDeliveryScore(15, 20), Eps);
            Assert.AreEqual(0.5f, SupplierRatingRules.OnTimeDeliveryScore(0, 0), Eps); // 実績なし＝中立
            Assert.AreEqual(1f, SupplierRatingRules.OnTimeDeliveryScore(99, 20), Eps);  // クランプ
        }

        /// <summary>品質スコア＝不良率の裏。</summary>
        [Test]
        public void QualityScore_IsInverseOfDefectRate()
        {
            Assert.AreEqual(0.9f, SupplierRatingRules.QualityScore(0.1f), Eps);
            Assert.AreEqual(0f, SupplierRatingRules.QualityScore(1.5f), Eps); // クランプ
        }

        /// <summary>信頼性の非対称更新＝1回の成功(+0.05)より1回の失敗(−0.4)が桁違いに大きい＝崩すのは一度。</summary>
        [Test]
        public void ReliabilityTick_IsAsymmetric_LossDwarfsGain()
        {
            float start = 0.8f;
            float afterSuccess = SupplierRatingRules.ReliabilityTick(start, true, 1f);
            float afterFailure = SupplierRatingRules.ReliabilityTick(start, false, 1f);
            Assert.AreEqual(0.85f, afterSuccess, Eps);
            Assert.AreEqual(0.4f, afterFailure, Eps);
            float gain = afterSuccess - start;   // +0.05
            float loss = start - afterFailure;   // +0.4
            Assert.Greater(loss, gain * 5f, "失敗の打撃は成功の積み増しを大きく上回る（非対称）");
        }

        /// <summary>関係スコア＝飽和年数(10年)で0.5、トラブルが蓄積で蝕む。</summary>
        [Test]
        public void RelationshipScore_TenureMinusDisputes()
        {
            // years=10, saturation=10 → tenure=0.5、紛争なし
            Assert.AreEqual(0.5f, SupplierRatingRules.RelationshipScore(10f, 0), Eps);
            // 同じ年数でも紛争2件で 0.5 − 2×0.15 = 0.2
            Assert.AreEqual(0.2f, SupplierRatingRules.RelationshipScore(10f, 2), Eps);
        }

        /// <summary>総合評価＝信頼性最重視の加重和。信頼性が高い候補が品質僅差で上回る。</summary>
        [Test]
        public void OverallRating_WeightsReliabilityMost()
        {
            // onTime/quality/relationship を揃え、信頼性だけ差をつける
            float low = SupplierRatingRules.OverallRating(0.8f, 0.8f, 0.3f, 0.8f);
            float high = SupplierRatingRules.OverallRating(0.8f, 0.8f, 0.9f, 0.8f);
            Assert.Greater(high, low);
            // 既定重み 0.2/0.25/0.4/0.15（和1.0）で手計算検証
            float expected = 0.2f * 0.8f + 0.25f * 0.8f + 0.4f * 0.9f + 0.15f * 0.8f;
            Assert.AreEqual(expected, high, Eps);
        }

        /// <summary>優先サプライヤー＝最安が最良とは限らない。信頼性の高い高評価が安い候補を退ける。</summary>
        [Test]
        public void PreferredSupplier_CheapestIsNotAlwaysBest()
        {
            // 候補0＝高評価だが高価、候補1＝低評価だが激安
            float[] ratings = { 0.9f, 0.4f };
            float[] price = { 0.2f, 1.0f }; // priceCompetitiveness 高い＝安い
            // 既定 priceWeight=0.3 → score0=0.7*0.9+0.3*0.2=0.69, score1=0.7*0.4+0.3*1.0=0.58
            int chosen = SupplierRatingRules.PreferredSupplier(ratings, price);
            Assert.AreEqual(0, chosen, "評価が高ければ最安でなくても選ばれる");
            Assert.AreEqual(-1, SupplierRatingRules.PreferredSupplier(null, null));
        }

        /// <summary>乗り換えリスク＝切替コストが囲い込む。代替が良くても高コストなら動けない（ロックイン）。</summary>
        [Test]
        public void SwitchingRisk_LockInDampensByCost()
        {
            // 代替が現行を0.4上回る。コスト0.5なら 0.4*(1-0.5)=0.2
            Assert.AreEqual(0.2f, SupplierRatingRules.SwitchingRisk(0.5f, 0.9f, 0.5f), Eps);
            // 代替が良くない＝乗り換え動機なし
            Assert.AreEqual(0f, SupplierRatingRules.SwitchingRisk(0.8f, 0.6f, 0.0f), Eps);
            // 切替コスト満額＝完全ロックイン
            Assert.AreEqual(0f, SupplierRatingRules.SwitchingRisk(0.5f, 0.9f, 1.0f), Eps);
        }
    }
}
