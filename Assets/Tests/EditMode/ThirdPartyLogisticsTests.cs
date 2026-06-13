using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>物流3PL（#2025・<see cref="ThirdPartyLogisticsRules"/>）：契約収益(3PL-1)・庫内オペ(3PL-2)・アセットライト(3PL-3)・利益(3PL-4)。</summary>
    public class ThirdPartyLogisticsTests
    {
        [Test]
        public void Revenue_AndOpsCost()
        {
            Assert.AreEqual(50000f, ThirdPartyLogisticsRules.ContractLogisticsRevenue(10000f, 5f), 1e-1f);
            Assert.AreEqual(25000f, ThirdPartyLogisticsRules.WarehouseOpsCost(1000f, 20f, 5000f), 1e-1f); // 人件費2万+設備5千
        }

        [Test]
        public void AssetLight_AndProfit()
        {
            Assert.AreEqual(0.2f, ThirdPartyLogisticsRules.AssetLightMargin(50000f, 40000f), 1e-4f);
            Assert.AreEqual(15000f, ThirdPartyLogisticsRules.LogisticsProfit(50000f, 25000f, 10000f), 1e-1f);
        }
    }
}
