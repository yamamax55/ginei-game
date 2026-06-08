using NUnit.Framework;
using UnityEngine;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 前線回廊（自勢力↔敵対勢力をつなぐ回廊）の挙動を固定する（C-1/C-3 #34/#36）。
    /// 前線は FTL 不可だが**亜光速（遅い）で進入できる**。FTL専用ルーティングは前線を回避できる。
    /// </summary>
    public class FtlBlockTests
    {
        // 0(帝国) —(2,友軍FTL)— 1(帝国) 、0 —(2,前線)— 2(同盟)。同距離で速度比較。
        private GalaxyMap MakeMap()
        {
            var m = new GalaxyMap();
            m.AddSystem(new StarSystem(0, "A", Vector2.zero, Faction.帝国));
            m.AddSystem(new StarSystem(1, "B", Vector2.right, Faction.帝国));
            m.AddSystem(new StarSystem(2, "C", Vector2.up, Faction.同盟));
            m.AddCorridor(new Corridor(0, 1, 2f)); // 帝国-帝国 友軍
            m.AddCorridor(new Corridor(0, 2, 2f)); // 帝国-同盟 前線
            return m;
        }

        [Test]
        public void IsFtlBlocked_Frontline_True_Friendly_False()
        {
            var m = MakeMap();
            Assert.IsTrue(StrategyRules.IsFtlBlocked(m, m.GetCorridor(0, 2)));
            Assert.IsFalse(StrategyRules.IsFtlBlocked(m, m.GetCorridor(0, 1)));
        }

        [Test]
        public void BeginWarp_AcrossFrontline_Succeeds_AtSublight()
        {
            var m = MakeMap();
            var ftl = new StrategicFleet(1, 0, Faction.帝国, 1f);
            Assert.IsTrue(ftl.BeginWarp(m, 1));  // 友軍 len2 / speed1 → ETA 2
            Assert.IsFalse(ftl.IsSublight);

            var sub = new StrategicFleet(2, 0, Faction.帝国, 1f) { sublightFactor = 0.5f };
            Assert.IsTrue(sub.BeginWarp(m, 2));  // 前線も進入可（亜光速）len2 / 0.5 → ETA 4
            Assert.IsTrue(sub.IsSublight);
            Assert.Greater(sub.Eta, ftl.Eta);    // 亜光速＝同距離でも遅い
        }

        [Test]
        public void WarpTo_CanCrossFrontline_Sublight()
        {
            var m = MakeMap();
            var f = new StrategicFleet(1, 0, Faction.帝国);
            Assert.IsTrue(f.WarpTo(m, 2));       // 前線越しでも到達可（亜光速）
            Assert.IsTrue(f.IsMoving);
            Assert.IsTrue(f.IsSublight);
        }

        [Test]
        public void FindPath_AvoidFtlBlocked_AvoidsFrontline_WhenAsked()
        {
            var m = MakeMap();
            // FTL専用ルーティング（任意）：0→2 は前線のみ → avoid=true で到達不能。
            Assert.AreEqual(0, GalaxyPathfinder.FindPath(m, 0, 2, avoidFtlBlocked: true).Count);
            // 既定（avoid=false）は前線を直行できる。
            CollectionAssert.AreEqual(new[] { 0, 2 }, GalaxyPathfinder.FindPath(m, 0, 2));
        }
    }
}
