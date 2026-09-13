using NUnit.Framework;
using UnityEngine;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 戦略MAPの経路表示（<see cref="FleetRouteDisplayRules"/>）を固定する。
    /// MAP は表示専用になるので、<b>別々の経路が1本に見えない</b>こと（<see cref="FleetRouteDisplayRules.RouteKey"/>）と
    /// 矢じり・ラベルの幾何が既定パラメータの具体値で崩れないことを担保する。
    /// </summary>
    public class FleetRouteDisplayRulesTests
    {
        private const float Eps = 1e-4f;
        private static RouteDisplayParams P => RouteDisplayParams.Default;

        [Test]
        public void Default_HasFixedValues()
        {
            Assert.AreEqual(0.55f, P.arrowHeadLength, Eps);
            Assert.AreEqual(0.28f, P.arrowHeadHalfWidth, Eps);
            Assert.AreEqual(0.35f, P.labelOffset, Eps);
        }

        // ---------------- LabelPosition ----------------

        [Test]
        public void LabelPosition_IsMidpointOffsetAlongNormal()
        {
            Vector2 pos = FleetRouteDisplayRules.LabelPosition(new Vector2(0f, 0f), new Vector2(10f, 0f), P);
            Assert.AreEqual(5f, pos.x, Eps, "中点になっていない");
            Assert.AreEqual(0.35f, pos.y, Eps, "線から labelOffset だけ離れていない");
        }

        [Test]
        public void LabelPosition_OffsetDistanceIsLabelOffset_ForAnyDirection()
        {
            Vector2 from = new Vector2(-2f, 3f);
            Vector2 to = new Vector2(4f, 11f);           // 長さ10の斜め線
            Vector2 mid = new Vector2(1f, 7f);
            Vector2 pos = FleetRouteDisplayRules.LabelPosition(from, to, P);
            Assert.AreEqual(0.35f, (pos - mid).magnitude, Eps);
            // 線に垂直（進行方向との内積が0）
            Assert.AreEqual(0f, Vector2.Dot(pos - mid, (to - from).normalized), Eps);
        }

        [Test]
        public void LabelPosition_Degenerate_ReturnsSamePointWithoutThrowing()
        {
            Vector2 p = new Vector2(2f, -5f);
            Vector2 pos = FleetRouteDisplayRules.LabelPosition(p, p, P);
            Assert.AreEqual(2f, pos.x, Eps);
            Assert.AreEqual(-5f, pos.y, Eps);
        }

        // ---------------- ArrowHead ----------------

        [Test]
        public void ArrowHead_TipIsDestination_AndBaseIsBehindIt()
        {
            FleetRouteDisplayRules.ArrowHead(new Vector2(0f, 0f), new Vector2(10f, 0f), P,
                out Vector2 tip, out Vector2 left, out Vector2 right);
            Assert.AreEqual(10f, tip.x, Eps);
            Assert.AreEqual(0f, tip.y, Eps);
            Assert.AreEqual(10f - 0.55f, left.x, Eps, "矢じりが to の手前に置かれていない");
            Assert.AreEqual(10f - 0.55f, right.x, Eps);
            Assert.AreEqual(0.28f, left.y, Eps);
            Assert.AreEqual(-0.28f, right.y, Eps);
        }

        [Test]
        public void ArrowHead_IsSymmetricAndSized_ForDiagonal()
        {
            Vector2 from = new Vector2(1f, 1f);
            Vector2 to = new Vector2(4f, 5f);            // 長さ5
            FleetRouteDisplayRules.ArrowHead(from, to, P, out Vector2 tip, out Vector2 left, out Vector2 right);

            Vector2 baseMid = new Vector2((left.x + right.x) * 0.5f, (left.y + right.y) * 0.5f);
            Assert.AreEqual(0.55f, (tip - baseMid).magnitude, Eps, "矢じりの長さが違う");
            Assert.AreEqual(0.28f, (left - baseMid).magnitude, Eps, "左の半幅が違う");
            Assert.AreEqual(0.28f, (right - baseMid).magnitude, Eps, "右の半幅が違う");
            // 底辺は進行方向に垂直＝左右対称
            Assert.AreEqual(0f, Vector2.Dot(left - right, (to - from).normalized), Eps);
            // 底辺の中心は tip から from 側へ後退している
            Assert.Less((baseMid - from).magnitude, (tip - from).magnitude);
        }

        [Test]
        public void ArrowHead_Degenerate_CollapsesToPointWithoutThrowing()
        {
            Vector2 p = new Vector2(-3f, 7f);
            FleetRouteDisplayRules.ArrowHead(p, p, P, out Vector2 tip, out Vector2 left, out Vector2 right);
            Assert.AreEqual(0f, (tip - p).magnitude, Eps);
            Assert.AreEqual(0f, (left - p).magnitude, Eps);
            Assert.AreEqual(0f, (right - p).magnitude, Eps);
        }

        // ---------------- HeadingText ----------------

        [Test]
        public void HeadingText_DelegatesToClusterHeadingLabel()
        {
            Assert.AreEqual("東へ", FleetRouteDisplayRules.HeadingText(new Vector2(0f, 0f), new Vector2(5f, 0f)));
            Assert.AreEqual("北東へ", FleetRouteDisplayRules.HeadingText(new Vector2(0f, 0f), new Vector2(3f, 3f)));
            Assert.AreEqual("北へ", FleetRouteDisplayRules.HeadingText(new Vector2(0f, 0f), new Vector2(0f, 2f)));
            Assert.AreEqual("南西へ", FleetRouteDisplayRules.HeadingText(new Vector2(1f, 1f), new Vector2(-1f, -1f)));
        }

        [Test]
        public void HeadingText_Degenerate_IsEmpty()
        {
            Vector2 p = new Vector2(4f, 4f);
            Assert.AreEqual("", FleetRouteDisplayRules.HeadingText(p, p));
        }

        // ---------------- RouteKey ----------------

        [Test]
        public void RouteKey_SameFourElements_AreEqual()
        {
            Assert.AreEqual(FleetRouteDisplayRules.RouteKey(1, 2, 3, Faction.同盟),
                            FleetRouteDisplayRules.RouteKey(1, 2, 3, Faction.同盟));
        }

        [Test]
        public void RouteKey_AnyDifferingElement_MakesDifferentKey()
        {
            long baseKey = FleetRouteDisplayRules.RouteKey(1, 2, 3, Faction.同盟);
            Assert.AreNotEqual(baseKey, FleetRouteDisplayRules.RouteKey(9, 2, 3, Faction.同盟), "出発元の違いが潰れた");
            Assert.AreNotEqual(baseKey, FleetRouteDisplayRules.RouteKey(1, 9, 3, Faction.同盟), "現在区間の違いが潰れた");
            Assert.AreNotEqual(baseKey, FleetRouteDisplayRules.RouteKey(1, 2, 9, Faction.同盟), "最終目的地の違いが潰れた");
            Assert.AreNotEqual(baseKey, FleetRouteDisplayRules.RouteKey(1, 2, 3, Faction.帝国), "勢力の違いが潰れた");
        }

        [Test]
        public void RouteKey_ReversedRoute_DoesNotCollide()
        {
            Assert.AreNotEqual(FleetRouteDisplayRules.RouteKey(1, 2, 3, Faction.同盟),
                               FleetRouteDisplayRules.RouteKey(3, 2, 1, Faction.同盟));
            Assert.AreNotEqual(FleetRouteDisplayRules.RouteKey(1, 2, 3, Faction.同盟),
                               FleetRouteDisplayRules.RouteKey(2, 1, 3, Faction.同盟));
            Assert.AreNotEqual(FleetRouteDisplayRules.RouteKey(1, 2, 3, Faction.同盟),
                               FleetRouteDisplayRules.RouteKey(1, 3, 2, Faction.同盟));
        }

        [Test]
        public void RouteKey_ManyCombinations_AreAllDistinct()
        {
            var seen = new System.Collections.Generic.HashSet<long>();
            for (int a = 0; a < 6; a++)
                for (int b = 0; b < 6; b++)
                    for (int c = 0; c < 6; c++)
                    {
                        Assert.IsTrue(seen.Add(FleetRouteDisplayRules.RouteKey(a, b, c, Faction.同盟)),
                            $"キーが衝突した ({a},{b},{c})");
                        Assert.IsTrue(seen.Add(FleetRouteDisplayRules.RouteKey(a, b, c, Faction.帝国)),
                            $"勢力違いのキーが衝突した ({a},{b},{c})");
                    }
        }

        // ---------------- RouteText ----------------

        [Test]
        public void RouteText_NoVia_HasNoParenthesis()
        {
            Assert.AreEqual("モンブラン → ローガン",
                FleetRouteDisplayRules.RouteText("モンブラン", "ローガン", 0));
            Assert.AreEqual("モンブラン → ローガン",
                FleetRouteDisplayRules.RouteText("モンブラン", "ローガン", -1));
        }

        [Test]
        public void RouteText_WithVia_ShowsCount()
        {
            Assert.AreEqual("モンブラン → ローガン（経由 1 星系）",
                FleetRouteDisplayRules.RouteText("モンブラン", "ローガン", 1));
            Assert.AreEqual("モンブラン → ローガン（経由 3 星系）",
                FleetRouteDisplayRules.RouteText("モンブラン", "ローガン", 3));
        }

        [Test]
        public void RouteText_NullNames_AreTreatedAsEmpty()
        {
            Assert.AreEqual(" → ", FleetRouteDisplayRules.RouteText(null, null, 0));
        }
    }
}
