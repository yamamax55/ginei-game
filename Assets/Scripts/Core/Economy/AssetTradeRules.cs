using System;
using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 所有者（人物/国家/企業）の資金へのアクセス（#2056/#163/#1022 を統一窓口に）。人物の財産 <see cref="Person.wealth"/>／
    /// 国家の国庫 <see cref="FactionState.treasury"/>／企業の資本 <see cref="Enterprise.capital"/> を <see cref="OwnerRef"/> で読み書きする。
    /// 解決子（resolver）は Game 側が渡す＝Core は型に依存せず資金を移せる。<see cref="AssetTradeRules"/> の決済で使う。
    /// </summary>
    public readonly struct FundsAccess
    {
        readonly Func<int, Person> resolvePerson;
        readonly Func<int, Enterprise> resolveEnterprise;
        readonly Func<Faction, FactionState> resolveState;

        public FundsAccess(Func<int, Person> resolvePerson, Func<int, Enterprise> resolveEnterprise, Func<Faction, FactionState> resolveState)
        {
            this.resolvePerson = resolvePerson;
            this.resolveEnterprise = resolveEnterprise;
            this.resolveState = resolveState;
        }

        /// <summary>所有者の現在資金（解決不能は0）。</summary>
        public float Get(OwnerRef o)
        {
            switch (o.kind)
            {
                case AssetOwnerKind.人物: { Person p = resolvePerson?.Invoke(o.personId); return p != null ? p.wealth : 0f; }
                case AssetOwnerKind.企業: { Enterprise e = resolveEnterprise?.Invoke(o.enterpriseId); return e != null ? e.capital : 0f; }
                default: { FactionState s = resolveState?.Invoke(o.faction); return s != null ? s.treasury : 0f; }
            }
        }

        /// <summary>資金を加減算（非負クランプ）。解決できれば true。</summary>
        public bool Add(OwnerRef o, float delta)
        {
            switch (o.kind)
            {
                case AssetOwnerKind.人物: { Person p = resolvePerson?.Invoke(o.personId); if (p == null) return false; p.wealth = Mathf.Max(0f, p.wealth + delta); return true; }
                case AssetOwnerKind.企業: { Enterprise e = resolveEnterprise?.Invoke(o.enterpriseId); if (e == null) return false; e.capital = Mathf.Max(0f, e.capital + delta); return true; }
                default: { FactionState s = resolveState?.Invoke(o.faction); if (s == null) return false; s.treasury = Mathf.Max(0f, s.treasury + delta); return true; }
            }
        }

        /// <summary>その額を払えるか（資金 ≥ 額）。</summary>
        public bool CanAfford(OwnerRef o, float amount) => Get(o) >= Mathf.Max(0f, amount);
    }

    /// <summary>
    /// 財産の購入・売却システムの純ロジック（#2063/#2070 横断・唯一の窓口）。ネームド資産（邸宅/領地/旗艦/美術品/企業/…）と
    /// 金融持分（株式/債券/投資信託）を<b>所有者間で売買</b>する＝価格を評価し、所有権/口数を付け替え、資金を決済する
    /// （買主の資金を引き、売主へ渡す）。所有者は人物/国家/企業（<see cref="OwnerRef"/>）。土地は <see cref="LandTransactionRules"/>（#2019）が担い、
    /// 本ルールはネームド資産・金融持分を担う。譲渡可否は <see cref="AssetTransferRules.CanTransfer"/>（称号は不可）。台帳は
    /// <see cref="NamedAssetRegistry"/>/<see cref="FinancialHoldingRegistry"/>。資金は <see cref="FundsAccess"/> で抽象化。test-first。
    /// </summary>
    public static class AssetTradeRules
    {
        // ===== 評価（価格） =====

        /// <summary>ネームド資産の取引価格＝時価×係数（プレミアム/ディスカウント）。</summary>
        public static float Price(NamedAsset a, float priceFactor = 1f)
            => a == null ? 0f : Mathf.Max(0f, a.value) * Mathf.Max(0f, priceFactor);

        /// <summary>金融持分の取引価格＝口数×単価（時価）。</summary>
        public static float Price(FinancialHolding h, float units)
            => h == null ? 0f : Mathf.Clamp(units, 0f, Mathf.Max(0f, h.units)) * Mathf.Max(0f, h.unitPrice);

        /// <summary>資金で買える最大口数＝資金/単価（単価0以下は0）。</summary>
        public static float MaxAffordableUnits(OwnerRef buyer, float unitPrice, FundsAccess funds)
            => unitPrice <= 0f ? 0f : funds.Get(buyer) / unitPrice;

        // ===== ネームド資産の売買 =====

        /// <summary>
        /// ネームド資産を購入＝現所有者（売主）から買主へ所有権を移し、価格ぶんの資金を決済（買主→売主）。
        /// 譲渡不可（称号等）/自己取引/資金不足は不成立（false）。成立価格は out。
        /// </summary>
        public static bool BuyNamedAsset(NamedAsset asset, OwnerRef buyer, FundsAccess funds, out float price, float priceFactor = 1f)
        {
            price = Price(asset, priceFactor);
            if (asset == null || !AssetTransferRules.CanTransfer(asset)) return false;
            OwnerRef seller = OwnerRef.FromAsset(asset);
            if (seller.Key == buyer.Key) return false;          // 自己取引不可
            if (!funds.CanAfford(buyer, price)) return false;   // 資金不足
            funds.Add(buyer, -price);
            funds.Add(seller, price);
            buyer.ApplyTo(asset);                                // 所有権の付け替え
            return true;
        }

        // ===== 金融持分の売買 =====

        /// <summary>売主の持分から口数を移し、買主の同一原資産の持分へ足す（無ければ作成）。実際に移した口数を返す。資金決済なし（純移転）。</summary>
        public static float TransferUnits(FinancialHolding seller, OwnerRef buyer, float units)
        {
            if (seller == null) return 0f;
            float moved = Mathf.Clamp(units, 0f, Mathf.Max(0f, seller.units));
            if (moved <= 0f) return 0f;
            seller.units -= moved;

            FinancialHolding dest = FindHolding(seller.underlyingId, seller.instrument, buyer);
            if (dest == null)
            {
                dest = new FinancialHolding(0, seller.instrument, seller.underlyingName)
                { underlyingId = seller.underlyingId, unitPrice = seller.unitPrice, incomePerUnit = seller.incomePerUnit };
                buyer.ApplyTo(dest);
                FinancialHoldingRegistry.Register(dest);
            }
            dest.units += moved;
            return moved;
        }

        /// <summary>指定原資産×商品の指定所有者の持分を探す（無ければ null）。</summary>
        public static FinancialHolding FindHolding(int underlyingId, FinancialInstrument instrument, OwnerRef owner)
        {
            var all = FinancialHoldingRegistry.All;
            for (int i = 0; i < all.Count; i++)
            {
                FinancialHolding h = all[i];
                if (h == null || h.underlyingId != underlyingId || h.instrument != instrument) continue;
                if (owner.Owns(h)) return h;
            }
            return null;
        }

        /// <summary>
        /// 金融持分を購入＝売主の口数を買主へ移し、口数×単価ぶんの資金を決済（買主→売主）。資金が足りなければ買える口数まで（部分約定）。
        /// 自己取引は不成立。移転口数は out、成立価格は戻り値。
        /// </summary>
        public static float BuyHolding(FinancialHolding seller, OwnerRef buyer, float units, FundsAccess funds, out float movedUnits)
        {
            movedUnits = 0f;
            if (seller == null) return 0f;
            OwnerRef sellerOwner = OwnerRef.FromHolding(seller);
            if (sellerOwner.Key == buyer.Key) return 0f;
            float want = Mathf.Clamp(units, 0f, Mathf.Max(0f, seller.units));
            if (seller.unitPrice > 0f)
            {
                float affordable = MaxAffordableUnits(buyer, seller.unitPrice, funds);
                want = Mathf.Min(want, affordable); // 部分約定（買える口数まで）
            }
            if (want <= 0f) return 0f;
            movedUnits = TransferUnits(seller, buyer, want);
            float price = movedUnits * Mathf.Max(0f, seller.unitPrice);
            funds.Add(buyer, -price);
            funds.Add(sellerOwner, price);
            return price;
        }
    }
}
