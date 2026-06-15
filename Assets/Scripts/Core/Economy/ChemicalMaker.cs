namespace Ginei
{
    /// <summary>
    /// 化学メーカー（東証33業種「化学」・#2024・純データ）。装置産業＝巨大プラントの高固定費で汎用化学品/スペシャリティを作る。
    /// プラント能力・稼働率・1単位マージン・固定費・スペシャリティか（高付加価値・安定）を持つ。市況スプレッド（製品−原料）で
    /// マージンが乱高下する。解決は <see cref="ChemicalRules"/>。少数集約（タイクン化回避）。
    /// </summary>
    [System.Serializable]
    public class ChemicalMaker
    {
        public string name = "化学メーカー";
        public Faction faction;

        /// <summary>プラント能力（生産上限）。</summary>
        public float capacity = 0f;

        /// <summary>稼働率（0..1。高固定費ゆえ稼働率が利益を大きく動かす＝オペレーティングレバレッジ）。</summary>
        public float utilization = 0.8f;

        /// <summary>固定費（プラントの維持費＝稼働に関わらずかかる）。</summary>
        public float fixedCost = 0f;

        /// <summary>スペシャリティか（高付加価値・市況に左右されにくい）。false＝汎用市況品。</summary>
        public bool isSpecialty = false;

        public ChemicalMaker() { }

        public ChemicalMaker(string name, float capacity = 0f, float utilization = 0.8f, float fixedCost = 0f,
            bool isSpecialty = false, Faction faction = default)
        {
            this.name = string.IsNullOrEmpty(name) ? "化学メーカー" : name;
            this.capacity = capacity;
            this.utilization = utilization;
            this.fixedCost = fixedCost;
            this.isSpecialty = isSpecialty;
            this.faction = faction;
        }
    }
}
