using NUnit.Framework;
using UnityEngine;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>財務異常検知（#1016）の純ロジック・テスト。既定Paramsの具体値で期待値を固定。</summary>
    public class FinancialAnomalyRulesTests
    {
        const float Eps = 1e-4f;

        /// <summary>台帳が貸借一致すれば不整合度0＝整合（記帳ミスなし）。</summary>
        [Test]
        public void LedgerInconsistency_Balanced_IsZero()
        {
            // Σ借方=Σ貸方=1000 → 乖離0。
            Assert.AreEqual(0f, FinancialAnomalyRules.LedgerInconsistency(1000f, 1000f), Eps);
            Assert.IsFalse(FinancialAnomalyRules.IsLedgerInconsistent(1000f, 1000f));
        }

        /// <summary>貸借がずれると総額比で不整合度が立ち、許容0.1%超でフラグ＝改竄の影。</summary>
        [Test]
        public void LedgerInconsistency_Mismatch_FlagsTampering()
        {
            // 借方1000・貸方900 → 乖離100/最大1000=0.1。
            Assert.AreEqual(0.1f, FinancialAnomalyRules.LedgerInconsistency(1000f, 900f), Eps);
            Assert.IsTrue(FinancialAnomalyRules.IsLedgerInconsistent(1000f, 900f));
            // 空台帳は整合。
            Assert.AreEqual(0f, FinancialAnomalyRules.LedgerInconsistency(0f, 0f), Eps);
        }

        /// <summary>異常支出は平均から何σ外れたかで検出＝既定3σ以上で横領の容疑。</summary>
        [Test]
        public void ExpenseOutlier_DetectsSigmaDeviation()
        {
            // 平均100・σ10。支出130 → 3.0σ。
            Assert.AreEqual(3.0f, FinancialAnomalyRules.ExpenseOutlier(130f, 100f, 10f), Eps);
            Assert.IsTrue(FinancialAnomalyRules.IsExpenseAnomalous(130f, 100f, 10f)); // 既定3σ到達
            // 115 → 1.5σ＝平常範囲。
            Assert.AreEqual(1.5f, FinancialAnomalyRules.ExpenseOutlier(115f, 100f, 10f), Eps);
            Assert.IsFalse(FinancialAnomalyRules.IsExpenseAnomalous(115f, 100f, 10f));
        }

        /// <summary>履歴が一定（σ=0）なら、わずかなズレも即異常（割れない＝極大扱い）。</summary>
        [Test]
        public void ExpenseOutlier_FlatHistory_AnyDeviationIsExtreme()
        {
            Assert.AreEqual(0f, FinancialAnomalyRules.ExpenseOutlier(100f, 100f, 0f), Eps); // 一致＝0
            Assert.AreEqual(float.MaxValue, FinancialAnomalyRules.ExpenseOutlier(101f, 100f, 0f)); // ズレ＝極大
            Assert.IsTrue(FinancialAnomalyRules.IsExpenseAnomalous(101f, 100f, 0f));
        }

        /// <summary>報告利益が実態より不自然に滑らか＝粉飾（利益平準化）の兆候。</summary>
        [Test]
        public void RevenueSmoothing_DetectsUnnaturalSmoothness()
        {
            // 報告分散0.1・実分散0.8 → ratio=0.125 → smoothing=0.875。既定 threshold0.3 → 0.125<0.3 でフラグ。
            Assert.AreEqual(0.875f, FinancialAnomalyRules.RevenueSmoothing(0.1f, 0.8f), Eps);
            Assert.IsTrue(FinancialAnomalyRules.IsRevenueSmoothed(0.1f, 0.8f));
            // 報告0.6・実0.8 → ratio0.75 → smoothing0.25。自然＝フラグ無し。
            Assert.AreEqual(0.25f, FinancialAnomalyRules.RevenueSmoothing(0.6f, 0.8f), Eps);
            Assert.IsFalse(FinancialAnomalyRules.IsRevenueSmoothed(0.6f, 0.8f));
        }

        /// <summary>公表債務と実態の乖離＝簿外債務リスク（隠れた借金）。実態以下なら0。</summary>
        [Test]
        public void UnrecordedLiability_DetectsHiddenDebt()
        {
            // 公表500・実態1000 → 乖離500/1000=0.5。既定 threshold0.1 超でフラグ。
            Assert.AreEqual(0.5f, FinancialAnomalyRules.UnrecordedLiabilityRisk(500f, 1000f), Eps);
            Assert.IsTrue(FinancialAnomalyRules.HasUnrecordedLiability(500f, 1000f));
            // 公表が実態以上＝隠していない。
            Assert.AreEqual(0f, FinancialAnomalyRules.UnrecordedLiabilityRisk(1000f, 800f), Eps);
            Assert.IsFalse(FinancialAnomalyRules.HasUnrecordedLiability(1000f, 800f));
        }

        /// <summary>総合異常度は最も濃い影が支配し、優先度は怪しさ×規模で配分される。</summary>
        [Test]
        public void AnomalyScoreAndAuditPriority()
        {
            // ledger0.1・sigma3(=3/3=1.0)・smooth0.2・liab0.0 → 最大1.0。
            float score = FinancialAnomalyRules.AnomalyScore(0.1f, 3f, 0.2f, 0f);
            Assert.AreEqual(1.0f, score, Eps);
            // 支配チャネルは横領（σが最大寄与）。
            Assert.AreEqual(AnomalyType.横領, FinancialAnomalyRules.DominantAnomaly(0.1f, 3f, 0.2f, 0f));
            // すべて閾値未満＝清廉。
            Assert.AreEqual(AnomalyType.なし, FinancialAnomalyRules.DominantAnomaly(0f, 0f, 0f, 0f));
            // 優先度＝score×log(1+size)。score1.0・size=e-1 → log(e)=1.0。
            float size = Mathf.Exp(1f) - 1f;
            Assert.AreEqual(1.0f, FinancialAnomalyRules.AuditPriority(score, size), Eps);
            // 怪しさ0なら規模が大きくても優先度0。
            Assert.AreEqual(0f, FinancialAnomalyRules.AuditPriority(0f, 1000f), Eps);
        }
    }
}
