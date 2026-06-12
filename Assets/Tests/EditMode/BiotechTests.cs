using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>バイオ・遺伝子（#2025・<see cref="BiotechRules"/>）：ライセンスアウト(BIO-1)・承認ゲート(BIO-2)・遺伝子治療(BIO-3)・利益(BIO-4)。</summary>
    public class BiotechTests
    {
        [Test]
        public void Licensing_AndApprovalGate()
        {
            // 5提携×(一時金100万+マイルストン300万) = 2000万
            Assert.AreEqual(20000000f, BiotechRules.PlatformLicensingRevenue(5, 1000000f, 3000000f), 1e1f);
            Assert.IsTrue(BiotechRules.RegulatoryApprovalGate(0.8f, 0.7f, 0.6f));  // 安全性・有効性とも閾値超
            Assert.IsFalse(BiotechRules.RegulatoryApprovalGate(0.5f, 0.7f, 0.6f)); // 安全性不足で不承認
        }

        [Test]
        public void Treatment_AndProfit()
        {
            Assert.AreEqual(500000000f, BiotechRules.GeneticTreatmentRevenue(1000, 500000f), 1e1f); // 超高単価
            Assert.AreEqual(150000000f, BiotechRules.BiotechProfit(500000000f, 200000000f, 100000000f, 50000000f), 1e1f);
        }
    }
}
