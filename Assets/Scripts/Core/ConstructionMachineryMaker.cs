namespace Ginei
{
    /// <summary>
    /// 建機メーカー（建設機械メーカー・#2022・純データ）。建設・鉱山・インフラ向けの資本財（B2B）を作る企業。汎用メーカー
    /// （#2016）の中でも<b>景気に超敏感（加速度原理）でアフターサービスが下支え</b>する業種。稼働台数（部品/整備の母数）・新車1台
    /// 粗利・1台あたり年間部品サービス収益・中古残価率を持つ。解決は <see cref="ConstructionMachineryRules"/>。少数集約（タイクン化回避）。
    /// </summary>
    [System.Serializable]
    public class ConstructionMachineryMaker
    {
        public string name = "建機メーカー";
        public Faction faction;

        /// <summary>稼働台数（installed base＝市場で動いている現役機。部品・整備収益の母数）。</summary>
        public float installedBase = 0f;

        /// <summary>新車1台あたり粗利。</summary>
        public float newUnitMargin = 0f;

        /// <summary>1台あたり年間部品・整備収益（アフターサービスの単価）。</summary>
        public float partsServiceRate = 0f;

        /// <summary>中古残価率（新車価格に対する中古売却の割合）。</summary>
        public float residualRate = 0.5f;

        public ConstructionMachineryMaker() { }

        public ConstructionMachineryMaker(string name, float installedBase = 0f, float newUnitMargin = 0f,
            float partsServiceRate = 0f, float residualRate = 0.5f, Faction faction = default)
        {
            this.name = string.IsNullOrEmpty(name) ? "建機メーカー" : name;
            this.installedBase = installedBase;
            this.newUnitMargin = newUnitMargin;
            this.partsServiceRate = partsServiceRate;
            this.residualRate = residualRate;
            this.faction = faction;
        }
    }
}
