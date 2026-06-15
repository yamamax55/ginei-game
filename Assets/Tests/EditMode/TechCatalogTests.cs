using System.Collections.Generic;
using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 研究ツリーの具体カタログ（<see cref="TechCatalog"/>）の整合を固定する：基礎技術（根）に前提が無いこと、
    /// すべての前提IDがカタログ内に実在すること（孤立した前提＝行き止まりを作らない）、各ノードに分野メタが付くこと、
    /// 分野別抽出が部分集合になること。
    /// </summary>
    public class TechCatalogTests
    {
        [Test]
        public void Catalog_IsNonEmpty()
        {
            Assert.Greater(TechCatalog.Count, 0);
            Assert.AreEqual(TechCatalog.Count, TechCatalog.Nodes.Count);
        }

        [Test]
        public void EveryPrerequisite_ExistsInCatalog()
        {
            var ids = new HashSet<string>();
            foreach (var n in TechCatalog.Nodes) ids.Add(n.techId);

            foreach (var n in TechCatalog.Nodes)
            {
                if (n.prerequisites == null) continue;
                foreach (var pre in n.prerequisites)
                    Assert.IsTrue(ids.Contains(pre), $"前提 '{pre}'（{n.techId} の）がカタログに無い");
            }
        }

        [Test]
        public void EveryNode_HasFieldAndDisplayName()
        {
            foreach (var n in TechCatalog.Nodes)
            {
                TechInfo ti = TechCatalog.Info(n.techId);
                Assert.IsTrue(ti.IsValid, $"{n.techId} に Info が無い");
                Assert.IsFalse(string.IsNullOrEmpty(TechCatalog.DisplayName(n.techId)));
            }
        }

        [Test]
        public void Catalog_HasRootsInEveryField()
        {
            foreach (ResearchField f in System.Enum.GetValues(typeof(ResearchField)))
            {
                var inField = TechCatalog.NodesInField(f);
                Assert.Greater(inField.Count, 0, $"分野 {f} に技術が無い");
                bool anyRoot = false;
                foreach (var n in inField) if (n.IsRoot) { anyRoot = true; break; }
                Assert.IsTrue(anyRoot, $"分野 {f} に基礎技術（根）が無い");
            }
        }

        [Test]
        public void UnknownTech_FallsBackGracefully()
        {
            Assert.IsFalse(TechCatalog.Info("存在しない技術").IsValid);
            Assert.AreEqual("存在しない技術", TechCatalog.DisplayName("存在しない技術"));
        }
    }
}
