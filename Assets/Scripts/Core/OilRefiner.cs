namespace Ginei
{
    /// <summary>石油精製会社（東証33業種「石油・石炭製品」・#2024・純データ）。原油を精製し石油製品を作る。精製マージン（クラックスプレッド）と原油市況・在庫評価損益が損益を左右する。解決は <see cref="OilRefiningRules"/>。</summary>
    [System.Serializable]
    public class OilRefiner
    {
        public string name = "石油精製会社";
        public Faction faction;
        /// <summary>固定費（製油所の維持費）。</summary>
        public float fixedCost = 0f;
        /// <summary>原油在庫（価格変動で評価損益が出る）。</summary>
        public float crudeInventory = 0f;

        public OilRefiner() { }
        public OilRefiner(string name, float fixedCost = 0f, float crudeInventory = 0f, Faction faction = default)
        {
            this.name = string.IsNullOrEmpty(name) ? "石油精製会社" : name;
            this.fixedCost = fixedCost; this.crudeInventory = crudeInventory; this.faction = faction;
        }
    }
}
