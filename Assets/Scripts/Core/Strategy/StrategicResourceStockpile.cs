using System.Collections.Generic;
using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 勢力の希少資源（戦略資源 #178）備蓄。基本資源の <see cref="ResourceStockpile"/> と別レイヤー＝種類が可変なので辞書で持つ。
    /// 非負（消費は不足なら失敗＝部分消費しない）。産出は <see cref="StrategicResourceRules"/> が惑星から加える。
    /// </summary>
    public class StrategicResourceStockpile
    {
        private readonly Dictionary<StrategicResourceType, float> amounts = new Dictionary<StrategicResourceType, float>();

        /// <summary>その希少資源の在庫量（無ければ0）。</summary>
        public float Get(StrategicResourceType type) => amounts.TryGetValue(type, out var v) ? v : 0f;

        /// <summary>加減算（下限0・負で減らせる）。</summary>
        public void Add(StrategicResourceType type, float amount)
            => amounts[type] = Mathf.Max(0f, Get(type) + amount);

        /// <summary>その量以上を保有するか。</summary>
        public bool Has(StrategicResourceType type, float amount) => Get(type) >= amount;

        /// <summary>足りれば消費して true（不足なら据え置きで false＝部分消費しない）。</summary>
        public bool TryConsume(StrategicResourceType type, float amount)
        {
            if (amount <= 0f) return true;
            if (Get(type) < amount) return false;
            amounts[type] = Get(type) - amount;
            return true;
        }

        /// <summary>全希少資源の合計量。</summary>
        public float Total
        {
            get { float s = 0f; foreach (var v in amounts.Values) s += v; return s; }
        }

        public bool IsEmpty => Total <= 0f;
    }
}
