using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>農業プラント・垂直農法（#2025・<see cref="VerticalFarmRules"/>）：屋内産出(VFRM-1)・エネルギー(VFRM-2)・環境独立(VFRM-3)・利益(VFRM-4)。</summary>
    public class VerticalFarmTests
    {
        [Test]
        public void Yield_AndEnergy()
        {
            Assert.AreEqual(60000f, VerticalFarmRules.IndoorYield(1000, 5f, 12), 1e-1f); // 棚×収量×多毛作12回
            Assert.AreEqual(50000f, VerticalFarmRules.EnergyCost(100000f, 0.5f), 1e-1f); // 照明電力が主費目
        }

        [Test]
        public void EnvironmentIndependence_AndProfit()
        {
            // 過酷な惑星(0.8)でも屋内は満額／露地は8割減
            Assert.AreEqual(60000f, VerticalFarmRules.EffectiveYieldOnPlanet(60000f, 0.8f, true), 1e-1f);
            Assert.AreEqual(12000f, VerticalFarmRules.EffectiveYieldOnPlanet(60000f, 0.8f, false), 1e-1f);
            Assert.AreEqual(80000f, VerticalFarmRules.VerticalFarmProfit(200000f, 50000f, 30000f, 40000f), 1e-1f);
        }
    }
}
