using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 信託銀行（#2003 TRST・<see cref="TrustBankRules"/>）を固定する：信託の基礎と分別管理(TRST-1)、金銭信託(TRST-2)、
    /// アセットマネジメント(TRST-3)、年金信託(TRST-4)、投資信託(TRST-5)、受託者責任・併営(TRST-6)。
    /// </summary>
    public class TrustBankTests
    {
        // ===== TRST-1 信託の基礎・分別管理 =====
        [Test]
        public void TrustFee_AndBankruptcyRemote()
        {
            Assert.AreEqual(100f, TrustBankRules.TrustFee(10000f, 0.01f), 1e-3f);
            // 倒産隔離：信託銀行が倒産しても受益者は全額取り戻せる
            Assert.AreEqual(10000f, TrustBankRules.RecoverableByBeneficiary(10000f, bankInsolvent: true), 1e-3f);
            Assert.AreEqual(10000f, TrustBankRules.RecoverableByBeneficiary(10000f, bankInsolvent: false), 1e-3f);
            Assert.IsTrue(TrustBankRules.IsBankruptcyRemote());
        }

        // ===== TRST-2 金銭信託 =====
        [Test]
        public void MoneyTrust_GuaranteedVsPerformance()
        {
            Assert.AreEqual(500f, TrustBankRules.TrustReturn(10000f, 0.05f), 1e-3f);
            // 実績配当：実際利回りをそのまま渡す
            Assert.AreEqual(500f, TrustBankRules.BeneficiaryDistribution(10000f, 0.05f, false, 0.02f), 1e-3f);
            // 元本保証：保証利回り固定（差額は信託銀行）
            Assert.AreEqual(200f, TrustBankRules.BeneficiaryDistribution(10000f, 0.05f, true, 0.02f), 1e-3f);
            // 補填：実際1%＜保証2%なら信託銀行が差額を埋める
            Assert.AreEqual(100f, TrustBankRules.PrincipalGuaranteeShortfall(10000f, 0.02f, 0.01f), 1e-3f);
            Assert.AreEqual(0f, TrustBankRules.PrincipalGuaranteeShortfall(10000f, 0.02f, 0.05f), 1e-3f); // 上回れば補填なし
        }

        // ===== TRST-3 アセットマネジメント =====
        [Test]
        public void AssetManagement_FeesAndAum()
        {
            Assert.AreEqual(1000f, TrustBankRules.ManagementFee(100000f, 0.01f), 1e-3f);
            Assert.AreEqual(500f, TrustBankRules.PerformanceFee(5000f, 0.1f), 1e-3f);
            Assert.AreEqual(0f, TrustBankRules.PerformanceFee(-5000f, 0.1f), 1e-3f); // 損失時は成功報酬なし
            Assert.AreEqual(115000f, TrustBankRules.AumAfterFlows(100000f, 10000f, 5000f), 1e-3f);
            Assert.AreEqual(1500f, TrustBankRules.AssetManagementRevenue(100000f, 0.01f, 5000f, 0.1f), 1e-3f);
        }

        // ===== TRST-4 年金信託 =====
        [Test]
        public void PensionTrust_FundingAndContribution()
        {
            Assert.AreEqual(0.8f, TrustBankRules.FundingRatio(8000f, 10000f), 1e-3f);
            Assert.IsTrue(TrustBankRules.IsUnderfunded(8000f, 10000f));   // 積立不足
            Assert.IsFalse(TrustBankRules.IsUnderfunded(10000f, 10000f));
            // 不足2000を5年で埋める＝年400
            Assert.AreEqual(400f, TrustBankRules.RequiredContribution(10000f, 8000f, 5), 1e-3f);
            Assert.AreEqual(400f, TrustBankRules.PensionBenefit(10000f, 0.04f), 1e-3f);
        }

        // ===== TRST-5 投資信託 =====
        [Test]
        public void InvestmentTrust_NavAndUnits()
        {
            Assert.AreEqual(120f, TrustBankRules.NetAssetValue(120000f, 1000f), 1e-3f); // 基準価額
            Assert.AreEqual(100f, TrustBankRules.UnitsIssued(12000f, 120f), 1e-3f);     // 購入口数
            Assert.AreEqual(0f, TrustBankRules.UnitsIssued(12000f, 0f), 1e-3f);
            Assert.AreEqual(600f, TrustBankRules.FundTrustFee(120000f, 0.005f), 1e-3f);
        }

        // ===== TRST-6 受託者責任・併営 =====
        [Test]
        public void Fiduciary_AndCombinedRevenue()
        {
            // ベンチマーク5%・許容差2%なら4%は許容内（3%以上）
            Assert.IsTrue(TrustBankRules.DueCareCompliant(0.04f, 0.05f, 0.02f));
            Assert.IsFalse(TrustBankRules.DueCareCompliant(0.02f, 0.05f, 0.01f)); // 著しく劣後
            Assert.AreEqual(200f, TrustBankRules.FiduciaryBreachLoss(10000f, 0.02f), 1e-3f);
            // 併営：銀行業務2000＋信託業務1500 = 3500
            Assert.AreEqual(3500f, TrustBankRules.CombinedRevenue(2000f, 1500f), 1e-3f);
        }
    }
}
