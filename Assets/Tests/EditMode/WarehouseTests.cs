using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>倉庫・運輸関連業（#2024・<see cref="WarehouseRules"/>）：保管料(WHS-1)・稼働率(WHS-2)・空き損失(WHS-3)・利益(WHS-4)。</summary>
    public class WarehouseTests
    {
        [Test]
        public void Storage_UtilizationAndVacancy()
        {
            Assert.AreEqual(8000f, WarehouseRules.StorageRevenue(800f, 10f), 1e-1f);
            Assert.AreEqual(0.8f, WarehouseRules.WarehouseUtilization(800f, 1000f), 1e-4f);
            Assert.AreEqual(2000f, WarehouseRules.VacancyLoss(1000f, 800f, 10f), 1e-1f); // 空き2割の機会損失
        }

        [Test]
        public void Profit()
        {
            Assert.AreEqual(3000f, WarehouseRules.WarehouseProfit(8000f, 5000f), 1e-1f);
        }
    }
}
