using System.Collections.Generic;
using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 純資産の集計（#2056/#2063/#2070 横断・唯一の窓口）。所有者（<see cref="OwnerRef"/>＝人物/国家/企業）の<b>総資産</b>を
    /// 各台帳から横断集計する＝ネームド資産（<see cref="NamedAssetRegistry"/>）＋金融持分（<see cref="FinancialHoldingRegistry"/>時価）＋
    /// 土地権利証（<see cref="PropertyDeedRegistry"/>評価）。現金（<see cref="Person.wealth"/>#2056／<see cref="FactionState.treasury"/>#163／
    /// <see cref="Enterprise.capital"/>#1022）は呼び側が渡す（液状＝Core は台帳資産のみ集計）。財産オブザーバ（観測層）の入力。
    /// 評価は各 Rules（<see cref="FinancialAssetRules.MarketValue"/>/<see cref="PropertyValuationRules.DeedValue"/>）へ委譲し二重実装しない。test-first。
    /// </summary>
    public static class NetWorthRules
    {
        /// <summary>所有者のネームド資産の時価合計。</summary>
        public static float NamedAssetValue(OwnerRef o)
        {
            var l = NamedListOf(o);
            float s = 0f;
            for (int i = 0; i < l.Count; i++) if (l[i] != null) s += Mathf.Max(0f, l[i].value);
            return s;
        }

        /// <summary>所有者の金融持分の時価合計（株式/債券/投資信託・暴落で目減り）。</summary>
        public static float HoldingsValue(OwnerRef o)
        {
            var l = HoldingListOf(o);
            float s = 0f;
            for (int i = 0; i < l.Count; i++) s += FinancialAssetRules.MarketValue(l[i]);
            return s;
        }

        /// <summary>所有者の土地権利証の評価合計（持分×評価額）。</summary>
        public static float LandValue(OwnerRef o)
        {
            var l = DeedListOf(o);
            float s = 0f;
            for (int i = 0; i < l.Count; i++) s += PropertyValuationRules.DeedValue(l[i]);
            return s;
        }

        /// <summary>所有者の台帳資産合計＝ネームド資産＋金融持分＋土地（現金を除く）。</summary>
        public static float AssetValue(OwnerRef o) => NamedAssetValue(o) + HoldingsValue(o) + LandValue(o);

        /// <summary>所有者の純資産＝現金（呼び側が渡す）＋台帳資産。</summary>
        public static float NetWorth(OwnerRef o, float liquid) => Mathf.Max(0f, liquid) + AssetValue(o);

        // --- 内部：所有者種別ごとの台帳引き ---
        static List<NamedAsset> NamedListOf(OwnerRef o)
        {
            switch (o.kind)
            {
                case AssetOwnerKind.人物: return NamedAssetRegistry.OwnedByPerson(o.personId);
                case AssetOwnerKind.企業: return NamedAssetRegistry.OwnedByEnterprise(o.enterpriseId);
                default: return NamedAssetRegistry.OwnedByFaction(o.faction);
            }
        }
        static List<FinancialHolding> HoldingListOf(OwnerRef o)
        {
            switch (o.kind)
            {
                case AssetOwnerKind.人物: return FinancialHoldingRegistry.OwnedByPerson(o.personId);
                case AssetOwnerKind.企業: return FinancialHoldingRegistry.OwnedByEnterprise(o.enterpriseId);
                default: return FinancialHoldingRegistry.OwnedByFaction(o.faction);
            }
        }
        static List<PropertyDeed> DeedListOf(OwnerRef o)
        {
            switch (o.kind)
            {
                case AssetOwnerKind.人物: return PropertyDeedRegistry.OwnedByPerson(o.personId);
                case AssetOwnerKind.企業: return PropertyDeedRegistry.OwnedByEnterprise(o.enterpriseId);
                default: return PropertyDeedRegistry.OwnedByFaction(o.faction);
            }
        }
    }
}
