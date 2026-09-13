using NUnit.Framework;
using UnityEngine;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 回廊要塞の<b>戦術マップ</b>の地形（#40 C-7）。
    /// #40 の「迂回不可」はここが本筋＝要塞の外周を回り込んで反対側へ抜けられないこと。
    /// 戦略グラフ側の別回廊による回り道は許す（<see cref="FortressBlockadeRulesTests"/>）。
    /// </summary>
    public class CorridorArenaRulesTests
    {
        private static CorridorArenaRules.CorridorArenaBounds B => CorridorArenaRules.CorridorArenaBounds.Default;

        // ── 幾何の不変条件（外周を回り込めない）──

        [Test]
        public void Default_LeavesNoGap_FortressSpansTheChannel()
        {
            Assert.IsTrue(CorridorArenaRules.LeavesNoGap(B));                       // 壁と要塞の間に隙間が無い
            Assert.IsTrue(CorridorArenaRules.BreakthroughImpossibleWhileHeld(B));   // ゆえに突破線へ届かない
        }

        [Test]
        public void NarrowFortress_LeavesGap_IsDetected()
        {
            // 要塞が細いと横をすり抜けられる＝設定ミスとして検出できること。
            var bad = new CorridorArenaRules.CorridorArenaBounds(18f, 60f, 18f, 5f, 52f);
            Assert.IsFalse(CorridorArenaRules.LeavesNoGap(bad));
            Assert.IsFalse(CorridorArenaRules.BreakthroughImpossibleWhileHeld(bad));
        }

        [Test]
        public void RequiredBypassOffset_ExceedsChannelHalfWidth()
        {
            // 回り込むのに要る横方向の距離が水路の半幅より大きい＝必ず岩壁にぶつかる。
            Assert.Greater(CorridorArenaRules.RequiredBypassOffset(B), B.channelHalfWidth - 0.0001f);
        }

        // ── 壁（航行不能領域）──

        [Test]
        public void Confine_ClampsToWalls_BothSides()
        {
            var b = B;
            Assert.AreEqual(b.channelHalfWidth, CorridorArenaRules.Confine(new Vector2(-50f, 999f), b, false).y, 1e-3f);
            Assert.AreEqual(-b.channelHalfWidth, CorridorArenaRules.Confine(new Vector2(-50f, -999f), b, false).y, 1e-3f);
        }

        [Test]
        public void Confine_ClampsToChannelEnds()
        {
            var b = B;
            Assert.AreEqual(-b.channelHalfLength, CorridorArenaRules.Confine(new Vector2(-999f, 0f), b, false).x, 1e-3f);
            Assert.AreEqual(b.channelHalfLength, CorridorArenaRules.Confine(new Vector2(999f, 0f), b, false).x, 1e-3f);
        }

        // ── 封鎖中は通り抜けられない（本命）──

        [Test]
        public void Confine_Blocked_StopsShortOfFortress()
        {
            var b = B;
            Vector2 p = CorridorArenaRules.Confine(new Vector2(999f, 0f), b, true);
            Assert.LessOrEqual(p.x, b.fortressX - b.fortressRadius + 1e-3f);
            Assert.IsFalse(CorridorArenaRules.IsBreakthrough(p, b));
        }

        [Test]
        public void Confine_Blocked_CannotSlipAroundAlongEitherWall()
        {
            var b = B;
            // 壁ぎりぎりを這って回り込もうとしても、封鎖線より先へは出られない。
            for (int i = 0; i <= 40; i++)
            {
                float y = Mathf.Lerp(-b.channelHalfWidth * 2f, b.channelHalfWidth * 2f, i / 40f);
                Vector2 p = CorridorArenaRules.Confine(new Vector2(b.channelHalfLength, y), b, true);
                Assert.LessOrEqual(p.x, b.fortressX - b.fortressRadius + 1e-3f, $"y={y} で回り込めてしまった");
                Assert.IsFalse(CorridorArenaRules.IsBreakthrough(p, b));
                Assert.LessOrEqual(Mathf.Abs(p.y), b.channelHalfWidth + 1e-3f);
            }
        }

        [Test]
        public void Confine_Blocked_RepeatedStepsNeverReachBreakthrough()
        {
            // 少しずつ前進を繰り返しても（毎フレームの移動を模擬）突破線へは到達しない。
            var b = B;
            Vector2 p = new Vector2(-b.channelHalfLength, 3f);
            for (int step = 0; step < 500; step++)
            {
                p += new Vector2(1.5f, 0.7f);                 // 斜めに押し込む
                p = CorridorArenaRules.Confine(p, b, true);
                Assert.IsFalse(CorridorArenaRules.IsBreakthrough(p, b), $"step={step} で突破された");
            }
        }

        // ── 要塞が落ちれば通れる（制圧後は通行状態が更新される）──

        [Test]
        public void Confine_NotBlocked_CanReachBreakthrough()
        {
            var b = B;
            Vector2 p = CorridorArenaRules.Confine(new Vector2(b.channelHalfLength, 0f), b, false);
            Assert.IsTrue(CorridorArenaRules.IsBreakthrough(p, b));
        }

        [Test]
        public void Confine_NotBlocked_StillPushedOutOfFortressBody()
        {
            // 封鎖が解けても要塞の<b>実体</b>にはめり込まない（味方でも）。押し出しは実体半径で行う。
            var b = B;
            Vector2 p = CorridorArenaRules.Confine(new Vector2(b.fortressX, 0f), b, false);
            Assert.GreaterOrEqual((p - new Vector2(b.fortressX, 0f)).magnitude, b.fortressBodyRadius - 1e-3f);
        }

        [Test]
        public void Confine_NotBlocked_CanActuallyTravelPastTheFortress()
        {
            // ★制圧後は<b>通り抜けられる</b>こと。封鎖半径をそのまま障害物にしていると、
            // 少しずつ前進しても要塞の手前へ押し戻され続け、占領が永久に成立しない（統合QAでの指摘）。
            var b = B;
            Vector2 p = new Vector2(-b.channelHalfLength, 0f);
            for (int step = 0; step < 500; step++)
            {
                p += new Vector2(1.5f, 0.4f);
                p = CorridorArenaRules.Confine(p, b, false);
                if (CorridorArenaRules.IsBreakthrough(p, b)) return;   // 到達できた
            }
            Assert.Fail($"制圧後なのに制圧線へ到達できない（最終位置 {p}）");
        }

        [Test]
        public void BodyRadius_IsSmallerThanChannel_SoShipsCanPass()
        {
            var b = B;
            Assert.Less(b.fortressBodyRadius, b.channelHalfWidth, "実体が水路を覆い切ると制圧後も通れない");
            Assert.Greater(b.fortressBodyRadius, 0f);
        }

        [Test]
        public void Confine_NotBlocked_NeverLeavesTheWalls()
        {
            var b = B;
            for (int i = 0; i <= 40; i++)
            {
                float y = Mathf.Lerp(-99f, 99f, i / 40f);
                Vector2 p = CorridorArenaRules.Confine(new Vector2(b.fortressX, y), b, false);
                Assert.LessOrEqual(Mathf.Abs(p.y), b.channelHalfWidth + 1e-3f);
            }
        }

        [Test]
        public void Confine_IsIdempotent()
        {
            // すでに地形内に収まっている点は動かさない（毎フレーム適用でも震えない）。
            var b = B;
            Vector2 once = CorridorArenaRules.Confine(new Vector2(-30f, 5f), b, true);
            Vector2 twice = CorridorArenaRules.Confine(once, b, true);
            Assert.AreEqual(once.x, twice.x, 1e-4f);
            Assert.AreEqual(once.y, twice.y, 1e-4f);
        }
    }
}
