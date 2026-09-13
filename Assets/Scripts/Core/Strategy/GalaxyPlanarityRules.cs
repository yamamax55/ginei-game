using System.Collections.Generic;
using UnityEngine;

namespace Ginei
{
    /// <summary>航路1本（星系idの対）。向きは持たない（a-b と b-a は同じ）。</summary>
    public readonly struct LayoutEdge
    {
        public readonly int aId;
        public readonly int bId;

        public LayoutEdge(int aId, int bId) { this.aId = aId; this.bId = bId; }

        /// <summary>この航路が星系 id を端点に持つか。</summary>
        public bool Touches(int id) => aId == id || bId == id;
        /// <summary>もう一方の端点（持たない id を渡したら -1）。</summary>
        public int Other(int id) => aId == id ? bId : (bId == id ? aId : -1);
    }

    /// <summary>非交差化の調整値。<see cref="Default"/> を持つ。</summary>
    public readonly struct GalaxyPlanarityParams
    {
        /// <summary>星系が「自分の端点でない航路」から最低限離れる距離。これ未満は“航路が星を貫いている”とみなす。</summary>
        public readonly float starClearance;
        /// <summary>1ノードあたりに試す候補位置の数。多いほど解けるが遅い。</summary>
        public readonly int candidatesPerNode;
        /// <summary>全ノードを走査する最大パス数。これを尽くしても解けなければ非平面の疑い。</summary>
        public readonly int maxPasses;
        /// <summary>候補位置を探す初期半径（この距離まわりを探る）。パスごとに縮む。</summary>
        public readonly float searchRadius;
        /// <summary>幾何判定の許容誤差。</summary>
        public readonly float epsilon;

        public GalaxyPlanarityParams(float starClearance, int candidatesPerNode, int maxPasses, float searchRadius, float epsilon)
        {
            this.starClearance = Mathf.Max(0.01f, starClearance);
            this.candidatesPerNode = Mathf.Max(4, candidatesPerNode);
            this.maxPasses = Mathf.Max(1, maxPasses);
            this.searchRadius = Mathf.Max(0.1f, searchRadius);
            this.epsilon = Mathf.Max(1e-6f, epsilon);
        }

        public static GalaxyPlanarityParams Default => new GalaxyPlanarityParams(0.85f, 48, 40, 4.0f, 1e-4f);
    }

    /// <summary>
    /// 航路の交差を無くす（#航路が交錯する）。銀河図で航路がXに交わると、どこが繋がっているのか読めず
    /// 進軍先の判断ができない。<b>辺は一切消さない・繋ぎ替えない</b>（消すと進軍経路が失われる）＝
    /// <b>座標だけを動かして</b>交差を解く。星系id・所有・回廊接続・保存済みの回廊長はそのまま。
    ///
    /// 不正とみなすのは3種類：①共有端点以外での線分交差 ②共線で重なった航路 ③端点でない星系を貫く航路。
    /// 解法は局所探索＝違反に関与するノードを、陣営の側と最小星間距離を守れる候補位置へ動かして違反を減らす。
    /// 星系は数〜十数個・航路は十数本なので、読み込み時に一度回すだけで十分軽い（毎フレームでは呼ばない）。
    ///
    /// <b>非平面の入力</b>（K5/K3,3 を含むなど、そもそも平面に交差なく描けないグラフ）では 0 にできない。
    /// その場合は <see cref="Untangle"/> が false を返し、残った違反数を出す＝呼び出し側が明示的に扱う
    /// （黙って辺を消さない）。決定論＝乱数は roll(0..1) で外から受ける。
    /// </summary>
    public static class GalaxyPlanarityRules
    {
        // ===== 幾何 =====

        /// <summary>o→a と o→b の外積（符号で左右が分かる）。</summary>
        private static float Cross(Vector2 o, Vector2 a, Vector2 b)
            => (a.x - o.x) * (b.y - o.y) - (a.y - o.y) * (b.x - o.x);

        private static bool Same(Vector2 a, Vector2 b, float eps)
            => Mathf.Abs(a.x - b.x) <= eps && Mathf.Abs(a.y - b.y) <= eps;

        /// <summary>点 p が線分 ab の（共線前提で）内側にあるか。</summary>
        private static bool WithinSpan(Vector2 a, Vector2 b, Vector2 p, float eps)
            => p.x >= Mathf.Min(a.x, b.x) - eps && p.x <= Mathf.Max(a.x, b.x) + eps
            && p.y >= Mathf.Min(a.y, b.y) - eps && p.y <= Mathf.Max(a.y, b.y) + eps;

        /// <summary>
        /// 2本の線分が「不正に」交わるか。共有端点で触れるだけは正常（同じ星系から伸びる航路）。
        /// 真の交差（互いの内部で交わる）と、共線での重なり（長さのある重複）を不正とする。
        /// </summary>
        public static bool SegmentsCross(Vector2 p1, Vector2 p2, Vector2 q1, Vector2 q2, float eps)
        {
            bool shared = Same(p1, q1, eps) || Same(p1, q2, eps) || Same(p2, q1, eps) || Same(p2, q2, eps);

            float d1 = Cross(q1, q2, p1);
            float d2 = Cross(q1, q2, p2);
            float d3 = Cross(p1, p2, q1);
            float d4 = Cross(p1, p2, q2);

            // 共線（4点とも同一直線上）＝重なっていれば不正。共有端点があっても、
            // 端点を越えて重なっていれば「航路が別の航路の上に乗る」＝読めないので不正とする。
            if (Mathf.Abs(d1) <= eps && Mathf.Abs(d2) <= eps && Mathf.Abs(d3) <= eps && Mathf.Abs(d4) <= eps)
                return CollinearOverlaps(p1, p2, q1, q2, eps);

            if (shared) return false; // 共線でなく端点を共有するだけ＝正常

            // 真の交差＝互いに相手の線分をまたぐ
            bool straddleA = (d1 > eps && d2 < -eps) || (d1 < -eps && d2 > eps);
            bool straddleB = (d3 > eps && d4 < -eps) || (d3 < -eps && d4 > eps);
            if (straddleA && straddleB) return true;

            // 端点が相手の線分上に載る（T字接触）＝図として読めないので不正
            if (Mathf.Abs(d1) <= eps && WithinSpan(q1, q2, p1, eps)) return true;
            if (Mathf.Abs(d2) <= eps && WithinSpan(q1, q2, p2, eps)) return true;
            if (Mathf.Abs(d3) <= eps && WithinSpan(p1, p2, q1, eps)) return true;
            if (Mathf.Abs(d4) <= eps && WithinSpan(p1, p2, q2, eps)) return true;

            return false;
        }

        /// <summary>共線の2線分が「長さを持って」重なっているか（点で触れるだけは重なりとしない）。</summary>
        private static bool CollinearOverlaps(Vector2 p1, Vector2 p2, Vector2 q1, Vector2 q2, float eps)
        {
            Vector2 dir = p2 - p1;
            float len = dir.magnitude;
            if (len <= eps) return false;
            dir /= len;

            float a0 = 0f, a1 = len;
            float b0 = Vector2.Dot(q1 - p1, dir);
            float b1 = Vector2.Dot(q2 - p1, dir);
            if (b0 > b1) { float t = b0; b0 = b1; b1 = t; }

            float lo = Mathf.Max(a0, b0);
            float hi = Mathf.Min(a1, b1);
            return (hi - lo) > eps * 10f; // 点接触は許す・区間として重なったら不正
        }

        /// <summary>点 p と線分 ab の距離。</summary>
        public static float PointSegmentDistance(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float len2 = ab.sqrMagnitude;
            if (len2 <= 1e-12f) return Vector2.Distance(p, a);
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / len2);
            return Vector2.Distance(p, a + ab * t);
        }

        // ===== 違反の数え上げ =====

        /// <summary>共有端点以外で交わっている航路の対の数。</summary>
        public static int CountCrossings(IList<LayoutNode> nodes, IList<LayoutEdge> edges, GalaxyPlanarityParams p)
        {
            if (nodes == null || edges == null || edges.Count < 2) return 0;
            int count = 0;
            for (int i = 0; i < edges.Count; i++)
            {
                if (!TryEnds(nodes, edges[i], out Vector2 a1, out Vector2 a2)) continue;
                for (int j = i + 1; j < edges.Count; j++)
                {
                    if (!TryEnds(nodes, edges[j], out Vector2 b1, out Vector2 b2)) continue;
                    if (SegmentsCross(a1, a2, b1, b2, p.epsilon)) count++;
                }
            }
            return count;
        }

        /// <summary>端点でない星系を（<see cref="GalaxyPlanarityParams.starClearance"/> 未満に）貫いている航路の数。</summary>
        public static int CountStarsOnEdges(IList<LayoutNode> nodes, IList<LayoutEdge> edges, GalaxyPlanarityParams p)
        {
            if (nodes == null || edges == null) return 0;
            int count = 0;
            for (int e = 0; e < edges.Count; e++)
            {
                LayoutEdge ed = edges[e];
                if (!TryEnds(nodes, ed, out Vector2 a, out Vector2 b)) continue;
                for (int n = 0; n < nodes.Count; n++)
                {
                    int id = nodes[n].id;
                    if (ed.Touches(id)) continue; // 端点は当然この航路の上にある
                    if (PointSegmentDistance(nodes[n].position, a, b) < p.starClearance) count++;
                }
            }
            return count;
        }

        /// <summary>違反の総数（交差＋星の貫通）。0 なら図として読める。</summary>
        public static int CountViolations(IList<LayoutNode> nodes, IList<LayoutEdge> edges, GalaxyPlanarityParams p)
            => CountCrossings(nodes, edges, p) + CountStarsOnEdges(nodes, edges, p);

        /// <summary>交差も貫通も無いか。</summary>
        public static bool IsCrossingFree(IList<LayoutNode> nodes, IList<LayoutEdge> edges, GalaxyPlanarityParams p)
            => CountViolations(nodes, edges, p) == 0;

        // ===== 非交差化（座標だけを動かす） =====

        /// <summary>
        /// 座標を動かして交差と貫通を解く。<b>edges は読むだけ＝接続は不変</b>。
        /// 既に違反0なら何も動かさない（＝再読み込みで座標が流れない＝冪等）。
        /// 解けたら true。解けなければ false（<paramref name="remaining"/> に残った違反数）。
        /// </summary>
        public static bool Untangle(IList<LayoutNode> nodes, IList<LayoutEdge> edges,
            GalaxyLayoutParams layout, GalaxyPlanarityParams p, System.Func<float> roll, out int remaining)
        {
            remaining = 0;
            if (nodes == null || edges == null || nodes.Count == 0) return true;

            remaining = CountViolations(nodes, edges, p);
            if (remaining == 0) return true; // 冪等：既に読める図なら触らない

            float R() => roll != null ? Mathf.Clamp01(roll()) : 0.5f;

            for (int pass = 0; pass < p.maxPasses && remaining > 0; pass++)
            {
                // パスが進むほど探索半径を絞る（大きく動かして解けなければ細かく詰める）。
                float radius = p.searchRadius * (1f - 0.75f * pass / Mathf.Max(1, p.maxPasses - 1));
                bool improvedThisPass = false;

                for (int i = 0; i < nodes.Count && remaining > 0; i++)
                {
                    if (NodeViolations(nodes, edges, p, i) == 0) continue; // 無関係なノードは動かさない

                    LayoutNode original = nodes[i];
                    Vector2 bestPos = original.position;
                    int bestScore = remaining;

                    for (int c = 0; c < p.candidatesPerNode; c++)
                    {
                        Vector2 cand = Candidate(original, radius, layout, R);
                        LayoutNode probe = original; probe.position = cand; nodes[i] = probe;

                        if (!SeparationOk(nodes, layout.minSeparation, i) || !SideOk(probe, layout))
                        {
                            nodes[i] = original;
                            continue;
                        }

                        int score = CountViolations(nodes, edges, p);
                        if (score < bestScore) { bestScore = score; bestPos = cand; }
                        nodes[i] = original;

                        if (bestScore == 0) break;
                    }

                    if (bestScore < remaining)
                    {
                        LayoutNode moved = original; moved.position = bestPos; nodes[i] = moved;
                        remaining = bestScore;
                        improvedThisPass = true;
                    }
                }

                if (!improvedThisPass && remaining > 0)
                {
                    // 局所解に嵌った：関与ノードを1つ大きく振ってから続ける（決定論的な揺さぶり）。
                    int stuck = FirstViolatingNode(nodes, edges, p);
                    if (stuck < 0) break;
                    LayoutNode n = nodes[stuck];
                    n.position = Candidate(n, p.searchRadius * 1.5f, layout, R);
                    nodes[stuck] = n;
                    remaining = CountViolations(nodes, edges, p);
                }
            }

            return remaining == 0;
        }

        /// <summary>そのノードが関与している違反の数（交差＋自分を貫く航路）。</summary>
        public static int NodeViolations(IList<LayoutNode> nodes, IList<LayoutEdge> edges, GalaxyPlanarityParams p, int index)
        {
            if (nodes == null || edges == null || index < 0 || index >= nodes.Count) return 0;
            int id = nodes[index].id;
            int count = 0;

            for (int i = 0; i < edges.Count; i++)
            {
                bool touchesI = edges[i].Touches(id);
                if (!TryEnds(nodes, edges[i], out Vector2 a1, out Vector2 a2)) continue;

                // 自分が端点でない航路に貫かれている
                if (!touchesI && PointSegmentDistance(nodes[index].position, a1, a2) < p.starClearance) count++;

                if (!touchesI) continue;
                for (int j = 0; j < edges.Count; j++)
                {
                    if (j == i) continue;
                    if (!TryEnds(nodes, edges[j], out Vector2 b1, out Vector2 b2)) continue;
                    if (SegmentsCross(a1, a2, b1, b2, p.epsilon)) count++;
                }
            }
            return count;
        }

        /// <summary>違反に関与している最初のノード（無ければ -1）。</summary>
        private static int FirstViolatingNode(IList<LayoutNode> nodes, IList<LayoutEdge> edges, GalaxyPlanarityParams p)
        {
            for (int i = 0; i < nodes.Count; i++)
                if (NodeViolations(nodes, edges, p, i) > 0) return i;
            return -1;
        }

        /// <summary>候補位置＝現在地のまわり（枠内へクランプ）。陣営の側は <see cref="SideOk"/> で別途担保する。</summary>
        private static Vector2 Candidate(LayoutNode n, float radius, GalaxyLayoutParams layout, System.Func<float> R)
        {
            float ang = R() * Mathf.PI * 2f;
            float r = radius * Mathf.Sqrt(Mathf.Max(0f, R())); // 面積一様に散らす
            Vector2 q = n.position + new Vector2(Mathf.Cos(ang) * r, Mathf.Sin(ang) * r);
            return new Vector2(
                Mathf.Clamp(q.x, -layout.halfWidth, layout.halfWidth),
                Mathf.Clamp(q.y, -layout.halfHeight, layout.halfHeight));
        }

        /// <summary>index のノードが他の全ノードから最小星間距離を保っているか。</summary>
        public static bool SeparationOk(IList<LayoutNode> nodes, float minSeparation, int index)
        {
            for (int j = 0; j < nodes.Count; j++)
            {
                if (j == index) continue;
                if (Vector2.Distance(nodes[index].position, nodes[j].position) < minSeparation * 0.999f) return false;
            }
            return true;
        }

        /// <summary>陣営の側（左/右）を保っているか。中央(0)は自由。前線が入れ替わると勢力範囲が読めなくなる。</summary>
        public static bool SideOk(LayoutNode n, GalaxyLayoutParams layout)
        {
            if (n.side < 0) return n.position.x <= -layout.frontGap * 0.25f;
            if (n.side > 0) return n.position.x >= layout.frontGap * 0.25f;
            return true;
        }

        /// <summary>航路の両端の座標を引く（欠けた星系を指す航路は false）。</summary>
        private static bool TryEnds(IList<LayoutNode> nodes, LayoutEdge e, out Vector2 a, out Vector2 b)
        {
            a = Vector2.zero; b = Vector2.zero;
            bool foundA = false, foundB = false;
            for (int i = 0; i < nodes.Count; i++)
            {
                if (!foundA && nodes[i].id == e.aId) { a = nodes[i].position; foundA = true; }
                if (!foundB && nodes[i].id == e.bId) { b = nodes[i].position; foundB = true; }
                if (foundA && foundB) break;
            }
            return foundA && foundB && e.aId != e.bId;
        }

        // ===== 連結性（辺を消していないことの確認に使う） =====

        /// <summary>全星系が航路だけで行き来できるか（連結）。孤立した星系があれば false。</summary>
        public static bool IsConnected(IList<LayoutNode> nodes, IList<LayoutEdge> edges)
        {
            if (nodes == null || nodes.Count == 0) return true;
            if (nodes.Count == 1) return true;
            if (edges == null || edges.Count == 0) return false;

            var seen = new HashSet<int>();
            var stack = new Stack<int>();
            stack.Push(nodes[0].id);
            seen.Add(nodes[0].id);

            while (stack.Count > 0)
            {
                int cur = stack.Pop();
                for (int i = 0; i < edges.Count; i++)
                {
                    if (!edges[i].Touches(cur)) continue;
                    int other = edges[i].Other(cur);
                    if (other < 0 || seen.Contains(other)) continue;
                    seen.Add(other);
                    stack.Push(other);
                }
            }
            return seen.Count == nodes.Count;
        }
    }
}
