using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>戦場の地形：小惑星帯=減速＋射程低下、星雲=射程低下のみ。</summary>
    public class TerrainRulesTests
    {
        [Test]
        public void Asteroid_SlowsAndShortensRange()
        {
            Assert.AreEqual(TerrainRules.AsteroidSpeed, TerrainRules.SpeedFactor(TerrainType.小惑星帯), 1e-4f);
            Assert.Less(TerrainRules.SpeedFactor(TerrainType.小惑星帯), 1f);
            Assert.Less(TerrainRules.RangeFactor(TerrainType.小惑星帯), 1f);
        }

        [Test]
        public void Nebula_ShortensRangeButNotSpeed()
        {
            Assert.AreEqual(1f, TerrainRules.SpeedFactor(TerrainType.星雲), 1e-4f);          // 減速しない
            Assert.AreEqual(TerrainRules.NebulaRange, TerrainRules.RangeFactor(TerrainType.星雲), 1e-4f);
            Assert.Less(TerrainRules.RangeFactor(TerrainType.星雲), 1f);                      // 射程/索敵低下
        }
    }
}
