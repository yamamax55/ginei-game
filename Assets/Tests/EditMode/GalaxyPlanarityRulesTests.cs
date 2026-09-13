using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 航路の非交差化（<see cref="GalaxyPlanarityRules"/>）を固定する。実機で「保存戦役の右側の航路がXに交差」
    /// していた＝どこが繋がっているか読めず進軍先が判断できない。ここでは
    /// ①交差判定そのもの ②3アーキタイプすべての新規生成トポロジー ③実セーブ相当の座標
    /// ④退化ケース（同一点・共線） ⑤再読み込みの安定性（冪等） ⑥接続と連結性を壊さないこと、を検証する。
    /// <b>辺は消さない</b>のが前提なので、どのテストも「辺の本数と端点が不変」を併せて確認する。
    /// </summary>
    public class GalaxyPlanarityRulesTests
    {
        private static GalaxyPlanarityParams P => GalaxyPlanarityParams.Default;
        private static GalaxyLayoutParams L => GalaxyLayoutParams.Default;

        private static System.Func<float> Roll(int seed)
        {
            var rng = new System.Random(seed);
            return () => (float)rng.NextDouble();
        }

        private static List<LayoutNode> Nodes(params (int id, int side, float x, float y)[] src)
        {
            var list = new List<LayoutNode>(src.Length);
            for (int i = 0; i < src.Length; i++)
                list.Add(new LayoutNode(src[i].id, src[i].side, new Vector2(src[i].x, src[i].y)));
            return list;
        }

        private static List<LayoutEdge> Edges(params (int a, int b)[] src)
        {
            var list = new List<LayoutEdge>(src.Length);
            for (int i = 0; i < src.Length; i++) list.Add(new LayoutEdge(src[i].a, src[i].b));
            return list;
        }

        // ===== 交差判定そのもの =====

        [Test]
        public void SegmentsCross_DetectsProperCrossing()
        {
            Assert.IsTrue(GalaxyPlanarityRules.SegmentsCross(
                new Vector2(-1f, -1f), new Vector2(1f, 1f),
                new Vector2(-1f, 1f), new Vector2(1f, -1f), 1e-4f), "X字の交差を見逃した");
        }

        [Test]
        public void SegmentsCross_AllowsSharedEndpoint()
        {
            // 同じ星系から2本伸びるのは正常（交差ではない）。
            Assert.IsFalse(GalaxyPlanarityRules.SegmentsCross(
                new Vector2(0f, 0f), new Vector2(2f, 1f),
                new Vector2(0f, 0f), new Vector2(2f, -1f), 1e-4f), "共有端点を交差と誤判定した");
        }

        [Test]
        public void SegmentsCross_DetectsCollinearOverlap()
        {
            // 共線で重なる＝2本の航路が同じ線の上に乗る＝図として読めない。
            Assert.IsTrue(GalaxyPlanarityRules.SegmentsCross(
                new Vector2(0f, 0f), new Vector2(4f, 0f),
                new Vector2(2f, 0f), new Vector2(6f, 0f), 1e-4f), "共線の重なりを見逃した");
        }

        [Test]
        public void SegmentsCross_AllowsCollinearTouchAtEndpoint()
        {
            // 端点で点接触するだけ（重なり長さ0）は許す。
            Assert.IsFalse(GalaxyPlanarityRules.SegmentsCross(
                new Vector2(0f, 0f), new Vector2(2f, 0f),
                new Vector2(2f, 0f), new Vector2(4f, 0f), 1e-4f), "点接触を重なりと誤判定した");
        }

        [Test]
        public void SegmentsCross_DetectsTJunction()
        {
            // 端点が相手の線分の上に載る（T字）＝別の航路を踏んでいる。
            Assert.IsTrue(GalaxyPlanarityRules.SegmentsCross(
                new Vector2(-2f, 0f), new Vector2(2f, 0f),
                new Vector2(0f, 0f), new Vector2(0f, 3f), 1e-4f), "T字接触を見逃した");
        }

        [Test]
        public void SegmentsCross_IgnoresDisjointSegments()
        {
            Assert.IsFalse(GalaxyPlanarityRules.SegmentsCross(
                new Vector2(-5f, -5f), new Vector2(-4f, -4f),
                new Vector2(4f, 4f), new Vector2(5f, 5f), 1e-4f));
        }

        [Test]
        public void CountStarsOnEdges_DetectsAStarPiercedByACorridor()
        {
            // 0—2 の航路がちょうど星1を貫いている。
            var nodes = Nodes((0, -1, -4f, 0f), (1, 0, 0f, 0f), (2, 1, 4f, 0f));
            var edges = Edges((0, 2));
            Assert.AreEqual(1, GalaxyPlanarityRules.CountStarsOnEdges(nodes, edges, P), "星を貫く航路を見逃した");
        }

        // ===== 実セーブ相当（実機で交差していた盤面）=====

        /// <summary>
        /// 実機スクリーンショット（保存戦役）の座標を写した fixture。
        /// 同盟4（ローガン/ポポカテペトル/モンブラン/アマダブラム）＋帝国4（カンチェンジュンガ/アンナプルナ/
        /// コトパクシ/コジオスコ）。カンチェンジュンガ—コジオスコ と アンナプルナ—コトパクシ がXに交差する。
        /// </summary>
        private static void SavedFixture(out List<LayoutNode> nodes, out List<LayoutEdge> edges)
        {
            nodes = Nodes(
                (0, -1, -5.74f, 2.93f),   // ローガン
                (1, -1, -5.54f, 0.46f),   // ポポカテペトル
                (2, -1, -4.42f, -1.16f),  // モンブラン
                (3, -1, -3.66f, -3.52f),  // アマダブラム
                (4, 1, 4.32f, 1.88f),     // カンチェンジュンガ
                (5, 1, 5.74f, 3.23f),     // アンナプルナ
                (6, 1, 3.89f, -0.99f),    // コトパクシ
                (7, 1, 5.52f, -2.06f));   // コジオスコ

            edges = Edges(
                (0, 2), (1, 2), (2, 3),   // 同盟クラスタ
                (4, 7), (5, 6), (5, 7),   // 帝国クラスタ（4-7 と 5-6 が交差する）
                (2, 6));                  // 前線（要衝）
        }

        [Test]
        public void SavedFixture_ReproducesTheReportedCrossing()
        {
            SavedFixture(out var nodes, out var edges);
            Assert.Greater(GalaxyPlanarityRules.CountCrossings(nodes, edges, P), 0,
                "実機で見えていた交差が fixture で再現できていない（テストの前提が崩れている）");
        }

        /// <summary>
        /// 読み込み時に Game 側が通す順序をそのまま再現する＝<see cref="GalaxyLayoutRules.Refine"/>（重なりを解いて
        /// 枠へ寄せる）→<see cref="GalaxyPlanarityRules.Untangle"/>（交差を解く）。Refine は相似変換なので交差の有無を変えない。
        /// </summary>
        private static bool UntangleLikeTheGame(List<LayoutNode> nodes, List<LayoutEdge> edges, int seed, out int remaining)
        {
            GalaxyLayoutRules.Refine(nodes, L);
            return GalaxyPlanarityRules.Untangle(nodes, edges, L, P, Roll(seed), out remaining);
        }

        [Test]
        public void SavedFixture_IsUntangledToZeroCrossings()
        {
            SavedFixture(out var nodes, out var edges);
            int before = edges.Count;

            bool ok = UntangleLikeTheGame(nodes, edges, 1, out int remaining);

            Assert.IsTrue(ok, $"実セーブ相当の盤面で交差を解けなかった（残り {remaining}）");
            Assert.AreEqual(0, remaining);
            Assert.AreEqual(before, edges.Count, "航路の本数が変わった＝辺を消している（進軍経路が失われる）");
            Assert.IsTrue(GalaxyPlanarityRules.IsConnected(nodes, edges), "連結性が壊れた");
        }

        [Test]
        public void SavedFixture_KeepsIdsSidesAndSeparation()
        {
            SavedFixture(out var nodes, out var edges);
            UntangleLikeTheGame(nodes, edges, 2, out _);

            for (int i = 0; i < nodes.Count; i++)
            {
                Assert.AreEqual(i, nodes[i].id, "星系 id が変わった＝所有/回廊/セーブとの対応が壊れる");
                Assert.IsTrue(GalaxyPlanarityRules.SideOk(nodes[i], L), $"[{i}] 陣営の側（左右）が入れ替わった");
            }
            Assert.IsTrue(GalaxyLayoutRules.SatisfiesSeparation(nodes, L.minSeparation), "最小星間距離が崩れた");
        }

        // ===== 3アーキタイプの新規生成トポロジー =====

        /// <summary>archetype 0＝対峙（左右クラスタを鎖で繋ぎ、内側どうしを1〜2本の前線で橋渡し）。</summary>
        private static void BuildStandoff(int perSide, out List<LayoutNode> nodes, out List<LayoutEdge> edges)
        {
            var n = new List<LayoutNode>(); var e = new List<LayoutEdge>();
            int id = 0;
            var left = new List<int>(); var right = new List<int>();
            for (int i = 0; i < perSide; i++) { n.Add(new LayoutNode(id, -1, Vector2.zero)); left.Add(id++); }
            for (int i = 0; i < perSide; i++) { n.Add(new LayoutNode(id, +1, Vector2.zero)); right.Add(id++); }
            for (int i = 1; i < left.Count; i++) e.Add(new LayoutEdge(left[i - 1], left[i]));
            for (int i = 1; i < right.Count; i++) e.Add(new LayoutEdge(right[i - 1], right[i]));
            e.Add(new LayoutEdge(left[left.Count / 2], right[right.Count / 2])); // 前線
            nodes = n; edges = e;
        }

        /// <summary>archetype 1＝中央ハブ争奪（中央の1星系が両陣営と繋がる）。</summary>
        private static void BuildHub(int perSide, out List<LayoutNode> nodes, out List<LayoutEdge> edges)
        {
            BuildStandoff(perSide, out nodes, out edges);
            int hub = nodes.Count;
            nodes.Add(new LayoutNode(hub, 0, Vector2.zero));
            edges.Add(new LayoutEdge(hub, 0));               // 左の1つへ
            edges.Add(new LayoutEdge(hub, perSide));         // 右の1つへ
            }

        /// <summary>archetype 2＝長い前線（各行で左右を橋渡し＝前線が複数本）。</summary>
        private static void BuildLongFront(int perSide, out List<LayoutNode> nodes, out List<LayoutEdge> edges)
        {
            var n = new List<LayoutNode>(); var e = new List<LayoutEdge>();
            int id = 0;
            var left = new List<int>(); var right = new List<int>();
            for (int r = 0; r < perSide; r++)
            {
                n.Add(new LayoutNode(id, -1, Vector2.zero)); left.Add(id++);
                n.Add(new LayoutNode(id, +1, Vector2.zero)); right.Add(id++);
            }
            for (int i = 1; i < left.Count; i++) e.Add(new LayoutEdge(left[i - 1], left[i]));
            for (int i = 1; i < right.Count; i++) e.Add(new LayoutEdge(right[i - 1], right[i]));
            for (int r = 0; r < perSide; r++) e.Add(new LayoutEdge(left[r], right[r])); // 各行で前線
            nodes = n; edges = e;
        }

        private static void AssertLaidOutWithoutCrossings(List<LayoutNode> nodes, List<LayoutEdge> edges, int seed, string label)
        {
            int edgeCount = edges.Count;
            GalaxyLayoutRules.Layout(nodes, L, Roll(seed));
            bool ok = GalaxyPlanarityRules.Untangle(nodes, edges, L, GalaxyPlanarityParams.Default, Roll(seed + 5000), out int remaining);

            Assert.IsTrue(ok, $"{label}(seed={seed}) で交差を解けなかった（残り {remaining}）");
            Assert.AreEqual(edgeCount, edges.Count, $"{label}(seed={seed}) で航路が消えた");
            Assert.IsTrue(GalaxyPlanarityRules.IsConnected(nodes, edges), $"{label}(seed={seed}) で連結性が壊れた");
            Assert.IsTrue(GalaxyLayoutRules.SatisfiesSeparation(nodes, L.minSeparation), $"{label}(seed={seed}) で星が近すぎる");
        }

        [Test]
        public void Standoff_ManySeeds_HaveNoCrossings()
        {
            for (int seed = 1; seed <= 40; seed++)
                for (int perSide = 3; perSide <= 5; perSide++)
                {
                    BuildStandoff(perSide, out var nodes, out var edges);
                    AssertLaidOutWithoutCrossings(nodes, edges, seed * 10 + perSide, "対峙");
                }
        }

        [Test]
        public void Hub_ManySeeds_HaveNoCrossings()
        {
            for (int seed = 1; seed <= 40; seed++)
                for (int perSide = 3; perSide <= 5; perSide++)
                {
                    BuildHub(perSide, out var nodes, out var edges);
                    AssertLaidOutWithoutCrossings(nodes, edges, seed * 10 + perSide, "中央ハブ");
                }
        }

        [Test]
        public void LongFront_ManySeeds_HaveNoCrossings()
        {
            for (int seed = 1; seed <= 40; seed++)
                for (int perSide = 3; perSide <= 5; perSide++)
                {
                    BuildLongFront(perSide, out var nodes, out var edges);
                    AssertLaidOutWithoutCrossings(nodes, edges, seed * 10 + perSide, "長い前線");
                }
        }

        // ===== 退化ケース =====

        [Test]
        public void CoincidentStars_AreSeparatedAndUntangled()
        {
            // 完全に重なった星系（同一点）から始めても、分離して交差なしへ持っていける。
            var nodes = Nodes((0, -1, -3f, 0f), (1, -1, -3f, 0f), (2, 1, 3f, 0f), (3, 1, 3f, 0f));
            var edges = Edges((0, 1), (2, 3), (0, 2));
            GalaxyLayoutRules.Relax(nodes, L); // まず重なりを解く（配置側の役割）
            bool ok = GalaxyPlanarityRules.Untangle(nodes, edges, L, P, Roll(7), out int remaining);
            Assert.IsTrue(ok, $"同一点からの復帰に失敗（残り {remaining}）");
            Assert.IsTrue(GalaxyLayoutRules.SatisfiesSeparation(nodes, L.minSeparation));
        }

        [Test]
        public void CollinearStars_AreUntangled()
        {
            // 一直線に並んだ星系＝共線の重なりと貫通が起きやすい配置。
            var nodes = Nodes((0, -1, -6f, 0f), (1, -1, -3f, 0f), (2, 1, 3f, 0f), (3, 1, 6f, 0f));
            var edges = Edges((0, 1), (2, 3), (0, 3), (1, 2)); // 0-3 が 1 と 2 を貫く
            bool ok = GalaxyPlanarityRules.Untangle(nodes, edges, L, P, Roll(8), out int remaining);
            Assert.IsTrue(ok, $"共線配置を解けなかった（残り {remaining}）");
        }

        // ===== 再読み込みの安定性（冪等）=====

        [Test]
        public void Untangle_IsIdempotent_AcrossRepeatedLoads()
        {
            SavedFixture(out var nodes, out var edges);
            UntangleLikeTheGame(nodes, edges, 9, out _);

            var after = new List<LayoutNode>(nodes);
            // 会戦↔戦略の往復・再読み込みのたびに呼ばれても、既に交差0なら1mmも動かない。
            for (int again = 0; again < 3; again++)
            {
                bool ok = GalaxyPlanarityRules.Untangle(nodes, edges, L, P, Roll(9), out int remaining);
                Assert.IsTrue(ok);
                Assert.AreEqual(0, remaining);
                for (int i = 0; i < nodes.Count; i++)
                {
                    Assert.AreEqual(after[i].position.x, nodes[i].position.x, 1e-5f, $"[{i}] 再適用で x が動いた");
                    Assert.AreEqual(after[i].position.y, nodes[i].position.y, 1e-5f, $"[{i}] 再適用で y が動いた");
                }
            }
        }

        // ===== 非平面の入力（明示的に扱う）=====

        [Test]
        public void NonPlanarGraph_ReportsFailureInsteadOfDeletingEdges()
        {
            // K5（5点完全グラフ）は平面に交差なく描けない＝解けないことを false で明示する。
            var nodes = Nodes((0, 0, -2f, 2f), (1, 0, 2f, 2f), (2, 0, 3f, -1f), (3, 0, 0f, -3f), (4, 0, -3f, -1f));
            var edges = new List<LayoutEdge>();
            for (int i = 0; i < 5; i++)
                for (int j = i + 1; j < 5; j++) edges.Add(new LayoutEdge(i, j));
            int before = edges.Count;

            bool ok = GalaxyPlanarityRules.Untangle(nodes, edges, L, P, Roll(11), out int remaining);

            Assert.IsFalse(ok, "K5 が解けたことになっている（判定が甘い）");
            Assert.Greater(remaining, 0);
            Assert.AreEqual(before, edges.Count, "解けないときに辺を消してしまっている（進軍経路が失われる）");
            Assert.IsTrue(GalaxyPlanarityRules.IsConnected(nodes, edges), "解けないときも連結性は保つこと");
        }

        // ===== 連結性 =====

        [Test]
        public void IsConnected_DetectsAnIsolatedSystem()
        {
            var nodes = Nodes((0, -1, -3f, 0f), (1, -1, -1f, 0f), (2, 1, 3f, 0f));
            Assert.IsFalse(GalaxyPlanarityRules.IsConnected(nodes, Edges((0, 1))), "孤立星系を見逃した");
            Assert.IsTrue(GalaxyPlanarityRules.IsConnected(nodes, Edges((0, 1), (1, 2))));
        }

        [Test]
        public void NullAndEmpty_AreSafe()
        {
            Assert.AreEqual(0, GalaxyPlanarityRules.CountCrossings(null, null, P));
            Assert.IsTrue(GalaxyPlanarityRules.IsConnected(null, null));
            var empty = new List<LayoutNode>();
            Assert.IsTrue(GalaxyPlanarityRules.Untangle(empty, new List<LayoutEdge>(), L, P, null, out int rem));
            Assert.AreEqual(0, rem);
        }
    }
}
