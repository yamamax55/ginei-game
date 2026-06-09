using System.Collections.Generic;
using UnityEngine;
using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 物流・版図の一体化（地政学 GEO-3 #844）を固定する：連結した版図は一体化度1、
    /// 敵に分断された／散在する版図は一体化度が下がる（所有星系のみを通って数える）。
    /// </summary>
    public class LogisticsRulesTests
    {
        private static StarSystem Sys(int id, Faction owner) => new StarSystem(id, "S" + id, Vector2.zero, owner);

        [Test]
        public void FullyConnectedTerritory_CohesionOne()
        {
            var m = new GalaxyMap();
            m.AddSystem(Sys(0, Faction.帝国));
            m.AddSystem(Sys(1, Faction.帝国));
            m.AddSystem(Sys(2, Faction.帝国));
            m.AddSystem(Sys(3, Faction.帝国));
            m.AddCorridor(new Corridor(0, 1, 1f));
            m.AddCorridor(new Corridor(1, 2, 1f));
            m.AddCorridor(new Corridor(2, 3, 1f));

            Assert.AreEqual(4, LogisticsRules.LargestConnectedComponent(m, new List<int> { 0, 1, 2, 3 }));
            Assert.AreEqual(1f, LogisticsRules.CohesionFactor(m, Faction.帝国), 1e-4f);
        }

        [Test]
        public void IsolatedSystem_LowersCohesion()
        {
            var m = new GalaxyMap();
            m.AddSystem(Sys(0, Faction.帝国));
            m.AddSystem(Sys(1, Faction.帝国));
            m.AddSystem(Sys(2, Faction.帝国));
            m.AddSystem(Sys(3, Faction.帝国)); // 孤立（回廊なし）
            m.AddCorridor(new Corridor(0, 1, 1f));
            m.AddCorridor(new Corridor(1, 2, 1f));

            // 最大連結成分 {0,1,2}=3 / 所有4 = 0.75
            Assert.AreEqual(3, LogisticsRules.LargestConnectedComponent(m, new List<int> { 0, 1, 2, 3 }));
            Assert.AreEqual(0.75f, LogisticsRules.CohesionFactor(m, Faction.帝国), 1e-4f);
        }

        [Test]
        public void EnemySystemInBetween_FragmentsTerritory()
        {
            // 帝国 {0,1} と {3,4} の間に同盟 2 が挟まる。回廊は一直線 0-1-2-3-4。
            var m = new GalaxyMap();
            m.AddSystem(Sys(0, Faction.帝国));
            m.AddSystem(Sys(1, Faction.帝国));
            m.AddSystem(Sys(2, Faction.同盟)); // 敵の楔
            m.AddSystem(Sys(3, Faction.帝国));
            m.AddSystem(Sys(4, Faction.帝国));
            m.AddCorridor(new Corridor(0, 1, 1f));
            m.AddCorridor(new Corridor(1, 2, 1f));
            m.AddCorridor(new Corridor(2, 3, 1f));
            m.AddCorridor(new Corridor(3, 4, 1f));

            // 帝国は {0,1} と {3,4} に分断（2 を通れない）＝最大2 / 所有4 = 0.5
            Assert.AreEqual(0.5f, LogisticsRules.CohesionFactor(m, Faction.帝国), 1e-4f);
            Assert.AreEqual(1f, LogisticsRules.CohesionFactor(m, Faction.同盟), 1e-4f); // 同盟は1星系で完全連結
        }

        [Test]
        public void EmptyOwnership_CohesionZero()
        {
            var m = new GalaxyMap();
            m.AddSystem(Sys(0, Faction.帝国));
            Assert.AreEqual(0f, LogisticsRules.CohesionFactor(m, Faction.同盟), 1e-4f); // 同盟は所有0
        }
    }
}
