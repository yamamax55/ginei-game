using System.Collections.Generic;
using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// MRP netting（SCM-2・#2105・純ロジック）。総所要から手持ち在庫を差し引いて、実際に調達/生産すべき正味所要を出す。
    /// test-first。
    /// </summary>
    public static class NetRequirementsRules
    {
        /// <summary>正味所要＝max(0, 総所要−手持ち)。</summary>
        public static float Net(float gross, float onHand)
            => Mathf.Max(0f, gross - Mathf.Max(0f, onHand));

        /// <summary>総所要 dict を手持ち在庫で netting＝正の正味所要のみの dict を返す。</summary>
        public static Dictionary<int, float> NetRequirements(Dictionary<int, float> gross, CommodityStock onHand)
        {
            var net = new Dictionary<int, float>();
            if (gross == null) return net;
            foreach (var kv in gross)
            {
                float oh = onHand != null ? onHand.Get(kv.Key) : 0f;
                float n = Net(kv.Value, oh);
                if (n > 0f) net[kv.Key] = n;
            }
            return net;
        }
    }
}
