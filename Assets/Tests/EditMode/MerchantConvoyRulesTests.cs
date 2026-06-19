using NUnit.Framework;
using UnityEngine;

namespace Ginei.Tests
{
    /// <summary>
    /// 商船団運航（<see cref="MerchantConvoyRules"/>）の純ロジックテスト。既定 <see cref="MerchantConvoyParams.Default"/> で
    /// 期待値を固定（効率指数1.0・護衛比上限2.0・露出重み1.0・損失鋭さ1.0・損失上限0.9・基礎保険率0.02）。
    /// Pow/Sqrt 箇所のみ許容差を緩める。期待値は手計算で検算。
    /// </summary>
    public class MerchantConvoyRulesTests
    {
        const float Eps = 1e-4f;
        const float PowEps = 1e-3f;

        [Test]
        public void ConvoyEfficiency_ShipsOverOverhead()
        {
            // pow(10,1)/(1+1) = 10/2 = 5
            Assert.AreEqual(5f, MerchantConvoyRules.ConvoyEfficiency(10f, 1f), PowEps);
        }

        [Test]
        public void EscortRatio_IsEscortPerSize()
        {
            // 60/120 = 0.5
            Assert.AreEqual(0.5f, MerchantConvoyRules.EscortRatio(60f, 120f), Eps);
        }

        [Test]
        public void EscortRatio_CappedAtMax()
        {
            // 1000/100 = 10 → 上限2.0
            Assert.AreEqual(2f, MerchantConvoyRules.EscortRatio(1000f, 100f), Eps);
        }

        [Test]
        public void EscortRatio_ZeroConvoyIsZero()
        {
            Assert.AreEqual(0f, MerchantConvoyRules.EscortRatio(50f, 0f), Eps);
        }

        [Test]
        public void RaiderThreat_RaidersTimesExposure()
        {
            // 8*(1 + 1*0.5) = 8*1.5 = 12
            Assert.AreEqual(12f, MerchantConvoyRules.RaiderThreat(8f, 0.5f), Eps);
        }

        [Test]
        public void ConvoyVulnerability_HighThreatThinEscort()
        {
            // cover = 1/(1+1) = 0.5、exposed = 3*(1-0.5) = 1.5、1.5/(1.5+1) = 0.6
            Assert.AreEqual(0.6f, MerchantConvoyRules.ConvoyVulnerability(3f, 1f), Eps);
        }

        [Test]
        public void LossRate_LinearWithVulnerability()
        {
            // pow(0.6,1) = 0.6
            Assert.AreEqual(0.6f, MerchantConvoyRules.LossRate(0.6f), PowEps);
        }

        [Test]
        public void LossRate_CappedAtMax()
        {
            // pow(1,1) = 1 → 上限0.9
            Assert.AreEqual(0.9f, MerchantConvoyRules.LossRate(1f), PowEps);
        }

        [Test]
        public void InsurancePremium_RisesWithLossRate()
        {
            // (0.02 + 0.2)*1000 = 220
            Assert.AreEqual(220f, MerchantConvoyRules.InsurancePremium(0.2f, 1000f), Eps);
        }

        [Test]
        public void RouteSafety_DangerousWhenThreatHigh()
        {
            // cover = 0.5、guarded = 0.5*(1+0) = 0.5、0.5/(1+1) = 0.25
            Assert.AreEqual(0.25f, MerchantConvoyRules.RouteSafety(1f, 1f, 0f), Eps);
        }

        [Test]
        public void RouteSafety_FullControlNoThreatIsSafe()
        {
            // cover = 0.5、guarded = 0.5*(1+1) = 1.0、1.0/(1+0) = 1.0 → clamp 1
            Assert.AreEqual(1f, MerchantConvoyRules.RouteSafety(1f, 0f, 1f), Eps);
        }

        [Test]
        public void TradeRevenue_EfficiencyTimesValueLessLoss()
        {
            // 5*1000*(1-0.2) = 4000
            Assert.AreEqual(4000f, MerchantConvoyRules.TradeRevenue(5f, 1000f, 0.2f), Eps);
        }

        [Test]
        public void NullSafe_ClampsNegativeAndOutOfRange()
        {
            // 負入力は安全側へ。船数・効率・脅威は0で底打ち、収益も非負。
            Assert.AreEqual(0f, MerchantConvoyRules.ConvoyEfficiency(-5f, -1f), Eps);
            Assert.AreEqual(0f, MerchantConvoyRules.RaiderThreat(-3f, 5f), Eps);
            Assert.AreEqual(0f, MerchantConvoyRules.TradeRevenue(-1f, -1f, 2f), Eps);
            // 損失率は0..1へクランプ（2→1で全損扱い）→収益0
            Assert.AreEqual(0f, MerchantConvoyRules.TradeRevenue(5f, 1000f, 2f), Eps);
        }

        /// <summary>
        /// 物語テスト：通商破壊の脅威下で、護衛を厚くし制宙権を握ると航路が安全になり損失が減って交易が立つ。
        /// フェザーン航路が前線に晒され敵レイダーが湧くと脆弱化し損失・保険料が跳ね上がる、という流れを通しで検算。
        /// </summary>
        [Test]
        public void Narrative_EscortAndControlRescueTradeFromRaiders()
        {
            // 露出した航路に敵レイダー10、護衛は薄い（船団規模100に護衛50＝比0.5）。
            float threat = MerchantConvoyRules.RaiderThreat(10f, 1f); // 10*(1+1) = 20
            Assert.AreEqual(20f, threat, Eps);

            float thinEscort = MerchantConvoyRules.EscortRatio(50f, 100f); // 0.5
            float vulnThin = MerchantConvoyRules.ConvoyVulnerability(threat, thinEscort);
            float lossThin = MerchantConvoyRules.LossRate(vulnThin);

            // 護衛を厚く（比2.0で頭打ち）＋制宙権を握る → 安全度上昇・脆弱性低下。
            float heavyEscort = MerchantConvoyRules.EscortRatio(400f, 100f); // 4 → clamp 2
            Assert.AreEqual(2f, heavyEscort, Eps);
            float vulnHeavy = MerchantConvoyRules.ConvoyVulnerability(threat, heavyEscort);
            float lossHeavy = MerchantConvoyRules.LossRate(vulnHeavy);

            Assert.Less(vulnHeavy, vulnThin, "護衛を厚くすれば脆弱性は下がる");
            Assert.Less(lossHeavy, lossThin, "脆弱性が下がれば損失率も下がる");

            // 航路の安全度：薄い護衛・無制宙権 < 厚い護衛・制宙権あり。
            float safeThin = MerchantConvoyRules.RouteSafety(thinEscort, threat, 0f);
            float safeHeavy = MerchantConvoyRules.RouteSafety(heavyEscort, threat, 1f);
            Assert.Less(safeThin, safeHeavy, "護衛と制宙権で航路は安全になる");

            // 保険料：損失率が高い薄護衛のほうが高騰する。
            float premThin = MerchantConvoyRules.InsurancePremium(lossThin, 1000f);
            float premHeavy = MerchantConvoyRules.InsurancePremium(lossHeavy, 1000f);
            Assert.Greater(premThin, premHeavy, "危険な薄護衛は保険料が高い");

            // 交易の純収益：損失が減った厚護衛のほうが手取りが多い（同じ効率・積荷で）。
            float eff = MerchantConvoyRules.ConvoyEfficiency(10f, 1f); // 5
            float revThin = MerchantConvoyRules.TradeRevenue(eff, 1000f, lossThin);
            float revHeavy = MerchantConvoyRules.TradeRevenue(eff, 1000f, lossHeavy);
            Assert.Greater(revHeavy, revThin, "損失が減れば交易の手取りは増える");
        }
    }
}
