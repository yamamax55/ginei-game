using UnityEngine;

namespace Ginei
{
    /// <summary>代表生産チェーンの品目（VCHAIN-1・#2091）。森林→木材→建材→住宅 の中間財。</summary>
    public enum SupplyChainGood { 木材, 建材, 住宅 }

    /// <summary>
    /// 代表生産チェーンの惑星在庫（VCHAIN-1・#2091・純データ・後方互換）。
    /// 森林（再生資源）＋木材/建材/住宅（中間財・耐久財）の在庫を惑星ごとに持つ＝産業連鎖が流れる器。
    /// 集約（惑星×3品目）＝個体粒度へ降りない。test-first。
    /// </summary>
    public class ChainStock
    {
        public float forest;     // 森林（再生資源＝木材の源）
        public float wood;       // 木材
        public float materials;  // 建材
        public float housing;    // 住宅ストック（耐久財）

        public ChainStock() { }

        public ChainStock(float forest, float wood = 0f, float materials = 0f, float housing = 0f)
        {
            this.forest = Mathf.Max(0f, forest);
            this.wood = Mathf.Max(0f, wood);
            this.materials = Mathf.Max(0f, materials);
            this.housing = Mathf.Max(0f, housing);
        }

        /// <summary>品目の在庫を取得（森林は別アクセサ）。</summary>
        public float Get(SupplyChainGood g)
        {
            switch (g)
            {
                case SupplyChainGood.建材: return materials;
                case SupplyChainGood.住宅: return housing;
                default: return wood;
            }
        }

        /// <summary>品目の在庫を増減（負にはならない）。</summary>
        public void Add(SupplyChainGood g, float amount)
        {
            switch (g)
            {
                case SupplyChainGood.建材: materials = Mathf.Max(0f, materials + amount); break;
                case SupplyChainGood.住宅: housing = Mathf.Max(0f, housing + amount); break;
                default: wood = Mathf.Max(0f, wood + amount); break;
            }
        }
    }
}
