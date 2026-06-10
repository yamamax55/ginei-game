using System.Collections.Generic;
using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 部品表BOM（#983・BomRules）の純ロジックテスト。多層展開の数量積算（艦1→部品3→素材2＝素材6）・
    /// 階層深さ・末端素材一覧・特定素材の総所要・総部品点数・共通部品の節約を既定Paramsの具体値で固定。
    /// </summary>
    public class BomRulesTests
    {
        // 艦1隻 = 部品A 3個 + 部品B 2個。
        // 部品A = 素材鋼 2個。部品B = 素材鋼 1個 + 素材樹脂 4個。
        // 鋼の総量 = 3×2 + 2×1 = 8。樹脂 = 2×4 = 8。
        private static BomNode SampleShip()
        {
            var partA = new BomNode("部品A", 3f, new List<BomNode>
            {
                new BomNode("鋼", 2f),
            });
            var partB = new BomNode("部品B", 2f, new List<BomNode>
            {
                new BomNode("鋼", 1f),
                new BomNode("樹脂", 4f),
            });
            return new BomNode("艦", 1f, new List<BomNode> { partA, partB });
        }

        /// <summary>多層展開＝各階層の数量を掛け合わせて末端素材を積算（同一素材は合算）。</summary>
        [Test]
        public void Explode_掛け算で素材を積算し同一素材を合算する()
        {
            var result = BomRules.Explode(SampleShip(), 1f);

            Assert.AreEqual(2, result.Count, "末端素材は鋼と樹脂の2種");
            Assert.AreEqual(8f, result["鋼"], 1e-4f, "鋼=3×2+2×1=8");
            Assert.AreEqual(8f, result["樹脂"], 1e-4f, "樹脂=2×4=8");
        }

        /// <summary>「艦1→部品3→素材2なら素材6」＝最小例で再帰積算を担保。</summary>
        [Test]
        public void Explode_艦1部品3素材2なら素材6()
        {
            var node = new BomNode("艦", 1f, new List<BomNode>
            {
                new BomNode("部品", 3f, new List<BomNode>
                {
                    new BomNode("素材", 2f),
                }),
            });

            var result = BomRules.Explode(node, 1f);
            Assert.AreEqual(6f, result["素材"], 1e-4f, "1×3×2=6");
        }

        /// <summary>ルート数量倍で全素材が比例（艦2隻なら2倍）。</summary>
        [Test]
        public void Explode_ルート数量に比例する()
        {
            var result = BomRules.Explode(SampleShip(), 2f);
            Assert.AreEqual(16f, result["鋼"], 1e-4f, "8×2");
            Assert.AreEqual(16f, result["樹脂"], 1e-4f, "8×2");
        }

        /// <summary>特定素材の総所要＝全階層を辿って合算（Explodeの1キーと一致）。</summary>
        [Test]
        public void TotalMaterialRequirement_特定素材の総量を返す()
        {
            Assert.AreEqual(8f, BomRules.TotalMaterialRequirement(SampleShip(), 1f, "鋼"), 1e-4f);
            Assert.AreEqual(24f, BomRules.TotalMaterialRequirement(SampleShip(), 3f, "鋼"), 1e-4f, "8×3");
            Assert.AreEqual(0f, BomRules.TotalMaterialRequirement(SampleShip(), 1f, "存在しない"), 1e-4f);
        }

        /// <summary>階層の深さ＝艦→部品→素材で3段。</summary>
        [Test]
        public void BomDepth_艦部品素材で3段()
        {
            Assert.AreEqual(3, BomRules.BomDepth(SampleShip()));
            Assert.AreEqual(1, BomRules.BomDepth(new BomNode("素材", 1f)), "葉のみは深さ1");
        }

        /// <summary>末端素材一覧＝原材料IDの集合（重複なし）。</summary>
        [Test]
        public void LeafMaterials_末端素材のみ重複なしで返す()
        {
            var leaves = BomRules.LeafMaterials(SampleShip());
            Assert.AreEqual(2, leaves.Count);
            CollectionAssert.Contains(leaves, "鋼");
            CollectionAssert.Contains(leaves, "樹脂");
            CollectionAssert.DoesNotContain(leaves, "部品A", "中間部品は素材に含めない");
        }

        /// <summary>総部品点数＝木の全ノード数（艦+部品A+部品B+鋼+鋼+樹脂=6）。</summary>
        [Test]
        public void ComponentCount_全節点を数える()
        {
            Assert.AreEqual(6, BomRules.ComponentCount(SampleShip()));
        }

        /// <summary>共通部品の節約＝同じ部品IDを複数の上位が使うほど大きい・重複無しは0。</summary>
        [Test]
        public void SharedComponentSavings_共通部品で節約が出る()
        {
            // 鋼が2箇所に出る＝延べ6出現・ユニーク5種 → 1−5/6≒0.1667。
            float shared = BomRules.SharedComponentSavings(SampleShip());
            Assert.Greater(shared, 0f, "共通部品（鋼）があるので節約>0");
            Assert.AreEqual(1f - 5f / 6f, shared, 1e-4f);

            // 全ID固有の木は節約0。
            var distinct = new BomNode("艦", 1f, new List<BomNode>
            {
                new BomNode("X", 1f, new List<BomNode> { new BomNode("a", 1f) }),
                new BomNode("Y", 1f, new List<BomNode> { new BomNode("b", 1f) }),
            });
            Assert.AreEqual(0f, BomRules.SharedComponentSavings(distinct), 1e-4f);
        }

        /// <summary>循環BOMでも深さ上限で打ち切り暴走しない。</summary>
        [Test]
        public void Explode_循環でも上限で打ち切る()
        {
            var node = new BomNode("ループ", 1f);
            node.children = new List<BomNode> { node }; // 自己参照
            // 例外・無限ループにならず有限で返る。
            var result = BomRules.Explode(node, 1f, new BomParams(8));
            Assert.IsNotNull(result);
            Assert.AreEqual(8, BomRules.BomDepth(node, new BomParams(8)), "上限で打ち切られる");
        }
    }
}
