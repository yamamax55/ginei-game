namespace Ginei
{
    /// <summary>
    /// 士官学校（#155 LIFE-5・武の出自経路 <see cref="CareerTrack.士官学校"/> の供給源）。POP の徴募源（軍属 #96）から候補生を集め、
    /// 1学年ぶんの<b>新たな士官（軍人 <see cref="Person"/>）</b>を席次付きで世に送り出す。卒業の解決は <see cref="OfficerAcademyRules"/>。
    /// 教育の質が卒業生の平均能力を、定員と徴募源が輩出数を決める＝<b>人口と国力が将官を支える</b>。純データ。
    /// </summary>
    [System.Serializable]
    public class Academy
    {
        /// <summary>学校ID（学閥＝同窓判定 <see cref="Person.schoolId"/> の出所・#155）。</summary>
        public int schoolId;

        /// <summary>所属勢力。</summary>
        public Faction faction;

        public string name = "士官学校";

        /// <summary>1学年の定員（卒業生数の上限）。</summary>
        public int capacity = 8;

        /// <summary>教育の質（0..1）。卒業生の平均能力に効く（名門ほど良将を出す）。</summary>
        public float quality = 0.5f;

        /// <summary>創立年（暦）。</summary>
        public int foundedYear;

        public Academy() { }

        public Academy(int schoolId, Faction faction, string name, int capacity = 8, float quality = 0.5f)
        {
            this.schoolId = schoolId;
            this.faction = faction;
            this.name = string.IsNullOrEmpty(name) ? "士官学校" : name;
            this.capacity = capacity;
            this.quality = quality;
        }
    }
}
