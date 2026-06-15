using System.Collections.Generic;
using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 離散技術ツリーの進行（<see cref="TechProgressRules"/>）を固定する：研究中が無ければ解禁可能から選ぶ・
    /// 政体の得意分野を優先・研究力で進め完成で習得済みへ・前提が積み上がり次が解禁・研究し尽くしたら停止。
    /// </summary>
    public class TechProgressRulesTests
    {
        // 小さな自前ツリー（カタログ非依存で挙動を固定）。
        // 軍A(root) → 軍B、生C(root)。
        private static List<TechNode> Tree() => new List<TechNode>
        {
            new TechNode("軍A", null, 2f, 1),
            new TechNode("軍B", new List<string> { "軍A" }, 4f, 2),
            new TechNode("生C", null, 2f, 1),
        };

        [Test]
        public void SelectNextTech_PicksRootInitially()
        {
            var tree = Tree();
            var p = new FactionTechProgress();
            TechNode n = TechProgressRules.SelectNextTech(tree, p, "");
            Assert.IsNotNull(n);
            Assert.IsTrue(n.IsRoot, "最初は基礎技術（前提なし）を選ぶ");
        }

        [Test]
        public void Advance_AccumulatesAndCompletes()
        {
            var tree = Tree();
            var p = new FactionTechProgress { currentTechId = "軍A", currentProgress = 0f };

            // output 1 × dt 1 → 進捗1（コスト2には未達）。
            string done = TechProgressRules.Advance(tree, p, 1f, 1f);
            Assert.IsNull(done);
            Assert.AreEqual(1f, p.currentProgress, 0.0001f);

            // さらに output 1 × dt 1 → 進捗2＝完成。
            done = TechProgressRules.Advance(tree, p, 1f, 1f);
            Assert.AreEqual("軍A", done);
            Assert.IsTrue(p.IsResearched("軍A"));
            Assert.IsNull(p.currentTechId, "完成で研究中はクリアされる");
        }

        [Test]
        public void Tick_AutoSelectsAndUnlocksFrontier()
        {
            var tree = Tree();
            var p = new FactionTechProgress();

            // 大きな output で1tickごとに1技術完成（root コスト2/4）。
            var completed = new List<string>();
            for (int i = 0; i < 10; i++)
            {
                string done = TechProgressRules.Tick(tree, p, 100f, 1f, "");
                if (!string.IsNullOrEmpty(done)) completed.Add(done);
            }

            // 全3技術が完成し、前提依存（軍A→軍B）の順序が守られる。
            Assert.AreEqual(3, p.ResearchedCount);
            Assert.IsTrue(p.IsResearched("軍A"));
            Assert.IsTrue(p.IsResearched("軍B"));
            Assert.IsTrue(p.IsResearched("生C"));
            int idxA = completed.IndexOf("軍A");
            int idxB = completed.IndexOf("軍B");
            Assert.Less(idxA, idxB, "軍A は軍B より先に完成する（前提充足）");
        }

        [Test]
        public void Tick_ReturnsNull_WhenAllResearched()
        {
            var tree = Tree();
            var p = new FactionTechProgress();
            for (int i = 0; i < 20; i++) TechProgressRules.Tick(tree, p, 100f, 1f, "");
            Assert.AreEqual(3, p.ResearchedCount);

            // 研究し尽くした後はもう完成しない（解禁可能が無い）。
            string done = TechProgressRules.Tick(tree, p, 100f, 1f, "");
            Assert.IsNull(done);
        }

        [Test]
        public void SelectNextTech_PrefersFavoredFieldByIdeology()
        {
            // カタログ実物で：専制（軍事得意）は軍事分野の根を優先する。
            var p = new FactionTechProgress();
            TechNode n = TechProgressRules.SelectNextTech(TechCatalog.Nodes, p, ResearchRules.Ideology専制);
            Assert.IsNotNull(n);
            Assert.AreEqual(ResearchField.軍事, TechCatalog.FieldOf(n.techId),
                "専制は軍事分野を優先して研究する");
        }
    }
}
