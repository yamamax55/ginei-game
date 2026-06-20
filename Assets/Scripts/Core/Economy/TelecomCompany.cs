namespace Ginei
{
    /// <summary>通信会社（東証33業種「情報・通信業」・#2024・純データ）。加入者基盤×ARPU の安定収益、解約率（チャーン）、巨額の設備投資（基地局）、ネットワーク効果が特徴。解決は <see cref="TelecomRules"/>。</summary>
    [System.Serializable]
    public class TelecomCompany
    {
        public string name = "通信会社";
        public Faction faction;
        /// <summary>加入者数。</summary>
        public float subscribers = 0f;
        /// <summary>ARPU（加入者1人あたり収入）。</summary>
        public float arpu = 0f;
        /// <summary>解約率（churn・0..1）。</summary>
        public float churnRate = 0.1f;

        public TelecomCompany() { }
        public TelecomCompany(string name, float subscribers = 0f, float arpu = 0f, float churnRate = 0.1f, Faction faction = default)
        {
            this.name = string.IsNullOrEmpty(name) ? "通信会社" : name;
            this.subscribers = subscribers; this.arpu = arpu; this.churnRate = churnRate; this.faction = faction;
        }
    }
}
