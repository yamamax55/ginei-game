using System.Collections.Generic;
using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 配下艦の分離（重なり解消）の純ロジック（#69/#80 EMOV-5・test-first）。
    /// 同一部隊内で minSeparation 未満に重なった艦を半分ずつ押し離す変位を求める。
    /// <b>一様グリッド（セル=minSeparation）で近傍だけ比較</b>＝総当り O(n²) を ~O(n) に削る
    /// （終盤の多部隊会戦のラグ対策＝スケーラビリティ規律）。挙動（押し離し量）は総当りと同値。
    /// Squadron（Game）はこの変位を Transform に適用するだけ＝分離の数式を二重実装しない。
    /// </summary>
    public static class SeparationResolveRules
    {
        private const int MinimumRetainedGridCells = 64;

        /// <summary>各メンバの押し離し変位を返す（テスト/簡易用＝毎回確保）。</summary>
        public static Vector2[] Resolve(IList<Vector2> positions, int count, float minSeparation, float strength)
        {
            var displace = new Vector2[count < 0 ? 0 : count];
            Resolve(positions, count, minSeparation, strength, displace, null);
            return displace;
        }

        /// <summary>
        /// 各メンバの押し離し変位を <paramref name="displace"/> に書き込む（runtime＝バッファ再利用で GC 回避）。
        /// <paramref name="gridBuf"/> を渡すと内部でクリアして使い回す（null なら新規確保）。
        /// </summary>
        public static void Resolve(IList<Vector2> positions, int count, float minSeparation, float strength,
                                   Vector2[] displace, Dictionary<long, List<int>> gridBuf)
        {
            for (int i = 0; i < count && i < displace.Length; i++) displace[i] = Vector2.zero;
            if (count <= 1 || minSeparation <= 0f || strength <= 0f) return;

            float cell = minSeparation;
            float minSq = minSeparation * minSeparation;

            var grid = gridBuf ?? new Dictionary<long, List<int>>(count);
            // 通常はリストを残して再利用する。長距離航行で過去セルが増え続けた場合だけ辞書を
            // まとめて解放し、毎回すべての過去キーを走査する負荷と保持メモリを制限する。
            int retainedCellLimit = Mathf.Max(MinimumRetainedGridCells, count * 4);
            if (grid.Count > retainedCellLimit) grid.Clear();
            else foreach (var kv in grid) kv.Value.Clear();

            // バケットへ投入。
            for (int i = 0; i < count; i++)
            {
                long key = CellKey(positions[i], cell);
                if (!grid.TryGetValue(key, out var bucket))
                {
                    bucket = new List<int>(4);
                    grid[key] = bucket;
                }
                bucket.Add(i);
            }

            // 各メンバについて自セル＋8近傍セルのメンバとだけ比較（i<j で1回）。
            for (int i = 0; i < count; i++)
            {
                Vector2 pa = positions[i];
                int cx = CellCoord(pa.x, cell);
                int cy = CellCoord(pa.y, cell);
                for (int gx = cx - 1; gx <= cx + 1; gx++)
                {
                    for (int gy = cy - 1; gy <= cy + 1; gy++)
                    {
                        if (!grid.TryGetValue(PackCell(gx, gy), out var bucket)) continue;
                        for (int b = 0; b < bucket.Count; b++)
                        {
                            int j = bucket[b];
                            if (j <= i) continue; // 各ペア1回だけ
                            Vector2 d = pa - positions[j];
                            float dsq = d.sqrMagnitude;
                            if (dsq >= minSq) continue;
                            Vector2 push;
                            if (dsq <= 1e-6f)
                            {
                                // 完全同位置でもスキップせず、ペア添字から決定論的な方向へ分離する。
                                push = CoincidentDirection(i, j) * (minSeparation * 0.5f * strength);
                            }
                            else
                            {
                                float dist = Mathf.Sqrt(dsq);
                                push = d / dist * ((minSeparation - dist) * 0.5f * strength);
                            }
                            if (i < displace.Length) displace[i] += push;
                            if (j < displace.Length) displace[j] -= push;
                        }
                    }
                }
            }
        }

        /// <summary>完全同位置ペアを分離する決定論的な8方向。乱数やフレーム順序に依存しない。</summary>
        public static Vector2 CoincidentDirection(int firstIndex, int secondIndex)
        {
            unchecked
            {
                int bucket = ((firstIndex * 73856093) ^ (secondIndex * 19349663)) & 7;
                const float diagonal = 0.70710678f;
                switch (bucket)
                {
                    case 0: return Vector2.right;
                    case 1: return new Vector2(diagonal, diagonal);
                    case 2: return Vector2.up;
                    case 3: return new Vector2(-diagonal, diagonal);
                    case 4: return Vector2.left;
                    case 5: return new Vector2(-diagonal, -diagonal);
                    case 6: return Vector2.down;
                    default: return new Vector2(diagonal, -diagonal);
                }
            }
        }

        private static int CellCoord(float v, float cell) => Mathf.FloorToInt(v / cell);
        private static long CellKey(Vector2 p, float cell) => PackCell(CellCoord(p.x, cell), CellCoord(p.y, cell));

        // 2つの int セル座標を1つの long キーへ（負値も衝突しないようオフセット）。
        private static long PackCell(int x, int y)
        {
            const long bias = 0x40000000L; // 2^30：±10億のセル座標まで衝突しない
            return ((x + bias) << 32) ^ (y + bias);
        }
    }
}
