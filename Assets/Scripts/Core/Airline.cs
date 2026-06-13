namespace Ginei
{
    /// <summary>航空会社（東証33業種「空運業」・#2024・純データ）。高固定費＝座席稼働率（ロードファクター）が損益を決め、燃料費が重く、イールドマネジメントで運賃を最適化する。解決は <see cref="AirlineRules"/>。</summary>
    [System.Serializable]
    public class Airline
    {
        public string name = "航空会社";
        public Faction faction;
        /// <summary>座席数（供給）。</summary>
        public float seats = 0f;
        /// <summary>固定費（機材リース・人件費＝飛んでもいなくてもかかる）。</summary>
        public float fixedCost = 0f;
        /// <summary>燃料費（1便あたり。原油価格に連動）。</summary>
        public float fuelCost = 0f;

        public Airline() { }
        public Airline(string name, float seats = 0f, float fixedCost = 0f, float fuelCost = 0f, Faction faction = default)
        {
            this.name = string.IsNullOrEmpty(name) ? "航空会社" : name;
            this.seats = seats; this.fixedCost = fixedCost; this.fuelCost = fuelCost; this.faction = faction;
        }
    }
}
