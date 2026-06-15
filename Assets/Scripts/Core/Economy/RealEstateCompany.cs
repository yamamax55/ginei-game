namespace Ginei
{
    /// <summary>
    /// 不動産会社（#2019・純データ）。土地・建物を取得し賃貸・売買・開発する企業。保有土地の評価額・建物の評価額・年間賃料・
    /// 空室率・取得原価を持つ。<b>土地を私有財産にできるかは政体による</b>（共産は国有＝私有地として保有/売買不可・<see cref="PropertyRules"/>）。
    /// 地価が賃料に比して割高になりすぎるとバブル→崩壊。解決は <see cref="RealEstateRules"/>。少数集約（タイクン化回避）。
    /// </summary>
    [System.Serializable]
    public class RealEstateCompany
    {
        public string name = "不動産会社";
        public Faction faction;

        /// <summary>保有土地の評価額（地価）。</summary>
        public float landValue = 0f;

        /// <summary>建物の評価額。</summary>
        public float propertyValue = 0f;

        /// <summary>年間総賃料（満室想定）。</summary>
        public float grossRent = 0f;

        /// <summary>空室率（0..1。空室は賃料を生まない）。</summary>
        public float vacancyRate = RealEstateRules.DefaultVacancyRate;

        /// <summary>取得原価（売買差益の基準）。</summary>
        public float acquisitionCost = 0f;

        public RealEstateCompany() { }

        public RealEstateCompany(string name, float landValue = 0f, float propertyValue = 0f, float grossRent = 0f,
            float vacancyRate = RealEstateRules.DefaultVacancyRate, float acquisitionCost = 0f, Faction faction = default)
        {
            this.name = string.IsNullOrEmpty(name) ? "不動産会社" : name;
            this.landValue = landValue;
            this.propertyValue = propertyValue;
            this.grossRent = grossRent;
            this.vacancyRate = vacancyRate;
            this.acquisitionCost = acquisitionCost;
            this.faction = faction;
        }
    }
}
