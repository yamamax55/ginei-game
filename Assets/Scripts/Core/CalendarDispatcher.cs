using System;

namespace Ginei
{
    /// <summary>
    /// 暦粒度の Tick ディスパッチャ（TIME-6 #952）。前回 <see cref="Advance"/> からの game-seconds 経過で
    /// 日/月/年 の境界を跨いだ回数ぶん、対応コールバックを発火する。連続 dt（per-second 相当）は呼び出し側で別途処理。
    /// 状態は最後に処理した経過秒（<see cref="LastElapsed"/>）のみ＝ポーズで止まり倍速で速く進む統一クロックと整合し、
    /// 「倍速にしても暦比で同じ帰結／ポーズで完全停止」を保証する。境界判定は <see cref="CalendarTick"/> に委譲。
    /// </summary>
    public sealed class CalendarDispatcher
    {
        /// <summary>1回の Advance で発火する境界数の安全上限（暴走防止）。日換算で十数年ぶんを許容。</summary>
        public const int MaxFirePerAdvance = 4000;

        private double lastElapsed;

        /// <summary>暦の調整値（1日の秒数・月の日数・年の月数）。</summary>
        public GameDate.DateParams Params { get; set; }

        /// <summary>最後に処理した経過秒。</summary>
        public double LastElapsed => lastElapsed;

        public CalendarDispatcher(GameDate.DateParams p, double startElapsed = 0d)
        {
            Params = p;
            lastElapsed = startElapsed < 0d ? 0d : startElapsed;
        }

        /// <summary>基準経過秒をリセットする（境界を発火させずに同期＝シーン遷移時など）。</summary>
        public void Reset(double elapsedSeconds)
        {
            lastElapsed = elapsedSeconds < 0d ? 0d : elapsedSeconds;
        }

        /// <summary>
        /// 現在の経過秒まで進め、跨いだ境界ぶんコールバックを発火する。日→月→年の順に、それぞれ独立に発火
        /// （月境界の日は onDay も発火する＝per-day/per-month/per-year フックは重なる）。
        /// nowElapsed が過去（巻き戻り）なら基準だけ更新して何も発火しない。安全上限 <see cref="MaxFirePerAdvance"/>。
        /// </summary>
        public void Advance(double nowElapsed, Action onDay = null, Action onMonth = null, Action onYear = null)
        {
            if (nowElapsed < 0d) nowElapsed = 0d;
            if (nowElapsed <= lastElapsed)
            {
                lastElapsed = nowElapsed; // 巻き戻り/停止は基準同期のみ
                return;
            }

            Fire(CalendarTick.DayBoundaries(lastElapsed, nowElapsed, Params), onDay);
            Fire(CalendarTick.MonthBoundaries(lastElapsed, nowElapsed, Params), onMonth);
            Fire(CalendarTick.YearBoundaries(lastElapsed, nowElapsed, Params), onYear);

            lastElapsed = nowElapsed;
        }

        private static void Fire(long count, Action cb)
        {
            if (cb == null || count <= 0) return;
            if (count > MaxFirePerAdvance) count = MaxFirePerAdvance;
            for (long i = 0; i < count; i++) cb();
        }
    }
}
