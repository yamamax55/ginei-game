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
            // 再利用時は中身を空にする（リストは残して使い回し＝GC を増やさない）。
            foreach (var kv in grid) kv.Value.Clear();

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
                            if (dsq >= minSq || dsq <= 1e-6f) continue;
                            float dist = Mathf.Sqrt(dsq);
                            Vector2 push = d / dist * ((minSeparation - dist) * 0.5f * strength);
                            if (i < displace.Length) displace[i] += push;
                            if (j < displace.Length) displace[j] -= push;
                        }
                    }
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
