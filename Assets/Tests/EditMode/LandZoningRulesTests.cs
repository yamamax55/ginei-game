using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 惑星の土地分割（ゾーニング・#2019・<see cref="LandZoningRules"/>）を固定する：用途比率（地目）・大きさに応じた分割数・
    /// ゾーン価値（地価×用途比率）・zoned 権利の全惑星換算持分。
    /// </summary>
    public class LandZoningRulesTests
    {
        [Test]
        public void ZoneWeight_SumsToOnePerSystemType()
        {
            foreach (SystemType t in new[] { SystemType.居住, SystemType.農業, SystemType.工業, SystemType.鉱業 })
            {
                float sum = 0f;
                foreach (LandUseType u in LandZoningRules.AllUses) sum += LandZoningRules.ZoneWeight(t, u);
                Assert.AreEqual(1f, sum, 1e-4f, $"{t} の用途比率合計");
            }
            // 類型ごとの偏り：鉱業は鉱山が厚い・農業は農地が厚い・居住は住宅地が厚い
            Assert.Greater(LandZoningRules.ZoneWeight(SystemType.鉱業, LandUseType.鉱山), LandZoningRules.ZoneWeight(SystemType.居住, LandUseType.鉱山));
            Assert.Greater(LandZoningRules.ZoneWeight(SystemType.農業, LandUseType.農地), LandZoningRules.ZoneWeight(SystemType.工業, LandUseType.農地));
            Assert.Greater(LandZoningRules.ZoneWeight(SystemType.居住, LandUseType.住宅地), LandZoningRules.ZoneWeight(SystemType.鉱業, LandUseType.住宅地));
        }

        [Test]
        public void MaxSubdivisions_ScalesWithPlanetSize_Bounded()
        {
            var small = new Province(1, "民主", 100f);
            var big = new Province(2, "民主", 5000f);
            var tiny = new Province(3, "民主", 0f);
            Assert.AreEqual(3, LandZoningRules.MaxSubdivisions(small));   // 1 + floor(100/50)
            Assert.AreEqual(LandZoningRules.MaxSubdivisionsCap, LandZoningRules.MaxSubdivisions(big)); // 上限クランプ
            Assert.AreEqual(LandZoningRules.MinSubdivisions, LandZoningRules.MaxSubdivisions(tiny));    // 下限
            Assert.Greater(LandZoningRules.MaxSubdivisions(big), LandZoningRules.MaxSubdivisions(small)); // 大きい惑星ほど分割可
        }

        [Test]
        public void ZoneValue_IsPlanetValueTimesWeight()
        {
            // 居住：住宅地0.5
            Assert.AreEqual(500f, LandZoningRules.ZoneValue(1000f, SystemType.居住, LandUseType.住宅地), 1e-3f);
            Assert.AreEqual(50f, LandZoningRules.ZoneValue(1000f, SystemType.居住, LandUseType.鉱山), 1e-3f); // 鉱山0.05
        }

        [Test]
        public void WholePlanetEquivalentShare_ZonedIsApportioned()
        {
            var whole = new PropertyDeed(0, 5, 0.3f, 1000f) { zoned = false };
            var zoned = new PropertyDeed(0, 5, 0.4f, 500f) { zoned = true, landUse = LandUseType.住宅地 };
            Assert.AreEqual(0.3f, LandZoningRules.WholePlanetEquivalentShare(whole, SystemType.居住), 1e-4f); // whole は持分そのまま
            Assert.AreEqual(0.2f, LandZoningRules.WholePlanetEquivalentShare(zoned, SystemType.居住), 1e-4f); // 0.4×0.5
        }
    }
}
