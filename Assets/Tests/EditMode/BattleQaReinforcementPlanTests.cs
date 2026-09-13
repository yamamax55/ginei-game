using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>固定会戦QAのQA所有の時限援軍の明細（SPEED-08）：陣営・軍団をプリセットの味方へ合わせる・固定IDの重なり・不正値の拒否・記録。</summary>
    public class BattleQaReinforcementPlanTests
    {
        private static BattleQaPreset EnemyOnlyPreset()
        {
            var f = new List<BattleQaFleetSpec>
            {
                new BattleQaFleetSpec(11, "敵のみ", Faction.帝国, "", BattleQaCommandRole.敵,
                    100, 100, "QA敵のみ", 50, 50, 100f, Vector2.zero, 0f, Formation.紡錘陣, false),
            };
            return new BattleQaPreset(BattleQaPresetKind.退却, "敵のみ", 1, 1f, false, "", f);
        }

        [Test]
        public void ForPreset_UsesAlliedFactionAndFirstAlliedCorps_WithDefaults()
        {
            BattleQaPreset formation = BattleQaPresetCatalog.FormationChange(BattleQaPresetCatalog.DefaultSeed);
            BattleQaReinforcementPlan p = BattleQaReinforcementPlan.ForPreset(formation, BattleQaReinforcementPlan.DefaultArrivalSeconds);
            Assert.AreEqual(90, p.fleetId);
            Assert.AreEqual(Faction.同盟, p.faction);
            Assert.AreEqual(BattleQaPresetCatalog.FormationCorps, p.corpsName);
            Assert.AreEqual(1000, p.shipCount);
            Assert.AreEqual(2f, p.arrivalSeconds, 1e-4f);
            Assert.AreEqual(20f, p.edgeRadius, 1e-4f);
            Assert.AreEqual(-10f, p.spawnY, 1e-4f);
            CollectionAssert.IsEmpty(p.Validate(formation));

            // 不退転は味方が軍団なし＝援軍も軍団なし。
            BattleQaPreset lockPreset = BattleQaPresetCatalog.MoraleLock(1);
            BattleQaReinforcementPlan q = BattleQaReinforcementPlan.ForPreset(lockPreset, 3f);
            Assert.AreEqual("", q.corpsName);
            Assert.AreEqual(3f, q.arrivalSeconds, 1e-4f);
            CollectionAssert.IsEmpty(q.Validate(lockPreset));

            // 全プリセットで既定の固定IDは重ならない。
            foreach (BattleQaPresetKind k in new[] { BattleQaPresetKind.退却, BattleQaPresetKind.不退転, BattleQaPresetKind.陣形変更 })
            {
                BattleQaPreset preset = BattleQaPresetCatalog.Create(k, 1);
                CollectionAssert.IsEmpty(BattleQaReinforcementPlan.ForPreset(preset, 2f).Validate(preset), k + " で既定の明細が不整合");
            }
        }

        [Test]
        public void TryResolveAlliedFaction_SkipsEnemies_AndFailsWithoutAllies()
        {
            Assert.IsTrue(BattleQaReinforcementPlan.TryResolveAlliedFaction(BattleQaPresetCatalog.Retreat(1), out Faction f));
            Assert.AreEqual(Faction.同盟, f);
            Assert.IsFalse(BattleQaReinforcementPlan.TryResolveAlliedFaction(EnemyOnlyPreset(), out _));
            Assert.IsFalse(BattleQaReinforcementPlan.TryResolveAlliedFaction(null, out _));
        }

        [Test]
        public void Validate_RejectsOverlapBadValuesWrongFactionAndUnknownCorps()
        {
            BattleQaPreset preset = BattleQaPresetCatalog.FormationChange(1);
            var overlap = new BattleQaReinforcementPlan(1, Faction.同盟, "", 1000, 2f, 20f, -10f, Formation.紡錘陣);
            StringAssert.Contains("重なる", string.Join("／", overlap.Validate(preset)));

            var zeroArrival = new BattleQaReinforcementPlan(90, Faction.同盟, "", 1000, 0f, 20f, -10f, Formation.紡錘陣);
            StringAssert.Contains("到着時刻", string.Join("／", zeroArrival.Validate(preset)), "0秒は製品側で在場扱い＝時限増援にならない");
            var nanArrival = new BattleQaReinforcementPlan(90, Faction.同盟, "", 1000, float.NaN, 20f, -10f, Formation.紡錘陣);
            StringAssert.Contains("到着時刻", string.Join("／", nanArrival.Validate(preset)));
            var noShips = new BattleQaReinforcementPlan(90, Faction.同盟, "", 0, 2f, 20f, -10f, Formation.紡錘陣);
            StringAssert.Contains("艦艇数", string.Join("／", noShips.Validate(preset)));
            var badRadius = new BattleQaReinforcementPlan(90, Faction.同盟, "", 1000, 2f, 0f, -10f, Formation.紡錘陣);
            StringAssert.Contains("出現半径", string.Join("／", badRadius.Validate(preset)));

            var wrongFaction = new BattleQaReinforcementPlan(90, Faction.帝国, "", 1000, 2f, 20f, -10f, Formation.紡錘陣);
            StringAssert.Contains("味方", string.Join("／", wrongFaction.Validate(preset)));
            var unknownCorps = new BattleQaReinforcementPlan(90, Faction.同盟, "存在しない軍団", 1000, 2f, 20f, -10f, Formation.紡錘陣);
            StringAssert.Contains("存在しない軍団", string.Join("／", unknownCorps.Validate(preset)));

            StringAssert.Contains("味方の艦隊が無い", string.Join("／",
                BattleQaReinforcementPlan.ForPreset(EnemyOnlyPreset(), 2f).Validate(EnemyOnlyPreset())));
            CollectionAssert.IsNotEmpty(BattleQaReinforcementPlan.ForPreset(preset, 2f).Validate(null));
        }

        [Test]
        public void Describe_RecordsIdCorpsShipsArrivalAndSeed()
        {
            BattleQaPreset preset = BattleQaPresetCatalog.FormationChange(BattleQaPresetCatalog.DefaultSeed);
            string s = BattleQaReinforcementPlan.ForPreset(preset, 2f).Describe(preset.seed);
            StringAssert.Contains("固定ID=90", s);
            StringAssert.Contains("軍団=" + BattleQaPresetCatalog.FormationCorps, s);
            StringAssert.Contains("艦艇数=1000", s);
            StringAssert.Contains("到着=開始から 2 ゲーム秒", s);
            StringAssert.Contains("seed=" + BattleQaPresetCatalog.DefaultSeed, s);
            StringAssert.Contains("軍団=(なし)", BattleQaReinforcementPlan.ForPreset(BattleQaPresetCatalog.MoraleLock(1), 2f).Describe(1));
        }
    }
}
