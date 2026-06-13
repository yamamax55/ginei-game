namespace Ginei
{
    /// <summary>サービス会社（東証33業種「サービス業」・#2024・純データ）。労働集約＝人件費が主コスト、人の稼働率（可動率）が収益を決める。従業員数を持つ。解決は <see cref="ServiceRules"/>。</summary>
    [System.Serializable]
    public class ServiceCompany
    {
        public string name = "サービス会社";
        public Faction faction;
        /// <summary>従業員数（労働集約＝人が資産）。</summary>
        public float staff = 0f;

        public ServiceCompany() { }
        public ServiceCompany(string name, float staff = 0f, Faction faction = default)
        {
            this.name = string.IsNullOrEmpty(name) ? "サービス会社" : name;
            this.staff = staff; this.faction = faction;
        }
    }
}
