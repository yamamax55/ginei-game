using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 予測会計#1015を固定する：N期後残高＝準備金＋純CF×期数、債務超過までの残期間＝準備金÷燃焼率
    /// （赤字が尽きる日を逆算＝早期警告の核・黒字は無限大）、燃焼率＝赤字の絶対値、警告段階は残期間が
    /// 短いほど赤信号、必要改善幅＝目標期間まで持たせる不足分、信頼度は分散が大きいほど落ちる（下限あり）。
    /// 既定Params＝注意12/警戒6/危機3・信頼度下限0.1。クランプを担保。
    /// </summary>
    public class CashFlowForecastRulesTests
    {
        private static readonly CashFlowForecastParams P =
            CashFlowForecastParams.Default;

        [Test]
        public void ProjectedBalance_DeficitTrendDrainsReserves()
        {
            // 準備金100・毎期−10の赤字トレンド→6期後は40
            Assert.AreEqual(40f, CashFlowForecastRules.ProjectedBalance(100f, -10f, 6f), 1e-4f);
            // 黒字トレンドは増える
            Assert.AreEqual(150f, CashFlowForecastRules.ProjectedBalance(100f, 10f, 5f), 1e-4f);
            // 0期は現状そのまま／負の期数はクランプ
            Assert.AreEqual(100f, CashFlowForecastRules.ProjectedBalance(100f, -10f, 0f), 1e-4f);
            Assert.AreEqual(100f, CashFlowForecastRules.ProjectedBalance(100f, -10f, -5f), 1e-4f);
        }

        [Test]
        public void PeriodsUntilInsolvency_DeficitCountsDownToZero()
        {
            // 早期警告の核：準備金120・毎期−10燃焼＝あと12期で尽きる
            Assert.AreEqual(12f, CashFlowForecastRules.PeriodsUntilInsolvency(120f, -10f), 1e-4f);
            Assert.AreEqual(5f, CashFlowForecastRules.PeriodsUntilInsolvency(100f, -20f), 1e-4f);
        }

        [Test]
        public void PeriodsUntilInsolvency_SurplusNeverRunsOut()
        {
            // 黒字トレンドは尽きない＝無限大
            Assert.IsTrue(float.IsPositiveInfinity(CashFlowForecastRules.PeriodsUntilInsolvency(100f, 10f)));
            // 横ばい（純CF=0）も尽きない
            Assert.IsTrue(float.IsPositiveInfinity(CashFlowForecastRules.PeriodsUntilInsolvency(100f, 0f)));
            // 既に底（準備金≤0）の赤字は即0期
            Assert.AreEqual(0f, CashFlowForecastRules.PeriodsUntilInsolvency(0f, -10f), 1e-4f);
            Assert.AreEqual(0f, CashFlowForecastRules.PeriodsUntilInsolvency(-50f, -10f), 1e-4f);
        }

        [Test]
        public void BurnRate_IsDeficitMagnitude()
        {
            Assert.AreEqual(10f, CashFlowForecastRules.BurnRate(-10f), 1e-4f); // 毎期10ずつ溶ける
            Assert.AreEqual(0f, CashFlowForecastRules.BurnRate(10f), 1e-4f);   // 黒字は燃えない
            Assert.AreEqual(0f, CashFlowForecastRules.BurnRate(0f), 1e-4f);
        }

        [Test]
        public void WarningLevel_RedderAsTimeRunsShort()
        {
            // 既定＝注意12/警戒6/危機3
            Assert.AreEqual(CashFlowWarning.余裕,
                CashFlowForecastRules.WarningLevel(CashFlowForecastRules.Infinite, P));        // 黒字＝余裕
            Assert.AreEqual(CashFlowWarning.余裕,
                CashFlowForecastRules.WarningLevel(20f, P));                                   // 12超＝余裕
            Assert.AreEqual(CashFlowWarning.注意,
                CashFlowForecastRules.WarningLevel(8f, P));                                    // 6超12以下＝注意
            Assert.AreEqual(CashFlowWarning.警戒,
                CashFlowForecastRules.WarningLevel(4f, P));                                    // 3超6以下＝警戒
            Assert.AreEqual(CashFlowWarning.危機,
                CashFlowForecastRules.WarningLevel(2f, P));                                    // 3以下＝危機
            Assert.AreEqual(CashFlowWarning.危機,
                CashFlowForecastRules.WarningLevel(0f, P));                                    // 底＝危機
        }

        [Test]
        public void RequiredCorrection_GapToSurviveTarget()
        {
            // 準備金120・現状−20・目標12期で持たせる→必要純CF=−10、不足は−10−(−20)=10
            Assert.AreEqual(10f, CashFlowForecastRules.RequiredCorrection(120f, -20f, 12f), 1e-4f);
            // 既に十分（現状−5で目標純CF−10を上回る）＝改善不要
            Assert.AreEqual(0f, CashFlowForecastRules.RequiredCorrection(120f, -5f, 12f), 1e-4f);
            // 黒字は当然不要
            Assert.AreEqual(0f, CashFlowForecastRules.RequiredCorrection(120f, 10f, 12f), 1e-4f);
            // 目標期間0＝制約なし
            Assert.AreEqual(0f, CashFlowForecastRules.RequiredCorrection(120f, -20f, 0f), 1e-4f);
        }

        [Test]
        public void TrendConfidence_VolatilityErodesButFloorRemains()
        {
            Assert.AreEqual(1f, CashFlowForecastRules.TrendConfidence(0f, P), 1e-4f);    // 安定＝予測が当たる
            Assert.AreEqual(0.7f, CashFlowForecastRules.TrendConfidence(0.3f, P), 1e-4f);
            Assert.AreEqual(0.1f, CashFlowForecastRules.TrendConfidence(1f, P), 1e-4f);  // 激動期でも下限0.1は残る
            Assert.AreEqual(0.1f, CashFlowForecastRules.TrendConfidence(5f, P), 1e-4f);  // 入力クランプ
        }

        [Test]
        public void Params_CtorEnforcesOrderingAndClamps()
        {
            // 危機≤警戒≤注意 の不変条件＋信頼度クランプ
            var p = new CashFlowForecastParams(-1f, -1f, -1f, 2f);
            Assert.AreEqual(0f, p.crisisPeriods, 1e-4f);
            Assert.AreEqual(0f, p.alertPeriods, 1e-4f);
            Assert.AreEqual(0f, p.cautionPeriods, 1e-4f);
            Assert.AreEqual(1f, p.minConfidence, 1e-4f);
        }
    }
}
