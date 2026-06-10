using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>GameDate（game-seconds → 宇宙暦/帝国暦の純変換 TIME-2 #948）の EditMode テスト。</summary>
    public class GameDateTests
    {
        // 既定＝60秒/日・30日/月・12月/年・offset0
        private static GameDate.DateParams P => GameDate.DateParams.Default;

        [Test]
        public void FromSeconds_Zero_ReturnsStartYearMonth1Day1()
        {
            // 0秒＝開始年の1月1日
            var d = GameDate.FromSeconds(0d, 798, P);
            Assert.AreEqual(798, d.year);
            Assert.AreEqual(1, d.month);
            Assert.AreEqual(1, d.day);
        }

        [Test]
        public void FromSeconds_OneDayBoundary_RollsToNextDay()
        {
            // 60秒で1日跨ぎ＝2日目。直前（59秒）は1日目。
            var before = GameDate.FromSeconds(59d, 798, P);
            Assert.AreEqual(1, before.day);

            var after = GameDate.FromSeconds(60d, 798, P);
            Assert.AreEqual(798, after.year);
            Assert.AreEqual(1, after.month);
            Assert.AreEqual(2, after.day);
        }

        [Test]
        public void FromSeconds_MonthBoundary_30Days_RollsToNextMonth()
        {
            // 30日＝1800秒で月跨ぎ＝2月1日。直前（29日目）は1月30日。
            var lastDayOfMonth = GameDate.FromSeconds(29 * 60d, 798, P);
            Assert.AreEqual(1, lastDayOfMonth.month);
            Assert.AreEqual(30, lastDayOfMonth.day);

            var nextMonth = GameDate.FromSeconds(30 * 60d, 798, P);
            Assert.AreEqual(798, nextMonth.year);
            Assert.AreEqual(2, nextMonth.month);
            Assert.AreEqual(1, nextMonth.day);
        }

        [Test]
        public void FromSeconds_YearBoundary_360Days_RollsToNextYear()
        {
            // 360日（12月×30日）＝21600秒で年跨ぎ＝翌年1月1日。直前は12月30日。
            var lastDayOfYear = GameDate.FromSeconds(359 * 60d, 798, P);
            Assert.AreEqual(798, lastDayOfYear.year);
            Assert.AreEqual(12, lastDayOfYear.month);
            Assert.AreEqual(30, lastDayOfYear.day);

            var nextYear = GameDate.FromSeconds(360 * 60d, 798, P);
            Assert.AreEqual(799, nextYear.year);
            Assert.AreEqual(1, nextYear.month);
            Assert.AreEqual(1, nextYear.day);
        }

        [Test]
        public void FromSeconds_NegativeSeconds_ClampsToStart()
        {
            // 負秒は0クランプ＝開始年の1月1日
            var d = GameDate.FromSeconds(-99999d, 800, P);
            Assert.AreEqual(800, d.year);
            Assert.AreEqual(1, d.month);
            Assert.AreEqual(1, d.day);
        }

        [Test]
        public void FromSeconds_LargeElapsed_AccumulatesCorrectly()
        {
            // 大経過＝開始から ちょうど5年＋2月＋3日 後を検証。
            // 5年=1800日, +2月=60日, +3日 → 合計1863日。秒=1863*60=111780。
            double seconds = (5 * 360 + 2 * 30 + 3) * 60d;
            var d = GameDate.FromSeconds(seconds, 700, P);
            Assert.AreEqual(705, d.year);
            Assert.AreEqual(3, d.month); // 0月オフセット+2 → 3月
            Assert.AreEqual(4, d.day);   // 0日オフセット+3 → 4日
        }

        [Test]
        public void ImperialYear_AppliesOffset()
        {
            // 帝国暦＝宇宙暦−offset。SE798, offset310 → IC488。
            var p = new GameDate.DateParams(60d, 30, 12, 310);
            Assert.AreEqual(488, GameDate.ImperialYear(798, p));
            // offset0 なら宇宙暦と同値
            Assert.AreEqual(798, GameDate.ImperialYear(798, P));
        }

        [Test]
        public void ToString_FormatsSpaceEra()
        {
            var d = new GameDate(798, 4, 13);
            Assert.AreEqual("SE798.4.13", d.ToString());
            Assert.AreEqual("SE798.4.13", d.ToSpaceEraString());
        }

        [Test]
        public void ToImperialString_And_DualString_UseOffset()
        {
            var p = new GameDate.DateParams(60d, 30, 12, 310);
            var d = new GameDate(798, 4, 13);
            Assert.AreEqual("IC488.4.13", d.ToImperialString(p));
            Assert.AreEqual("SE798.4.13 / IC488.4.13", d.ToDualString(p));
        }

        [Test]
        public void DateParams_ClampsInvalidValues()
        {
            // 不正値（0以下）はクランプ＝0除算・無限ループ回避
            var p = new GameDate.DateParams(0d, 0, 0, 5);
            Assert.AreEqual(1d, p.secondsPerDay);
            Assert.AreEqual(1, p.daysPerMonth);
            Assert.AreEqual(1, p.monthsPerYear);
            // クランプ後でも FromSeconds が破綻しない（1秒=1日=1月=1年）
            var d = GameDate.FromSeconds(3d, 10, p);
            Assert.AreEqual(13, d.year); // 3日=3年（1月/年・1日/月）
            Assert.AreEqual(1, d.month);
            Assert.AreEqual(1, d.day);
        }
    }
}
