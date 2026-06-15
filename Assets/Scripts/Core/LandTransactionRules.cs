using System.Collections.Generic;
using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 土地の取引（購入・売却・貸出）の純ロジック（#2019 惑星土地・唯一の窓口）。区画（惑星×地目×持分）の権利を
    /// <b>売買で所有者を移し</b>（持分の移転）、<b>貸出（リース）で所有権を保ったまま用益を貸す</b>（地代）。価格は権利の評価額
    /// （<see cref="PropertyValuationRules.DeedValue"/>＝ゾーン価値×持分）に基づく。資金の決済（wealth#2056/国庫#163/企業資本#1022）は
    /// 呼び側が行う＝ここは<b>所有権・契約の付け替え</b>と<b>価格/地代の算定</b>に徹する。台帳は <see cref="PropertyDeedRegistry"/>/
    /// <see cref="LandLeaseRegistry"/>。個別区画 micro へ降りない（集約＝タイクン化回避）。test-first。
    /// </summary>
    public static class LandTransactionRules
    {
        /// <summary>取引価格＝権利の評価額（baseValue×持分）×価格係数（プレミアム/ディスカウント）。</summary>
        public static float Price(PropertyDeed deed, float share, float priceFactor = 1f)
        {
            if (deed == null) return 0f;
            float s = Mathf.Clamp(share, 0f, Mathf.Max(0f, deed.share));
            return Mathf.Max(0f, deed.baseValue) * s * Mathf.Max(0f, priceFactor);
        }

        /// <summary>年地代＝権利の評価額（baseValue×持分）×地代率。</summary>
        public static float LeaseAnnualRent(float zoneValue, float share, float rentRate)
            => Mathf.Max(0f, zoneValue) * Mathf.Clamp01(share) * Mathf.Max(0f, rentRate);

        /// <summary>指定区画の指定所有者の権利証を探す（無ければ null）。zoned/landUse も一致を要求。</summary>
        public static PropertyDeed FindDeed(int systemId, bool zoned, LandUseType landUse, OwnerRef owner)
        {
            var ds = PropertyDeedRegistry.DeedsOnSystem(systemId);
            for (int i = 0; i < ds.Count; i++)
            {
                PropertyDeed d = ds[i];
                if (d == null || d.zoned != zoned) continue;
                if (zoned && d.landUse != landUse) continue;
                if (owner.Owns(d)) return d;
            }
            return null;
        }

        /// <summary>
        /// 持分の移転（売買の本体）：売主の権利証から <paramref name="share"/> を引き、買主の同区画の権利証へ足す（無ければ作る）。
        /// 区画属性（systemId/zoned/landUse/baseValue/rentRate）は売主から引き継ぐ。実際に移転した持分を返す（売主の保有を超えない）。
        /// 資金決済は呼び側（<see cref="Price"/> で価格を算定）。
        /// </summary>
        public static float TransferShare(PropertyDeed seller, OwnerRef buyer, float share)
        {
            if (seller == null) return 0f;
            float moved = Mathf.Clamp(share, 0f, Mathf.Max(0f, seller.share));
            if (moved <= 0f) return 0f;
            seller.share -= moved;

            PropertyDeed dest = FindDeed(seller.systemId, seller.zoned, seller.landUse, buyer);
            if (dest == null)
            {
                dest = new PropertyDeed(0, seller.systemId, 0f, seller.baseValue)
                { zoned = seller.zoned, landUse = seller.landUse, rentRate = seller.rentRate };
                buyer.ApplyTo(dest);
                PropertyDeedRegistry.Register(dest);
            }
            dest.share = Mathf.Clamp01(dest.share + moved);
            return moved;
        }

        /// <summary>売買＝<see cref="TransferShare"/>＋価格算定（戻り値＝成立価格・移転持分は out）。資金の受け払いは呼び側。</summary>
        public static float Sell(PropertyDeed seller, OwnerRef buyer, float share, out float movedShare, float priceFactor = 1f)
        {
            float price = Price(seller, share, priceFactor);
            movedShare = TransferShare(seller, buyer, share);
            // 端数調整：実移転がリクエストに満たない場合は価格を移転比で按分。
            float req = Mathf.Clamp(share, 0f, seller != null ? Mathf.Max(0f, movedShare + seller.share) : 0f);
            if (req > 1e-6f && movedShare < req) price *= movedShare / req;
            return price;
        }

        /// <summary>
        /// 貸出（リース）の締結：貸主が所有権を保ったまま、借主へ区画の持分を期間つきで貸す。年地代を算定して <see cref="LandLease"/> を登録。
        /// 所有権（権利証）は移さない＝貸主の権利証はそのまま。借主は地代を払う（決済は呼び側）。
        /// </summary>
        public static LandLease Lease(PropertyDeed deed, OwnerRef lessee, float share, float rentRate, int termYears, int startYear)
        {
            if (deed == null) return null;
            float s = Mathf.Clamp(share, 0f, Mathf.Max(0f, deed.share));
            if (s <= 0f) return null;
            var lease = new LandLease
            {
                systemId = deed.systemId,
                zoned = deed.zoned,
                landUse = deed.landUse,
                lessorKind = deed.ownerKind, lessorPersonId = deed.ownerPersonId, lessorFaction = deed.ownerFaction, lessorEnterpriseId = deed.ownerEnterpriseId,
                lesseeKind = lessee.kind, lesseePersonId = lessee.personId, lesseeFaction = lessee.faction, lesseeEnterpriseId = lessee.enterpriseId,
                share = s,
                annualRent = LeaseAnnualRent(deed.baseValue, s, rentRate),
                termYears = Mathf.Max(1, termYears),
                startYear = startYear,
            };
            return LandLeaseRegistry.Register(lease);
        }

        /// <summary>リースが満了したか（開始年＋期間 ≤ 現在年）。</summary>
        public static bool IsExpired(LandLease lease, int currentYear)
            => lease != null && currentYear >= lease.startYear + Mathf.Max(1, lease.termYears);

        /// <summary>満了したリースを台帳から除去し、件数を返す（年次の整理）。</summary>
        public static int ExpireDue(int currentYear)
        {
            var all = LandLeaseRegistry.All;
            var expired = new List<int>();
            for (int i = 0; i < all.Count; i++)
                if (IsExpired(all[i], currentYear)) expired.Add(all[i].id);
            for (int i = 0; i < expired.Count; i++) LandLeaseRegistry.Remove(expired[i]);
            return expired.Count;
        }
    }
}
