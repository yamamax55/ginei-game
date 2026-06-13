using NUnit.Framework;
using System.Collections.Generic;
using UnityEngine;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>配下艦の分離（EMOV-5）：グリッド版の押し離しが総当りと同値で、近傍だけ比較する。</summary>
    public class SeparationResolveRulesTests
    {
        // 総当り（O(n²)）の参照実装。グリッド版と一致することを担保する。
        private static Vector2[] Brute(IList<Vector2> p, int n, float minSep, float strength)
        {
            var d = new Vector2[n];
            float minSq = minSep * minSep;
            for (int i = 0; i < n; i++)
                for (int j = i + 1; j < n; j++)
                {
                    Vector2 diff = p[i] - p[j];
                    float dsq = diff.sqrMagnitude;
                    if (dsq >= minSq || dsq <= 1e-6f) continue;
                    float dist = Mathf.Sqrt(dsq);
                    Vector2 push = diff / dist * ((minSep - dist) * 0.5f * strength);
                    d[i] += push;
                    d[j] -= push;
                }
            return d;
        }

        [Test]
        public void Resolve_TwoOverlapping_PushApart()
        {
            var p = new List<Vector2> { new Vector2(0, 0), new Vector2(0.3f, 0) };
            var d = SeparationResolveRules.Resolve(p, 2, 0.6f, 1f);
            Assert.Less(d[0].x, 0f);    // member0 は -x（離れる）
            Assert.Greater(d[1].x, 0f); // member1 は +x（離れる）
            Assert.AreEqual(0.15f, Mathf.Abs(d[0].x), 1e-4f);
        }

        [Test]
        public void Resolve_FarApart_NoDisplacement()
        {
            var p = new List<Vector2> { new Vector2(0, 0), new Vector2(5, 0) };
            var d = SeparationResolveRules.Resolve(p, 2, 0.6f, 1f);
            Assert.AreEqual(0f, d[0].magnitude, 1e-5f);
            Assert.AreEqual(0f, d[1].magnitude, 1e-5f);
        }

        [Test]
        public void Resolve_GridMatchesBruteForce()
        {
            // 決定論的な擬似乱数で密な点群をつくり、グリッド版＝総当りを確認。
            int n = 120;
            var p = new List<Vector2>(n);
            uint s = 12345u;
            for (int i = 0; i < n; i++)
            {
                s = s * 1664525u + 1013904223u; float x = (s % 1000) / 100f; // 0..10
                s = s * 1664525u + 1013904223u; float y = (s % 1000) / 100f;
                p.Add(new Vector2(x, y));
            }
            float minSep = 0.6f, strength = 0.5f;
            var grid = SeparationResolveRules.Resolve(p, n, minSep, strength);
            var brute = Brute(p, n, minSep, strength);
            for (int i = 0; i < n; i++)
            {
                Assert.AreEqual(brute[i].x, grid[i].x, 1e-3f, $"x mismatch at {i}");
                Assert.AreEqual(brute[i].y, grid[i].y, 1e-3f, $"y mismatch at {i}");
            }
        }

        [Test]
        public void Resolve_BufferReuse_MatchesFresh()
        {
            var p = new List<Vector2> { new Vector2(0, 0), new Vector2(0.2f, 0), new Vector2(0.4f, 0) };
            var displace = new Vector2[3];
            var gridBuf = new Dictionary<long, List<int>>();
            SeparationResolveRules.Resolve(p, 3, 0.6f, 1f, displace, gridBuf);
            var fresh = SeparationResolveRules.Resolve(p, 3, 0.6f, 1f);
            // 2回目（バッファ再利用）でも同じ結果＝クリアが効いている。
            SeparationResolveRules.Resolve(p, 3, 0.6f, 1f, displace, gridBuf);
            for (int i = 0; i < 3; i++)
            {
                Assert.AreEqual(fresh[i].x, displace[i].x, 1e-4f);
                Assert.AreEqual(fresh[i].y, displace[i].y, 1e-4f);
            }
        }
    }
}
