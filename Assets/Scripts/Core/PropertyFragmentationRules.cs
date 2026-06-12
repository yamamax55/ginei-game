using System.Collections.Generic;
using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 不動産の細分化の純ロジック（NFIN-5・#2070＝分地相続）。
    /// 相続#152 のたびに権利証が相続人へ等分され、1惑星の持分が細かく分かれていく（<b>時とともに細分化傾向</b>）。
    /// 台帳 <see cref="PropertyDeedRegistry"/> を編集。test-first。
    /// </summary>
    public static class PropertyFragmentationRules
    {
        /// <summary>持分を相続人数で等分（各 share/N）。N≤1 はそのまま1枠。</summary>
        public static float SplitShares(float share, int heirCount)
        {
            if (heirCount <= 1) return Mathf.Max(0f, share);
            return Mathf.Max(0f, share) / heirCount;
        }

        /// <summary>
        /// 相続による細分化（分地相続）。元の権利証を除去し、相続人ごとに持分を等分した権利証を登録＝1枚→N枚。
        /// 相続人不在（heirIds 空）は false（呼び側が国家へ単独移転＝細分化しない）。新規 deed のリストを返す。
        /// </summary>
        public static List<PropertyDeed> FragmentOnInheritance(PropertyDeed deed, IList<int> heirIds)
        {
            var created = new List<PropertyDeed>();
            if (deed == null || heirIds == null || heirIds.Count == 0) return created;

            float each = SplitShares(deed.share, heirIds.Count);
            PropertyDeedRegistry.Remove(deed.id);
            for (int i = 0; i < heirIds.Count; i++)
            {
                var part = new PropertyDeed(0, deed.systemId, each, deed.baseValue)
                {
                    ownerKind = AssetOwnerKind.人物, ownerPersonId = heirIds[i], rentRate = deed.rentRate
                };
                PropertyDeedRegistry.Register(part);
                created.Add(part);
            }
            return created;
        }

        /// <summary>細分化度＝指定惑星の権利証枚数（多いほど細分化＝<see cref="PropertyDeedRegistry.CountDeedsOnSystem"/>）。</summary>
        public static int FragmentationIndex(int systemId)
            => PropertyDeedRegistry.CountDeedsOnSystem(systemId);

        /// <summary>
        /// 買い集め（細分化の逆＝統合）。指定惑星の権利証を1人へ集約し、持分を合算した1枚にする。
        /// 集約する権利証が無ければ null。
        /// </summary>
        public static PropertyDeed Consolidate(int systemId, int toPersonId)
        {
            var onSystem = PropertyDeedRegistry.DeedsOnSystem(systemId);
            if (onSystem.Count == 0) return null;
            float totalShare = 0f;
            float baseValue = onSystem[0].baseValue;
            float rentRate = onSystem[0].rentRate;
            for (int i = 0; i < onSystem.Count; i++)
            {
                totalShare += onSystem[i].share;
                PropertyDeedRegistry.Remove(onSystem[i].id);
            }
            var merged = new PropertyDeed(0, systemId, Mathf.Clamp01(totalShare), baseValue)
            {
                ownerKind = AssetOwnerKind.人物, ownerPersonId = toPersonId, rentRate = rentRate
            };
            return PropertyDeedRegistry.Register(merged);
        }
    }
}
