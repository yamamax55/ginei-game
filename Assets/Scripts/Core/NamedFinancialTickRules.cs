using System;

namespace Ginei
{
    /// <summary>
    /// 金融資産・不動産の暦境界オーケストレータ（NFIN-6・#2070 配線・純ロジック）。
    /// 全 holding の配当/クーポン/分配と全 deed の地代を所有者 <see cref="Person.wealth"/>#2056 へ加算（人物解決 <see cref="Func{T,TResult}"/>）。
    /// 地価は <see cref="PropertyValuationRules.ValueAfterYear"/> で更新。国家ぶんは呼び側が集計して国庫#163 へ。
    /// <b>薄い窓口</b>＝判定は各ルールへ委譲（<see cref="NamedAssetTickRules"/> と同型）。test-first。
    /// </summary>
    public static class NamedFinancialTickRules
    {
        /// <summary>
        /// 1年ぶん：金融資産の収益＋不動産の地代を人物所有者の <see cref="Person.wealth"/> へ加算し、地価を更新。
        /// <paramref name="resolvePerson"/> は personId→Person（不在/死亡は null＝収益は捨てられる）。
        /// </summary>
        public static void TickYear(Func<int, Person> resolvePerson)
        {
            // 金融資産の配当/クーポン/分配
            var fin = FinancialHoldingRegistry.All;
            for (int i = 0; i < fin.Count; i++)
            {
                FinancialHolding h = fin[i];
                if (h == null) continue;
                if (h.IsPersonOwned && resolvePerson != null)
                {
                    Person owner = resolvePerson(h.ownerPersonId);
                    if (owner != null && owner.deathYear == 0)
                        owner.wealth = UnityEngine.Mathf.Max(0f, owner.wealth + FinancialAssetRules.AnnualIncome(h));
                }
            }

            // 不動産の地代＋地価更新
            var prop = PropertyDeedRegistry.All;
            for (int i = 0; i < prop.Count; i++)
            {
                PropertyDeed d = prop[i];
                if (d == null) continue;
                if (d.IsPersonOwned && resolvePerson != null)
                {
                    Person owner = resolvePerson(d.ownerPersonId);
                    if (owner != null && owner.deathYear == 0)
                        owner.wealth = UnityEngine.Mathf.Max(0f, owner.wealth + PropertyValuationRules.RentIncome(d));
                }
                d.baseValue = PropertyValuationRules.ValueAfterYear(d.baseValue, 0f); // 既定は据え置き（地価変動は呼び側が率を渡す拡張余地）
            }
        }

        /// <summary>国家の金融＋不動産の年間収益合計（国庫#163 へ流す入力）。</summary>
        public static float FactionAnnualIncome(Faction faction)
        {
            float sum = 0f;
            var fin = FinancialHoldingRegistry.OwnedByFaction(faction);
            for (int i = 0; i < fin.Count; i++) sum += FinancialAssetRules.AnnualIncome(fin[i]);
            var prop = PropertyDeedRegistry.OwnedByFaction(faction);
            for (int i = 0; i < prop.Count; i++) sum += PropertyValuationRules.RentIncome(prop[i]);
            return sum;
        }
    }
}
