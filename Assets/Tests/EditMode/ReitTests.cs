using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>REIT（#2025・<see cref="ReitRules"/>）：分配可能利益(REIT-1)・導管性(REIT-2)・分配金利回り(REIT-3)・LTV(REIT-4)。</summary>
    public class ReitTests
    {
        [Test]
        public void Distributable_AndTaxExemption()
        {
            Assert.AreEqual(500f, ReitRules.DistributableIncome(600f, 100f), 1e-3f); // NOI−利息
            Assert.IsTrue(ReitRules.IsTaxExempt(450f, 500f, 0.9f));   // 90%丁度＝非課税
            Assert.IsFalse(ReitRules.IsTaxExempt(400f, 500f, 0.9f));  // 80%＝課税
        }

        [Test]
        public void Yield_AndLtv()
        {
            Assert.AreEqual(0.05f, ReitRules.DistributionYield(50f, 1000f), 1e-4f);
            Assert.AreEqual(0.4f, ReitRules.LoanToValue(4000f, 10000f), 1e-4f);
        }
    }
}
