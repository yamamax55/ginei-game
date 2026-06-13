using NUnit.Framework;
using UnityEngine;

namespace Ginei.Tests
{
    /// <summary>
    /// AttritionExchangeRules（消耗交換比＝質と数のトレードオフ）の EditMode テスト。
    /// 既定 Params で期待値を固定（Pow を含む交換比のみ許容誤差を緩める）。
    /// </summary>
    public class AttritionExchangeRulesTests
    {
        private const float Eps = 1e-4f;
        private const float PowEps = 1e-3f; // Pow を含む値はやや緩める

        [Test]
        public void ExchangeRatio_EqualQuality_IsOne()
        {
            float r = AttritionExchangeRules.ExchangeRatio(50f, 50f);
            Assert.AreEqual(1f, r, Eps);
        }

        [Test]
        public void ExchangeRatio_HigherQuality_FavorsOwn()
        {
            // (80/20)^0.5 = sqrt(4) = 2.0
            float r = AttritionExchangeRules.ExchangeRatio(80f, 20f);
            Assert.AreEqual(2f, r, PowEps);

            // 逆向きは (20/80)^0.5 = 0.5
            float rInv = AttritionExchangeRules.ExchangeRatio(20f, 80f);
            Assert.AreEqual(0.5f, rInv, PowEps);
        }

        [Test]
        public void ExchangeRatio_ClampedToMax()
        {
            // (100/1)^0.5 = 10 > maxRatio(4.0) → 4.0 にクランプ
            float r = AttritionExchangeRules.ExchangeRatio(100f, 1f);
            Assert.AreEqual(4f, r, Eps);
        }

        [Test]
        public void Losses_FavorableRatio_InflictMoreTakeLess()
        {
            // R60v40 = (60/40)^0.5 ≈ 1.2247449
            float r = AttritionExchangeRules.ExchangeRatio(60f, 40f);
            Assert.AreEqual(1.2247449f, r, PowEps);

            float infl = AttritionExchangeRules.LossesInflicted(1000f, r, 0.1f);
            Assert.AreEqual(122.47449f, infl, 1e-2f);

            float taken = AttritionExchangeRules.LossesTaken(2000f, r, 0.1f);
            Assert.AreEqual(163.29932f, taken, 1e-2f);
        }

        [Test]
        public void FavorableExchange_AboveThreshold()
        {
            Assert.IsTrue(AttritionExchangeRules.FavorableExchange(1.5f, AttritionExchangeParams.Default));
            Assert.IsFalse(AttritionExchangeRules.FavorableExchange(0.8f, AttritionExchangeParams.Default));
            // 任意しきい値版
            Assert.IsTrue(AttritionExchangeRules.FavorableExchange(2.5f, 2f));
            Assert.IsFalse(AttritionExchangeRules.FavorableExchange(1.9f, 2f));
        }

        [Test]
        public void WarOfAttritionWinner_QualityCrushAndStalemate()
        {
            // 等戦力・質も拮抗 → 0
            Assert.AreEqual(0, AttritionExchangeRules.WarOfAttritionWinner(1000f, 50f, 1000f, 50f));
            // 同数で質圧倒（90 vs 30）→ 自軍勝ち -1
            Assert.AreEqual(-1, AttritionExchangeRules.WarOfAttritionWinner(1000f, 90f, 1000f, 30f));
        }

        [Test]
        public void QualityVsQuantity_TugOfWar()
        {
            // 質の優位 0.3 と数の劣位 -0.5 → -0.2（数寄りに傾く）
            Assert.AreEqual(-0.2f, AttritionExchangeRules.QualityVsQuantity(0.3f, -0.5f), Eps);
            // 両極端は -1..1 にクランプ
            Assert.AreEqual(1f, AttritionExchangeRules.QualityVsQuantity(0.9f, 0.9f), Eps);
            Assert.AreEqual(-1f, AttritionExchangeRules.QualityVsQuantity(-0.9f, -0.9f), Eps);
        }

        [Test]
        public void SustainableLossRate_AndBleedingOut()
        {
            // 予備900・補充100 → 100/1000 = 0.1
            float sus = AttritionExchangeRules.SustainableLossRate(900f, 100f);
            Assert.AreEqual(0.1f, sus, Eps);
            // 損耗0.3は補充0.1を超える＝消耗負け
            Assert.IsTrue(AttritionExchangeRules.IsBleedingOut(0.3f, sus));
            // 損耗0.05は支えられる
            Assert.IsFalse(AttritionExchangeRules.IsBleedingOut(0.05f, sus));
        }

        /// <summary>
        /// 物語テスト：少数精鋭（800隻・質80）は低品質の大軍（2000隻・質40）に対し
        /// 有利な交換比（2.0）を得るが、実効戦力 800×2.0=1600 が 2000 に届かず数で押し切られる。
        /// さらに、その消耗で補充を超える損耗率に陥れば消耗負けが確定する。
        /// </summary>
        [Test]
        public void Story_QualityFleetOutnumberedAndBledOut()
        {
            // 有利な交換比を得る（質が高い）
            float r = AttritionExchangeRules.ExchangeRatio(80f, 40f);
            Assert.IsTrue(AttritionExchangeRules.FavorableExchange(r, AttritionExchangeParams.Default),
                "高品質側は有利な交換比を得るはず");

            // しかし数で押し切られる（敵勝ち = 1）
            int winner = AttritionExchangeRules.WarOfAttritionWinner(800f, 80f, 2000f, 40f);
            Assert.AreEqual(1, winner, "有利な交換比でも数の差で消耗戦に敗れる");

            // 乏しい補充では損耗を支えきれず消耗負け
            float sus = AttritionExchangeRules.SustainableLossRate(200f, 20f); // ≈0.0909
            Assert.IsTrue(AttritionExchangeRules.IsBleedingOut(0.25f, sus),
                "補充を超える損耗で消耗負け");
        }
    }
}
