namespace Ginei
{
    /// <summary>精密機器メーカー（東証33業種「精密機器」・#2024・純データ）。高付加価値・ニッチ・高利益率＝技術障壁で守られた少量高採算が特徴。技術水準を持つ。解決は <see cref="PrecisionRules"/>。</summary>
    [System.Serializable]
    public class PrecisionMaker
    {
        public string name = "精密機器メーカー";
        public Faction faction;
        /// <summary>技術水準（高いほど競合に対する価格支配力＝高マージン）。</summary>
        public float techLevel = 0f;

        public PrecisionMaker() { }
        public PrecisionMaker(string name, float techLevel = 0f, Faction faction = default)
        {
            this.name = string.IsNullOrEmpty(name) ? "精密機器メーカー" : name;
            this.techLevel = techLevel; this.faction = faction;
        }
    }
}
