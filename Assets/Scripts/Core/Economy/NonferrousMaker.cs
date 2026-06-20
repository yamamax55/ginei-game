namespace Ginei
{
    /// <summary>非鉄金属メーカー（東証33業種「非鉄金属」・#2024・純データ）。鉱石を製錬して地金（銅/アルミ等）を作る。価格は国際市況（LME）で決まる price taker、製錬マージン（TC/RC）で稼ぐ。固定費を持つ。解決は <see cref="NonferrousRules"/>。</summary>
    [System.Serializable]
    public class NonferrousMaker
    {
        public string name = "非鉄金属メーカー";
        public Faction faction;
        /// <summary>固定費（製錬所の維持費）。</summary>
        public float fixedCost = 0f;

        public NonferrousMaker() { }
        public NonferrousMaker(string name, float fixedCost = 0f, Faction faction = default)
        {
            this.name = string.IsNullOrEmpty(name) ? "非鉄金属メーカー" : name;
            this.fixedCost = fixedCost; this.faction = faction;
        }
    }
}
