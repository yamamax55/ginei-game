using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>データセンター（#2025・<see cref="DataCenterRules"/>）：コロケーション(DC-1)・PUE(DC-2)・電力コスト(DC-3)・利益(DC-4)。</summary>
    public class DataCenterTests
    {
        [Test]
        public void Colocation_AndPue()
        {
            Assert.AreEqual(50000000f, DataCenterRules.ColocationRevenue(1000, 50000f), 1e1f);
            Assert.AreEqual(1.5f, DataCenterRules.PowerUsageEffectiveness(15000f, 10000f), 1e-4f); // 全電力/IT電力
        }

        [Test]
        public void PowerCost_AndProfit()
        {
            Assert.AreEqual(30000f, DataCenterRules.PowerOperatingCost(10000f, 1.5f, 2f), 1e-1f); // IT×PUE×単価
            Assert.AreEqual(10000000f, DataCenterRules.DataCenterProfit(50000000f, 30000000f, 10000000f), 1e1f);
        }
    }
}
