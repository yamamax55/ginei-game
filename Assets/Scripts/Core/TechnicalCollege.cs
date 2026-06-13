namespace Ginei
{
    /// <summary>
    /// 高等専門学校（高専・#157 LIFE-7）。<b>中学校から直接（高校を経ず）入学する5年制の実践的技術者養成校</b>。
    /// 高校→大学の学術路とは別ルートで、若くして<b>手を動かす技術者（テクノクラート）</b>を輩出する＝実技重視・早期戦力。
    /// 解決は <see cref="TechnicalCollegeRules"/>。文官の科挙路（<see cref="University"/> 科挙）に対する技術の実務路。純データ。
    /// </summary>
    [System.Serializable]
    public class TechnicalCollege
    {
        public int schoolId;
        public Faction faction;
        public string name = "高等専門学校";

        /// <summary>1学年の定員（卒業生数の上限）。</summary>
        public int capacity = 6;

        /// <summary>教育の質（0..1）。卒業生の技術力に効く。</summary>
        public float quality = 0.5f;

        public TechnicalCollege() { }

        public TechnicalCollege(int schoolId, Faction faction, string name, int capacity = 6, float quality = 0.5f)
        {
            this.schoolId = schoolId;
            this.faction = faction;
            this.name = string.IsNullOrEmpty(name) ? "高等専門学校" : name;
            this.capacity = capacity;
            this.quality = quality;
        }
    }
}
