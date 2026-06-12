namespace Ginei
{
    /// <summary>
    /// 短期大学（短大・#156 LIFE-6）。高校卒後の<b>2年制</b>で、行政・一般事務の<b>中堅文民</b>を輩出する。4年制大学（<see cref="University"/>）
    /// より天井が低く（早く現場へ）、科挙の進士のような上級官にはならないが、官界の裾野を支える。解決は <see cref="JuniorCollegeRules"/>。純データ。
    /// </summary>
    [System.Serializable]
    public class JuniorCollege
    {
        public int schoolId;
        public Faction faction;
        public string name = "短期大学";
        public int capacity = 6;
        public float quality = 0.5f;

        public JuniorCollege() { }

        public JuniorCollege(int schoolId, Faction faction, string name, int capacity = 6, float quality = 0.5f)
        {
            this.schoolId = schoolId;
            this.faction = faction;
            this.name = string.IsNullOrEmpty(name) ? "短期大学" : name;
            this.capacity = capacity;
            this.quality = quality;
        }
    }
}
