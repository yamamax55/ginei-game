using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// game-seconds → 架空の暦日（宇宙暦 SE Y/M/D）への純変換（TIME-2 #948）。閏なしの単純暦。
    /// MonoBehaviour/シーン非依存・Game層型不参照＝plain 引数で完結する。
    /// Calendar 連携（加齢）は別タスク（TIME-2 配線）＝ここでは純変換のみ。
    /// </summary>
    [System.Serializable]
    public readonly struct GameDate
    {
        /// <summary>宇宙暦の年。</summary>
        public readonly int year;
        /// <summary>月（1始まり）。</summary>
        public readonly int month;
        /// <summary>日（1始まり）。</summary>
        public readonly int day;

        public GameDate(int year, int month, int day)
        {
            this.year = year;
            this.month = month;
            this.day = day;
        }

        /// <summary>「SE年.月.日」形式（例＝SE798.4.13）。</summary>
        public override string ToString()
        {
            return $"SE{year}.{month}.{day}";
        }

        /// <summary>暦の調整値（1日の秒数・月の日数・年の月数・帝国暦オフセット）。</summary>
        public readonly struct DateParams
        {
            public readonly double secondsPerDay;  // 1日あたりの game-seconds
            public readonly int daysPerMonth;       // 1月あたりの日数
            public readonly int monthsPerYear;      // 1年あたりの月数
            public readonly int imperialOffset;     // 帝国暦＝宇宙暦−offset

            public DateParams(double secondsPerDay, int daysPerMonth, int monthsPerYear, int imperialOffset)
            {
                this.secondsPerDay = secondsPerDay > 0d ? secondsPerDay : 1d;
                this.daysPerMonth = Mathf.Max(1, daysPerMonth);
                this.monthsPerYear = Mathf.Max(1, monthsPerYear);
                this.imperialOffset = imperialOffset;
            }

            /// <summary>既定＝1日60秒・30日/月・12月/年・帝国暦オフセット0。</summary>
            public static DateParams Default => new DateParams(60d, 30, 12, 0);
        }

        /// <summary>
        /// 経過秒を日数換算し、<paramref name="startYear"/> からの暦日を返す。
        /// 日→月→年へ繰り上げ。負の経過秒は0クランプ（startYear の1月1日）。
        /// </summary>
        public static GameDate FromSeconds(double elapsedSeconds, int startYear, DateParams p)
        {
            if (elapsedSeconds < 0d) elapsedSeconds = 0d;

            long totalDays = (long)(elapsedSeconds / p.secondsPerDay);
            int daysPerYear = p.daysPerMonth * p.monthsPerYear; // 閏なし＝固定

            long year = startYear + totalDays / daysPerYear;
            long dayOfYear = totalDays % daysPerYear;
            int month = (int)(dayOfYear / p.daysPerMonth) + 1;
            int day = (int)(dayOfYear % p.daysPerMonth) + 1;

            return new GameDate((int)year, month, day);
        }

        /// <summary>宇宙暦の年から帝国暦の年を返す（帝国暦＝宇宙暦−offset）。</summary>
        public static int ImperialYear(int seYear, DateParams p)
        {
            return seYear - p.imperialOffset;
        }

        /// <summary>
        /// 時刻（時:分）を返す。1日(<paramref name="secondsPerDay"/>)を24時間に割り当て、経過秒の日内位置から
        /// hour(0..23)/minute(0..59) を出す（HH:MM 表示用）。負の経過秒は0クランプ。
        /// </summary>
        public static void TimeOfDay(double elapsedSeconds, double secondsPerDay, out int hour, out int minute)
        {
            if (elapsedSeconds < 0d) elapsedSeconds = 0d;
            double spd = secondsPerDay > 0d ? secondsPerDay : 1d;
            double secondsIntoDay = elapsedSeconds % spd;   // 日内の経過秒 0..spd
            double hours = (secondsIntoDay / spd) * 24d;     // 0..24
            hour = (int)hours;
            minute = (int)((hours - hour) * 60d);
            if (hour > 23) hour = 23;
            if (minute > 59) minute = 59;
        }

        /// <summary>時刻を "HH:MM"（ゼロ埋め2桁）で返す。</summary>
        public static string TimeString(double elapsedSeconds, double secondsPerDay)
        {
            TimeOfDay(elapsedSeconds, secondsPerDay, out int h, out int m);
            return $"{h:00}:{m:00}";
        }

        /// <summary>宇宙暦の文字列（このインスタンスの ToString と同等）。</summary>
        public string ToSpaceEraString()
        {
            return ToString();
        }

        /// <summary>帝国暦の文字列（例＝IC488.4.13）。年だけ帝国暦へ変換し月日はそのまま。</summary>
        public string ToImperialString(DateParams p)
        {
            return $"IC{ImperialYear(year, p)}.{month}.{day}";
        }

        /// <summary>二重暦表示（宇宙暦／帝国暦）。例＝SE798.4.13 / IC488.4.13。</summary>
        public string ToDualString(DateParams p)
        {
            return $"{ToSpaceEraString()} / {ToImperialString(p)}";
        }
    }
}
