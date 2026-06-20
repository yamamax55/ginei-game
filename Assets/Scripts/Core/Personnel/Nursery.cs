namespace Ginei
{
    /// <summary>
    /// 保育園＝保育（#153/#110 連携）。幼稚園 <see cref="Kindergarten"/> が<b>教育</b>なのに対し、保育園は<b>保育（子育て支援）</b>＝
    /// 働く親を支えて<b>労働参加を上げ</b>、子育ての負担を下げて<b>出生率を上げる</b>。教育チェーン（素質/進学）には効かず、
    /// POP の出生（<see cref="PopulationDynamicsRules"/>）と労働の母数に効くのが幼稚園との違い。解決は <see cref="NurseryRules"/>。純データ。
    /// </summary>
    [System.Serializable]
    public class Nursery
    {
        public int schoolId;
        public Faction faction;
        public string name = "保育園";

        /// <summary>整備率（0..1・保育を必要とする世帯のうち利用できる割合）。高いほど労働参加↑・出生率↑。</summary>
        public float coverage = 0.5f;

        public Nursery() { }

        public Nursery(int schoolId, Faction faction, string name, float coverage = 0.5f)
        {
            this.schoolId = schoolId;
            this.faction = faction;
            this.name = string.IsNullOrEmpty(name) ? "保育園" : name;
            this.coverage = coverage;
        }
    }
}
