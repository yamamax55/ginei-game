using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>宇宙採掘（#2025・<see cref="AsteroidMiningRules"/>）：採掘量(AMIN-1)・鉱石価値(AMIN-2)・探鉱(AMIN-3)・利益(AMIN-4)。</summary>
    public class AsteroidMiningTests
    {
        [Test]
        public void Volume_AndGradeValue()
        {
            Assert.AreEqual(15000f, AsteroidMiningRules.ExtractedVolume(10, 50f, 30), 1e-1f); // 10機×50×30日
            Assert.AreEqual(300000f, AsteroidMiningRules.OreGradeValue(15000f, 0.2f, 100f), 1e-1f);
        }

        [Test]
        public void Prospecting_AndProfit()
        {
            Assert.IsTrue(AsteroidMiningRules.ProspectingSuccess(0.7f, 0.6f));
            Assert.IsFalse(AsteroidMiningRules.ProspectingSuccess(0.5f, 0.6f)); // 外れ＝探査費丸損
            Assert.AreEqual(100000f, AsteroidMiningRules.AsteroidMiningProfit(300000f, 100000f, 80000f, 20000f), 1e-1f);
        }
    }
}
