using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>製薬会社（#2024・<see cref="PharmaRules"/>）：R&D期待価値(PHM-1)・治験(PHM-2)・特許利益(PHM-3)・パテントクリフ(PHM-4)。</summary>
    public class PharmaTests
    {
        [Test]
        public void Rd_AndTrial()
        {
            Assert.AreEqual(1000f, PharmaRules.ExpectedRdValue(10000f, 0.1f), 1e-3f); // ピーク売上×低成功率
            Assert.IsTrue(PharmaRules.TrialSuccess(0.3f, 0.5f));
            Assert.IsFalse(PharmaRules.TrialSuccess(0.7f, 0.5f));
        }

        [Test]
        public void Patent_AndCliff()
        {
            Assert.AreEqual(4000f, PharmaRules.PatentProfit(5000f, 0.8f), 1e-3f); // 特許保護下の高利益
            Assert.AreEqual(500f, PharmaRules.PostPatentSales(5000f, 0.9f), 1e-3f); // 特許切れで9割消失＝パテントクリフ
            Assert.IsTrue(PharmaRules.IsBlockbuster(5000f, 1000f));
            Assert.IsFalse(PharmaRules.IsBlockbuster(500f, 1000f));
        }
    }
}
