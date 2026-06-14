using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace Ginei
{
    public partial class GalaxyView
    {
        /// <summary>
        /// 星系ごとの造船所を用意する（#884→#148）。各勢力の初期艦隊プールをシードし、所有星系に造船所を置いて連続建艦を積む。
        /// 完成は暦の日次（<see cref="RunDailyCampaignTick"/>）で所有勢力の <see cref="FleetPool"/> へ就役＝編成画面の総艦艇が増える。
        /// </summary>
        private void SetupShipyard()
        {
            shipyards = new List<Shipyard>();
            if (map == null) return;
            var seeded = new HashSet<Faction>();
            foreach (var s in map.systems)
            {
                if (s == null) continue;
                if (seeded.Add(s.owner) && FleetPool.Get(s.owner) <= 0) FleetPool.Set(s.owner, Mathf.Max(0, initialFleetPool));
                var yard = new Shipyard(s.id, s.owner, 1, Mathf.Max(0f, shipyardBuildPower));
                ShipyardRules.Enqueue(yard, ShipClass.巡航艦, ShipRole.戦闘艦);
                shipyards.Add(yard);
            }
        }

        /// <summary>
        /// 暦の1日ぶん全造船所の建艦を進め、完成艦を所有勢力プールへ就役させる（#884→#148）。生産力は内政（Province 安定度＝BUILD-2）連動。
        /// プレイヤー勢力の完成のみ HUD 告知（AI 建艦は静かに進む）。
        /// </summary>
        private void TickShipyard(float secondsPerDay)
        {
            if (shipyards == null) return;
            Faction pf = GameSettings.Instance != null ? GameSettings.Instance.playerFaction : Faction.帝国;
            int playerBuilt = 0;
            for (int i = 0; i < shipyards.Count; i++)
            {
                Shipyard yard = shipyards[i];
                if (yard == null) continue;
                provinces.TryGetValue(yard.systemId, out var prov);
                float factor = ShipyardRules.ProductionFactor(prov); // BUILD-2：安定度比例＝支配≠即建艦
                factor *= ShipbuildingFundingFactor(yard.faction);   // G3：建艦予算の出資度が建艦速度に効く（#163→#884）
                var done = ShipyardRules.Tick(yard, secondsPerDay, factor);
                for (int j = 0; j < done.Count; j++)
                {
                    int built = ShipyardRules.CommissionToPool(done[j]);
                    if (yard.faction == pf) playerBuilt += built;
                }
                if (yard.queue.Count == 0) ShipyardRules.Enqueue(yard, ShipClass.巡航艦, ShipRole.戦闘艦);
            }
            if (playerBuilt > 0)
            {
                NotificationCenter.Push(NotificationCategory.建艦, $"造船完成：艦艇 +{playerBuilt}（プールへ／編成画面 B で配分）");
            }
        }

        /// <summary>
        /// 年次の財政：①予算編成（歳入レート×支出性向を分野重みで配分）②形式財政（債務/利払い）③債務スパイラル通知。
        /// 現金の執行は日次 <see cref="CampaignRules.TickBudgetDay"/> が予算総額を国庫から引いて行う（予算が満ちて初めて執行が動く）。
        /// 数式は <see cref="BudgetRules"/>/<see cref="FiscalRules"/>/<see cref="CampaignRules"/> へ委譲。
        /// </summary>
        private void RunFiscalYearTick()
        {
            var camp = StrategySession.Campaign;
            if (camp == null || camp.states == null) return;

            // ① 予算編成（帝国＝軍拡で赤字気味／同盟＝均衡・内政厚め）。重みは 軍事/建艦/内政/社会保障/研究/外交。
            for (int i = 0; i < camp.states.Count; i++)
            {
                FactionState s = camp.states[i];
                if (s == null || s.budget == null) continue;
                float revenueRate = FiscalRules.TaxRevenue(CampaignRules.EconomyBase(s), s.taxRate);
                float propensity = s.faction == Faction.帝国 ? 1.1f : 1.0f;
                float[] weights = s.faction == Faction.帝国
                    ? new float[] { 3, 2, 1, 1, 1, 1 }
                    : new float[] { 1, 1, 2, 2, 1, 1 };
                BudgetRules.AllocateByWeights(s.budget, revenueRate * propensity, weights);
            }

            // ② 形式財政：赤字→国債→利払い→翌年（債務繰り越し）。
            CampaignRules.TickFiscalYear(camp, 1f);

            // ③ 帰結（出資度→実効・G3/G5）：社会保障→希望／財政健全度→希望／内政→安定度／債務スパイラル通知。
            var p = FiscalRules.FiscalParams.Default;
            var adminBonusByFaction = new System.Collections.Generic.Dictionary<Faction, float>();
            for (int i = 0; i < camp.states.Count; i++)
            {
                FactionState s = camp.states[i];
                if (s == null || s.budget == null || s.fiscal == null) continue;
                float economy = CampaignRules.EconomyBase(s);
                float revenueRate = FiscalRules.TaxRevenue(economy, s.taxRate);

                // 社会保障の希望加点（＋）と財政難の希望毀損（−）＝民心へ
                if (s.community != null)
                {
                    float welfareBonus = BudgetRules.WelfareHopeBonus(s.budget, revenueRate * 0.15f); // ±0.3
                    float health = economy > 0f ? FiscalRules.FiscalHealthFactor(s.fiscal, economy, p) : 1f;
                    float hopeDelta = welfareBonus * 0.1f - (1f - health) * 0.05f;
                    s.community.hope = Mathf.Clamp01(s.community.hope + hopeDelta);
                }

                // 内政の安定度加点（所有 Province へ後段で反映）
                adminBonusByFaction[s.faction] = BudgetRules.AdministrationStabilityBonus(s.budget, revenueRate * 0.2f); // ±10

                if (FiscalRules.IsDebtSpiral(s.fiscal, economy, p))
                    NotificationCenter.Push(NotificationCategory.内政, NotificationSeverity.警告, $"{s.faction} 債務スパイラル（債務 {s.fiscal.debt:0}）");
            }

            // 内政予算の出資度を所有星系の Province 安定度へ年次反映（過剰で+・不足で−・0..100）。
            if (map != null)
                foreach (var sys in map.systems)
                {
                    if (sys == null || !provinces.TryGetValue(sys.id, out var prov) || prov == null) continue;
                    if (adminBonusByFaction.TryGetValue(sys.owner, out float ab))
                        prov.stability = Mathf.Clamp(prov.stability + ab, 0f, 100f);
                }
        }

        /// <summary>建艦の出資度（G3）＝建艦予算/必要額。歳入の2割を満額基準とする（不足で建艦が遅れる）。</summary>
        private float ShipbuildingFundingFactor(Faction f)
        {
            var camp = StrategySession.Campaign;
            if (camp == null) return 1f;
            FactionState s = CampaignRules.GetState(camp, f);
            if (s == null || s.budget == null) return 1f;
            float need = FiscalRules.TaxRevenue(CampaignRules.EconomyBase(s), s.taxRate) * 0.2f;
            if (need <= 0f) return 1f;
            return BudgetRules.ShipbuildingFactor(s.budget, need);
        }

        // --- 国家・惑星の行政物資消費（STATEDEM・#2077 デモ配線） ---
        private readonly System.Collections.Generic.Dictionary<Faction, ResourceStockpile> stateStockpiles
            = new System.Collections.Generic.Dictionary<Faction, ResourceStockpile>();

        /// <summary>国家ごとに所有惑星から産出→行政・インフラが消費→不足で統治逼迫＝安定度低下（STATEDEM-6）。</summary>
        private void RunStateConsumptionTick()
        {
            if (map == null || provinces == null) return;
            for (int f = 0; f < DemoFactions.Length; f++)
            {
                Faction fac = DemoFactions[f];
                var owned = new System.Collections.Generic.List<Province>();
                int systemCount = 0;
                foreach (var s in map.systems)
                {
                    if (s == null || s.owner != fac) continue;
                    systemCount++;
                    if (provinces.TryGetValue(s.id, out var prov) && prov != null) owned.Add(prov);
                }
                if (systemCount == 0) continue;

                // 国庫（資源備蓄）を冪等生成。
                if (!stateStockpiles.TryGetValue(fac, out var stock) || stock == null)
                {
                    stock = new ResourceStockpile(200f, 0f, 100f);
                    stateStockpiles[fac] = stock;
                }
                // 年次産出（所有惑星の類型×統治で物資/燃料を産む）。
                for (int i = 0; i < owned.Count; i++)
                    ResourceProductionRules.ProduceFromProvince(stock, owned[i], 1f);

                // 行政・インフラ・公共サービスの物資消費＝総需要を国庫から引く。
                var result = StateConsumptionTickRules.TickState(owned, systemCount, stock);
                if (result.overall < 0.999f)
                {
                    // 行政物資不足＝統治が回らず安定度低下（緩やかに削る＝GovernanceRules 収束と競合させない）。
                    float penalty = StateConsumptionEffectRules.StabilityPenalty(result.overall) * 0.1f;
                    for (int i = 0; i < owned.Count; i++)
                        owned[i].stability = UnityEngine.Mathf.Max(0f, owned[i].stability - penalty);
                    NotificationCenter.Push(NotificationCategory.内政, NotificationSeverity.警告,
                        $"{fac} 行政物資が不足（充足 {(int)(result.overall * 100)}%）＝統治逼迫で安定度低下");
                }

                // 企業の投入制約つき生産（FIRMPROD-6・#2084）：工員#110 から計画産出を見積り、国庫を投入に実産出を解く。
                // 原材料（物資）/エネルギー（燃料）が足りないと工場が遊休＝減産。実産出ぶんの投入を消費する。
                float industryWorkers = 0f;
                for (int i = 0; i < owned.Count; i++) industryWorkers += OccupationRules.Workers(owned[i], Occupation.工員);
                if (industryWorkers > 0f)
                {
                    float planned = industryWorkers; // 計画産出 proxy（労働×生産性=1）
                    var pr = EnterpriseProductionTickRules.Produce(planned, stock.Get(ResourceType.物資), stock.Get(ResourceType.燃料), float.MaxValue);
                    EnterpriseProductionTickRules.Consume(stock, pr.realizedOutput);
                    if (pr.inputConstrained && pr.utilization < 0.999f)
                        NotificationCenter.Push(NotificationCategory.内政, NotificationSeverity.注意,
                            $"{fac} 工業が{pr.binding}不足で減産（稼働 {(int)(pr.utilization * 100)}%）");
                }
            }
        }

        // --- 代表生産チェーン（森林→木材→建材→住宅・VCHAIN・#2091 デモ配線） ---
        private readonly System.Collections.Generic.Dictionary<int, ChainStock> chainStocks
            = new System.Collections.Generic.Dictionary<int, ChainStock>();

        /// <summary>類型ごとの森林初期量（居住/農業は森が多く、工業/鉱業は少ない）。</summary>
        private static float SeedForest(SystemType t)
        {
            switch (t)
            {
                case SystemType.農業: return 1000f;
                case SystemType.居住: return 800f;
                case SystemType.鉱業: return 200f;
                default: return 300f; // 工業
            }
        }

        /// <summary>惑星ごとに森林→木材→建材→住宅 を年次で流し、住宅充足で生活水準を補正（VCHAIN-6）。</summary>
        private void RunSupplyChainTick()
        {
            if (provinces == null) return;
            var p = SupplyChainParams.Default;
            int shortageCount = 0, depletionCount = 0;
            foreach (var kv in provinces)
            {
                Province prov = kv.Value;
                if (prov == null) continue;
                if (!chainStocks.TryGetValue(kv.Key, out var cs) || cs == null)
                {
                    // 初期住宅は需要の8割（最初から住んでいる）。
                    cs = new ChainStock(SeedForest(prov.systemType), 0f, 0f, prov.population * p.perCapitaHousing * 0.8f);
                    chainStocks[kv.Key] = cs;
                }
                var r = SupplyChainTickRules.TickYear(cs, prov.population, p);
                // 住宅充足で生活水準#181 を補正（不足は頭打ち＝#2042 がその年に設定した値へ乗算）。
                prov.livingStandard *= HousingDemandRules.LivingStandardFactor(r.occupancy, 0.7f);
                if (r.occupancy < 0.8f) shortageCount++;
                if (r.overharvest) depletionCount++;
            }
            if (shortageCount > 0)
                NotificationCenter.Push(NotificationCategory.内政, NotificationSeverity.注意, $"住宅不足の星系 {shortageCount}（木材・建材の供給不足）");
            if (depletionCount > 0)
                NotificationCenter.Push(NotificationCategory.内政, NotificationSeverity.注意, $"森林の過伐採 {depletionCount} 星系（再生が追いつかない）");
        }

        // --- 汎用BOM消費財（食品/衣類・BOM・#2098 デモ配線） ---
        private readonly System.Collections.Generic.Dictionary<int, CommodityStock> bomStocks
            = new System.Collections.Generic.Dictionary<int, CommodityStock>();
        private bool bomSeeded;
        private int grainId, fiberId, clothId, foodId, clothingId;
        private Recipe foodRecipe, clothRecipe, clothingRecipe;

        /// <summary>品目カタログとレシピを冪等 seed（食品←穀物、布←繊維、衣類←布）。</summary>
        private void EnsureBomContent()
        {
            if (bomSeeded) return;
            grainId = CommodityCatalog.Register("穀物", CommodityCategory.原材料).id;
            fiberId = CommodityCatalog.Register("繊維", CommodityCategory.原材料).id;
            clothId = CommodityCatalog.Register("布", CommodityCategory.中間財).id;
            foodId = CommodityCatalog.Register("食品", CommodityCategory.消費財).id;
            clothingId = CommodityCatalog.Register("衣類", CommodityCategory.消費財).id;
            foodRecipe = RecipeBook.Register(new Recipe(foodId).AddInput(grainId, 1f));        // 食品←穀物×1
            clothRecipe = RecipeBook.Register(new Recipe(clothId).AddInput(fiberId, 2f));       // 布←繊維×2
            clothingRecipe = RecipeBook.Register(new Recipe(clothingId).AddInput(clothId, 2f)); // 衣類←布×2
            bomSeeded = true;
        }

        /// <summary>惑星ごとに原材料を供給→食品/衣類をレシピ生産→消費財需要を消費し、不足で生活水準を補正（BOM-6）。</summary>
        private void RunBomConsumerTick()
        {
            if (provinces == null) return;
            EnsureBomContent();
            // Phase 1: 原材料供給（人口×安定度比例＝荒れた惑星は産まない）。
            foreach (var kv in provinces)
            {
                Province prov = kv.Value;
                if (prov == null) continue;
                if (!bomStocks.TryGetValue(kv.Key, out var cs) || cs == null) { cs = new CommodityStock(); bomStocks[kv.Key] = cs; }
                float outFactor = GovernanceRules.OutputFactor(prov);
                cs.Add(grainId, prov.population * 1.5f * outFactor);
                cs.Add(fiberId, prov.population * 0.6f * outFactor);
            }
            // Phase 2: 域内物流（DIST-6・#2112）＝余剰の穀物を不足惑星へ回廊で配送（通商破壊で分断）。生産の前に回す。
            RunRegionalDistributionTick();
            // Phase 3: レシピ生産＋消費財需要の充足。
            int foodShort = 0, clothingShort = 0;
            foreach (var kv in provinces)
            {
                Province prov = kv.Value;
                if (prov == null) continue;
                if (!bomStocks.TryGetValue(kv.Key, out var cs) || cs == null) continue;
                float pop = prov.population;
                // レシピ生産（上流→下流）：食品←穀物、布←繊維、衣類←布。
                BomTickRules.Produce(cs, foodRecipe, pop * 1.0f);
                BomTickRules.Produce(cs, clothRecipe, pop * 0.4f);
                BomTickRules.Produce(cs, clothingRecipe, pop * 0.2f);
                // 消費財需要の充足（食品は全員・衣類は控えめ）。
                float foodDemand = ConsumerDemandRules.Demand(pop, 1.0f);
                float clothingDemand = ConsumerDemandRules.Demand(pop, 0.2f);
                float foodFulfill = ConsumerDemandRules.Fulfillment(cs.Get(foodId), foodDemand);
                float clothingFulfill = ConsumerDemandRules.Fulfillment(cs.Get(clothingId), clothingDemand);
                ConsumerDemandRules.Consume(cs, foodId, foodDemand);
                ConsumerDemandRules.Consume(cs, clothingId, clothingDemand);
                float consumerFactor = ConsumerDemandRules.LivingStandardFactor(UnityEngine.Mathf.Min(foodFulfill, clothingFulfill), 0.6f);
                prov.livingStandard *= consumerFactor;
                if (foodFulfill < 0.8f) foodShort++;
                if (clothingFulfill < 0.8f) clothingShort++;
            }
            if (foodShort > 0)
                NotificationCenter.Push(NotificationCategory.内政, NotificationSeverity.警告, $"食料不足の星系 {foodShort}（穀物・食品の供給不足）");
            if (clothingShort > 0)
                NotificationCenter.Push(NotificationCategory.内政, NotificationSeverity.情報, $"衣類不足の星系 {clothingShort}（繊維・布の供給不足）");
        }

        // --- SCM計画（MRP所要量展開・SCM・#2105 read-only 配線） ---
        /// <summary>勢力ごとに消費財需要をMRP展開し、原材料供給見込みと突き合わせて逼迫品目を通知（状態は変えない）。</summary>
        private void RunScmPlanTick()
        {
            if (map == null || provinces == null) return;
            EnsureBomContent();
            for (int f = 0; f < DemoFactions.Length; f++)
            {
                Faction fac = DemoFactions[f];
                float totalPop = 0f, grainSupply = 0f, fiberSupply = 0f;
                foreach (var s in map.systems)
                {
                    if (s == null || s.owner != fac) continue;
                    if (!provinces.TryGetValue(s.id, out var prov) || prov == null) continue;
                    float pop = prov.population;
                    float outFactor = GovernanceRules.OutputFactor(prov);
                    totalPop += pop;
                    grainSupply += pop * 1.5f * outFactor; // RunBomConsumerTick と同じ供給見込み
                    fiberSupply += pop * 0.6f * outFactor;
                }
                if (totalPop <= 0f) continue;

                var demands = new System.Collections.Generic.Dictionary<int, float>
                {
                    { foodId, totalPop * 1.0f },     // 食品＝全員
                    { clothingId, totalPop * 0.2f }, // 衣類＝控えめ
                };
                var onHand = new CommodityStock();
                onHand.Add(grainId, grainSupply);
                onHand.Add(fiberId, fiberSupply);

                var plan = ScmTickRules.Plan(demands, onHand);
                if (plan.serviceLevel < 0.7f && plan.criticalCommodity >= 0)
                {
                    var crit = CommodityCatalog.Get(plan.criticalCommodity);
                    string name = crit != null ? crit.name : "原材料";
                    NotificationCenter.Push(NotificationCategory.内政, NotificationSeverity.注意,
                        $"{fac} SCM計画：{name}が逼迫（消費財の充足見込み {(int)(plan.serviceLevel * 100)}%）");
                }
            }
        }

        // --- 勢力内供給配分（域内物流・DIST・#2112 配線） ---
        private const float DistributionLoss = 0.05f; // 回廊輸送ロス

        /// <summary>勢力ごとに連結領域内で穀物を再配分＝余剰の穀倉惑星が不足惑星を養う（通商破壊で分断・封鎖惑星は孤立）。</summary>
        private void RunRegionalDistributionTick()
        {
            if (map == null || provinces == null) return;
            // 通商破壊#95：敵艦が在席する星系は中継不能＝領域を分断する。
            var blocked = new System.Collections.Generic.HashSet<int>();
            foreach (var s in map.systems)
                if (s != null && HasHostileFleetAt(s)) blocked.Add(s.id);

            for (int f = 0; f < DemoFactions.Length; f++)
            {
                Faction fac = DemoFactions[f];
                var components = RegionReachabilityRules.Components(map, fac, blocked);
                for (int ci = 0; ci < components.Count; ci++)
                {
                    var ids = new System.Collections.Generic.List<int>();
                    foreach (var id in components[ci])
                        if (provinces.TryGetValue(id, out var pv) && pv != null && bomStocks.TryGetValue(id, out var st) && st != null)
                            ids.Add(id);
                    if (ids.Count < 2) continue; // 2惑星以上ないと配分の意味がない

                    var stocks = new CommodityStock[ids.Count];
                    var grainDemand = new float[ids.Count];
                    for (int i = 0; i < ids.Count; i++)
                    {
                        stocks[i] = bomStocks[ids[i]];
                        grainDemand[i] = provinces[ids[i]].population * 1.0f; // 食品の素＝穀物の地元需要
                    }
                    RegionalDistributionTickRules.Distribute(stocks, grainId, grainDemand, float.MaxValue, DistributionLoss);
                }
            }
        }

    }
}
