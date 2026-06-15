using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 立法府の<b>選挙の実施タイミング</b>の純ロジック（選挙システム基盤・日本の国会を範に・唯一の窓口）。
    /// <b>下院（衆議院相当）は任期4年または解散で総選挙</b>（全議席改選）、<b>上院（参議院相当）は任期6年で3年ごとに半数改選</b>（解散なし）。
    /// 得票の集計/集約は <see cref="ElectionRules"/>、政体ごとの選挙の有無は <see cref="ElectoralSystemRules"/> が担う（ここは日程のみ）。
    /// 暦は年単位（<see cref="Calendar"/>／GalaxyView 年次 Tick から駆動する想定）。決定論・基準値非破壊。test-first。
    /// </summary>
    public static class ElectionScheduleRules
    {
        /// <summary>議院→任期プロファイル（日本の国会＝下院 任期4年/解散可/全議席・上院 任期6年/解散不可/半数改選）。単一の対応表。</summary>
        public static ChamberTermProfile ProfileFor(LegislativeChamber chamber)
        {
            switch (chamber)
            {
                case LegislativeChamber.下院: return new ChamberTermProfile(termYears: 4, dissolvable: true, classCount: 1);
                case LegislativeChamber.上院: return new ChamberTermProfile(termYears: 6, dissolvable: false, classCount: 2);
                default:                       return new ChamberTermProfile(termYears: 4, dissolvable: true, classCount: 1);
            }
        }

        /// <summary>その議院は解散できるか（衆議院相当のみ）。</summary>
        public static bool CanDissolve(LegislativeChamber chamber) => ProfileFor(chamber).dissolvable;

        /// <summary>議院を設立し、最初の通常選挙を任期の間隔ぶん先に予約した日程を返す。</summary>
        public static ChamberSchedule Found(LegislativeChamber chamber, int foundedYear)
        {
            var p = ProfileFor(chamber);
            return new ChamberSchedule(chamber)
            {
                lastElectionYear = 0,
                currentClass = 0,
                nextElectionYear = foundedYear + p.ElectionIntervalYears,
            };
        }

        /// <summary>通常選挙の実施年に達したか（次の選挙年以上）。</summary>
        public static bool IsElectionDue(ChamberSchedule s, int currentYear)
            => s != null && currentYear >= s.nextElectionYear;

        /// <summary>今回の選挙で改選される議席の割合（下院=1.0 全議席／上院=0.5 半数）。</summary>
        public static float SeatFractionUp(ChamberSchedule s)
            => s == null ? 0f : ProfileFor(s.chamber).SeatFractionPerElection;

        /// <summary>今回改選される区分（上院の半数改選で交互に進む。下院は常に0＝全議席）。</summary>
        public static int CurrentClassUp(ChamberSchedule s) => s == null ? 0 : s.currentClass;

        /// <summary>
        /// 選挙を実施して日程を進める：直近選挙年を記録し、改選区分を一つ進め（半数改選の交代）、
        /// 次の通常選挙を間隔ぶん先へ予約する。下院は全議席改選で次回は4年後、上院は半数改選で次回は3年後。
        /// </summary>
        public static void RunElection(ChamberSchedule s, int currentYear)
        {
            if (s == null) return;
            var p = ProfileFor(s.chamber);
            s.lastElectionYear = currentYear;
            s.currentClass = (s.currentClass + 1) % p.classCount;
            s.nextElectionYear = currentYear + p.ElectionIntervalYears;
        }

        /// <summary>
        /// 解散総選挙（下院＝衆議院相当のみ）。即時に総選挙を実施＝任期を今からリセットする（次回は4年後）。
        /// 解散できない議院（上院）では何もせず false。
        /// </summary>
        public static bool TryDissolve(ChamberSchedule s, int currentYear)
        {
            if (s == null || !CanDissolve(s.chamber)) return false;
            RunElection(s, currentYear); // 解散＝即時総選挙・任期リセット
            return true;
        }

        /// <summary>
        /// 年次の進行：通常選挙の年に達していれば実施して日程を進め true（GalaxyView 年次 Tick から呼ぶ想定）。
        /// 未到来なら false。解散は別途 <see cref="TryDissolve"/>（早期実施）。
        /// </summary>
        public static bool TickYear(ChamberSchedule s, int currentYear)
        {
            if (!IsElectionDue(s, currentYear)) return false;
            RunElection(s, currentYear);
            return true;
        }
    }
}
