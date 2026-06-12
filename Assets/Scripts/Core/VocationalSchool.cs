namespace Ginei
{
    /// <summary>
    /// 専門学校（#157 LIFE-7）。高校卒後の<b>2年制</b>で、特定分野の<b>実務specialist（職業人）</b>を養成する。高専（<see cref="TechnicalCollege"/>）
    /// より学術色は薄く現場直結、短大（<see cref="JuniorCollege"/>）の技術版＝<b>実務（計画/生産）寄りの中堅</b>。解決は <see cref="VocationalSchoolRules"/>。純データ。
    /// </summary>
    [System.Serializable]
    public class VocationalSchool
    {
        public int schoolId;
        public Faction faction;
        public string name = "専門学校";
        public int capacity = 6;
        public float quality = 0.5f;

        public VocationalSchool() { }

        public VocationalSchool(int schoolId, Faction faction, string name, int capacity = 6, float quality = 0.5f)
        {
            this.schoolId = schoolId;
            this.faction = faction;
            this.name = string.IsNullOrEmpty(name) ? "専門学校" : name;
            this.capacity = capacity;
            this.quality = quality;
        }
    }
}
