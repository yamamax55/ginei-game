using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>映画スタジオ（#2025・<see cref="FilmStudioRules"/>）：興行配分(FILM-1)・ライブラリ(FILM-2)・予算超過(FILM-3)・利益(FILM-4)。</summary>
    public class FilmStudioTests
    {
        [Test]
        public void BoxOffice_AndLibrary()
        {
            Assert.AreEqual(100000000f, FilmStudioRules.BoxOfficeShare(200000000f, 0.5f), 1e2f); // 劇場と折半
            Assert.AreEqual(100000000f, FilmStudioRules.LibraryLicensingRevenue(200, 500000f), 1e2f); // 旧作資産
        }

        [Test]
        public void Overrun_AndProfit()
        {
            Assert.AreEqual(15000000f, FilmStudioRules.BudgetOverrun(50000000f, 65000000f), 1e1f); // 製作費博打
            // 興行1億+ライセンス1億−製作1.2億−宣伝0.5億 = 0.3億
            Assert.AreEqual(30000000f, FilmStudioRules.FilmStudioProfit(100000000f, 100000000f, 120000000f, 50000000f), 1e2f);
        }
    }
}
