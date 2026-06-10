namespace Ginei
{
    /// <summary>
    /// 暦境界（日/月/年）の越え数を数える純ロジック（TIME-6 #952）。GameClock の game-seconds の区間
    /// [from, to) で何回 日/月/年 の境界を跨いだかを返す。倍速でもポーズでも「暦比で同じ帰結」を保証する土台。
    /// MonoBehaviour/シーン非依存・Game層型不参照＝plain 引数で完結。暦の定義は <see cref="GameDate.DateParams"/>。
    /// </summary>
    public static class CalendarTick
    {
        /// <summary>経過秒の通算日数（0始まり・閏なし）。負秒は0クランプ。</summary>
        public static long TotalDays(double elapsedSeconds, GameDate.DateParams p)
        {
            if (elapsedSeconds < 0d) elapsedSeconds = 0d;
            double spd = p.secondsPerDay > 0d ? p.secondsPerDay : 1d;
            return (long)(elapsedSeconds / spd);
        }

        /// <summary>経過秒の通算月数（0始まり）。</summary>
        public static long TotalMonths(double elapsedSeconds, GameDate.DateParams p)
        {
            long days = TotalDays(elapsedSeconds, p);
            int dpm = p.daysPerMonth > 0 ? p.daysPerMonth : 1;
            return days / dpm;
        }

        /// <summary>経過秒の通算年数（0始まり）。</summary>
        public static long TotalYears(double elapsedSeconds, GameDate.DateParams p)
        {
            long months = TotalMonths(elapsedSeconds, p);
            int mpy = p.monthsPerYear > 0 ? p.monthsPerYear : 1;
            return months / mpy;
        }

        /// <summary>区間 [fromSec, toSec) で跨いだ日境界の回数（非負）。to&lt;from は0。</summary>
        public static long DayBoundaries(double fromSec, double toSec, GameDate.DateParams p)
        {
            long diff = TotalDays(toSec, p) - TotalDays(fromSec, p);
            return diff > 0 ? diff : 0;
        }

        /// <summary>区間 [fromSec, toSec) で跨いだ月境界の回数（非負）。</summary>
        public static long MonthBoundaries(double fromSec, double toSec, GameDate.DateParams p)
        {
            long diff = TotalMonths(toSec, p) - TotalMonths(fromSec, p);
            return diff > 0 ? diff : 0;
        }

        /// <summary>区間 [fromSec, toSec) で跨いだ年境界の回数（非負）。</summary>
        public static long YearBoundaries(double fromSec, double toSec, GameDate.DateParams p)
        {
            long diff = TotalYears(toSec, p) - TotalYears(fromSec, p);
            return diff > 0 ? diff : 0;
        }
    }
}
