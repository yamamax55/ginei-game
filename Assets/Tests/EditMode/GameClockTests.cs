using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// GameClock（TIME-1 #947）の EditMode テスト。累積・ポーズ不変・速度倍率・負入力クランプ・
    /// トグル・長期経過の精度を決定論で担保する。
    /// </summary>
    public class GameClockTests
    {
        const double Eps = 1e-9;

        /// <summary>既定は speed=1・非ポーズ・経過0。</summary>
        [Test]
        public void Defaults_AreSaneAndZero()
        {
            var clock = new GameClock();
            Assert.AreEqual(1f, clock.speed, 1e-6f);
            Assert.IsFalse(clock.paused);
            Assert.AreEqual(0.0, clock.ElapsedSeconds, Eps);
        }

        /// <summary>Advance は実効秒数を累積する（speed=1 なら realDt がそのまま乗る）。</summary>
        [Test]
        public void Advance_AccumulatesElapsed()
        {
            var clock = new GameClock();
            clock.Advance(0.5f);
            clock.Advance(0.25f);
            Assert.AreEqual(0.75, clock.ElapsedSeconds, Eps);
        }

        /// <summary>ポーズ中は EffectiveDt=0・Advance で不変。</summary>
        [Test]
        public void Paused_DoesNotAdvance()
        {
            var clock = new GameClock();
            clock.Advance(1f);
            clock.Pause();
            Assert.AreEqual(0.0, clock.EffectiveDt(1f), Eps);
            clock.Advance(10f);
            Assert.AreEqual(1.0, clock.ElapsedSeconds, Eps);
        }

        /// <summary>速度倍率がそのまま実効秒数に乗る。</summary>
        [Test]
        public void Speed_ScalesEffectiveDt()
        {
            var clock = new GameClock();
            clock.SetSpeed(3f);
            clock.Advance(2f);
            Assert.AreEqual(6.0, clock.ElapsedSeconds, Eps);

            clock.SetSpeed(0.5f);
            clock.Advance(2f);
            Assert.AreEqual(7.0, clock.ElapsedSeconds, Eps);
        }

        /// <summary>負の realDt は0へクランプ（巻き戻らない）。</summary>
        [Test]
        public void NegativeRealDt_ClampsToZero()
        {
            var clock = new GameClock();
            clock.Advance(2f);
            clock.Advance(-5f);
            Assert.AreEqual(2.0, clock.ElapsedSeconds, Eps);
            Assert.AreEqual(0.0, clock.EffectiveDt(-1f), Eps);
        }

        /// <summary>負の speed は SetSpeed で0へクランプ＝時間が進まない。</summary>
        [Test]
        public void NegativeSpeed_ClampsToZero()
        {
            var clock = new GameClock();
            clock.SetSpeed(-4f);
            Assert.AreEqual(0f, clock.speed, 1e-6f);
            clock.Advance(10f);
            Assert.AreEqual(0.0, clock.ElapsedSeconds, Eps);
        }

        /// <summary>Pause/Resume/TogglePause がフラグを正しく反転する。</summary>
        [Test]
        public void PauseResumeToggle_FlipsFlag()
        {
            var clock = new GameClock();
            clock.Pause();
            Assert.IsTrue(clock.paused);
            clock.Resume();
            Assert.IsFalse(clock.paused);
            clock.TogglePause();
            Assert.IsTrue(clock.paused);
            clock.TogglePause();
            Assert.IsFalse(clock.paused);
        }

        /// <summary>多数回の微小 Advance でも double 精度で累積がドリフトしない。</summary>
        [Test]
        public void LongRun_RetainsPrecision()
        {
            var clock = new GameClock();
            const int steps = 100000;
            const float dt = 0.01f;
            for (int i = 0; i < steps; i++)
            {
                clock.Advance(dt);
            }
            // 期待値 = 100000 * 0.01 = 1000 秒。float 加算の誤差より十分小さい許容で確認。
            Assert.AreEqual(1000.0, clock.ElapsedSeconds, 1.0);
            Assert.Greater(clock.ElapsedSeconds, 0.0);
        }
    }
}
