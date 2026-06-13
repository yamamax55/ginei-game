using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>葬祭・冠婚（#2025・<see cref="CeremonyRules"/>）：施行収入(CER-1)・前受金(CER-2)・高粗利率(CER-3)・利益(CER-4)。</summary>
    public class CeremonyTests
    {
        [Test]
        public void Revenue_AndPrepaid()
        {
            Assert.AreEqual(500000000f, CeremonyRules.CeremonyRevenue(500, 1000000f), 1e1f); // 高単価低頻度
            Assert.AreEqual(360000000f, CeremonyRules.PrepaidDeposits(10000, 3000f, 12), 1e1f); // 互助会積立
        }

        [Test]
        public void Margin_AndProfit()
        {
            Assert.AreEqual(0.6f, CeremonyRules.HighMarginRate(1000000f, 400000f), 1e-4f); // 高粗利
            Assert.AreEqual(150000000f, CeremonyRules.CeremonyProfit(500000000f, 200000000f, 150000000f), 1e1f);
        }
    }
}
