using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>暦境界カウント（CalendarTick）と暦粒度ディスパッチャ（CalendarDispatcher）の EditMode テスト（TIME-6 #952）。</summary>
    public class CalendarDispatcherTests
    {
        // 既定＝60秒/日・30日/月・12月/年。1日=60s, 1月=1800s, 1年=21600s。
        private static GameDate.DateParams P => GameDate.DateParams.Default;

        // ───────── CalendarTick（純カウント）─────────

        [Test]
        public void TotalDays_FloorsBySecondsPerDay()
        {
            Assert.AreEqual(0L, CalendarTick.TotalDays(0d, P));
            Assert.AreEqual(0L, CalendarTick.TotalDays(59d, P));
            Assert.AreEqual(1L, CalendarTick.TotalDays(60d, P));
            Assert.AreEqual(2L, CalendarTick.TotalDays(125d, P));
            // 負秒は0
            Assert.AreEqual(0L, CalendarTick.TotalDays(-100d, P));
        }

        [Test]
        public void TotalMonths_And_Years()
        {
            Assert.AreEqual(1L, CalendarTick.TotalMonths(30 * 60d, P));  // 30日=1月
            Assert.AreEqual(0L, CalendarTick.TotalMonths(29 * 60d, P));
            Assert.AreEqual(1L, CalendarTick.TotalYears(360 * 60d, P));  // 360日=1年
            Assert.AreEqual(0L, CalendarTick.TotalYears(359 * 60d, P));
        }

        [Test]
        public void DayBoundaries_CountsCrossings()
        {
            // 0.5日→2.5日 = 日境界2回（1日目・2日目の頭）
            Assert.AreEqual(2L, CalendarTick.DayBoundaries(30d, 150d, P));
            // 同一日内は0
            Assert.AreEqual(0L, CalendarTick.DayBoundaries(10d, 50d, P));
            // 巻き戻りは0
            Assert.AreEqual(0L, CalendarTick.DayBoundaries(150d, 30d, P));
        }

        [Test]
        public void MonthAndYearBoundaries_CountsCrossings()
        {
            // 29日目→31日目 = 月境界1回（30日で1月跨ぎ）
            Assert.AreEqual(1L, CalendarTick.MonthBoundaries(29 * 60d, 31 * 60d, P));
            // 359日目→361日目 = 年境界1回 かつ 月境界1回
            Assert.AreEqual(1L, CalendarTick.YearBoundaries(359 * 60d, 361 * 60d, P));
            Assert.AreEqual(1L, CalendarTick.MonthBoundaries(359 * 60d, 361 * 60d, P));
        }

        // ───────── CalendarDispatcher（発火）─────────

        [Test]
        public void Advance_FiresOncePerDayBoundary()
        {
            var d = new CalendarDispatcher(P, 0d);
            int days = 0, months = 0, years = 0;
            // 0→90秒（1.5日）＝日境界1回
            d.Advance(90d, () => days++, () => months++, () => years++);
            Assert.AreEqual(1, days);
            Assert.AreEqual(0, months);
            Assert.AreEqual(0, years);
            Assert.AreEqual(90d, d.LastElapsed);
        }

        [Test]
        public void Advance_Accumulates_AcrossCalls()
        {
            var d = new CalendarDispatcher(P, 0d);
            int days = 0;
            d.Advance(60d, () => days++);    // 1日境界
            d.Advance(120d, () => days++);   // もう1日境界
            d.Advance(150d, () => days++);   // 同一日内＝発火なし
            Assert.AreEqual(2, days);
        }

        [Test]
        public void Advance_FiresDayMonthYear_Independently_OnYearRollover()
        {
            var d = new CalendarDispatcher(P, 359 * 60d); // 359日目（年末直前）
            int days = 0, months = 0, years = 0;
            d.Advance(360 * 60d, () => days++, () => months++, () => years++); // 360日目＝翌年頭
            // 日・月・年いずれも1回ずつ（境界は重なる）
            Assert.AreEqual(1, days);
            Assert.AreEqual(1, months);
            Assert.AreEqual(1, years);
        }

        [Test]
        public void Advance_PausedOrRewind_FiresNothing()
        {
            var d = new CalendarDispatcher(P, 100d);
            int days = 0;
            d.Advance(100d, () => days++);  // 停止（同値）
            d.Advance(50d, () => days++);   // 巻き戻り
            Assert.AreEqual(0, days);
            Assert.AreEqual(50d, d.LastElapsed); // 巻き戻りは基準同期
        }

        [Test]
        public void Reset_SyncsWithoutFiring()
        {
            var d = new CalendarDispatcher(P, 0d);
            int days = 0;
            d.Reset(1000d);                  // 基準だけ進める（発火なし）
            d.Advance(1000d, () => days++);  // 同値＝発火なし
            Assert.AreEqual(0, days);
            d.Advance(1060d, () => days++);  // +1日＝1回
            Assert.AreEqual(1, days);
        }

        [Test]
        public void Advance_SafetyCap_LimitsFireCount()
        {
            var d = new CalendarDispatcher(P, 0d);
            int days = 0;
            // 巨大経過（百万日相当）でも上限でクランプ＝暴走しない
            d.Advance(1_000_000 * 60d, () => days++);
            Assert.AreEqual(CalendarDispatcher.MaxFirePerAdvance, days);
        }

        [Test]
        public void NullCallbacks_DoNotThrow()
        {
            var d = new CalendarDispatcher(P, 0d);
            Assert.DoesNotThrow(() => d.Advance(100000d)); // 全 null でも安全
        }
    }
}
