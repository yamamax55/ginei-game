using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// イージング窓口の不変量テスト（DEV効率化①）。端点（0→0,1→1）・クランプ・単調性・
    /// オーバーシュート（Back/Elastic は中間で1超）を固定し、曲線を壊す変更を検出する。
    /// </summary>
    public class EasingTests
    {
        private const float Eps = 1e-4f;

        [Test]
        public void Endpoints_AreZeroAndOne()
        {
            System.Func<float, float>[] eases =
            {
                Easing.Linear, Easing.SmoothStep, Easing.InQuad, Easing.OutQuad,
                Easing.InCubic, Easing.OutCubic, Easing.InOutCubic,
                t => Easing.OutBack(t), Easing.OutElastic, Easing.OutBounce
            };
            foreach (var e in eases)
            {
                Assert.AreEqual(0f, e(0f), Eps, "t=0 で 0");
                Assert.AreEqual(1f, e(1f), Eps, "t=1 で 1");
            }
        }

        [Test]
        public void Clamps_OutOfRangeInput()
        {
            Assert.AreEqual(0f, Easing.SmoothStep(-5f), Eps);
            Assert.AreEqual(1f, Easing.SmoothStep(5f), Eps);
            Assert.AreEqual(0f, Easing.OutCubic(-1f), Eps);
            Assert.AreEqual(1f, Easing.OutCubic(2f), Eps);
        }

        [Test]
        public void Monotonic_BasicEases()
        {
            System.Func<float, float>[] eases =
            {
                Easing.SmoothStep, Easing.InQuad, Easing.OutQuad,
                Easing.InCubic, Easing.OutCubic, Easing.InOutCubic
            };
            foreach (var e in eases)
            {
                float prev = e(0f);
                for (int i = 1; i <= 20; i++)
                {
                    float v = e(i / 20f);
                    Assert.GreaterOrEqual(v + Eps, prev, "基本イージングは単調非減少");
                    prev = v;
                }
            }
        }

        [Test]
        public void OutBack_Overshoots_PastOne()
        {
            // ポップの肝＝終端手前で 1 を超える。
            bool over = false;
            for (int i = 1; i < 20; i++)
                if (Easing.OutBack(i / 20f) > 1f + Eps) { over = true; break; }
            Assert.IsTrue(over, "OutBack は中間で 1 を超える（行き過ぎ）");
        }

        [Test]
        public void Pulse_IsZeroAtEnds_PeaksAtMiddle()
        {
            Assert.AreEqual(0f, Easing.Pulse(0f), Eps);
            Assert.AreEqual(0f, Easing.Pulse(1f), Eps);
            Assert.AreEqual(1f, Easing.Pulse(0.5f), Eps, "中央で最大1");
            Assert.Greater(Easing.Pulse(0.5f), Easing.Pulse(0.1f));
        }

        [Test]
        public void SmoothStep_HalfIsHalf()
        {
            Assert.AreEqual(0.5f, Easing.SmoothStep(0.5f), Eps, "対称点 0.5 は 0.5");
        }
    }
}
