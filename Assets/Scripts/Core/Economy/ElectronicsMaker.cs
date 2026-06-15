namespace Ginei
{
    /// <summary>電気機器/半導体メーカー（東証33業種「電気機器」・#2024・純データ）。シリコンサイクル（需給で価格乱高下）・微細化世代・巨額設備投資・短い製品寿命が特徴。解決は <see cref="ElectronicsRules"/>。</summary>
    [System.Serializable]
    public class ElectronicsMaker
    {
        public string name = "電機メーカー";
        public Faction faction;
        /// <summary>プロセス世代水準（微細化の進み＝大きいほど先端）。</summary>
        public float processNodeLevel = 0f;
        /// <summary>固定費（巨額の半導体工場 fab）。</summary>
        public float fixedCost = 0f;

        public ElectronicsMaker() { }
        public ElectronicsMaker(string name, float processNodeLevel = 0f, float fixedCost = 0f, Faction faction = default)
        {
            this.name = string.IsNullOrEmpty(name) ? "電機メーカー" : name;
            this.processNodeLevel = processNodeLevel; this.fixedCost = fixedCost; this.faction = faction;
        }
    }
}
