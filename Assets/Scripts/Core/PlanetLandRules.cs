using System.Collections.Generic;
using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 惑星の土地＝ネームド財産の所有ロジック（#2019/#2070 連携・純ロジック・唯一の窓口）。惑星の土地は
    /// <b>権利証（<see cref="PropertyDeed"/>）の持分</b>として国家・個人が所有する＝個人（人物）が私有持分を持ち、
    /// <b>国家は残り（残余持分）を所有</b>して土地は完全に所有される（国家＋個人で合計＝1）。土地の評価額は
    /// <b>惑星の不動産基盤</b>（<see cref="PlanetRealEstate.landValue"/>・<see cref="PlanetRealEstateRules"/>）に連動する。
    /// 土地を私有できるかは政体による（共産＝国有のみ＝私有持分は国有化・<see cref="RealEstateRules.CanPrivatizeLand"/> RE-1）。
    /// 台帳は <see cref="PropertyDeedRegistry"/>、評価/地代は <see cref="PropertyValuationRules"/> へ委譲し二重実装しない。test-first。
    /// </summary>
    public static class PlanetLandRules
    {
        /// <summary>国家保有地の既定地代率（年）。</summary>
        public const float DefaultStateRentRate = 0.04f;

        /// <summary>惑星の私有持分の合計（人物所有の権利証のみ・0..1にクランプ）。</summary>
        public static float PrivateShare(int systemId)
        {
            float sum = 0f;
            var ds = PropertyDeedRegistry.DeedsOnSystem(systemId);
            for (int i = 0; i < ds.Count; i++)
                if (ds[i] != null && ds[i].IsPersonOwned) sum += Mathf.Max(0f, ds[i].share);
            return Mathf.Clamp01(sum);
        }

        /// <summary>惑星の国家持分＝1−私有持分（国家は残りすべてを所有＝土地は完全所有）。</summary>
        public static float StateShare(int systemId) => Mathf.Clamp01(1f - PrivateShare(systemId));

        /// <summary>権利証の市場評価＝惑星評価額×持分（<see cref="PropertyValuationRules.DeedValue"/> へ委譲）。</summary>
        public static float DeedMarketValue(PropertyDeed d) => PropertyValuationRules.DeedValue(d);

        /// <summary>惑星の全権利証の評価額（baseValue＝惑星全体価値）を現在の地価へ同期＝土地の価値が不動産基盤に連動する。</summary>
        public static void SyncValuations(int systemId, float planetLandValue)
        {
            float v = Mathf.Max(0f, planetLandValue);
            var ds = PropertyDeedRegistry.DeedsOnSystem(systemId);
            for (int i = 0; i < ds.Count; i++)
                if (ds[i] != null) ds[i].baseValue = v;
        }

        /// <summary>
        /// 惑星の私有地を国有化（共産＝土地は国有のみ RE-1）：人物所有の権利証を国家所有へ移し、持分を国家へ集約する。
        /// 残余持分の集約は <see cref="EnsureStateDeed"/> が行う（国家持分＝1へ）。
        /// </summary>
        public static void NationalizePrivateDeeds(int systemId, Faction faction)
        {
            var ds = PropertyDeedRegistry.DeedsOnSystem(systemId);
            for (int i = 0; i < ds.Count; i++)
            {
                PropertyDeed d = ds[i];
                if (d == null || !d.IsPersonOwned) continue;
                d.ownerKind = AssetOwnerKind.国家;
                d.ownerFaction = faction;
                d.share = 0f; // 私有持分は没収（国家の残余持分へ集約）
            }
        }

        /// <summary>
        /// 国家の残余持分を持つ権利証を確保＝<b>国家が私有の残りを所有</b>して土地を完全所有させる（無ければ作る・冪等）。
        /// <paramref name="canPrivatize"/>＝false（共産）なら私有を国有化してから国家持分＝1。評価額は惑星の地価に同期。
        /// </summary>
        public static PropertyDeed EnsureStateDeed(int systemId, Faction faction, float planetLandValue, bool canPrivatize)
        {
            if (!canPrivatize) NationalizePrivateDeeds(systemId, faction);

            // この勢力の国家権利証を1枚に集約（最初の1枚を本体、余分は持分0へ）。
            PropertyDeed state = null;
            var ds = PropertyDeedRegistry.DeedsOnSystem(systemId);
            for (int i = 0; i < ds.Count; i++)
            {
                PropertyDeed d = ds[i];
                if (d == null || !d.IsFactionOwned || d.ownerFaction != faction) continue;
                if (state == null) state = d;
                else d.share = 0f; // 重複した国家権利証は本体へ集約
            }
            if (state == null)
            {
                state = new PropertyDeed(0, systemId, 0f, Mathf.Max(0f, planetLandValue))
                {
                    ownerKind = AssetOwnerKind.国家,
                    ownerFaction = faction,
                    rentRate = DefaultStateRentRate,
                };
                PropertyDeedRegistry.Register(state);
            }
            // 国家は残余持分（1−私有）を所有。評価額は地価に同期。
            float priv = canPrivatize ? PrivateShare(systemId) : 0f;
            state.share = Mathf.Clamp01(1f - priv);
            state.baseValue = Mathf.Max(0f, planetLandValue);
            if (state.rentRate <= 0f) state.rentRate = DefaultStateRentRate;
            return state;
        }

        /// <summary>惑星の土地が完全所有か＝持分合計が1付近（国家＋個人）。</summary>
        public static bool IsFullyOwned(int systemId, float eps = 1e-3f)
            => Mathf.Abs(PropertyDeedRegistry.TotalShareOnSystem(systemId) - 1f) <= Mathf.Max(0f, eps);
    }
}
