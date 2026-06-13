using NUnit.Framework;
using UnityEngine;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 功績昇進制（#1064・Almagest）の純ロジックテスト。
    /// 既定 <see cref="MeritPromotionParams.Default"/> の具体値で期待値を固定する。
    /// </summary>
    public class MeritPromotionRulesTests
    {
        private MeritPromotionParams P => MeritPromotionParams.Default;

        /// <summary>勝利×満貢献×同格は基準功績（10）を満額得る。</summary>
        [Test]
        public void MeritGain_勝利満額()
        {
            float g = MeritPromotionRules.MeritGain(1f, 1f, true, P);
            Assert.AreEqual(10f, g, 1e-4f);
        }

        /// <summary>格上撃破ほど功績が跳ねる（敵2倍戦力＝upsetBonusMax=2倍）。敗北は0.25倍。</summary>
        [Test]
        public void MeritGain_格上撃破ボーナスと敗北減衰()
        {
            // 敵戦力比2.0 → upset = 1 + clamp(1)*(2-1) = 2 倍
            float upset = MeritPromotionRules.MeritGain(1f, 2f, true, P);
            Assert.AreEqual(20f, upset, 1e-4f);

            // 同条件で敗北 → 0.25倍
            float defeat = MeritPromotionRules.MeritGain(1f, 2f, false, P);
            Assert.AreEqual(5f, defeat, 1e-4f);
        }

        /// <summary>昇進閾値は上の階級ほど逓増する（base20 × 1.6^tier）。</summary>
        [Test]
        public void MeritForNextPromotion_逓増()
        {
            Assert.AreEqual(20f, MeritPromotionRules.MeritForNextPromotion(0, P), 1e-3f);
            Assert.AreEqual(32f, MeritPromotionRules.MeritForNextPromotion(1, P), 1e-3f);   // 20*1.6
            Assert.AreEqual(51.2f, MeritPromotionRules.MeritForNextPromotion(2, P), 1e-3f); // 20*1.6^2
            // 上の階級ほど壁が高い
            Assert.Greater(MeritPromotionRules.MeritForNextPromotion(3, P),
                MeritPromotionRules.MeritForNextPromotion(2, P));
        }

        /// <summary>累積功績が閾値に達したときだけ昇進可能。</summary>
        [Test]
        public void PromotionReady_閾値判定()
        {
            Assert.IsFalse(MeritPromotionRules.PromotionReady(19.9f, 0, P));
            Assert.IsTrue(MeritPromotionRules.PromotionReady(20f, 0, P));
            // tier1の閾値は32 → 20では足りない
            Assert.IsFalse(MeritPromotionRules.PromotionReady(20f, 1, P));
            Assert.IsTrue(MeritPromotionRules.PromotionReady(32f, 1, P));
        }

        /// <summary>最大編成数は階級が上がるほど増え、上限27でクランプされる。</summary>
        [Test]
        public void MaxFormationSize_階級で増加し上限()
        {
            Assert.AreEqual(1, MeritPromotionRules.MaxFormationSize(0, P));   // 1
            Assert.AreEqual(1, MeritPromotionRules.MaxFormationSize(1, P));   // floor(1*1.7)=1
            Assert.AreEqual(2, MeritPromotionRules.MaxFormationSize(2, P));   // floor(2.89)=2
            Assert.AreEqual(4, MeritPromotionRules.MaxFormationSize(3, P));   // floor(4.913)=4
            // 上に行くほど大きな部隊を率いる（単調非減少）
            Assert.GreaterOrEqual(MeritPromotionRules.MaxFormationSize(5, P),
                MeritPromotionRules.MaxFormationSize(3, P));
            // 高tierは上限27でクランプ
            Assert.AreEqual(27, MeritPromotionRules.MaxFormationSize(20, P));
        }

        /// <summary>功績は平時に減衰する。平時が長いほど速く古びる。</summary>
        [Test]
        public void MeritDecay_平時で減衰()
        {
            // 戦時直後（平時0）は減衰しない
            Assert.AreEqual(100f, MeritPromotionRules.MeritDecay(100f, 0f, 1f, P), 1e-4f);

            // 長い平時ほど多く削れる
            float shortPeace = MeritPromotionRules.MeritDecay(100f, 60f, 1f, P);
            float longPeace = MeritPromotionRules.MeritDecay(100f, 240f, 1f, P);
            Assert.Less(shortPeace, 100f);
            Assert.Less(longPeace, shortPeace);
            // 0未満にならない
            Assert.GreaterOrEqual(MeritPromotionRules.MeritDecay(1f, 10000f, 10000f, P), 0f);
        }

        /// <summary>天井に達すると功績があっても昇進できない（アップオアアウトの入力）。</summary>
        [Test]
        public void PromotionStagnation_天井で頭打ち()
        {
            // 閾値到達かつ天井未満 → 昇進できる
            Assert.IsTrue(MeritPromotionRules.PromotionStagnation(30f, 20f, ceilingTier: 7, currentTier: 5));
            // 閾値到達だが天井 → 停滞（昇進不可）
            Assert.IsFalse(MeritPromotionRules.PromotionStagnation(30f, 20f, ceilingTier: 5, currentTier: 5));
            // そもそも閾値未達
            Assert.IsFalse(MeritPromotionRules.PromotionStagnation(10f, 20f, ceilingTier: 7, currentTier: 5));
        }
    }
}
