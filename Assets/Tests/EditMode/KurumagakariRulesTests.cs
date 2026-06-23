using NUnit.Framework;
using UnityEngine;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>車懸かり（旋回突撃）の運動ロジック：旋回角の進行とスロット回転。</summary>
    public class KurumagakariRulesTests
    {
        [Test]
        public void AdvanceAngle_Accumulates_FrameRateIndependent()
        {
            // 30度/秒 × 1秒 = 30度
            Assert.AreEqual(30f, KurumagakariRules.AdvanceAngle(0f, 30f, 1f), 1e-4f);
            // 0.5秒ずつ2回でも同じ（フレームレート非依存）
            float a = KurumagakariRules.AdvanceAngle(0f, 30f, 0.5f);
            a = KurumagakariRules.AdvanceAngle(a, 30f, 0.5f);
            Assert.AreEqual(30f, a, 1e-4f);
        }

        [Test]
        public void AdvanceAngle_Wraps_0_360()
        {
            // 350度 + 20度 = 370 → 10度にラップ
            Assert.AreEqual(10f, KurumagakariRules.AdvanceAngle(350f, 20f, 1f), 1e-4f);
            // 負の速度（逆旋回）も正にラップ
            float a = KurumagakariRules.AdvanceAngle(0f, -30f, 1f);
            Assert.AreEqual(330f, a, 1e-4f);
        }

        [Test]
        public void AdvanceAngle_ZeroDt_NoChange_ButWraps()
        {
            Assert.AreEqual(45f, KurumagakariRules.AdvanceAngle(45f, 30f, 0f), 1e-4f);
            Assert.AreEqual(10f, KurumagakariRules.AdvanceAngle(370f, 30f, 0f), 1e-4f); // 既存値もラップ
        }

        [Test]
        public void RotateLocalSlot_PreservesMagnitude()
        {
            Vector2 slot = new Vector2(3f, 4f); // magnitude 5
            Vector2 r = KurumagakariRules.RotateLocalSlot(slot, 123.456f);
            Assert.AreEqual(5f, r.magnitude, 1e-3f);
        }

        [Test]
        public void RotateLocalSlot_90Degrees()
        {
            // (1,0) を90度回すと (0,1)（反時計回り）
            Vector2 r = KurumagakariRules.RotateLocalSlot(new Vector2(1f, 0f), 90f);
            Assert.AreEqual(0f, r.x, 1e-4f);
            Assert.AreEqual(1f, r.y, 1e-4f);
        }

        [Test]
        public void RotateLocalSlot_ZeroAngle_Identity()
        {
            Vector2 slot = new Vector2(-2f, 7f);
            Vector2 r = KurumagakariRules.RotateLocalSlot(slot, 0f);
            Assert.AreEqual(slot.x, r.x, 1e-4f);
            Assert.AreEqual(slot.y, r.y, 1e-4f);
        }
    }
}
