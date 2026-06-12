namespace Ginei
{
    /// <summary>
    /// 大学（#156/#157 LIFE-6/7・文/技の出自経路 <see cref="CareerTrack.科挙"/>／<see cref="CareerTrack.テクノクラート"/> の供給源）。
    /// 士官学校（<see cref="Academy"/>）の<b>文民版</b>＝POP から学生を集め、1学年ぶんの<b>文官（行政/外交）または技術者</b>を
    /// 席次付きで世に送り出す。卒業の解決は <see cref="UniversityRules"/>。教育の質が平均能力を、定員と候補が輩出数を決める。純データ。
    /// </summary>
    [System.Serializable]
    public class University
    {
        /// <summary>学校ID（文官閥/専門閥＝同窓判定 <see cref="Person.schoolId"/> の出所・#156/#157）。</summary>
        public int schoolId;

        public Faction faction;

        public string name = "大学";

        /// <summary>1学年の定員（卒業生数の上限）。</summary>
        public int capacity = 8;

        /// <summary>教育の質（0..1）。卒業生の平均能力に効く。</summary>
        public float quality = 0.5f;

        public int foundedYear;

        /// <summary>進路（<see cref="CareerTrack.科挙"/>＝文官/行政・<see cref="CareerTrack.テクノクラート"/>＝技術者）。既定=科挙。</summary>
        public CareerTrack track = CareerTrack.科挙;

        public University() { }

        public University(int schoolId, Faction faction, string name, CareerTrack track, int capacity = 8, float quality = 0.5f)
        {
            this.schoolId = schoolId;
            this.faction = faction;
            this.name = string.IsNullOrEmpty(name) ? "大学" : name;
            this.track = track;
            this.capacity = capacity;
            this.quality = quality;
        }
    }
}
