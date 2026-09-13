using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>固定会戦QAのプリセット（明細の明示・固定ID順・AIの使い方の導出・矛盾検出）。</summary>
    public class BattleQaPresetCatalogTests
    {
        private static BattleQaFleetSpec Spec(int id, BattleQaCommandRole role, string corps, bool ai = true,
            Faction faction = Faction.同盟)
            => new BattleQaFleetSpec(id, "f" + id, faction, corps, role, 100, 100, "a" + id, 50, 50, 100f,
                Vector2.zero, 0f, Formation.紡錘陣, ai);

        [Test]
        public void AllPresets_AreValid()
        {
            foreach (BattleQaPresetKind k in System.Enum.GetValues(typeof(BattleQaPresetKind)))
            {
                BattleQaPreset p = BattleQaPresetCatalog.Create(k, BattleQaPresetCatalog.DefaultSeed);
                Assert.AreEqual(k, p.kind);
                CollectionAssert.IsEmpty(p.Validate(), k + " のプリセットに矛盾がある");
            }
        }

        [Test]
        public void SameKindAndSeed_ProduceIdenticalSpecs()
        {
            foreach (BattleQaPresetKind k in System.Enum.GetValues(typeof(BattleQaPresetKind)))
            {
                BattleQaPreset a = BattleQaPresetCatalog.Create(k, 11);
                BattleQaPreset b = BattleQaPresetCatalog.Create(k, 11);
                Assert.AreEqual(a.Fleets.Count, b.Fleets.Count);
                for (int i = 0; i < a.Fleets.Count; i++)
                {
                    BattleQaFleetSpec x = a.Fleets[i], y = b.Fleets[i];
                    Assert.AreEqual(x.fleetId, y.fleetId);
                    Assert.AreEqual(x.corpsName, y.corpsName);
                    Assert.AreEqual(x.role, y.role);
                    Assert.AreEqual(x.shipCount, y.shipCount);
                    Assert.AreEqual(x.maxShipCount, y.maxShipCount);
                    Assert.AreEqual(x.initialMorale, y.initialMorale);
                    Assert.AreEqual(x.position, y.position);
                    Assert.AreEqual(x.headingDeg, y.headingDeg);
                    Assert.AreEqual(x.aiEnabled, y.aiEnabled);
                }
            }
        }

        [Test]
        public void Seed_IsCarriedAsGiven()
        {
            Assert.AreEqual(BattleQaPresetCatalog.DefaultSeed, BattleQaPresetCatalog.Retreat(BattleQaPresetCatalog.DefaultSeed).seed);
            Assert.AreEqual(BattleQaPresetCatalog.AlternateSeed, BattleQaPresetCatalog.Retreat(BattleQaPresetCatalog.AlternateSeed).seed);
            Assert.AreNotEqual(BattleQaPresetCatalog.DefaultSeed, BattleQaPresetCatalog.AlternateSeed);
        }

        [Test]
        public void Fleets_AreSortedById_RegardlessOfInputOrder()
        {
            var p = new BattleQaPreset(BattleQaPresetKind.退却, "x", 1, 1f, false, "",
                new List<BattleQaFleetSpec> { Spec(9, BattleQaCommandRole.独立, ""), Spec(2, BattleQaCommandRole.独立, ""), Spec(5, BattleQaCommandRole.敵, "", true, Faction.帝国) });
            Assert.AreEqual(2, p.Fleets[0].fleetId);
            Assert.AreEqual(5, p.Fleets[1].fleetId);
            Assert.AreEqual(9, p.Fleets[2].fleetId);
        }

        [Test]
        public void AiMode_IsDerivedFromFleets()
        {
            Assert.AreEqual(BattleQaAiMode.通常AI, BattleQaPresetCatalog.Retreat(1).AiMode);
            Assert.AreEqual(BattleQaAiMode.AI停止, BattleQaPresetCatalog.MoraleLock(1).AiMode);
            Assert.AreEqual(BattleQaAiMode.混在, BattleQaPresetCatalog.FormationChange(1).AiMode);
            Assert.AreEqual(BattleQaAiMode.AI停止,
                new BattleQaPreset(BattleQaPresetKind.退却, "空", 1, 1f, false, "", null).AiMode, "0件は AI停止");
        }

        [Test]
        public void Retreat_ShipRatio_SeparatesCorpsRetreatFromIndividualRetreat()
        {
            BattleQaPreset p = BattleQaPresetCatalog.Retreat(1);
            float corpsThreshold = CorpsRetreatRules.RetreatThreshold(BattleQaPresetCatalog.NeutralStat, BattleQaPresetCatalog.NeutralStat);
            const float individualRetreatRatio = 0.3f;   // FleetAI.retreatRatio の既定
            int members = 0, bystanders = 0;
            for (int i = 0; i < p.Fleets.Count; i++)
            {
                BattleQaFleetSpec f = p.Fleets[i];
                if (f.corpsName == BattleQaPresetCatalog.RetreatCorps)
                {
                    members++;
                    Assert.Less(f.ShipRatio, corpsThreshold, "軍団総退却のしきい値を下回ること");
                    Assert.GreaterOrEqual(f.ShipRatio, individualRetreatRatio, "個艦撤退比は上回ること（原因の切り分け）");
                }
                else if (f.role != BattleQaCommandRole.敵)
                {
                    bystanders++;
                    Assert.AreEqual(1f, f.ShipRatio, 1e-4f, "所属外は無傷");
                }
            }
            Assert.AreEqual(3, members);
            Assert.AreEqual(3, bystanders, "独立1＋別軍団2");
        }

        [Test]
        public void MoraleLock_ShootersAreInRange_AndLanesDoNotInteract()
        {
            BattleQaPreset p = BattleQaPresetCatalog.MoraleLock(1);
            Assert.IsTrue(p.TryGetFleet(21, out BattleQaFleetSpec a));
            Assert.IsTrue(p.TryGetFleet(31, out BattleQaFleetSpec sa));
            Assert.IsTrue(p.TryGetFleet(32, out BattleQaFleetSpec sb));
            const float weaponRange = 10f;   // WeaponArc.range の既定
            Assert.Less(Vector2.Distance(a.position, sa.position), weaponRange);
            Assert.Greater(Vector2.Distance(a.position, sb.position), weaponRange, "隣の組の撃ち手は射程外");
            // 撃ち手は標的の方を向き、標的は背を向ける（撃ち返さない＝撃ち手が敗走して被弾が途切れない）。
            Assert.Greater(Vector2.Dot(sa.Forward, (a.position - sa.position).normalized), 0.99f);
            Assert.Less(Vector2.Dot(a.Forward, (sa.position - a.position).normalized), -0.99f);
        }

        [Test]
        public void FormationPreset_CorpsAiWouldRecommendDifferentFromHeldFormation()
        {
            BattleQaPreset p = BattleQaPresetCatalog.FormationChange(1);
            float own = 0f, enemy = 0f;
            for (int i = 0; i < p.Fleets.Count; i++)
            {
                if (p.Fleets[i].corpsName == BattleQaPresetCatalog.FormationCorps) own += p.Fleets[i].shipCount;
                else enemy += p.Fleets[i].shipCount;
            }
            Assert.AreNotEqual(Formation.円陣, FormationDoctrineRules.RecommendFormation(own, enemy, false, null),
                "軍団AIの推奨が保持陣形（円陣）と同じだと保持の試験が空振りになる");
        }

        [Test]
        public void Validate_DetectsContradictions()
        {
            var dup = new BattleQaPreset(BattleQaPresetKind.退却, "x", 1, 1f, false, "",
                new List<BattleQaFleetSpec> { Spec(1, BattleQaCommandRole.独立, ""), Spec(1, BattleQaCommandRole.独立, "") });
            Assert.IsTrue(dup.Validate().Exists(e => e.Contains("重複")));

            var noCommander = new BattleQaPreset(BattleQaPresetKind.退却, "x", 1, 1f, false, "",
                new List<BattleQaFleetSpec> { Spec(1, BattleQaCommandRole.隷下, "A") });
            Assert.IsTrue(noCommander.Validate().Exists(e => e.Contains("軍団長のいない")));

            var corpslessSub = new BattleQaPreset(BattleQaPresetKind.退却, "x", 1, 1f, false, "",
                new List<BattleQaFleetSpec> { Spec(1, BattleQaCommandRole.隷下, "") });
            Assert.IsTrue(corpslessSub.Validate().Exists(e => e.Contains("軍団名のない")));

            var enemyInCorps = new BattleQaPreset(BattleQaPresetKind.退却, "x", 1, 1f, false, "",
                new List<BattleQaFleetSpec> { Spec(1, BattleQaCommandRole.敵, "A", true, Faction.帝国) });
            Assert.IsTrue(enemyInCorps.Validate().Exists(e => e.Contains("軍団所属あり")));

            var twoCommanders = new BattleQaPreset(BattleQaPresetKind.退却, "x", 1, 1f, false, "",
                new List<BattleQaFleetSpec> { Spec(1, BattleQaCommandRole.軍団長, "A"), Spec(2, BattleQaCommandRole.軍団長, "A") });
            Assert.IsTrue(twoCommanders.Validate().Exists(e => e.Contains("複数")));

            var empty = new BattleQaPreset(BattleQaPresetKind.退却, "x", 1, 1f, false, "", null);
            Assert.IsTrue(empty.Validate().Exists(e => e.Contains("0件")));
        }

        [Test]
        public void Spec_ClampsInputs_AndForwardMatchesTransformUp()
        {
            var s = new BattleQaFleetSpec(1, null, Faction.同盟, null, BattleQaCommandRole.独立, 0, -5, null, 150, -3, -1f,
                Vector2.zero, 90f, Formation.紡錘陣, false);
            Assert.AreEqual(1, s.shipCount);
            Assert.AreEqual(1, s.maxShipCount, "定数は現在数以上");
            Assert.AreEqual(100, s.leadership);
            Assert.AreEqual(0, s.ambition);
            Assert.AreEqual(0f, s.initialMorale);
            Assert.AreEqual("", s.corpsName);
            // 90度（反時計回り）＝ -X を向く（Transform.up と同じ規約）。
            Assert.AreEqual(-1f, s.Forward.x, 1e-4f);
            Assert.AreEqual(0f, s.Forward.y, 1e-4f);
        }
    }
}
