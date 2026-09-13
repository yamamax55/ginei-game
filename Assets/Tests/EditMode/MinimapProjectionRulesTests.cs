using NUnit.Framework;
using UnityEngine;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 戦術ミニマップの写像（<see cref="MinimapProjectionRules"/>）。
    /// 実機で見つかった3つの症状に対応する不変条件を固定する：
    /// ①原点がずれると全部が端へ貼り付く ②縦横が独立に伸びて戦場の形が歪む
    /// ③視界枠が中心しか合わず端やズームで破綻する。
    /// </summary>
    public class MinimapProjectionRulesTests
    {
        private static readonly Vector2 Min = new Vector2(-60f, -18f);   // 回廊アリーナ（120×36）
        private static readonly Vector2 Max = new Vector2(60f, 18f);
        private const float PanelW = 200f;
        private const float PanelH = 200f;

        private static MinimapProjectionRules.Fit FitOf(Vector2 min, Vector2 max)
            => MinimapProjectionRules.FitPreserveAspect(min, max, PanelW, PanelH);

        // ── アスペクト保持 ──

        [Test]
        public void Fit_PreservesAspect_AndFitsInsidePanel()
        {
            MinimapProjectionRules.Fit fit = FitOf(Min, Max);

            float worldAspect = (Max.x - Min.x) / (Max.y - Min.y);   // 120/36
            Assert.AreEqual(worldAspect, fit.width / fit.height, 1e-3f, "戦場の縦横比が保たれていない");
            Assert.LessOrEqual(fit.width, PanelW + 1e-3f);
            Assert.LessOrEqual(fit.height, PanelH + 1e-3f);
            // 横長なので幅がパネル一杯・高さが余る（レターボックス）。
            Assert.AreEqual(PanelW, fit.width, 1e-3f);
            Assert.Less(fit.height, PanelH);
        }

        [Test]
        public void Fit_TallField_LeavesSideMargins()
        {
            var min = new Vector2(-10f, -100f);
            var max = new Vector2(10f, 100f);
            MinimapProjectionRules.Fit fit = FitOf(min, max);
            Assert.AreEqual(PanelH, fit.height, 1e-3f);
            Assert.Less(fit.width, PanelW);
        }

        [Test]
        public void Fit_DegenerateSpan_DoesNotDivideByZero()
        {
            var p = new Vector2(5f, 5f);
            Assert.DoesNotThrow(() =>
            {
                MinimapProjectionRules.Fit fit = FitOf(p, p);
                Assert.Greater(fit.width, 0f);
                Assert.Greater(fit.height, 0f);
            });
        }

        // ── 写像の往復（クリックした点にカメラが行く） ──

        [Test]
        public void RoundTrip_AtOrigin_ReturnsSamePoint()
        {
            MinimapProjectionRules.Fit fit = FitOf(Min, Max);
            var world = new Vector2(23f, -7f);
            Vector2 local = MinimapProjectionRules.WorldToLocal(world, Min, Max, fit);
            Vector2 back = MinimapProjectionRules.LocalToWorld(local, Min, Max, fit);
            Assert.AreEqual(world.x, back.x, 1e-3f);
            Assert.AreEqual(world.y, back.y, 1e-3f);
        }

        /// <summary>
        /// ★遠方オフセット（ウィンドウ化会戦の戦場原点）でも往復が一致すること。
        /// 従来は境界だけ原点なし・座標は原点ありで突き合わせていたため、ここが壊れていた。
        /// </summary>
        [Test]
        public void RoundTrip_WithDistantOrigin_ReturnsSamePoint()
        {
            var origin = new Vector2(100000f, -50000f);
            Vector2 min = Min, max = Max;
            MinimapProjectionRules.Offset(ref min, ref max, origin);
            MinimapProjectionRules.Fit fit = FitOf(min, max);

            Vector2 world = new Vector2(23f, -7f) + origin;
            Vector2 local = MinimapProjectionRules.WorldToLocal(world, min, max, fit);
            Vector2 back = MinimapProjectionRules.LocalToWorld(local, min, max, fit);
            Assert.AreEqual(world.x, back.x, 0.05f);
            Assert.AreEqual(world.y, back.y, 0.05f);
        }

        /// <summary>原点をずらしても、戦場内の同じ相対位置は<b>ミニマップ上の同じ点</b>に出る。</summary>
        [Test]
        public void SameRelativePosition_MapsToSameLocal_RegardlessOfOrigin()
        {
            MinimapProjectionRules.Fit fitA = FitOf(Min, Max);
            Vector2 localA = MinimapProjectionRules.WorldToLocal(new Vector2(30f, 9f), Min, Max, fitA);

            var origin = new Vector2(-77777f, 12345f);
            Vector2 min = Min, max = Max;
            MinimapProjectionRules.Offset(ref min, ref max, origin);
            MinimapProjectionRules.Fit fitB = FitOf(min, max);
            Vector2 localB = MinimapProjectionRules.WorldToLocal(new Vector2(30f, 9f) + origin, min, max, fitB);

            Assert.AreEqual(localA.x, localB.x, 0.05f, "原点が違うだけで別の場所へ写っている");
            Assert.AreEqual(localA.y, localB.y, 0.05f);
        }

        [Test]
        public void Corners_MapToCornersOfFit()
        {
            MinimapProjectionRules.Fit fit = FitOf(Min, Max);
            Vector2 bl = MinimapProjectionRules.WorldToLocal(Min, Min, Max, fit);
            Vector2 tr = MinimapProjectionRules.WorldToLocal(Max, Min, Max, fit);
            Assert.AreEqual(-fit.width * 0.5f, bl.x, 1e-3f);
            Assert.AreEqual(-fit.height * 0.5f, bl.y, 1e-3f);
            Assert.AreEqual(fit.width * 0.5f, tr.x, 1e-3f);
            Assert.AreEqual(fit.height * 0.5f, tr.y, 1e-3f);
        }

        [Test]
        public void Center_MapsToCenter()
        {
            MinimapProjectionRules.Fit fit = FitOf(Min, Max);
            Vector2 mid = (Min + Max) * 0.5f;
            Vector2 local = MinimapProjectionRules.WorldToLocal(mid, Min, Max, fit);
            Assert.AreEqual(0f, local.x, 1e-3f);
            Assert.AreEqual(0f, local.y, 1e-3f);
        }

        /// <summary>戦場の外の点は<b>外へ</b>写る（従来の Clamp01 は全部を辺へ貼り付けていた）。</summary>
        [Test]
        public void OutsidePoint_MapsOutside_NotClampedToEdge()
        {
            MinimapProjectionRules.Fit fit = FitOf(Min, Max);
            Vector2 local = MinimapProjectionRules.WorldToLocal(new Vector2(600f, 0f), Min, Max, fit);
            Assert.Greater(local.x, fit.width * 0.5f, "外の点が縁へ丸められている");
            Assert.IsFalse(MinimapProjectionRules.Contains(new Vector2(600f, 0f), Min, Max));
            Assert.IsTrue(MinimapProjectionRules.Contains(new Vector2(10f, 3f), Min, Max));
        }

        // ── 視界枠＝カメラ視界と戦場の交差 ──

        [Test]
        public void Viewport_Inside_IsCameraRect()
        {
            Assert.IsTrue(MinimapProjectionRules.TryViewportIntersection(
                Vector2.zero, 10f, 5f, Min, Max, out Vector2 vMin, out Vector2 vMax));
            Assert.AreEqual(-10f, vMin.x, 1e-3f);
            Assert.AreEqual(10f, vMax.x, 1e-3f);
            Assert.AreEqual(-5f, vMin.y, 1e-3f);
            Assert.AreEqual(5f, vMax.y, 1e-3f);
        }

        /// <summary>引きすぎ（視界が戦場より大きい）なら、枠は<b>戦場全体</b>で頭打ちになる。</summary>
        [Test]
        public void Viewport_ZoomedOut_ClampsToField()
        {
            Assert.IsTrue(MinimapProjectionRules.TryViewportIntersection(
                Vector2.zero, 500f, 500f, Min, Max, out Vector2 vMin, out Vector2 vMax));
            Assert.AreEqual(Min.x, vMin.x, 1e-3f);
            Assert.AreEqual(Max.x, vMax.x, 1e-3f);
            Assert.AreEqual(Min.y, vMin.y, 1e-3f);
            Assert.AreEqual(Max.y, vMax.y, 1e-3f);
        }

        /// <summary>端に寄ると枠は<b>半分だけ</b>になる（中心だけ丸めていた頃は はみ出していた）。</summary>
        [Test]
        public void Viewport_AtEdge_IsHalved()
        {
            Assert.IsTrue(MinimapProjectionRules.TryViewportIntersection(
                new Vector2(Max.x, 0f), 10f, 5f, Min, Max, out Vector2 vMin, out Vector2 vMax));
            Assert.AreEqual(Max.x - 10f, vMin.x, 1e-3f);
            Assert.AreEqual(Max.x, vMax.x, 1e-3f, "戦場の外へはみ出している");
        }

        [Test]
        public void Viewport_CompletelyOutside_ReturnsFalse()
        {
            Assert.IsFalse(MinimapProjectionRules.TryViewportIntersection(
                new Vector2(10000f, 0f), 10f, 5f, Min, Max, out _, out _));
        }

        [Test]
        public void PixelsPerWorld_IsSameForBothAxes()
        {
            MinimapProjectionRules.Fit fit = FitOf(Min, Max);
            float px = MinimapProjectionRules.PixelsPerWorld(Min, Max, fit);
            float py = fit.height / (Max.y - Min.y);
            Assert.AreEqual(px, py, 1e-3f, "縦横で倍率が違う＝アスペクトが崩れている");
        }
    }
}
