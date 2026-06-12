namespace Ginei
{
    /// <summary>
    /// 製薬会社（東証33業種「医薬品」・#2024・純データ）。巨額・長期・低成功率の研究開発で新薬を生み、特許で守られた間は高利益、
    /// 特許切れでジェネリックに侵食され急落（パテントクリフ）。研究開発水準・治験段階成功率・特許マージン・年間売上を持つ。
    /// 解決は <see cref="PharmaRules"/>。少数集約（タイクン化回避）。
    /// </summary>
    [System.Serializable]
    public class PharmaCompany
    {
        public string name = "製薬会社";
        public Faction faction;

        /// <summary>研究開発水準（パイプラインの厚み）。</summary>
        public float rdLevel = 0f;

        /// <summary>特許マージン（特許保護下の高い利益率）。</summary>
        public float patentMargin = 0.8f;

        /// <summary>主力薬の年間売上。</summary>
        public float annualSales = 0f;

        /// <summary>特許保護中か（false＝特許切れ＝ジェネリック侵食）。</summary>
        public bool patentProtected = true;

        public PharmaCompany() { }

        public PharmaCompany(string name, float rdLevel = 0f, float patentMargin = 0.8f,
            float annualSales = 0f, bool patentProtected = true, Faction faction = default)
        {
            this.name = string.IsNullOrEmpty(name) ? "製薬会社" : name;
            this.rdLevel = rdLevel;
            this.patentMargin = patentMargin;
            this.annualSales = annualSales;
            this.patentProtected = patentProtected;
            this.faction = faction;
        }
    }
}
