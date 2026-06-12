using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>陸運業（#2024・<see cref="LandTransportRules"/>）：トンキロ(LND-1)・運賃収益(LND-2)・採算(LND-3)・積載率(LND-4)。</summary>
    public class LandTransportTests
    {
        [Test]
        public void TonKm_RevenueAndProfit()
        {
            Assert.AreEqual(5000f, LandTransportRules.TonKilometers(100f, 50f), 1e-1f);
            Assert.AreEqual(10000f, LandTransportRules.TransportRevenue(5000f, 2f), 1e-1f);
            Assert.AreEqual(2000f, LandTransportRules.TransportProfit(10000f, 3000f, 5000f), 1e-1f); // 運賃−燃料−固定
        }

        [Test]
        public void LoadFactor()
        {
            Assert.AreEqual(0.8f, LandTransportRules.LoadFactor(800f, 1000f), 1e-4f); // 積載率＝空車を減らす
        }
    }
}
