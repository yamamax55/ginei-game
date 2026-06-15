namespace Ginei
{
    /// <summary>食品メーカー（東証33業種「食料品」・#2024・純データ）。生活必需＝景気に左右されにくいディフェンシブ需要、ブランド、原料コストの転嫁が特徴。解決は <see cref="FoodRules"/>。</summary>
    [System.Serializable]
    public class FoodMaker
    {
        public string name = "食品メーカー";
        public Faction faction;
        /// <summary>景気感応度（0..1。食料品は低い＝景気非敏感＝ディフェンシブ）。</summary>
        public float cyclicalSensitivity = 0.2f;
        /// <summary>原料コストの価格転嫁率（0..1。全部は転嫁できずマージンが削られる）。</summary>
        public float passThroughRate = 0.5f;

        public FoodMaker() { }
        public FoodMaker(string name, float cyclicalSensitivity = 0.2f, float passThroughRate = 0.5f, Faction faction = default)
        {
            this.name = string.IsNullOrEmpty(name) ? "食品メーカー" : name;
            this.cyclicalSensitivity = cyclicalSensitivity; this.passThroughRate = passThroughRate; this.faction = faction;
        }
    }
}
