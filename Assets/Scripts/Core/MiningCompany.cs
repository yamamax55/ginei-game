namespace Ginei
{
    /// <summary>
    /// 鉱山会社（採掘業・#2018・純データ）。鉱床から資源を掘り出す川上の operator。資源権益（#1029/#1039 採掘する権利）とは別＝
    /// 実際に採掘する操業主体。有限な埋蔵量・品位（鉱石あたり金属含有率）・1単位採掘コスト・累積採掘量（品位低下とコスト上昇の
    /// 進行度）を持つ。良鉱から先に掘るので品位が落ち深くなるほどコストが上がり、いずれ枯渇する。解決は <see cref="MiningRules"/>。
    /// </summary>
    [System.Serializable]
    public class MiningCompany
    {
        public string name = "鉱山会社";
        public Faction faction;

        /// <summary>埋蔵量（残り採掘可能な鉱石量。0で枯渇＝閉山）。</summary>
        public float reserves = 0f;

        /// <summary>品位（鉱石あたりの金属含有率 0..1。採掘が進むと低下）。</summary>
        public float oreGrade = MiningRules.DefaultOreGrade;

        /// <summary>1単位の採掘コスト（深部化・品位低下で上昇）。</summary>
        public float extractionCostPerUnit = 1f;

        /// <summary>累積採掘量（品位低下・コスト上昇の進行度＝枯渇率の分子）。</summary>
        public float cumulativeExtracted = 0f;

        public MiningCompany() { }

        public MiningCompany(string name, float reserves, float oreGrade = MiningRules.DefaultOreGrade,
            float extractionCostPerUnit = 1f, float cumulativeExtracted = 0f, Faction faction = default)
        {
            this.name = string.IsNullOrEmpty(name) ? "鉱山会社" : name;
            this.reserves = reserves;
            this.oreGrade = oreGrade;
            this.extractionCostPerUnit = extractionCostPerUnit;
            this.cumulativeExtracted = cumulativeExtracted;
            this.faction = faction;
        }
    }
}
