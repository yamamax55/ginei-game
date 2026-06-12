namespace Ginei
{
    /// <summary>
    /// 海運会社（東証33業種「海運業」・#2024・純データ）。船で貨物を運ぶ＝運賃市況が乱高下し船腹需給で運命が決まる業種。船腹
    /// （供給）・燃料費・用船料を持つ。戦時は通商破壊（#94/#95）で運賃/保険（#1982）が跳ねる。解決は <see cref="ShippingRules"/>。少数集約。
    /// </summary>
    [System.Serializable]
    public class ShippingCompany
    {
        public string name = "海運会社";
        public Faction faction;

        /// <summary>船腹（供給能力＝運べる貨物量）。</summary>
        public float fleetCapacity = 0f;

        /// <summary>燃料費（1航海あたり。原油価格に連動）。</summary>
        public float fuelCost = 0f;

        /// <summary>用船料（他社から船を借りるコスト。自社船はこれが0で固定費）。</summary>
        public float charterCost = 0f;

        public ShippingCompany() { }

        public ShippingCompany(string name, float fleetCapacity = 0f, float fuelCost = 0f,
            float charterCost = 0f, Faction faction = default)
        {
            this.name = string.IsNullOrEmpty(name) ? "海運会社" : name;
            this.fleetCapacity = fleetCapacity;
            this.fuelCost = fuelCost;
            this.charterCost = charterCost;
            this.faction = faction;
        }
    }
}
