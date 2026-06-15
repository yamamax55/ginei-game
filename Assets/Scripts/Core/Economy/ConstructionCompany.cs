namespace Ginei
{
    /// <summary>建設会社（東証33業種「建設業」・#2024・純データ）。受注産業＝請負契約を取り、工事進行に応じて収益計上し、原価超過で採算が崩れる。受注残・契約額を持つ。解決は <see cref="ConstructionRules"/>。</summary>
    [System.Serializable]
    public class ConstructionCompany
    {
        public string name = "建設会社";
        public Faction faction;
        /// <summary>受注残（受注したが未完の工事高）。</summary>
        public float orderBacklog = 0f;

        public ConstructionCompany() { }
        public ConstructionCompany(string name, float orderBacklog = 0f, Faction faction = default)
        {
            this.name = string.IsNullOrEmpty(name) ? "建設会社" : name;
            this.orderBacklog = orderBacklog; this.faction = faction;
        }
    }
}
