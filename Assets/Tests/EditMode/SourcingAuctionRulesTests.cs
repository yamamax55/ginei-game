using NUnit.Framework;
using UnityEngine;
using Ginei;
using SAParams = Ginei.SourcingAuctionRules.SourcingAuctionParams;

namespace Ginei.Tests
{
    /// <summary>
    /// ソーシング・競争見積・逆オークション（#1005）を固定する：最安入札 vs 総合最適入札（最安≠最良）、
    /// 競争激化での価格低下（入札者が多いほど買い手有利）、価格発見、談合の検出（札が揃うと疑い・ばらつくと0）、
    /// 予定価格の充足（不落判定）。既定Params具体値で期待値を固定する。
    /// </summary>
    public class SourcingAuctionRulesTests
    {
        // 最安入札：純粋価格競争＝最も安い札を採る
        [Test]
        public void LowestBid_ReturnsCheapest()
        {
            var bids = new[]
            {
                new Bid(1, 120f, 0.9f, 30f),
                new Bid(2, 100f, 0.5f, 60f),
                new Bid(3, 110f, 0.8f, 40f),
            };
            Bid low = SourcingAuctionRules.LowestBid(bids);
            Assert.IsNotNull(low);
            Assert.AreEqual(2, low.bidderId); // 最安=100
        }

        // 総合最適：最安≠最良＝品質/納期が効いて高品質の札を選ぶことがある
        [Test]
        public void BestValueBid_DiffersFromLowest_WhenQualityMatters()
        {
            var p = SAParams.Default; // price0.5 / quality0.3 / lead0.2 / ref100
            var bids = new[]
            {
                new Bid(1, 110f, 1.0f, 10f),  // 少し高いが高品質・即納
                new Bid(2, 100f, 0.1f, 90f),  // 最安だが低品質・遅い
            };
            Bid low = SourcingAuctionRules.LowestBid(bids);
            Bid best = SourcingAuctionRules.BestValueBid(bids, p);
            Assert.AreEqual(2, low.bidderId);   // 最安は札2
            Assert.AreEqual(1, best.bidderId);  // 総合最適は札1（最安≠最良）
        }

        // 総合最適：価格しか差が無いなら最安と一致する
        [Test]
        public void BestValueBid_EqualsLowest_WhenOnlyPriceDiffers()
        {
            var p = SAParams.Default;
            var bids = new[]
            {
                new Bid(1, 100f, 0.8f, 50f),
                new Bid(2, 120f, 0.8f, 50f),
            };
            Bid best = SourcingAuctionRules.BestValueBid(bids, p);
            Assert.AreEqual(1, best.bidderId); // 安い方
        }

        // 競争激化：入札者が多いほど買い手有利（単調増加）・独占は0
        [Test]
        public void CompetitionIntensity_RisesWithMoreBidders()
        {
            Assert.AreEqual(0f, SourcingAuctionRules.CompetitionIntensity(1), 1e-6f); // 独占=競争なし
            float two = SourcingAuctionRules.CompetitionIntensity(2);
            float five = SourcingAuctionRules.CompetitionIntensity(5);
            Assert.Greater(two, 0f);
            Assert.Greater(five, two); // 多いほど買い手有利

            // 既定 competitionFactor0.1：n=2 → 1-1/(1+0.1)=0.0909...
            Assert.AreEqual(1f - 1f / 1.1f, two, 1e-4f);
        }

        // 価格発見：入札の分布（平均）から実勢価格を読む
        [Test]
        public void PriceDiscovery_ReturnsMeanOfBids()
        {
            var bids = new[]
            {
                new Bid(1, 90f, 0.5f, 50f),
                new Bid(2, 100f, 0.5f, 50f),
                new Bid(3, 110f, 0.5f, 50f),
            };
            float discovered = SourcingAuctionRules.PriceDiscovery(bids);
            Assert.AreEqual(100f, discovered, 1e-4f); // 平均
        }

        // 談合の検出：価格が不自然に揃うと疑い高・健全にばらつくと低
        [Test]
        public void CollusionRisk_HighWhenPricesAligned_LowWhenDispersed()
        {
            var p = SAParams.Default; // collusionDispersion0.05
            var colluded = new[]
            {
                new Bid(1, 100f, 0.5f, 50f),
                new Bid(2, 101f, 0.5f, 50f),
                new Bid(3, 100.5f, 0.5f, 50f),
            };
            var competitive = new[]
            {
                new Bid(1, 80f, 0.5f, 50f),
                new Bid(2, 100f, 0.5f, 50f),
                new Bid(3, 130f, 0.5f, 50f),
            };
            float colludedRisk = SourcingAuctionRules.CollusionRisk(colluded, p);
            float compRisk = SourcingAuctionRules.CollusionRisk(competitive, p);
            Assert.Greater(colludedRisk, 0.8f);  // 揃いすぎ＝強い疑い
            Assert.AreEqual(0f, compRisk, 1e-6f); // ばらつく＝健全（疑いなし）
        }

        // 談合：札1件以下は比較不能＝0
        [Test]
        public void CollusionRisk_ZeroWhenTooFewBids()
        {
            var p = SAParams.Default;
            var single = new[] { new Bid(1, 100f, 0.5f, 50f) };
            Assert.AreEqual(0f, SourcingAuctionRules.CollusionRisk(single, p), 1e-6f);
            Assert.AreEqual(0f, SourcingAuctionRules.CollusionRisk(null, p), 1e-6f);
        }

        // 予定価格：最安が予定価格以下なら成立・上回れば不落
        [Test]
        public void ReservePriceMet_DeterminesAwardOrFailure()
        {
            var cheap = new Bid(1, 90f, 0.5f, 50f);
            var pricey = new Bid(2, 150f, 0.5f, 50f);
            Assert.IsTrue(SourcingAuctionRules.ReservePriceMet(cheap, 100f));   // 予定内＝落札
            Assert.IsFalse(SourcingAuctionRules.ReservePriceMet(pricey, 100f)); // 予定超＝不落
            Assert.IsFalse(SourcingAuctionRules.ReservePriceMet(null, 100f));   // 札なし＝不成立
            Assert.IsTrue(SourcingAuctionRules.ReservePriceMet(pricey, 0f));    // 予定価格なし＝成立
        }
    }
}
