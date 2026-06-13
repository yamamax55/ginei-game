using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 財務ガードレール（#1013・国策チャネル）を固定する：債務上限/準備金下限/税率レンジの逸脱判定・最重大逸脱の優先順・
    /// 財政余裕・総合遵守度。逸脱検知のみ＝財政の実体（FiscalRules）は動かさない。すべて純ロジック・決定論。
    /// </summary>
    public class FiscalPolicyRulesTests
    {
        // 既定：債務比率上限1.0／準備金下限50／税率レンジ0.1〜0.4。

        [Test]
        public void DebtCeiling_OverThreshold_Breaches()
        {
            var p = FiscalPolicy.Default; // 上限1.0
            Assert.IsFalse(FiscalPolicyRules.DebtCeilingBreached(0.9f, p)); // 枠内
            Assert.IsFalse(FiscalPolicyRules.DebtCeilingBreached(1.0f, p)); // ちょうどは可
            Assert.IsTrue(FiscalPolicyRules.DebtCeilingBreached(1.2f, p));  // 超過＝逸脱
        }

        [Test]
        public void ReserveFloor_BelowFloor_Breaches()
        {
            var p = FiscalPolicy.Default; // 下限50
            Assert.IsFalse(FiscalPolicyRules.ReserveFloorBreached(60f, p));
            Assert.IsFalse(FiscalPolicyRules.ReserveFloorBreached(50f, p)); // ちょうどは可
            Assert.IsTrue(FiscalPolicyRules.ReserveFloorBreached(30f, p));  // 割れ＝逸脱
        }

        [Test]
        public void TaxRate_OutsideRange_NotInRange()
        {
            var p = FiscalPolicy.Default; // 0.1〜0.4
            Assert.IsTrue(FiscalPolicyRules.TaxRateInRange(0.25f, p));  // レンジ内
            Assert.IsTrue(FiscalPolicyRules.TaxRateInRange(0.1f, p));   // 端は可
            Assert.IsTrue(FiscalPolicyRules.TaxRateInRange(0.4f, p));   // 端は可
            Assert.IsFalse(FiscalPolicyRules.TaxRateInRange(0.05f, p)); // 低すぎ＝歳入不足
            Assert.IsFalse(FiscalPolicyRules.TaxRateInRange(0.5f, p));  // 高すぎ＝不満
        }

        [Test]
        public void WorstBreach_PrioritizesDebtOverReserveOverTax()
        {
            var p = FiscalPolicy.Default;
            // 3枠すべて逸脱＝最優先の債務超過警戒を返す。
            Assert.AreEqual(PolicyBreach.債務超過警戒,
                FiscalPolicyRules.WorstBreach(1.5f, 10f, 0.6f, p));
            // 債務はOK・準備金と税率が逸脱＝準備金不足が優先。
            Assert.AreEqual(PolicyBreach.準備金不足,
                FiscalPolicyRules.WorstBreach(0.5f, 10f, 0.6f, p));
            // 税率のみ逸脱。
            Assert.AreEqual(PolicyBreach.税率逸脱,
                FiscalPolicyRules.WorstBreach(0.5f, 80f, 0.6f, p));
            // すべて枠内＝なし。
            Assert.AreEqual(PolicyBreach.なし,
                FiscalPolicyRules.WorstBreach(0.5f, 80f, 0.25f, p));
        }

        [Test]
        public void FiscalHeadroom_RemainingRoomToCeiling()
        {
            var p = FiscalPolicy.Default; // 上限1.0
            Assert.AreEqual(0.4f, FiscalPolicyRules.FiscalHeadroom(0.6f, p), 1e-4f); // あと0.4起債可
            Assert.AreEqual(0f, FiscalPolicyRules.FiscalHeadroom(1.0f, p), 1e-4f);   // 上限到達
            Assert.AreEqual(0f, FiscalPolicyRules.FiscalHeadroom(1.5f, p), 1e-4f);   // 超過でも下限0
        }

        [Test]
        public void PolicyComplianceScore_FullWhenAllWithinFrames()
        {
            var p = FiscalPolicy.Default;
            // 債務0（余地満額1.0/1.0=1）・準備金100（>=50=1）・税率0.25（レンジ内=1）→ 0.5+0.3+0.2=1。
            Assert.AreEqual(1f, FiscalPolicyRules.PolicyComplianceScore(0f, 100f, 0.25f, p), 1e-4f);
        }

        [Test]
        public void PolicyComplianceScore_DegradesWithBreaches()
        {
            var p = FiscalPolicy.Default;
            // 債務比率0.5（余地0.5→debtScore0.5×0.5=0.25）・準備金25（25/50=0.5→0.5×0.3=0.15）・税率0.25（レンジ内→0.2）。
            // 合計＝0.60。
            float score = FiscalPolicyRules.PolicyComplianceScore(0.5f, 25f, 0.25f, p);
            Assert.AreEqual(0.60f, score, 1e-4f);
            // 全枠逸脱の極端ケースは満点を割る。
            Assert.Less(FiscalPolicyRules.PolicyComplianceScore(1.5f, 0f, 0.9f, p), 1f);
        }
    }
}
