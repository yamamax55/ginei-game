using NUnit.Framework;
using UnityEngine;

namespace Ginei.Tests
{
    /// <summary>EncircleRules（敵旗艦の包囲リング配置）のテスト。</summary>
    public class EncircleRulesTests
    {
        [Test]
        public void リング上の点は中心から指定半径の位置にある()
        {
            var center = new Vector2(5f, -3f);
            const float radius = 2.5f;
            for (int i = 0; i < 8; i++)
            {
                Vector2 p = EncircleRules.RingSlot(center, i, 8, radius);
                Assert.AreEqual(radius, Vector2.Distance(center, p), 1e-3f);
            }
        }

        [Test]
        public void 等間隔で配置される_最初の点は位相0で真右()
        {
            Vector2 p0 = EncircleRules.RingSlot(Vector2.zero, 0, 4, 1f);
            Assert.AreEqual(1f, p0.x, 1e-3f);
            Assert.AreEqual(0f, p0.y, 1e-3f);

            // 4分割の2番目は真上（90°）
            Vector2 p1 = EncircleRules.RingSlot(Vector2.zero, 1, 4, 1f);
            Assert.AreEqual(0f, p1.x, 1e-3f);
            Assert.AreEqual(1f, p1.y, 1e-3f);
        }

        [Test]
        public void 添字は隻数で巡回する_範囲外でも安全()
        {
            Vector2 a = EncircleRules.RingSlot(Vector2.zero, 1, 4, 1f);
            Vector2 b = EncircleRules.RingSlot(Vector2.zero, 5, 4, 1f); // 5 % 4 == 1
            Assert.AreEqual(a.x, b.x, 1e-3f);
            Assert.AreEqual(a.y, b.y, 1e-3f);
        }

        [Test]
        public void 異常入力に安全_total0や負の半径()
        {
            // total<1 は 1 扱い・負の半径は0扱い（中心に重なる）
            Vector2 p = EncircleRules.RingSlot(new Vector2(2f, 2f), 0, 0, -5f);
            Assert.AreEqual(2f, p.x, 1e-3f);
            Assert.AreEqual(2f, p.y, 1e-3f);
        }
    }
}
