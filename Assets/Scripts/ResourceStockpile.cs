using UnityEngine;

namespace Ginei
{
    /// <summary>戦時兵站の資源（#92・少数に絞る）。人口は L-4 #96 で別管理。</summary>
    public enum ResourceType { 物資, 弾薬, 燃料 }

    /// <summary>星系の類型（L-1 #93）。支配星系が類型に応じて時間で産出する（建設マイクロ無し＝類型で決まる）。</summary>
    public enum SystemType { 工業, 農業, 鉱業, 居住 }

    /// <summary>
    /// 勢力の資源備蓄（L-1 #93）。物資/弾薬/燃料を保持し、生産で増え消費で減る。負にはならない。
    /// 産出は <see cref="ResourceProductionRules"/>、補給線/消費は <see cref="SupplyRules"/> が扱う。純データ。
    /// </summary>
    public class ResourceStockpile
    {
        public float supplies; // 物資（食料含む）
        public float ammo;     // 弾薬
        public float fuel;     // 燃料

        public ResourceStockpile() { }

        public ResourceStockpile(float supplies, float ammo, float fuel)
        {
            this.supplies = Mathf.Max(0f, supplies);
            this.ammo = Mathf.Max(0f, ammo);
            this.fuel = Mathf.Max(0f, fuel);
        }

        public float Get(ResourceType t)
        {
            switch (t)
            {
                case ResourceType.弾薬: return ammo;
                case ResourceType.燃料: return fuel;
                default: return supplies;
            }
        }

        private void Set(ResourceType t, float v)
        {
            v = Mathf.Max(0f, v);
            switch (t)
            {
                case ResourceType.弾薬: ammo = v; break;
                case ResourceType.燃料: fuel = v; break;
                default: supplies = v; break;
            }
        }

        /// <summary>増減する（合計は0未満にならない＝枯渇は0で止まる）。生産は正・枯渇は負で渡す。</summary>
        public void Add(ResourceType t, float amount) => Set(t, Get(t) + amount);

        /// <summary>各資源を一律に増減する（補給で+・補給切れで−）。</summary>
        public void AddAll(float amount)
        {
            Add(ResourceType.物資, amount);
            Add(ResourceType.弾薬, amount);
            Add(ResourceType.燃料, amount);
        }

        public bool CanAfford(ResourceType t, float amount) => Get(t) >= amount;

        /// <summary>消費する（足りれば引いて true、足りなければ何もせず false）。</summary>
        public bool TryConsume(ResourceType t, float amount)
        {
            if (amount < 0f || Get(t) < amount) return false;
            Set(t, Get(t) - amount);
            return true;
        }

        /// <summary>いずれかの資源が枯渇しているか（補給切れ＝飢餓の判定に使う）。</summary>
        public bool IsDepleted => supplies <= 0f || ammo <= 0f || fuel <= 0f;
    }
}
