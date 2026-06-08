using NUnit.Framework;
using UnityEngine;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 二層遷移（C-2 #586）の交戦固着ロジックを固定する：
    /// 接触した敵対艦隊は engaged になり回廊上で前進を止める（交戦中の回廊）。
    /// 潜行先の特定（TryGetEngagementOnCorridor）と、決着で固着解除（ApplyBattleResult）まで。
    /// </summary>
    public class EngagementTests
    {
        // 0(帝国) —— 1(帝国)：len4 の直線回廊（所有は同勢力＝前線でない＝亜光速にならない素直なケース）
        private GalaxyMap LineMap()
        {
            var m = new GalaxyMap();
            m.AddSystem(new StarSystem(0, "A", Vector2.zero, Faction.帝国));
            m.AddSystem(new StarSystem(1, "B", Vector2.right, Faction.帝国));
            m.AddCorridor(new Corridor(0, 1, 4f));
            return m;
        }

        // 同一回廊で正面から接触する敵対2艦（中間で交差）
        private StrategicFleetRegistry CollidingPair(GalaxyMap m, out StrategicFleet imp, out StrategicFleet ally)
        {
            var reg = new StrategicFleetRegistry(m);
            imp = new StrategicFleet(1, 0, Faction.帝国) { strength = 300 }; imp.BeginWarp(m, 1);  // 0→1
            ally = new StrategicFleet(2, 1, Faction.同盟) { strength = 200 }; ally.BeginWarp(m, 0); // 1→0
            reg.Add(imp); reg.Add(ally);
            return reg;
        }

        [Test]
        public void EngagedFleet_DoesNotAdvanceOnTick()
        {
            var m = LineMap();
            var f = new StrategicFleet(1, 0, Faction.帝国); f.BeginWarp(m, 1);
            f.Tick(1f);
            float p = f.Progress;
            Assert.Greater(p, 0f);

            f.engaged = true;
            f.Tick(2f);
            Assert.AreEqual(p, f.Progress, 1e-4f); // 交戦固着中は進まない
        }

        [Test]
        public void BeginEngagements_FreezesCollidedHostilePair()
        {
            var m = LineMap();
            var reg = CollidingPair(m, out var imp, out var ally);

            // 入った直後は未接触＝固着しない
            Assert.AreEqual(0, StrategyRules.BeginEngagements(reg));
            Assert.IsFalse(imp.engaged);

            // 中間まで進めて接触させる（len4・speed1 → 双方 progress 0.5 で交差）
            reg.Tick(2f);
            Assert.AreEqual(1, StrategyRules.BeginEngagements(reg));
            Assert.IsTrue(imp.engaged);
            Assert.IsTrue(ally.engaged);

            // 固着後はさらに Tick しても前進しない＝回廊上に留まり続ける
            float pi = imp.Progress;
            reg.Tick(5f);
            Assert.AreEqual(pi, imp.Progress, 1e-4f);

            // 冪等：再度呼んでも新規固着は0
            Assert.AreEqual(0, StrategyRules.BeginEngagements(reg));
        }

        [Test]
        public void TryGetEngagementOnCorridor_FindsCollidedPair()
        {
            var m = LineMap();
            var reg = CollidingPair(m, out var imp, out var ally);
            reg.Tick(2f); // 接触

            Assert.IsTrue(StrategyRules.TryGetEngagementOnCorridor(reg, 0, 1, out var a, out var b));
            Assert.IsNotNull(a); Assert.IsNotNull(b);
            // 向きは無向（1,0 でも見つかる）
            Assert.IsTrue(StrategyRules.TryGetEngagementOnCorridor(reg, 1, 0, out _, out _));
            // 無関係な回廊では見つからない
            Assert.IsFalse(StrategyRules.TryGetEngagementOnCorridor(reg, 0, 99, out _, out _));
        }

        [Test]
        public void ApplyBattleResult_ClearsEngagedOnWinner()
        {
            var m = LineMap();
            var reg = CollidingPair(m, out var imp, out var ally);
            reg.Tick(2f);
            StrategyRules.BeginEngagements(reg);
            Assert.IsTrue(imp.engaged);

            // 抽象決着＝勝者の固着が解けて前進を再開できる
            Assert.AreEqual(1, StrategyRules.ResolveEncounters(reg));
            Assert.IsNull(reg.GetFleet(2));     // 敗者除去
            Assert.IsFalse(imp.engaged);        // 勝者は固着解除
            Assert.IsTrue(imp.IsMoving);        // 前進を再開
        }
    }
}
