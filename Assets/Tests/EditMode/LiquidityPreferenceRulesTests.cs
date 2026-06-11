using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 流動性選好と金利下限（流動性の罠）の純ロジック検証（KEYN-4 #1548）。
    /// 貨幣需要・均衡金利・ゼロ下限・流動性の罠判定・金融政策の無力化・財政政策の有効・投機的需要・退蔵を既定Paramsで固定。
    /// </summary>
    public class LiquidityPreferenceRulesTests
    {
        private static LiquidityPreferenceParams P => LiquidityPreferenceParams.Default;

        /// <summary>貨幣需要＝取引(0.5×所得)＋投機(0.3×(1−金利))＋予備(0.2×不確実性)。</summary>
        [Test]
        public void LiquidityDemand_三動機の和()
        {
            // income=0.6→0.3、金利=0.2→投機0.3×0.8=0.24、不確実性=0.5→予備0.2×0.5=0.1。計0.64。
            float d = LiquidityPreferenceRules.LiquidityDemand(0.6f, 0.2f, 0.5f, P);
            Assert.AreEqual(0.64f, d, 1e-4f);
        }

        /// <summary>不確実性が高いほど予備的動機で貨幣需要が増える（流動性への選好）。</summary>
        [Test]
        public void LiquidityDemand_不確実性で増える()
        {
            float low = LiquidityPreferenceRules.LiquidityDemand(0.5f, 0.5f, 0.0f, P);
            float high = LiquidityPreferenceRules.LiquidityDemand(0.5f, 0.5f, 1.0f, P);
            Assert.Greater(high, low);
        }

        /// <summary>投機的貨幣需要は金利と逆相関＝低金利ほど現金を持つ。</summary>
        [Test]
        public void SpeculativeMoneyDemand_金利と逆相関()
        {
            Assert.AreEqual(0.9f, LiquidityPreferenceRules.SpeculativeMoneyDemand(0.1f), 1e-4f);
            Assert.AreEqual(0.2f, LiquidityPreferenceRules.SpeculativeMoneyDemand(0.8f), 1e-4f);
            Assert.Greater(LiquidityPreferenceRules.SpeculativeMoneyDemand(0.1f),
                           LiquidityPreferenceRules.SpeculativeMoneyDemand(0.8f));
        }

        /// <summary>均衡金利は需要超過で上昇するが、供給超過でもゼロ下限で打ち止め。</summary>
        [Test]
        public void MoneyMarketRate_需給とゼロ下限()
        {
            // 需要0.8>供給0.2→(0.8−0.2)×0.5=0.3。
            float tight = LiquidityPreferenceRules.MoneyMarketRate(0.2f, 0.8f, P);
            Assert.AreEqual(0.3f, tight, 1e-4f);
            // 供給0.9>需要0.1→生は負だがゼロ下限0で打ち止め。
            float loose = LiquidityPreferenceRules.MoneyMarketRate(0.9f, 0.1f, P);
            Assert.AreEqual(0f, loose, 1e-4f);
        }

        /// <summary>ゼロ下限＝名目金利を下限未満に下げられない。</summary>
        [Test]
        public void ZeroLowerBound_下限で打ち止め()
        {
            Assert.AreEqual(0f, LiquidityPreferenceRules.ZeroLowerBound(-0.05f, P), 1e-4f);
            Assert.AreEqual(0.04f, LiquidityPreferenceRules.ZeroLowerBound(0.04f, P), 1e-4f);
        }

        /// <summary>流動性の罠＝金利が罠閾値(5%)以下で成立し、財政政策だけが有効になる。</summary>
        [Test]
        public void IsLiquidityTrap_閾値以下で罠()
        {
            Assert.IsTrue(LiquidityPreferenceRules.IsLiquidityTrap(0.03f, P));
            Assert.IsFalse(LiquidityPreferenceRules.IsLiquidityTrap(0.10f, P));
        }

        /// <summary>金融政策は下限近接で弱まり、流動性の罠では完全に無力化する。</summary>
        [Test]
        public void MonetaryPolicyEffectiveness_罠で無力化()
        {
            // 罠でない金利（0.2）で近接0.3→1−0.3=0.7。
            float normal = LiquidityPreferenceRules.MonetaryPolicyEffectiveness(0.2f, 0.3f, P);
            Assert.AreEqual(0.7f, normal, 1e-4f);
            // 罠（金利0.02≤閾値0.05）では近接に関わらず0。
            float trapped = LiquidityPreferenceRules.MonetaryPolicyEffectiveness(0.02f, 0.0f, P);
            Assert.AreEqual(0f, trapped, 1e-4f);
        }

        /// <summary>財政政策は流動性の罠でフル有効（クラウディングアウト無し）・平時は半減。</summary>
        [Test]
        public void FiscalPolicyEffectiveness_罠でフル有効()
        {
            Assert.AreEqual(1f, LiquidityPreferenceRules.FiscalPolicyEffectiveness(true), 1e-4f);
            Assert.AreEqual(0.5f, LiquidityPreferenceRules.FiscalPolicyEffectiveness(false), 1e-4f);
        }

        /// <summary>退蔵圧力＝不確実性が高く信認が低いほど現金を抱え込む。</summary>
        [Test]
        public void HoardingPressure_不確実性高信認低で退蔵()
        {
            // 不確実性0.8×(1−信認0.25)=0.8×0.75=0.6。
            Assert.AreEqual(0.6f, LiquidityPreferenceRules.HoardingPressure(0.8f, 0.25f), 1e-4f);
            // 高信認では退蔵が消える。
            Assert.AreEqual(0f, LiquidityPreferenceRules.HoardingPressure(0.8f, 1.0f), 1e-4f);
        }
    }
}
