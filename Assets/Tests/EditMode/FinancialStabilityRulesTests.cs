using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 財政自動運営・金融危機オーケストレータ <see cref="FinancialStabilityRules"/> の EditMode テスト。
    /// 数式は委譲先（FiscalRules/DebtDeflationRules/FinancialContagionRules/AutoTreasuryRules）で担保済みのため、
    /// ここは合成・null安全・比例関係の境界のみを確認する。
    /// </summary>
    public class FinancialStabilityRulesTests
    {
        // ===== 危機検出 =====

        [Test]
        public void DetectCrisis_HighDebt_IsCrisis()
        {
            // 債務比率が crisis(2.0) を大きく超え財政健全度0＝危機度1。
            var fs = new FiscalState(revenue: 10f, baseExpenditure: 10f, debt: 1000f);
            Assert.IsTrue(FinancialStabilityRules.DetectCrisis(fs, economy: 100f),
                "高債務で危機を検出するはず");
            Assert.That(FinancialStabilityRules.CrisisSeverity(fs, 100f, 1f, 0f), Is.GreaterThanOrEqualTo(0.5f));
        }

        [Test]
        public void DetectCrisis_Healthy_IsNotCrisis()
        {
            // 黒字・無債務＝財政健全度1・危機度0。
            var fs = new FiscalState(revenue: 100f, baseExpenditure: 50f, debt: 0f);
            Assert.IsFalse(FinancialStabilityRules.DetectCrisis(fs, economy: 1000f),
                "健全財政で危機を検出しないはず");
            Assert.That(FinancialStabilityRules.CrisisSeverity(fs, 1000f, 1f, 0f), Is.EqualTo(0f).Within(1e-4f));
        }

        [Test]
        public void CrisisSeverity_DebtDeflation_RaisesSeverity()
        {
            // 財政は健全でも、物価が基準割れ＆重い債務負荷なら負債デフレで危機度が上がる。
            var fs = new FiscalState(revenue: 100f, baseExpenditure: 50f, debt: 0f);
            float withDeflation = FinancialStabilityRules.CrisisSeverity(fs, 1000f, priceLevel: 0.7f, debtLoad: 0.8f);
            float noDeflation = FinancialStabilityRules.CrisisSeverity(fs, 1000f, priceLevel: 1.0f, debtLoad: 0.8f);
            Assert.That(withDeflation, Is.GreaterThan(noDeflation), "負債デフレ突入で危機度が上がるはず");
        }

        // ===== 伝染 =====

        [Test]
        public void ContagionTo_ScalesWithLinkage()
        {
            float severity = 0.8f;
            float low = FinancialStabilityRules.ContagionTo(severity, linkage: 0.2f);
            float high = FinancialStabilityRules.ContagionTo(severity, linkage: 0.9f);
            Assert.That(high, Is.GreaterThan(low), "つながりが太いほど伝染が強いはず");

            // つながり0なら伝わらない。
            Assert.That(FinancialStabilityRules.ContagionTo(severity, linkage: 0f), Is.EqualTo(0f).Within(1e-4f));
        }

        [Test]
        public void ContagionTo_Firewall_DampensTransmission()
        {
            float raw = FinancialStabilityRules.ContagionTo(0.9f, 0.9f);
            float damped = FinancialStabilityRules.ContagionTo(0.9f, 0.9f, liquiditySupport: 1f, capitalControl: 1f);
            Assert.That(damped, Is.LessThan(raw), "防火壁が伝染を減衰させるはず");
        }

        // ===== 自動財務運営 =====

        [Test]
        public void TickAutoTreasury_ReserveShortfall_IssuesBonds()
        {
            // 準備金割れ＋債務余地あり＝起債。
            var fs = new FiscalState(revenue: 100f, baseExpenditure: 100f, debt: 10f);
            var action = FinancialStabilityRules.TickAutoTreasury(
                fs, economy: 1000f, reserves: 5f, reserveFloor: 50f, debtCeiling: 1.0f, dt: 1f);
            Assert.AreEqual(TreasuryAction.起債, action, "準備金割れ＋余地ありで起債するはず");
        }

        [Test]
        public void TickAutoTreasury_NullOrZeroDt_DoesNothing()
        {
            Assert.AreEqual(TreasuryAction.何もしない,
                FinancialStabilityRules.TickAutoTreasury(null, 1000f, 100f, 50f, 1.0f, 1f),
                "fs=null は無作動");
            var fs = new FiscalState(100f, 100f, 0f);
            Assert.AreEqual(TreasuryAction.何もしない,
                FinancialStabilityRules.TickAutoTreasury(fs, 1000f, 5f, 50f, 1.0f, dt: 0f),
                "dt<=0 は無作動");
        }

        // ===== null安全 =====

        [Test]
        public void NullSafe_NoThrow()
        {
            Assert.DoesNotThrow(() =>
            {
                FinancialStabilityRules.CrisisSeverity(null, 100f, 1f, 0f);
                FinancialStabilityRules.DetectCrisis(null, 100f);
                FinancialStabilityRules.ResolveSystemicCrisis(null, 0.5f);
            });
            Assert.That(FinancialStabilityRules.CrisisSeverity(null, 100f, 1f, 0f), Is.EqualTo(0f));
            var res = FinancialStabilityRules.ResolveSystemicCrisis(null, 0.5f);
            Assert.AreEqual(0, res.failedCount);
            Assert.That(res.totalLoss, Is.EqualTo(0f));
        }
    }
}
