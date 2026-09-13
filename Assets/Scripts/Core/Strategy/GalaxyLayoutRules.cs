using System.Collections.Generic;
using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 配置計算の対象ノード（星系1つ）。<see cref="side"/> は陣営の帯＝-1:左／+1:右／0:中央(係争)。
    /// 位置以外（id・所有・回廊）は呼び出し側が持ち、ここは座標だけを扱う＝Core 純ロジック。
    /// </summary>
    public struct LayoutNode
    {
        public int id;
        public int side;
        public Vector2 position;

        public LayoutNode(int id, int side, Vector2 position)
        {
            this.id = id; this.side = side; this.position = position;
        }
    }

    /// <summary>配置の調整値。<see cref="Default"/> を持つ（マジックナンバー禁止）。</summary>
    public readonly struct GalaxyLayoutParams
    {
        /// <summary>配置枠の半幅（ワールド単位）。</summary>
        public readonly float halfWidth;
        /// <summary>配置枠の半高（ワールド単位）。</summary>
        public readonly float halfHeight;
        /// <summary>星系どうしの最小距離。これを下回る対は反発で引き離す。</summary>
        public readonly float minSeparation;
        /// <summary>反発の反復回数（多いほど均等・少ないほど元の形を残す）。</summary>
        public readonly int relaxIterations;
        /// <summary>中央に空ける前線の幅（片側）。左右陣営はこの外側に置かれ、境界が読み取れる。</summary>
        public readonly float frontGap;

        public GalaxyLayoutParams(float halfWidth, float halfHeight, float minSeparation, int relaxIterations, float frontGap)
        {
            this.halfWidth = Mathf.Max(1f, halfWidth);
            this.halfHeight = Mathf.Max(1f, halfHeight);
            this.minSeparation = Mathf.Max(0.1f, minSeparation);
            this.relaxIterations = Mathf.Max(0, relaxIterations);
            this.frontGap = Mathf.Max(0f, frontGap);
        }

        /// <summary>
        /// 既定＝従来レイアウト（x が概ね ±7.6・y が ±4）と<b>同程度のワールド尺</b>。
        /// 回廊長は座標距離から作られるため、尺を合わせることでワープ所要時間が従来と同程度に保たれる。
        /// </summary>
        public static GalaxyLayoutParams Default => new GalaxyLayoutParams(8.6f, 5.0f, 2.1f, 24, 1.7f);
    }

    /// <summary>
    /// 戦略マップの星系配置（銀河図のレイアウト）。<b>座標だけ</b>を決め、星系id・所有・回廊接続には触れない
    /// ＝セーブ整合とゲーム進行（回廊で移動可否が決まる）に影響しない。
    ///
    /// 旧配置は「左右の細い塊＋中央が空白」で、画面の大半が余り、星が重なって名前も読めなかった。
    /// ここでは①陣営ごとの帯へ層化して配る（縦の偏りを消す）②反発で最小星間距離を満たす
    /// ③枠いっぱいへ正規化する、の3段で「意図のある有機的な配置」を作る。格子にはならない（層内をゆらす）。
    /// 星系数が変わっても層化と正規化が効くため破綻しない。決定論＝乱数は roll(0..1) で外から受ける。
    /// </summary>
    public static class GalaxyLayoutRules
    {
        /// <summary>
        /// 陣営の帯へ層化して配置し直す（新規生成むけ）。<paramref name="roll"/> が null なら中央値＝ゆらぎ無し（テスト用）。
        /// </summary>
        public static void Layout(IList<LayoutNode> nodes, GalaxyLayoutParams p, System.Func<float> roll)
        {
            if (nodes == null || nodes.Count == 0) return;

            // 帯ごとに通し番号を振る（層化＝同じ帯の中で縦に均等へ散らす）。
            int leftN = 0, rightN = 0, midN = 0;
            for (int i = 0; i < nodes.Count; i++)
            {
                int s = nodes[i].side;
                if (s < 0) leftN++; else if (s > 0) rightN++; else midN++;
            }

            int leftI = 0, rightI = 0, midI = 0;
            for (int i = 0; i < nodes.Count; i++)
            {
                LayoutNode n = nodes[i];
                int rank, total;
                if (n.side < 0) { rank = leftI++; total = leftN; }
                else if (n.side > 0) { rank = rightI++; total = rightN; }
                else { rank = midI++; total = midN; }

                n.position = SeedPosition(n.side, rank, total, p, roll);
                nodes[i] = n;
            }

            Relax(nodes, p);
            NormalizeToFrame(nodes, p);
        }

        /// <summary>
        /// いまの座標を活かしたまま整える（セーブ読み込みむけ）。相対の位置関係を保つので、
        /// 保存済みの回廊長（＝ワープ所要時間）と見た目の距離が大きくずれない。
        /// </summary>
        public static void Refine(IList<LayoutNode> nodes, GalaxyLayoutParams p)
        {
            if (nodes == null || nodes.Count == 0) return;
            Relax(nodes, p);
            NormalizeToFrame(nodes, p);
        }

        /// <summary>帯とランクから初期位置を作る（層化＋ゆらぎ＝縦に均等に散り、かつ格子に見えない）。</summary>
        public static Vector2 SeedPosition(int side, int rank, int total, GalaxyLayoutParams p, System.Func<float> roll)
        {
            float R() => roll != null ? Mathf.Clamp01(roll()) : 0.5f;

            // 縦：帯を total 層に割り、各層の中でゆらす（層化サンプリング＝偏りと団子を防ぐ）。
            float t = total <= 1 ? 0.5f : (rank + 0.15f + 0.7f * R()) / total;
            float y = Mathf.Lerp(-p.halfHeight, p.halfHeight, Mathf.Clamp01(t));

            // 横：陣営の帯の中でゆらす。中央(0)は前線帯の内側に置く＝係争地であることが位置で分かる。
            float x;
            if (side == 0)
            {
                x = Mathf.Lerp(-p.frontGap, p.frontGap, R());
            }
            else
            {
                float inner = p.frontGap;
                float outer = p.halfWidth;
                // 奥ほど疎・前線寄りほど密になりすぎないよう、帯の中央付近を厚めに使う。
                float u = 0.15f + 0.85f * R();
                float mag = Mathf.Lerp(inner, outer, u);
                x = side < 0 ? -mag : mag;
            }
            return new Vector2(x, y);
        }

        /// <summary>最小星間距離を満たすまで押し合う（O(N²)×反復。星系は数〜十数個なので十分軽い）。</summary>
        public static void Relax(IList<LayoutNode> nodes, GalaxyLayoutParams p)
        {
            if (nodes == null || nodes.Count < 2) return;
            float minSep = p.minSeparation;

            for (int iter = 0; iter < p.relaxIterations; iter++)
            {
                bool moved = false;
                for (int i = 0; i < nodes.Count; i++)
                {
                    for (int j = i + 1; j < nodes.Count; j++)
                    {
                        Vector2 a = nodes[i].position;
                        Vector2 b = nodes[j].position;
                        Vector2 d = b - a;
                        float dist = d.magnitude;
                        if (dist >= minSep) continue;

                        Vector2 dir;
                        if (dist > 1e-4f) dir = d / dist;
                        else
                        {
                            // 完全重複は id 由来の決定論的な向きで散らす（左右対称の膠着を避ける）。
                            float ang = (i * 2.399963f) + (j * 0.7654321f); // 黄金角ベースで規則性を出さない
                            dir = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang));
                        }

                        float push = (minSep - dist) * 0.5f;
                        LayoutNode na = nodes[i]; na.position = a - dir * push; nodes[i] = na;
                        LayoutNode nb = nodes[j]; nb.position = b + dir * push; nodes[j] = nb;
                        moved = true;
                    }
                }

                // 枠内へ引き戻す（押し出しで外へ出た分）。
                for (int i = 0; i < nodes.Count; i++)
                {
                    LayoutNode n = nodes[i];
                    n.position = new Vector2(
                        Mathf.Clamp(n.position.x, -p.halfWidth, p.halfWidth),
                        Mathf.Clamp(n.position.y, -p.halfHeight, p.halfHeight));
                    nodes[i] = n;
                }

                if (!moved) break; // 収束したら打ち切る
            }
        }

        /// <summary>外接矩形を枠いっぱいへ拡げて中央へ寄せる（画面の余白を無駄にしない）。縦横比は保つ。</summary>
        public static void NormalizeToFrame(IList<LayoutNode> nodes, GalaxyLayoutParams p)
        {
            if (nodes == null || nodes.Count == 0) return;

            float minX = float.MaxValue, maxX = float.MinValue;
            float minY = float.MaxValue, maxY = float.MinValue;
            for (int i = 0; i < nodes.Count; i++)
            {
                Vector2 q = nodes[i].position;
                if (q.x < minX) minX = q.x;
                if (q.x > maxX) maxX = q.x;
                if (q.y < minY) minY = q.y;
                if (q.y > maxY) maxY = q.y;
            }

            float w = maxX - minX;
            float h = maxY - minY;
            Vector2 center = new Vector2((minX + maxX) * 0.5f, (minY + maxY) * 0.5f);

            // 片方でも潰れている（1列に並ぶ等）ときは拡大しない＝異常な引き伸ばしを避ける。
            float sx = w > 1e-3f ? (p.halfWidth * 2f) / w : 1f;
            float sy = h > 1e-3f ? (p.halfHeight * 2f) / h : 1f;
            float s = Mathf.Min(sx, sy);          // 縦横比を保つ
            if (s > 4f) s = 4f;                   // 極端な拡大の抑制（点が少ないときの暴れ止め）
            if (s < 1f) s = Mathf.Max(s, 0.25f);  // 縮小もしすぎない

            for (int i = 0; i < nodes.Count; i++)
            {
                LayoutNode n = nodes[i];
                n.position = (n.position - center) * s;
                nodes[i] = n;
            }
        }

        /// <summary>配置の外接矩形（カメラのフィット＝全体表示に使う）。ノードが無ければ幅0の矩形。</summary>
        public static void Bounds(IList<LayoutNode> nodes, out Vector2 center, out Vector2 size)
        {
            center = Vector2.zero; size = Vector2.zero;
            if (nodes == null || nodes.Count == 0) return;

            float minX = float.MaxValue, maxX = float.MinValue;
            float minY = float.MaxValue, maxY = float.MinValue;
            for (int i = 0; i < nodes.Count; i++)
            {
                Vector2 q = nodes[i].position;
                if (q.x < minX) minX = q.x;
                if (q.x > maxX) maxX = q.x;
                if (q.y < minY) minY = q.y;
                if (q.y > maxY) maxY = q.y;
            }
            center = new Vector2((minX + maxX) * 0.5f, (minY + maxY) * 0.5f);
            size = new Vector2(maxX - minX, maxY - minY);
        }

        /// <summary>最小星間距離を満たしているか（テスト・検証用）。満たさない対があれば false。</summary>
        public static bool SatisfiesSeparation(IList<LayoutNode> nodes, float minSeparation)
        {
            if (nodes == null || nodes.Count < 2) return true;
            float tol = minSeparation * 0.999f; // 反復打ち切りの丸め誤差を許容
            for (int i = 0; i < nodes.Count; i++)
                for (int j = i + 1; j < nodes.Count; j++)
                    if (Vector2.Distance(nodes[i].position, nodes[j].position) < tol) return false;
            return true;
        }
    }
}
