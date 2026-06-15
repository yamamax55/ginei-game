using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 人物二者関係グラフ（TKO-3・<see cref="PersonRelationRules"/>/<see cref="PersonRelationGraph"/>・#2480）を固定する：
    /// 学閥/同期からのエッジ敷設（<see cref="CareerPipelineRules.CliqueBond"/> 流用）、指揮系統/師弟の双方向、
    /// 正味の親密度（遺恨は減算）、一人あたり本数の有界化（最弱から落とす・打ち切り可視化）。
    /// </summary>
    public class PersonRelationRulesTests
    {
        private static Person P(int id, int tier = 5, int schoolId = 1, int gradYear = 800)
        {
            var p = new Person(id, "人物" + id, Faction.帝国, PersonRole.軍人);
            p.rankTier = tier; p.schoolId = schoolId; p.graduationYear = gradYear;
            return p;
        }

        [Test]
        public void SeedFromCliques_AddsBidirectionalCohortEdges()
        {
            var g = new PersonRelationGraph();
            // 同窓(schoolId=1)かつ同期(800)の2人＝CliqueBond=0.3+0.4=0.7
            var people = new List<Person> { P(1), P(2), P(3, schoolId: 9, gradYear: 700) /*別学校・別期*/ };
            PersonRelationRules.SeedFromCliques(g, people);

            Assert.IsNotNull(g.Get(1, 2, RelationKind.同期));
            Assert.IsNotNull(g.Get(2, 1, RelationKind.同期), "双方向");
            Assert.AreEqual(0.7f, g.Get(1, 2, RelationKind.同期).strength, 1e-4);
            Assert.IsNull(g.Get(1, 3, RelationKind.同期), "別学校・別期は縁が張られない");
        }

        [Test]
        public void LinkCommand_RequiresSuperiorHigherTier_AndIsBidirectional()
        {
            var g = new PersonRelationGraph();
            var superior = P(1, tier: 7);
            var sub = P(2, tier: 5);
            PersonRelationRules.LinkCommand(g, superior, sub, 0.6f);

            Assert.IsNotNull(g.Get(1, 2, RelationKind.部下));
            Assert.IsNotNull(g.Get(2, 1, RelationKind.上官));

            // 上下が逆（下位を superior に渡す）と張られない
            var g2 = new PersonRelationGraph();
            PersonRelationRules.LinkCommand(g2, sub, superior, 0.6f);
            Assert.AreEqual(0, g2.relations.Count, "superior が上位 tier でなければ張らない");
        }

        [Test]
        public void LinkMentorship_CreatesMasterAndGratitude()
        {
            var g = new PersonRelationGraph();
            PersonRelationRules.LinkMentorship(g, P(1, tier: 8), P(2, tier: 5), 0.8f);
            Assert.AreEqual(RelationKind.師弟, g.Get(1, 2, RelationKind.師弟).kind);
            Assert.AreEqual(RelationKind.恩義, g.Get(2, 1, RelationKind.恩義).kind);
        }

        [Test]
        public void NetAffinity_SubtractsGrudge()
        {
            var g = new PersonRelationGraph();
            g.Set(1, 2, RelationKind.親愛, 0.6f);
            g.Set(1, 2, RelationKind.遺恨, 0.2f);
            // 0.6 - 0.2 = 0.4
            Assert.AreEqual(0.4f, PersonRelationRules.NetAffinity(g, 1, 2), 1e-4);
            Assert.AreEqual(0.4f, PersonRelationRules.PromotionFavor(g, 1, 2), 1e-4);
        }

        [Test]
        public void NetAffinity_ClampsToRange()
        {
            var g = new PersonRelationGraph();
            g.Set(1, 2, RelationKind.親愛, 1f);
            g.Set(1, 2, RelationKind.恩義, 1f);
            g.Set(1, 2, RelationKind.同期, 1f);
            Assert.AreEqual(1f, PersonRelationRules.NetAffinity(g, 1, 2), 1e-4, "+1 にクランプ");
        }

        [Test]
        public void Set_OverwritesSameKind()
        {
            var g = new PersonRelationGraph();
            g.Set(1, 2, RelationKind.親愛, 0.3f);
            g.Set(1, 2, RelationKind.親愛, 0.9f);
            Assert.AreEqual(1, g.relations.Count);
            Assert.AreEqual(0.9f, g.Get(1, 2, RelationKind.親愛).strength, 1e-4);
        }

        [Test]
        public void DegreeCap_DropsWeakestAndCountsDropped()
        {
            var g = new PersonRelationGraph { maxDegreePerPerson = 3 };
            // from=1 に 4本（強さ 0.1/0.2/0.3/0.4 を別 to×別種別で）。上限3で最弱(0.1)が落ちる。
            g.Set(1, 2, RelationKind.親愛, 0.1f);
            g.Set(1, 3, RelationKind.親愛, 0.2f);
            g.Set(1, 4, RelationKind.親愛, 0.3f);
            g.Set(1, 5, RelationKind.親愛, 0.4f);

            Assert.AreEqual(3, g.From(1).Count, "from=1 の縁は上限3");
            Assert.IsNull(g.Get(1, 2, RelationKind.親愛), "最弱(0.1)が落ちる");
            Assert.AreEqual(1, g.droppedCount, "打ち切り件数を可視化");
        }

        [Test]
        public void SelfLoop_Ignored()
        {
            var g = new PersonRelationGraph();
            g.Set(1, 1, RelationKind.親愛, 0.5f);
            Assert.AreEqual(0, g.relations.Count);
        }
    }
}
