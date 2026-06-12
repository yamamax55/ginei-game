using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>水産・農林業（#2024・<see cref="PrimaryIndustryRules"/>）：天候(PRI-1)・養殖(PRI-2)・乱獲(PRI-3)・収益(PRI-4)。</summary>
    public class PrimaryIndustryTests
    {
        [Test]
        public void Yield_AquacultureAndOverfishing()
        {
            Assert.AreEqual(800f, PrimaryIndustryRules.WeatherAdjustedYield(1000f, 0.8f), 1e-3f); // 不作で2割減
            Assert.AreEqual(300f, PrimaryIndustryRules.AquacultureOutput(500f, 0.6f), 1e-3f);
            Assert.AreEqual(0.2f, PrimaryIndustryRules.OverfishingRisk(1200f, 1000f), 1e-4f); // 持続可能量超過＝乱獲
            Assert.AreEqual(0f, PrimaryIndustryRules.OverfishingRisk(800f, 1000f), 1e-4f);
        }
    }
}
