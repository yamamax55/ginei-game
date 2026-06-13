using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 立法府の議院（選挙システム基盤・日本の国会を範に）。<b>下院＝衆議院相当</b>（任期4年・解散あり・全議席改選）、
    /// <b>上院＝参議院相当</b>（任期6年・解散なし・3年ごとに半数改選）。選挙日程は <see cref="ElectionScheduleRules"/> が窓口。
    /// </summary>
    public enum LegislativeChamber { 下院, 上院 }

    /// <summary>
    /// 議院の任期プロファイル（選挙システム基盤）。任期・解散の可否・改選区分（全議席=1／半数=2）から
    /// 通常選挙の間隔（任期/区分）と1回の改選割合（1/区分）を導く。<see cref="ElectionScheduleRules.ProfileFor"/> が単一の対応表。
    /// </summary>
    public readonly struct ChamberTermProfile
    {
        /// <summary>議員の任期（年）。</summary>
        public readonly int termYears;
        /// <summary>解散できるか（下院＝衆議院相当のみ true）。</summary>
        public readonly bool dissolvable;
        /// <summary>改選区分の数（1=全議席改選／2=半数改選＝参議院相当）。</summary>
        public readonly int classCount;

        public ChamberTermProfile(int termYears, bool dissolvable, int classCount)
        {
            this.termYears = Mathf.Max(1, termYears);
            this.dissolvable = dissolvable;
            this.classCount = Mathf.Max(1, classCount);
        }

        /// <summary>通常選挙の間隔（年）＝任期÷改選区分（下院4年・上院3年）。</summary>
        public int ElectionIntervalYears => termYears / classCount;

        /// <summary>1回の選挙で改選される議席の割合（全議席1.0／半数0.5）。</summary>
        public float SeatFractionPerElection => 1f / classCount;
    }

    /// <summary>
    /// 一議院の選挙日程の状態（選挙システム基盤・純データ・<see cref="ElectionScheduleRules"/> が更新）。
    /// 次の通常選挙年・直近選挙年・次に改選する区分（上院の半数改選用）を保持する。戦役セーブに乗せられる平データ。
    /// </summary>
    [System.Serializable]
    public class ChamberSchedule
    {
        public LegislativeChamber chamber;

        /// <summary>次の通常選挙の年（任期満了の年）。</summary>
        public int nextElectionYear;

        /// <summary>次に改選する区分（0..区分数-1・上院の半数改選で交互に進む）。</summary>
        public int currentClass;

        /// <summary>直近の選挙年（0=未実施）。</summary>
        public int lastElectionYear;

        public ChamberSchedule() { }

        public ChamberSchedule(LegislativeChamber chamber)
        {
            this.chamber = chamber;
        }
    }
}
