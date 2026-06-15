namespace Ginei
{
    /// <summary>公益事業の種別（#2021 UTL）。電気・ガス・水道＝生活/産業を支えるインフラ。</summary>
    public enum UtilityType { 電気, ガス, 水道 }

    /// <summary>
    /// 公益事業者（電気・ガス・水道・#2021・純データ）。高い固定費の自然独占ゆえ料金が規制される。規制資産（設備投資額＝料金の
    /// 根拠）・許容利益率・供給能力・需要を持つ。<b>民営かどうかは政体および法律による</b>（共産は国有・資本主義も民営化法次第・
    /// <see cref="PropertyRules"/>）。不採算地域にも供給するユニバーサルサービス義務。解決は <see cref="UtilityRules"/>。
    /// </summary>
    [System.Serializable]
    public class Utility
    {
        public string name = "公益事業者";
        public Faction faction;

        /// <summary>公益事業の種別。</summary>
        public UtilityType utilityType = UtilityType.電気;

        /// <summary>規制資産（rate base＝設備投資の累積。料金算定の根拠＝これに許容利益率を掛けて報酬）。</summary>
        public float rateBase = 0f;

        /// <summary>許容利益率（規制で認められた規制資産への報酬率）。</summary>
        public float allowedReturnRate = UtilityRules.DefaultAllowedReturn;

        /// <summary>供給能力（発電/供給の上限）。</summary>
        public float capacity = 0f;

        /// <summary>需要（ピーク需要。能力超過で停電/断水）。</summary>
        public float demand = 0f;

        public Utility() { }

        public Utility(string name, UtilityType utilityType = UtilityType.電気, float rateBase = 0f,
            float allowedReturnRate = UtilityRules.DefaultAllowedReturn, float capacity = 0f, float demand = 0f, Faction faction = default)
        {
            this.name = string.IsNullOrEmpty(name) ? "公益事業者" : name;
            this.utilityType = utilityType;
            this.rateBase = rateBase;
            this.allowedReturnRate = allowedReturnRate;
            this.capacity = capacity;
            this.demand = demand;
            this.faction = faction;
        }
    }
}
