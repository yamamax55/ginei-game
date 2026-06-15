using System.Collections.Generic;
using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 品目在庫（BOM-3・#2098・純データ）。エンティティ（惑星/勢力/企業）が品目→数量で持つ汎用在庫。
    /// `ResourceStockpile`#92 の汎用品目版＝少数品目の集約（全SKU展開しない）。test-first。
    /// </summary>
    public class CommodityStock
    {
        readonly Dictionary<int, float> amounts = new Dictionary<int, float>();

        /// <summary>在庫を取得（未登録は0）。</summary>
        public float Get(int commodityId)
            => amounts.TryGetValue(commodityId, out float v) ? v : 0f;

        /// <summary>在庫を増減（非負・0は保持）。</summary>
        public void Add(int commodityId, float amount)
        {
            float v = Mathf.Max(0f, Get(commodityId) + amount);
            amounts[commodityId] = v;
        }

        /// <summary>在庫を設定（非負）。</summary>
        public void Set(int commodityId, float amount)
            => amounts[commodityId] = Mathf.Max(0f, amount);

        public void Clear() => amounts.Clear();

        /// <summary>全在庫の合計（規模感）。</summary>
        public float Total
        {
            get
            {
                float sum = 0f;
                foreach (var kv in amounts) sum += kv.Value;
                return sum;
            }
        }
    }
}
