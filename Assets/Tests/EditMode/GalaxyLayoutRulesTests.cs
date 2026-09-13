using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 星系配置（<see cref="GalaxyLayoutRules"/>）の不変条件を固定する。旧配置の実害は
    /// ①左右の細い塊に密集し中央と画面の大半が余る ②星が重なって名前が読めない ③縦に偏る、の3つ。
    /// ここでは「最小星間距離を満たす」「枠を使い切る」「陣営が左右に分かれ前線が中央に来る」
    /// 「星系数が変わっても破綻しない」「id を触らない＝セーブ整合」を検証する。
    /// </summary>
    public class GalaxyLayoutRulesTests
    {
        private static List<LayoutNode> MakeSides(int perSide, int mid = 0)
        {
            var list = new List<LayoutNode>();
            int id = 0;
            for (int i = 0; i < perSide; i++) list.Add(new LayoutNode(id++, -1, Vector2.zero));
            for (int i = 0; i < perSide; i++) list.Add(new LayoutNode(id++, +1, Vector2.zero));
            for (int i = 0; i < mid; i++) list.Add(new LayoutNode(id++, 0, Vector2.zero));
            return list;
        }

        /// <summary>決定論の疑似乱数（roll 0..1）。テストを固定値で再現するため。</summary>
        private static System.Func<float> Roll(int seed)
        {
            var rng = new System.Random(seed);
            return () => (float)rng.NextDouble();
        }

        [Test]
        public void Layout_SatisfiesMinimumSeparation()
        {
            var p = GalaxyLayoutParams.Default;
            var nodes = MakeSides(4, 1);
            GalaxyLayoutRules.Layout(nodes, p, Roll(1));
            Assert.IsTrue(GalaxyLayoutRules.SatisfiesSeparation(nodes, p.minSeparation),
                "最小星間距離を満たしていない＝星が重なり名前が読めない");
        }

        [Test]
        public void Layout_UsesTheWholeFrame()
        {
            var p = GalaxyLayoutParams.Default;
            var nodes = MakeSides(4, 1);
            GalaxyLayoutRules.Layout(nodes, p, Roll(2));
            GalaxyLayoutRules.Bounds(nodes, out _, out Vector2 size);
            // 旧配置は縦横どちらかが枠の半分も使っていなかった。7割以上を使うことを固定する。
            Assert.Greater(size.x, p.halfWidth * 2f * 0.7f, "横方向に画面を使い切れていない");
            Assert.Greater(size.y, p.halfHeight * 2f * 0.7f, "縦方向に画面を使い切れていない");
        }

        [Test]
        public void Layout_SeparatesFactionsLeftAndRight()
        {
            var p = GalaxyLayoutParams.Default;
            var nodes = MakeSides(4);
            GalaxyLayoutRules.Layout(nodes, p, Roll(3));

            float leftMax = float.MinValue, rightMin = float.MaxValue;
            for (int i = 0; i < nodes.Count; i++)
            {
                if (nodes[i].side < 0 && nodes[i].position.x > leftMax) leftMax = nodes[i].position.x;
                if (nodes[i].side > 0 && nodes[i].position.x < rightMin) rightMin = nodes[i].position.x;
            }
            // 左陣営はすべて右陣営より左にある＝前線が中央に1本立ち、勢力範囲が一目で分かる。
            Assert.Less(leftMax, rightMin, "陣営の領域が左右に分かれていない＝前線が読み取れない");
        }

        [Test]
        public void Layout_IsNotAPerfectGrid()
        {
            var p = GalaxyLayoutParams.Default;
            var nodes = MakeSides(4);
            GalaxyLayoutRules.Layout(nodes, p, Roll(4));

            // 同じ帯の x がすべて同値＝定規で引いた列。有機的な配置ではそうならない。
            float first = float.NaN; bool allSame = true;
            for (int i = 0; i < nodes.Count; i++)
            {
                if (nodes[i].side >= 0) continue;
                if (float.IsNaN(first)) { first = nodes[i].position.x; continue; }
                if (Mathf.Abs(nodes[i].position.x - first) > 0.01f) { allSame = false; break; }
            }
            Assert.IsFalse(allSame, "帯の中で x が一定＝格子状（意図のある有機的な配置になっていない）");
        }

        [Test]
        public void Layout_HandlesVariousCounts_WithoutBreaking()
        {
            var p = GalaxyLayoutParams.Default;
            for (int perSide = 1; perSide <= 12; perSide++)
            {
                var nodes = MakeSides(perSide, perSide % 3 == 0 ? 1 : 0);
                GalaxyLayoutRules.Layout(nodes, p, Roll(perSide));
                for (int i = 0; i < nodes.Count; i++)
                {
                    Assert.IsFalse(float.IsNaN(nodes[i].position.x) || float.IsNaN(nodes[i].position.y),
                        $"perSide={perSide} で NaN が出た");
                }
            }
        }

        [Test]
        public void Layout_KeepsIdAndSide_ForSaveIntegrity()
        {
            var p = GalaxyLayoutParams.Default;
            var nodes = MakeSides(3, 1);
            GalaxyLayoutRules.Layout(nodes, p, Roll(6));
            for (int i = 0; i < nodes.Count; i++)
            {
                Assert.AreEqual(i, nodes[i].id, "星系 id が入れ替わった＝所有/回廊/セーブとの対応が壊れる");
            }
            Assert.AreEqual(-1, nodes[0].side);
            Assert.AreEqual(+1, nodes[3].side);
        }

        [Test]
        public void Refine_KeepsRelativeOrder_ForLoadedSaves()
        {
            var p = GalaxyLayoutParams.Default;
            // 保存済みの座標を模した入力（左に3・右に3）。Refine は相対の位置関係を保つ。
            var nodes = new List<LayoutNode>
            {
                new LayoutNode(0, -1, new Vector2(-6f, -3f)),
                new LayoutNode(1, -1, new Vector2(-5.5f, 0f)),
                new LayoutNode(2, -1, new Vector2(-6.2f, 3f)),
                new LayoutNode(3, +1, new Vector2(6f, -3f)),
                new LayoutNode(4, +1, new Vector2(5.5f, 0f)),
                new LayoutNode(5, +1, new Vector2(6.2f, 3f)),
            };
            GalaxyLayoutRules.Refine(nodes, p);

            Assert.IsTrue(GalaxyLayoutRules.SatisfiesSeparation(nodes, p.minSeparation));
            // 左右関係が保たれる＝保存済みの回廊長（ワープ所要時間）と見た目が大きくずれない。
            for (int i = 0; i < 3; i++)
                for (int j = 3; j < 6; j++)
                    Assert.Less(nodes[i].position.x, nodes[j].position.x, "Refine で左右が入れ替わった");
        }

        [Test]
        public void Refine_IsIdempotent()
        {
            // 会戦↔戦略の往復や再読み込みのたびに Refine が呼ばれても座標が流れないこと。
            // 流れると保存済みの回廊長（ワープ所要時間）と見た目が回を追うごとにずれる。
            var p = GalaxyLayoutParams.Default;
            var nodes = MakeSides(4, 1);
            GalaxyLayoutRules.Layout(nodes, p, Roll(11));

            var once = new List<LayoutNode>(nodes);
            GalaxyLayoutRules.Refine(nodes, p);
            GalaxyLayoutRules.Refine(nodes, p);

            for (int i = 0; i < nodes.Count; i++)
            {
                Assert.AreEqual(once[i].position.x, nodes[i].position.x, 1e-3f, $"[{i}] x が再適用で動いた");
                Assert.AreEqual(once[i].position.y, nodes[i].position.y, 1e-3f, $"[{i}] y が再適用で動いた");
            }
        }

        [Test]
        public void Relax_PushesApartCoincidentNodes()
        {
            var p = GalaxyLayoutParams.Default;
            var nodes = new List<LayoutNode>
            {
                new LayoutNode(0, -1, new Vector2(1f, 1f)),
                new LayoutNode(1, +1, new Vector2(1f, 1f)), // 完全重複
            };
            GalaxyLayoutRules.Relax(nodes, p);
            Assert.Greater(Vector2.Distance(nodes[0].position, nodes[1].position), p.minSeparation * 0.9f,
                "完全に重なった星系が分離されない");
        }

        [Test]
        public void Bounds_ReturnsCenterAndSize()
        {
            var nodes = new List<LayoutNode>
            {
                new LayoutNode(0, -1, new Vector2(-2f, -1f)),
                new LayoutNode(1, +1, new Vector2(4f, 3f)),
            };
            GalaxyLayoutRules.Bounds(nodes, out Vector2 c, out Vector2 size);
            Assert.AreEqual(1f, c.x, 1e-4f);
            Assert.AreEqual(1f, c.y, 1e-4f);
            Assert.AreEqual(6f, size.x, 1e-4f);
            Assert.AreEqual(4f, size.y, 1e-4f);
        }

        [Test]
        public void Layout_NullOrEmpty_IsSafe()
        {
            Assert.DoesNotThrow(() => GalaxyLayoutRules.Layout(null, GalaxyLayoutParams.Default, null));
            Assert.DoesNotThrow(() => GalaxyLayoutRules.Refine(new List<LayoutNode>(), GalaxyLayoutParams.Default));
            Assert.IsTrue(GalaxyLayoutRules.SatisfiesSeparation(null, 1f));
        }
    }
}
