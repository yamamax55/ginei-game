using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>固定会戦QAの初期スナップショット（2回初期化の一致・seed の区別・明細との照合・許容誤差）。</summary>
    public class BattleQaSnapshotTests
    {
        private static BattleQaFleetSnapshot Entry(int id, Vector2 pos, float morale = 100f, float heading = 0f,
            int ships = 100, Formation formation = Formation.紡錘陣)
            => new BattleQaFleetSnapshot(id, "A", id == 1, ships, 100, morale, pos, heading, formation, true, Faction.同盟);

        /// <summary>プリセットの明細をそのまま写したスナップショット（盤面が明細どおりだった場合の相当物）。</summary>
        private static BattleQaSnapshot FromPreset(BattleQaPreset p)
        {
            var list = new List<BattleQaFleetSnapshot>();
            for (int i = 0; i < p.Fleets.Count; i++)
            {
                BattleQaFleetSpec f = p.Fleets[i];
                list.Add(new BattleQaFleetSnapshot(f.fleetId, f.corpsName, f.role == BattleQaCommandRole.軍団長,
                    f.shipCount, f.maxShipCount, f.initialMorale, f.position, f.headingDeg, f.formation, f.aiEnabled, f.faction));
            }
            return new BattleQaSnapshot(p.name, p.seed, list);
        }

        [Test]
        public void SamePresetTwice_NoDifferences()
        {
            foreach (BattleQaPresetKind k in System.Enum.GetValues(typeof(BattleQaPresetKind)))
            {
                BattleQaSnapshot a = FromPreset(BattleQaPresetCatalog.Create(k, 5));
                BattleQaSnapshot b = FromPreset(BattleQaPresetCatalog.Create(k, 5));
                CollectionAssert.IsEmpty(BattleQaSnapshot.Compare(a, b, BattleQaTolerance.Default), k.ToString());
            }
        }

        [Test]
        public void DifferentSeed_IsReportedAsDifference()
        {
            BattleQaSnapshot a = FromPreset(BattleQaPresetCatalog.Retreat(BattleQaPresetCatalog.DefaultSeed));
            BattleQaSnapshot b = FromPreset(BattleQaPresetCatalog.Retreat(BattleQaPresetCatalog.AlternateSeed));
            List<string> d = BattleQaSnapshot.Compare(a, b, BattleQaTolerance.Default);
            Assert.AreEqual(1, d.Count, "初期配置は同じで seed だけが違う");
            StringAssert.StartsWith("seed", d[0]);
        }

        [Test]
        public void EntryOrder_DoesNotMatter()
        {
            var a = new BattleQaSnapshot("p", 1, new List<BattleQaFleetSnapshot> { Entry(1, Vector2.zero), Entry(2, Vector2.one) });
            var b = new BattleQaSnapshot("p", 1, new List<BattleQaFleetSnapshot> { Entry(2, Vector2.one), Entry(1, Vector2.zero) });
            CollectionAssert.IsEmpty(BattleQaSnapshot.Compare(a, b, BattleQaTolerance.Default));
            Assert.AreEqual(1, b.Fleets[0].fleetId);
        }

        [Test]
        public void WithinTolerance_IsEqual_BeyondIsDifferent()
        {
            var tol = new BattleQaTolerance(0.1f, 1f, 0.5f);
            var a = new BattleQaSnapshot("p", 1, new List<BattleQaFleetSnapshot> { Entry(1, Vector2.zero, 100f, 0f) });
            var near = new BattleQaSnapshot("p", 1, new List<BattleQaFleetSnapshot> { Entry(1, new Vector2(0.05f, 0f), 100.4f, 0.9f) });
            var far = new BattleQaSnapshot("p", 1, new List<BattleQaFleetSnapshot> { Entry(1, new Vector2(0.2f, 0f), 101f, 2f) });
            CollectionAssert.IsEmpty(BattleQaSnapshot.Compare(a, near, tol));
            Assert.AreEqual(3, BattleQaSnapshot.Compare(a, far, tol).Count, "位置・士気・向きの3件");
        }

        [Test]
        public void DiscreteFields_AreComparedExactly()
        {
            var a = new BattleQaSnapshot("p", 1, new List<BattleQaFleetSnapshot> { Entry(1, Vector2.zero, 100f, 0f, 100, Formation.紡錘陣) });
            var b = new BattleQaSnapshot("p", 1, new List<BattleQaFleetSnapshot> { Entry(1, Vector2.zero, 100f, 0f, 99, Formation.円陣) });
            Assert.AreEqual(2, BattleQaSnapshot.Compare(a, b, BattleQaTolerance.Default).Count, "艦艇数と陣形");
        }

        [Test]
        public void HeadingWrap_360And0_AreEqual()
        {
            Assert.AreEqual(0f, BattleQaSnapshot.AngleDelta(360f, 0f), 1e-4f);
            Assert.AreEqual(2f, BattleQaSnapshot.AngleDelta(359f, 1f), 1e-4f);
            Assert.AreEqual(180f, BattleQaSnapshot.AngleDelta(180f, 0f), 1e-4f);
            Assert.AreEqual(0f, BattleQaSnapshot.AngleDelta(-180f, 180f), 1e-4f);
        }

        [Test]
        public void CountMismatch_AndNull_AreReported()
        {
            var a = new BattleQaSnapshot("p", 1, new List<BattleQaFleetSnapshot> { Entry(1, Vector2.zero) });
            var b = new BattleQaSnapshot("p", 1, new List<BattleQaFleetSnapshot>());
            Assert.AreEqual(1, BattleQaSnapshot.Compare(a, b, BattleQaTolerance.Default).Count);
            Assert.AreEqual(1, BattleQaSnapshot.Compare(a, null, BattleQaTolerance.Default).Count);
            Assert.AreEqual(1, BattleQaSnapshot.CompareWithPreset(null, a, BattleQaTolerance.Default).Count);
        }

        [Test]
        public void CompareWithPreset_MatchesWhenBoardFollowsSpec_AndCatchesDrift()
        {
            BattleQaPreset p = BattleQaPresetCatalog.MoraleLock(3);
            BattleQaSnapshot ok = FromPreset(p);
            CollectionAssert.IsEmpty(BattleQaSnapshot.CompareWithPreset(p, ok, BattleQaTolerance.Default));

            // 士気が初期化で上書きされた盤面（明細 30 → 実測 100）は準備失敗にすべき差。
            var drift = new List<BattleQaFleetSnapshot>();
            for (int i = 0; i < ok.Fleets.Count; i++)
            {
                BattleQaFleetSnapshot s = ok.Fleets[i];
                drift.Add(new BattleQaFleetSnapshot(s.fleetId, s.corpsName, s.isCorpsCommander, s.shipCount, s.maxShipCount,
                    s.fleetId == 21 ? 100f : s.morale, s.position, s.headingDeg, s.formation, s.aiEnabled, s.faction));
            }
            List<string> d = BattleQaSnapshot.CompareWithPreset(p, new BattleQaSnapshot(p.name, p.seed, drift), BattleQaTolerance.Default);
            Assert.AreEqual(1, d.Count);
            StringAssert.Contains("艦隊21 士気", d[0]);
        }

        [Test]
        public void Describe_ListsEveryFleetOnce()
        {
            BattleQaSnapshot s = FromPreset(BattleQaPresetCatalog.Retreat(1));
            string text = s.Describe();
            StringAssert.Contains("seed=1", text);
            StringAssert.Contains("艦艇数=", text);
            StringAssert.DoesNotContain("兵力", text, "UI表記は艦艇数");
            int lines = text.Split('\n').Length - 2;   // 見出し1行＋末尾改行
            Assert.AreEqual(s.Fleets.Count, lines);
        }
    }
}
