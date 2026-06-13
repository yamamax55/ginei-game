namespace Ginei
{
    /// <summary>
    /// 高校＝中等教育（#155-157 LIFE-5/6/7 の土台）。若年人口を教育し、上級学校（士官学校 <see cref="Academy"/>／
    /// 大学 <see cref="University"/>）の<b>候補の母数と素質</b>を決める。<b>進学率</b>が普及（候補が増える）を、<b>質</b>が準備の良さ
    /// （候補の素質↑）を表す。解決は <see cref="HighSchoolRules"/>。勢力の教育政策レイヤー（民主は普及・専制は選別 等の差を付けられる）。純データ。
    /// </summary>
    [System.Serializable]
    public class HighSchool
    {
        public int schoolId;
        public Faction faction;
        public string name = "高校";

        /// <summary>進学率（0..1・若年が高校へ進む割合）。高いほど上級教育の候補が増える＝大衆教育。</summary>
        public float enrollmentRate = 0.6f;

        /// <summary>教育の質（0..1）。高いほど候補生の素質が上がる（準備の整った人材）。</summary>
        public float quality = 0.5f;

        public HighSchool() { }

        public HighSchool(int schoolId, Faction faction, string name, float enrollmentRate = 0.6f, float quality = 0.5f)
        {
            this.schoolId = schoolId;
            this.faction = faction;
            this.name = string.IsNullOrEmpty(name) ? "高校" : name;
            this.enrollmentRate = enrollmentRate;
            this.quality = quality;
        }
    }
}
