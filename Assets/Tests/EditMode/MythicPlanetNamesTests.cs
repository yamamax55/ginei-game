using System.Collections.Generic;
using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 惑星名プール（日本神話ベース・<see cref="MythicPlanetNames"/>）を固定する：プールに重複が無いこと、
    /// 巡回アクセス、決定論、同一星系内で軌道ごとに名が重複しないこと。
    /// </summary>
    public class MythicPlanetNamesTests
    {
        [Test]
        public void Pool_IsNonEmpty_AndUnique()
        {
            Assert.Greater(MythicPlanetNames.Count, 0);
            var seen = new HashSet<string>();
            foreach (var n in MythicPlanetNames.Names)
            {
                Assert.IsFalse(string.IsNullOrEmpty(n));
                Assert.IsTrue(seen.Add(n), $"重複した惑星名: {n}");
            }
        }

        [Test]
        public void At_Wraps()
        {
            int c = MythicPlanetNames.Count;
            Assert.AreEqual(MythicPlanetNames.At(0), MythicPlanetNames.At(c));
            Assert.AreEqual(MythicPlanetNames.At(c - 1), MythicPlanetNames.At(-1));
        }

        [Test]
        public void For_IsDeterministic()
        {
            Assert.AreEqual(MythicPlanetNames.For(7, 2), MythicPlanetNames.For(7, 2));
            Assert.AreEqual(MythicPlanetNames.For(123, 0), MythicPlanetNames.For(123, 0));
        }

        [Test]
        public void For_DistinctWithinSameSystem()
        {
            const int sys = 42;
            int n = System.Math.Min(MythicPlanetNames.Count, 8);
            var seen = new HashSet<string>();
            for (int i = 0; i < n; i++)
                Assert.IsTrue(seen.Add(MythicPlanetNames.For(sys, i)),
                    $"同一星系で惑星名が重複: 軌道 {i}");
        }
    }
}
