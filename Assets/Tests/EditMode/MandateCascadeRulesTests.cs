using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 主命の MBO カスケード（TKO-11・<see cref="MandateCascadeRules"/>・#2488）を固定する：指揮容量比での段階的縮小、
    /// 指揮系統チェーンの降下、末端=プレイヤー、寄与率（ロールアップ）、直属上官による委譲発令、最小規模の床、null安全。
    /// </summary>
    public class MandateCascadeRulesTests
    {
        private static MandateCascadeRules.CascadeParams CP => MandateCascadeRules.CascadeParams.Default;
        private static SovereignMandateRules.MandateParams MP => SovereignMandateRules.MandateParams.Default;

        private static Person P(int id, int tier)
            => new Person(id, "人物" + id, Faction.帝国, PersonRole.軍人) { rankTier = tier };

        // 君主(元帥12) → 上級大将11 → 中将9 → プレイヤー(准将7)
        private static List<Person> Chain() => new List<Person> { P(1, 12), P(2, 11), P(3, 9), P(4, 7) };

        [Test]
        public void Decompose_ScalesByCommandCapacityRatio()
        {
            Assert.AreEqual(30000f, MandateCascadeRules.Decompose(60000f, 12, 11, CP), 1f); // ×30000/60000
            Assert.AreEqual(12000f, MandateCascadeRules.Decompose(30000f, 11, 9, CP), 1f);  // ×12000/30000
            Assert.AreEqual(3000f, MandateCascadeRules.Decompose(12000f, 9, 7, CP), 1f);   // ×3000/12000
        }

        [Test]
        public void Decompose_FloorsAtMinScope()
        {
            // 100×(3000/60000=0.05)=5 → 最小規模500で床
            Assert.AreEqual(500f, MandateCascadeRules.Decompose(100f, 12, 7, CP), 1e-3);
        }

        [Test]
        public void Decompose_DoesNotGrowWhenChildOutranksParent()
        {
            // 子の容量比>1 はクランプ1＝親より大きくならない
            Assert.AreEqual(1000f, MandateCascadeRules.Decompose(1000f, 7, 12, CP), 1e-3);
        }

        [Test]
        public void Build_NarrowsDownTheChain()
        {
            var levels = MandateCascadeRules.Build(60000f, Chain(), CP);
            Assert.AreEqual(4, levels.Count);

            Assert.AreEqual(1, levels[0].holderId);
            Assert.AreEqual(60000f, levels[0].scope, 1f);
            Assert.AreEqual(-1, levels[0].parentIndex);

            Assert.AreEqual(30000f, levels[1].scope, 1f);
            Assert.AreEqual(0, levels[1].parentIndex);
            Assert.AreEqual(12000f, levels[2].scope, 1f);
            Assert.AreEqual(1, levels[2].parentIndex);
            Assert.AreEqual(3000f, levels[3].scope, 1f);
            Assert.AreEqual(2, levels[3].parentIndex);
        }

        [Test]
        public void Leaf_IsThePlayerAtTheBottom()
        {
            var levels = MandateCascadeRules.Build(60000f, Chain(), CP);
            var leaf = MandateCascadeRules.Leaf(levels);
            Assert.IsNotNull(leaf);
            Assert.AreEqual(4, leaf.holderId, "末端＝プレイヤー(准将)");
            Assert.AreEqual(3000f, leaf.scope, 1f);
        }

        [Test]
        public void Contribution_IsScopeRatioToParent()
        {
            var levels = MandateCascadeRules.Build(60000f, Chain(), CP);
            // 末端(3000) の親=中将(12000) → 0.25
            Assert.AreEqual(0.25f, MandateCascadeRules.Contribution(levels[3], levels[2]), 1e-3);
            // 中将(12000) の親=上級大将(30000) → 0.4
            Assert.AreEqual(0.4f, MandateCascadeRules.Contribution(levels[2], levels[1]), 1e-3);
        }

        [Test]
        public void ToLeafMandate_IssuedByDirectSuperior()
        {
            var levels = MandateCascadeRules.Build(60000f, Chain(), CP);
            var m = MandateCascadeRules.ToLeafMandate(levels, 99, Faction.帝国, MandateKind.占領, "辺境星系", 3, MP);

            Assert.IsNotNull(m);
            Assert.AreEqual(3, m.issuerId, "発令者＝直属の上官（中将）");
            Assert.AreEqual(4, m.assigneeId, "拝命者＝プレイヤー（准将）");
            Assert.AreEqual(MandateKind.占領, m.kind);
            Assert.AreEqual(MandateStatus.拝命, m.status);
            Assert.AreEqual(9, m.dueMonth); // 3 + 既定6
        }

        [Test]
        public void SingleLevelChain_NoLeafMandate()
        {
            var levels = MandateCascadeRules.Build(60000f, new List<Person> { P(1, 12) }, CP);
            Assert.AreEqual(1, levels.Count);
            Assert.IsNull(MandateCascadeRules.ToLeafMandate(levels, 1, Faction.帝国, MandateKind.出陣, "", 0, MP),
                "段が1つ＝カスケード不要（君主が直接発令する想定）");
        }

        [Test]
        public void NullAndEmpty_Safe()
        {
            Assert.AreEqual(0, MandateCascadeRules.Build(60000f, null, CP).Count);
            Assert.IsNull(MandateCascadeRules.Leaf(new List<CascadeLevel>()));
            Assert.AreEqual(0f, MandateCascadeRules.Contribution(null, null), 1e-4);
        }
    }
}
