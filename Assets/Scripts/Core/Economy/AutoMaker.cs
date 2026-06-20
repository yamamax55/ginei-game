namespace Ginei
{
    /// <summary>自動車メーカー（東証33業種「輸送用機器」・#2024・純データ）。量産・規模の経済・系列サプライチェーン・モデルチェンジ・リコールが特徴。解決は <see cref="AutoRules"/>。</summary>
    [System.Serializable]
    public class AutoMaker
    {
        public string name = "自動車メーカー";
        public Faction faction;
        /// <summary>固定費（巨大工場の維持費）。</summary>
        public float fixedCost = 0f;
        /// <summary>現行モデルの経過年（モデルが古いほど販売が落ちる）。</summary>
        public float modelAgeYears = 0f;

        public AutoMaker() { }
        public AutoMaker(string name, float fixedCost = 0f, float modelAgeYears = 0f, Faction faction = default)
        {
            this.name = string.IsNullOrEmpty(name) ? "自動車メーカー" : name;
            this.fixedCost = fixedCost; this.modelAgeYears = modelAgeYears; this.faction = faction;
        }
    }
}
