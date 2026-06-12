namespace Ginei
{
    /// <summary>
    /// 鉄鋼メーカー（東証33業種「鉄鋼」・#2024・純データ）。高炉（鉄鉱石＋石炭→粗鋼）または電炉（スクラップ→鋼）で鋼材を作る
    /// 装置産業。原料（鉄鉱石 #2018）依存・高固定費・市況連動。生産能力・固定費・電炉か（スクラップ循環）を持つ。
    /// 解決は <see cref="SteelRules"/>。少数集約（タイクン化回避）。
    /// </summary>
    [System.Serializable]
    public class SteelMaker
    {
        public string name = "鉄鋼メーカー";
        public Faction faction;

        /// <summary>生産能力（粗鋼の上限）。</summary>
        public float capacity = 0f;

        /// <summary>固定費（高炉等の維持費＝装置産業の重い固定費）。</summary>
        public float fixedCost = 0f;

        /// <summary>電炉か（スクラップを溶かす＝原料調達が国内循環・小回り）。false＝高炉（鉄鉱石#2018＋石炭依存）。</summary>
        public bool isElectricFurnace = false;

        public SteelMaker() { }

        public SteelMaker(string name, float capacity = 0f, float fixedCost = 0f,
            bool isElectricFurnace = false, Faction faction = default)
        {
            this.name = string.IsNullOrEmpty(name) ? "鉄鋼メーカー" : name;
            this.capacity = capacity;
            this.fixedCost = fixedCost;
            this.isElectricFurnace = isElectricFurnace;
            this.faction = faction;
        }
    }
}
