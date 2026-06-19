using NUnit.Framework;
using UnityEngine;

namespace Ginei.Tests
{
    /// <summary>
    /// 戦時インフレ純ロジックの EditMode テスト。既定 WartimeInflationParams で期待値を固定。
    /// （増発0.6・感応度0.5・国債吸収0.8・ハイパーゲイン2.0）
    /// </summary>
    public class WartimeInflationRulesTests
    {
        const float Eps = 1e-4f;

        [Test]
        public void MoneyPrinting_PrintsShareOfUncoveredDeficit()
        {
            // (1000 - 200) * 0.6 = 480
            float printing = WartimeInflationRules.MoneyPrinting(1000f, 200f);
            Assert.AreEqual(480f, printing, Eps);
        }

        [Test]
        public void MoneyPrinting_TaxCoversDeficit_NoPrinting()
        {
            // 税収が赤字を上回れば増発不要
            Assert.AreEqual(0f, WartimeInflationRules.MoneyPrinting(200f, 500f), Eps);
        }

        [Test]
        public void InflationRate_OutputDampensPrinting()
        {
            // 480 / 2400 * 0.5 = 0.1
            float rate = WartimeInflationRules.InflationRate(480f, 2400f);
            Assert.AreEqual(0.1f, rate, Eps);
        }

        [Test]
        public void PriceLevel_RisesWithInflationAndDt()
        {
            // 100 * (1 + 0.1 * 1) = 110
            Assert.AreEqual(110f, WartimeInflationRules.PriceLevel(100f, 0.1f, 1f), Eps);
        }

        [Test]
        public void PurchasingPowerErosion_FallsWithInflation()
        {
            // 1 / (1 + 0.1) = 0.909090...
            Assert.AreEqual(0.909090f, WartimeInflationRules.PurchasingPowerErosion(0.1f), Eps);
            // インフレ0なら不変
            Assert.AreEqual(1f, WartimeInflationRules.PurchasingPowerErosion(0f), Eps);
        }

        [Test]
        public void WarBondAbsorption_ScalesWithConfidence()
        {
            // 300 * 0.5 * 0.8 = 120
            Assert.AreEqual(120f, WartimeInflationRules.WarBondAbsorption(300f, 0.5f), Eps);
            // 信認0なら吸収されない
            Assert.AreEqual(0f, WartimeInflationRules.WarBondAbsorption(300f, 0f), Eps);
        }

        [Test]
        public void RealWarCost_InflatesNominal()
        {
            // 500 * (1 + 0.1) = 550
            Assert.AreEqual(550f, WartimeInflationRules.RealWarCost(500f, 0.1f), Eps);
        }

        [Test]
        public void HyperinflationRisk_RisesWhenCredibilityCollapses()
        {
            // 0.4 * (1 - 0.5) * 2 = 0.4
            Assert.AreEqual(0.4f, WartimeInflationRules.HyperinflationRisk(0.4f, 0.5f), Eps);
            // 信認1なら危険ゼロ
            Assert.AreEqual(0f, WartimeInflationRules.HyperinflationRisk(0.4f, 1f), Eps);
        }

        [Test]
        public void IsInflationSpiraling_AboveThreshold()
        {
            Assert.IsTrue(WartimeInflationRules.IsInflationSpiraling(0.6f));
            Assert.IsFalse(WartimeInflationRules.IsInflationSpiraling(0.4f));
        }

        [Test]
        public void Narrative_ProtractedWarErodesTheCurrency()
        {
            // 物語：戦費が税収を超え、財政赤字を通貨増発で賄う。生産が追いつかずインフレが進み、
            // 物価が上がり購買力が落ち、実質戦費が膨らむ。信認の低い政府ではハイパーインフレへ近づく。
            var p = WartimeInflationParams.Default;

            // 大きな戦費赤字、わずかな税収 → 多くを刷る
            float printing = WartimeInflationRules.MoneyPrinting(2000f, 400f, p);
            Assert.AreEqual((2000f - 400f) * WartimeInflationParams.DefaultPrintShare, printing, Eps);

            // 国債で一部を吸収（信認はそこそこ）→ 市中に残る純増発
            float absorbed = WartimeInflationRules.WarBondAbsorption(600f, 0.6f, p);
            float netPrinting = Mathf.Max(0f, printing - absorbed);
            Assert.Greater(netPrinting, 0f);

            // 生産が支え切れずインフレが進む
            float inflation = WartimeInflationRules.InflationRate(netPrinting, 2400f, p);
            Assert.Greater(inflation, 0f);

            // 物価は上がり、購買力は落ちる
            float price = WartimeInflationRules.PriceLevel(100f, inflation, 1f);
            Assert.Greater(price, 100f);
            Assert.Less(WartimeInflationRules.PurchasingPowerErosion(inflation), 1f);

            // 実質戦費は名目を上回る
            Assert.Greater(WartimeInflationRules.RealWarCost(500f, inflation), 500f);

            // 信認が崩れた政府ほどハイパーインフレの危険が高い
            float riskWeak = WartimeInflationRules.HyperinflationRisk(inflation, 0.2f, p);
            float riskStrong = WartimeInflationRules.HyperinflationRisk(inflation, 0.9f, p);
            Assert.Greater(riskWeak, riskStrong);
        }
    }
}
