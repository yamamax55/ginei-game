using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 戦時インフレを固定する：物価上昇率＝増発×影響−産出成長（貨幣数量説の簡易形）、
    /// 実質賃金は物価で目減り、増発は見えない税収を即金で生み、不満は上昇率に非線形、
    /// ハイパーインフレ閾値判定。クランプと「今日の楽・明日の痛み」の物語を担保。
    /// </summary>
    public class InflationRulesTests
    {
        private static readonly InflationParams P = InflationParams.Default;
        // 増発影響0.2/見えない税0.1/不満指数2/不満スケール4/ハイパー閾値0.5

        [Test]
        public void InflationRate_PrintingMinusGrowth()
        {
            Assert.AreEqual(0.2f, InflationRules.InflationRate(1f, 0f, P), 1e-5f);    // 全力増発・成長なし＝最大
            Assert.AreEqual(0f, InflationRules.InflationRate(0.5f, 0.1f, P), 1e-5f);  // 成長が増発に追いつけば物価安定
            Assert.AreEqual(-0.1f, InflationRules.InflationRate(0f, 0.1f, P), 1e-5f); // 増発なし・成長＝デフレ
            Assert.AreEqual(0.3f, InflationRules.InflationRate(1f, -0.1f, P), 1e-5f); // 戦時の生産崩壊はインフレを加速
        }

        [Test]
        public void PriceLevelTick_GrowsWithRate_FloorsAtMin()
        {
            Assert.AreEqual(1.2f, InflationRules.PriceLevelTick(1f, 1f, 0f, 1f, P), 1e-5f);  // 1×(1+0.2)
            Assert.AreEqual(1f, InflationRules.PriceLevelTick(1f, 0.5f, 0.1f, 1f, P), 1e-5f); // 均衡＝据え置き
            // 強デフレでも下限で止まる（rate=−1, dt=1 → ×0 → MinPriceLevel）
            Assert.AreEqual(InflationRules.MinPriceLevel,
                InflationRules.PriceLevelTick(0.5f, 0f, 1f, 1f, P), 1e-5f);
            Assert.AreEqual(1f, InflationRules.PriceLevelTick(1f, 1f, 0f, 0f, P), 1e-5f);     // dt=0＝不変
        }

        [Test]
        public void RealWageFactor_ErodedByPrices_CappedAtOne()
        {
            Assert.AreEqual(0.5f, InflationRules.RealWageFactor(2f, 1f), 1e-5f); // 物価2倍＝実質半減
            Assert.AreEqual(1f, InflationRules.RealWageFactor(1f, 1f), 1e-5f);   // 目減りなし
            Assert.AreEqual(1f, InflationRules.RealWageFactor(1f, 2f), 1e-5f);   // 賃金超過は1にクランプ
            Assert.AreEqual(0f, InflationRules.RealWageFactor(1f, -5f), 1e-5f);  // 負の賃金はクランプ
        }

        [Test]
        public void HiddenTaxRevenue_PrintingTimesEconomy()
        {
            Assert.AreEqual(10f, InflationRules.HiddenTaxRevenue(1f, 100f, P), 1e-5f);  // 1×100×0.1
            Assert.AreEqual(10f, InflationRules.HiddenTaxRevenue(0.5f, 200f, P), 1e-5f);
            Assert.AreEqual(0f, InflationRules.HiddenTaxRevenue(0f, 100f, P), 1e-5f);   // 刷らなければ無料の戦費もない
            Assert.AreEqual(10f, InflationRules.HiddenTaxRevenue(5f, 100f, P), 1e-5f);  // 増発は0..1にクランプ
        }

        [Test]
        public void DiscontentFromInflation_NonlinearPain()
        {
            Assert.AreEqual(0.04f, InflationRules.DiscontentFromInflation(0.1f, P), 1e-5f); // 4×0.1²
            Assert.AreEqual(0.16f, InflationRules.DiscontentFromInflation(0.2f, P), 1e-5f); // 4×0.2²
            Assert.AreEqual(1f, InflationRules.DiscontentFromInflation(0.5f, P), 1e-5f);    // 4×0.5²＝上限
            // 率2倍で不満4倍＝急激ほど非線形に痛い
            Assert.Greater(InflationRules.DiscontentFromInflation(0.2f, P),
                2f * InflationRules.DiscontentFromInflation(0.1f, P));
            Assert.AreEqual(0f, InflationRules.DiscontentFromInflation(-0.1f, P), 1e-5f);   // デフレは不満0
        }

        [Test]
        public void IsHyperinflation_ThresholdJudgement()
        {
            Assert.IsTrue(InflationRules.IsHyperinflation(0.5f, P));   // 閾値ちょうど＝制御不能
            Assert.IsFalse(InflationRules.IsHyperinflation(0.49f, P));
            Assert.IsTrue(InflationRules.IsHyperinflation(0.3f, 0.3f)); // カスタム閾値
            Assert.IsFalse(InflationRules.IsHyperinflation(0.2f, 0.3f));
        }

        [Test]
        public void LongRunStory_PrintToday_PayTomorrow()
        {
            // 全力増発：今日は戦費が湧く（見えない税）
            float revenue = InflationRules.HiddenTaxRevenue(1f, 100f, P);
            Assert.Greater(revenue, 0f);

            // だが刷り続けると物価が複利で上がり、賃金据え置きなら実質賃金が目減りして不満が積もる
            float price = 1f;
            for (int i = 0; i < 10; i++)
                price = InflationRules.PriceLevelTick(price, 1f, 0f, 1f, P);

            Assert.Greater(price, 1f);                                       // 物価は確実に上がっている
            float realWage = InflationRules.RealWageFactor(price, 1f);
            Assert.Less(realWage, 0.2f);                                     // 1.2^10≈6.19 → 実質賃金は2割未満
            float rate = InflationRules.InflationRate(1f, 0f, P);
            Assert.Greater(InflationRules.DiscontentFromInflation(rate, P), 0f); // 痛みは不満として返ってくる
        }
    }
}
