using NUnit.Framework;

namespace Ginei.Tests
{
    /// <summary>
    /// #戦闘ドクトリン Stage4：撤退して生き延びた敗者を盤面に残す ApplyBattleResult のオーバーロード検証。
    /// 既存の3引数版（敗者除去）は StrategyRulesTests が担保。ここは loserSurvivor>0 の保存挙動に特化。
    /// </summary>
    public class DefeatPenaltyRetreatTests
    {
        private static GalaxyMap MakeMap()
        {
            var m = new GalaxyMap();
            m.AddSystem(new StarSystem(0, "A", UnityEngine.Vector2.zero, Faction.帝国));
            m.AddSystem(new StarSystem(1, "B", UnityEngine.Vector2.right, Faction.帝国));
            m.AddCorridor(new Corridor(0, 1, 4f));
            return m;
        }

        [Test]
        public void 撤退して生き延びた敗者は除去されず盤面に残る()
        {
            var m = MakeMap();
            var a = new StrategicFleet(1, 0, Faction.帝国); a.strength = 150; a.engaged = true;
            var b = new StrategicFleet(2, 1, Faction.同盟); b.strength = 100; b.engaged = true;
            var reg = new StrategicFleetRegistry(m); reg.Add(a); reg.Add(b);

            // a 勝者・残存50。敗者 b は生存40で原隊へ帰す（除去しない）。
            StrategyRules.ApplyBattleResult(reg, a, b, new CorridorBattleResult(true, 50), 40);

            Assert.AreEqual(50, a.strength);
            Assert.IsFalse(a.engaged);
            Assert.IsNotNull(reg.GetFleet(2), "撤退生存の敗者は盤面に残る");
            Assert.IsFalse(b.engaged, "敗者は交戦固着が解除される");
            Assert.GreaterOrEqual(b.strength, 1, "敗者は最低1で原隊へ帰る（全滅しない）");
            Assert.LessOrEqual(b.strength, 40, "撤退損耗で兵力は目減りする");
        }

        [Test]
        public void 生存兵力ゼロ指定なら従来どおり敗者を除去する()
        {
            var m = MakeMap();
            var a = new StrategicFleet(1, 0, Faction.帝国); a.strength = 150;
            var b = new StrategicFleet(2, 1, Faction.同盟); b.strength = 100;
            var reg = new StrategicFleetRegistry(m); reg.Add(a); reg.Add(b);

            StrategyRules.ApplyBattleResult(reg, a, b, new CorridorBattleResult(true, 50), 0);
            Assert.IsNotNull(reg.GetFleet(1));
            Assert.IsNull(reg.GetFleet(2), "loserSurvivor=0 は従来どおり敗者除去（後方互換）");
        }
    }
}
