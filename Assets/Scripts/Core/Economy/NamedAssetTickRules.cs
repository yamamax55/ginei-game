using System;

namespace Ginei
{
    /// <summary>
    /// ネームド資産の暦境界オーケストレータ（NASSET-6・#2063 配線・純ロジック）。
    /// 全資産を1年ぶん回す＝時価を値上がりさせ（<see cref="NamedAssetRules.ValueAfterYear"/>）、
    /// 人物資産の純収益を <see cref="Person.wealth"/>#2056 へ加算（人物解決 <see cref="Func{T,TResult}"/> を受ける＝Game 非依存）。
    /// 国家収益は呼び側が <see cref="NamedAssetEffectRules.FactionAnnualIncome"/> で国庫#163 へ流す。<b>薄い窓口</b>＝判定は各ルールへ委譲。test-first。
    /// </summary>
    public static class NamedAssetTickRules
    {
        /// <summary>
        /// 1年ぶん：①全資産の時価を値上がり、②人物資産の純収益を所有者 <see cref="Person.wealth"/> へ加算。
        /// <paramref name="resolvePerson"/> は personId→Person（不在/死亡は null を返してよい＝収益は捨てられる）。
        /// </summary>
        public static void TickYear(Func<int, Person> resolvePerson)
        {
            var all = NamedAssetRegistry.All;
            for (int i = 0; i < all.Count; i++)
            {
                NamedAsset a = all[i];
                if (a == null) continue;

                // ① 純収益（値上がり前の時価で計上＝当年の収益）
                float net = NamedAssetRules.NetAnnualIncome(a);
                if (a.IsPersonOwned && resolvePerson != null)
                {
                    Person owner = resolvePerson(a.ownerPersonId);
                    if (owner != null && owner.deathYear == 0)
                        owner.wealth = UnityEngine.Mathf.Max(0f, owner.wealth + net);
                }

                // ② 時価の値上がり（美術品/財宝＝増、暴落#185＝減）
                a.value = NamedAssetRules.ValueAfterYear(a.value, a.appreciationRate);
            }
        }
    }
}
