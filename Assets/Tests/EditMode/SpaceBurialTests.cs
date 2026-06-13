using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>宇宙葬・軌道埋葬（#2025・<see cref="SpaceBurialRules"/>）：施行(SBUR-1)・打ち上げ按分(SBUR-2)・追悼サブスク(SBUR-3)・利益(SBUR-4)。</summary>
    public class SpaceBurialTests
    {
        [Test]
        public void Service_AndLaunchShare()
        {
            Assert.AreEqual(100000000f, SpaceBurialRules.BurialServiceRevenue(200, 500000f), 1e1f);
            Assert.AreEqual(500000f, SpaceBurialRules.LaunchCostPerCapsule(50000000f, 100), 1e-1f); // 相乗りで割る
            Assert.AreEqual(50000000f, SpaceBurialRules.LaunchCostPerCapsule(50000000f, 0), 1e1f);   // 相乗り無し=全額
        }

        [Test]
        public void Memorial_AndProfit()
        {
            Assert.AreEqual(50000000f, SpaceBurialRules.MemorialSubscription(5000, 10000f), 1e1f);
            // 施行1億+追悼0.5億−打ち上げ1億−固定0.2億 = 0.3億
            Assert.AreEqual(30000000f, SpaceBurialRules.SpaceBurialProfit(100000000f, 50000000f, 100000000f, 20000000f), 1e1f);
        }
    }
}
