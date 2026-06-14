using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace Ginei
{
    public partial class GalaxyView
    {
        // --- ネームド資産（NASSET・#2063 デモ配線） ---
        private static readonly Faction[] DemoFactions = { Faction.帝国, Faction.同盟 };
        private bool namedAssetsSeeded;

        /// <summary>id から司令を解決（資産収益を所有者 wealth へ流す・<see cref="NamedAssetTickRules"/> 用）。</summary>
        private Person ResolveCommander(int id)
        {
            if (commanders == null) return null;
            for (int i = 0; i < commanders.Count; i++)
                if (commanders[i] != null && commanders[i].id == id) return commanders[i];
            return null;
        }

        /// <summary>相続人＝故人と同勢力の最高位の存命司令（本人除く・同位は先頭）。不在なら null（=没収）。</summary>
        private Person FindHeir(Person d)
        {
            if (commanders == null || d == null) return null;
            Person best = null;
            for (int i = 0; i < commanders.Count; i++)
            {
                Person c = commanders[i];
                if (c == null || c.id == d.id || c.deathYear != 0) continue;
                if (c.faction != d.faction) continue;
                if (best == null || c.rankTier > best.rankTier) best = c;
            }
            return best;
        }

        /// <summary>デモ資産シード（冪等）：各司令に固有名の旗艦、各勢力に宮殿を1つ持たせる（NASSET-6）。</summary>
        private void SeedNamedAssets()
        {
            if (namedAssetsSeeded || NamedAssetRegistry.All.Count > 0) { namedAssetsSeeded = true; return; }
            // 各勢力の宮殿（国家所有＝維持費は重いが威信・正統性を生む）。
            for (int f = 0; f < DemoFactions.Length; f++)
            {
                var palace = new NamedAsset(NamedAssetRegistry.NextId(), $"{DemoFactions[f]}宮殿", NamedAssetCategory.宮殿)
                {
                    ownerKind = AssetOwnerKind.国家, ownerFaction = DemoFactions[f],
                    value = 5000f, yieldRate = 0.02f, upkeepRate = 0.05f, prestige = 30f
                };
                NamedAssetRegistry.Register(palace);
            }
            // 各司令の旗艦（人物所有＝維持費はかかるが威信。固有名は提督名から）。
            if (commanders != null)
                for (int i = 0; i < commanders.Count; i++)
                {
                    Person c = commanders[i];
                    if (c == null) continue;
                    var flagship = new NamedAsset(NamedAssetRegistry.NextId(), $"{c.name}旗艦", NamedAssetCategory.旗艦)
                    {
                        ownerKind = AssetOwnerKind.人物, ownerPersonId = c.id,
                        value = 800f, upkeepRate = 0.03f, prestige = 8f
                    };
                    NamedAssetRegistry.Register(flagship);
                }
            namedAssetsSeeded = true;
        }

        // --- ネームド金融資産・不動産（NFIN・#2070 デモ配線） ---
        private bool financialAssetsSeeded;

        /// <summary>相続人を最大 max 名（同勢力の存命司令を階級降順・本人除く）。細分化相続の分割先。</summary>
        private System.Collections.Generic.List<int> FindHeirs(Person d, int max)
        {
            var result = new System.Collections.Generic.List<int>();
            if (commanders == null || d == null) return result;
            var pool = new System.Collections.Generic.List<Person>();
            for (int i = 0; i < commanders.Count; i++)
            {
                Person c = commanders[i];
                if (c == null || c.id == d.id || c.deathYear != 0 || c.faction != d.faction) continue;
                pool.Add(c);
            }
            pool.Sort((a, b) => b.rankTier.CompareTo(a.rankTier)); // 階級降順
            for (int i = 0; i < pool.Count && i < max; i++) result.Add(pool[i].id);
            return result;
        }

        /// <summary>デモ金融/不動産シード（冪等）：各勢力に国有株式・首都惑星の deed、各司令に少数の株式・地所（NFIN-6）。</summary>
        private void SeedFinancialAssets()
        {
            if (financialAssetsSeeded || FinancialHoldingRegistry.All.Count > 0 || PropertyDeedRegistry.All.Count > 0)
            { financialAssetsSeeded = true; return; }

            int underlying = 1;
            for (int f = 0; f < DemoFactions.Length; f++)
            {
                // 国有の株式（配当）と債券（クーポン）。
                FinancialHoldingRegistry.Register(new FinancialHolding(0, FinancialInstrument.株式, $"{DemoFactions[f]}重工")
                { ownerKind = AssetOwnerKind.国家, ownerFaction = DemoFactions[f], underlyingId = underlying++, units = 1000f, unitPrice = 10f, incomePerUnit = 0.5f, bookCost = 10000f });
                FinancialHoldingRegistry.Register(new FinancialHolding(0, FinancialInstrument.債券, $"{DemoFactions[f]}国債")
                { ownerKind = AssetOwnerKind.国家, ownerFaction = DemoFactions[f], underlyingId = underlying++, units = 500f, unitPrice = 100f, incomePerUnit = 3f, bookCost = 50000f });
            }
            // 各司令に少数の株式（配当）と、首都星系（id=0 を仮の本拠）に地所（地代）。
            if (commanders != null)
                for (int i = 0; i < commanders.Count; i++)
                {
                    Person c = commanders[i];
                    if (c == null) continue;
                    FinancialHoldingRegistry.Register(new FinancialHolding(0, FinancialInstrument.投資信託, "銀河ファンド")
                    { ownerKind = AssetOwnerKind.人物, ownerPersonId = c.id, underlyingId = underlying, units = 50f, unitPrice = 12f, incomePerUnit = 0.4f, bookCost = 600f });
                    var deed = new PropertyDeed(0, c.faction == Faction.同盟 ? 1 : 0, 0.2f, 3000f)
                    { ownerKind = AssetOwnerKind.人物, ownerPersonId = c.id, rentRate = 0.04f };
                    PropertyDeedRegistry.Register(deed);
                }
            financialAssetsSeeded = true;
        }

        /// <summary>紙くず化デモ（NFIN-6・暴落#185）：低確率で1銘柄を時価0へ（同銘柄の全保有が紙くずに）。</summary>
        private void MaybeCrashAStock()
        {
            var stocks = FinancialHoldingRegistry.HoldingsOfInstrument(FinancialInstrument.株式);
            if (stocks.Count == 0 || UnityEngine.Random.value > 0.05f) return;
            int victim = stocks[UnityEngine.Random.Range(0, stocks.Count)].underlyingId;
            var affected = FinancialHoldingRegistry.HoldingsOfUnderlying(victim);
            string banner = null;
            for (int i = 0; i < affected.Count; i++)
            {
                FinancialAssetRules.MarkToMarket(affected[i], 0f, 0f); // 紙くず化
                banner = affected[i].underlyingName;
            }
            if (banner != null)
                NotificationCenter.Push(NotificationCategory.内政, NotificationSeverity.警告, $"{banner} 株が暴落＝紙くずに（保有 {affected.Count} 件が無価値化）");
        }

    }
}
