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
            // 基軸通貨（#ReserveCurrencyRules）用：世界全体の経済規模＝交易量・各勢力のシェアの分母。
            float totalEconomy = 0f;
            for (int i = 0; i < camp.states.Count; i++)
                if (camp.states[i] != null) totalEconomy += Mathf.Max(0f, CampaignRules.EconomyBase(camp.states[i]));
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

                // 通貨（#通貨 配線）：赤字の貨幣化→インフレ／財政健全度→為替。固有名は決定論で割り当て（冪等）。
                s.currency = CurrencyRules.Ensure(s.currency, s.faction);
                bool hyper = s.currency.priceLevel > 0f && InflationRules.IsHyperinflation(s.currency.inflationRate);
                float marketPressure = MarketPressureOf(s.faction); // 市場価格#179→物価（前年の市場逼迫＝コストプッシュ）
                CurrencyRules.TickYear(s.currency, s.fiscal, economy, 1f, marketPressure);
                if (!hyper && InflationRules.IsHyperinflation(s.currency.inflationRate))
                    NotificationCenter.Push(NotificationCategory.内政, NotificationSeverity.警告,
                        $"{s.faction} ハイパーインフレ（{s.currency.currencyName}・年率 {(int)(s.currency.inflationRate * 100)}%）");

                // 基軸通貨（#ReserveCurrencyRules 配線）：交易/軍事/信認シェアから基軸度→世界交易の発行益（不労所得）を国庫へ。
                float health2 = economy > 0f ? FiscalRules.FiscalHealthFactor(s.fiscal, economy, p) : 1f;
                float share = totalEconomy > 0f ? Mathf.Clamp01(economy / totalEconomy) : 0f;
                float reserveSeigniorage = CurrencyRules.TickReserve(s.currency, share, share, health2, totalEconomy, 1f);
                s.treasury += reserveSeigniorage;

                // 通貨改鋳（#CoinageRules 配線）：財政難ほど品位を落として発行益を得る（健全=純1.0／困窮=0.5まで改鋳）→信認毀損。
                float targetSilver = Mathf.Clamp01(0.5f + 0.5f * health2);
                float prevTrust = s.currency.publicTrust;
                float mintGain = CurrencyRules.TickCoinage(s.currency, targetSilver, 1f);
                s.treasury += mintGain;
                if (s.community != null && s.currency.publicTrust < 0.6f) // 改鋳の露見＝信認低下で民心毀損（グレシャム）
                    s.community.hope = Mathf.Clamp01(s.community.hope - (0.6f - s.currency.publicTrust) * 0.1f);
                if (prevTrust >= 0.5f && s.currency.publicTrust < 0.5f)
                    NotificationCenter.Push(NotificationCategory.内政, NotificationSeverity.警告,
                        $"{s.faction} 通貨改鋳の露見＝{s.currency.currencyName}の信認低下（品位 {(int)(s.currency.silverContent * 100)}%）");

                // 国債（#161/#185 配線）：額面を債務に同期し、財政健全度→信用リスク・市場金利→価格を収束（財政悪化→価格↓利回り↑）。
                s.sovereignBond = SovereignBondRules.Ensure(s.sovereignBond, s.faction);
                SovereignBondRules.TickYear(s.sovereignBond, s.fiscal, economy, 1f);
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

        // --- 希少資源備蓄（#178 配線）：所有惑星の鉱床（偏在）から年次産出を貯める勢力ごとのストア。 ---
        private readonly System.Collections.Generic.Dictionary<Faction, StrategicResourceStockpile> strategicStockpiles
            = new System.Collections.Generic.Dictionary<Faction, StrategicResourceStockpile>();

        // --- 市場経済（M-1 #179 配線）：勢力ごとに4財（物資/燃料/弾薬/奢侈品）の需給と価格。供給＝産出率・需要＝人口比例で価格が創発する。 ---
        private readonly System.Collections.Generic.Dictionary<Faction, Market[]> markets
            = new System.Collections.Generic.Dictionary<Faction, Market[]>();
        // 各財の基準価格（需給均衡の中心＝供給=需要で戻る先）。GoodType の並び順に対応。
        private static readonly Good[] MarketGoods =
        {
            new Good(GoodType.物資, 1f), new Good(GoodType.燃料, 2f), new Good(GoodType.弾薬, 3f), new Good(GoodType.奢侈品, 5f),
        };
        // 1人あたり需要係数（少量で価格が創発＝タイクン化回避）。
        private const float DemandSupplies = 0.012f, DemandFuel = 0.005f, DemandAmmo = 0.002f, DemandLuxury = 0.006f;

        /// <summary>戦略マップの現行インスタンス（観測層が国庫＝資源備蓄を read-only で読む弱参照。Strategy 以外では null）。</summary>
        public static GalaxyView Active { get; private set; }

        /// <summary>勢力の資源備蓄（物資/弾薬/燃料）。未生成なら null。観測層（兵站オブザーバ）専用＝read-only。</summary>
        public ResourceStockpile GetStateStockpile(Faction faction)
            => stateStockpiles.TryGetValue(faction, out var s) ? s : null;

        /// <summary>勢力の希少資源備蓄（レアメタル/反応物質/超伝導体/希少結晶・#178）。未生成なら null。観測層（兵站オブザーバ）専用＝read-only。</summary>
        public StrategicResourceStockpile GetStrategicStockpile(Faction faction)
            => strategicStockpiles.TryGetValue(faction, out var s) ? s : null;

        /// <summary>勢力の市場（指定財の需給/価格・M-1 #179）。未生成なら null。観測層（生産・流通オブザーバ）専用＝read-only。</summary>
        public Market GetMarket(Faction faction, GoodType good)
            => markets.TryGetValue(faction, out var arr) && arr != null && (int)good < arr.Length ? arr[(int)good] : null;

        // 市場価格#179→物価：市場の逼迫（需要>供給）をコストプッシュへ写す係数と上限（runaway 回避で控えめ・bounded）。
        private const float MarketCostPushScale = 0.1f, MarketCostPushCap = 0.2f;

        /// <summary>勢力の市場逼迫（前年）をインフレのコストプッシュ（0..上限）へ写す＝市場価格#179→物価。</summary>
        private float MarketPressureOf(Faction fac)
        {
            if (!markets.TryGetValue(fac, out var mk) || mk == null) return 0f;
            float sum = 0f; int n = 0;
            for (int g = 0; g < mk.Length; g++)
            {
                Market m = mk[g];
                if (m == null || m.supply <= 0f) continue;
                sum += Mathf.Max(0f, m.demand / m.supply - 1f); // 需給比>1＝逼迫
                n++;
            }
            return n == 0 ? 0f : Mathf.Clamp(sum / n * MarketCostPushScale, 0f, MarketCostPushCap);
        }

        /// <summary>勢力の国家状態（無ければ null）＝通貨/国庫/企業利潤の行き先解決に使う。</summary>
        private static FactionState StateOf(Faction fac)
        {
            var camp = StrategySession.Campaign;
            if (camp == null || camp.states == null) return null;
            for (int i = 0; i < camp.states.Count; i++)
                if (camp.states[i] != null && camp.states[i].faction == fac) return camp.states[i];
            return null;
        }

        /// <summary>勢力通貨の物価水準（インフレ）。未設定は1.0＝物価→市場の名目スケール。</summary>
        private float PriceLevelOf(Faction fac)
        {
            FactionState st = StateOf(fac);
            return st != null && st.currency != null ? Mathf.Max(0.01f, st.currency.priceLevel) : 1f;
        }

        // --- 企業（#1022 配線）：勢力ごとにセクター別の企業がPOP工員を雇い、市場価格で生産・利潤・資本蓄積する。 ---
        private readonly System.Collections.Generic.Dictionary<Faction, System.Collections.Generic.List<Enterprise>> enterprises
            = new System.Collections.Generic.Dictionary<Faction, System.Collections.Generic.List<Enterprise>>();

        /// <summary>勢力の企業一覧（雇用/資本/産出/利潤・#1022）。観測層（生産・流通オブザーバ）専用＝read-only。</summary>
        public System.Collections.Generic.IReadOnlyList<Enterprise> GetEnterprises(Faction faction)
            => enterprises.TryGetValue(faction, out var list) ? list : null;

        /// <summary>セクター（産業）が売る市場財＝市場価格の参照先（工業/農業→物資・鉱業→燃料・居住→奢侈品）。</summary>
        private static GoodType GoodForSector(SystemType sector)
        {
            switch (sector)
            {
                case SystemType.鉱業: return GoodType.燃料;
                case SystemType.居住: return GoodType.奢侈品;
                default: return GoodType.物資; // 工業/農業
            }
        }

        /// <summary>セクターが雇うPOP職業（工業→工員/農業→農民/鉱業→鉱員/居住→官吏）。</summary>
        private static Occupation OccForSector(SystemType sector)
        {
            switch (sector)
            {
                case SystemType.農業: return Occupation.農民;
                case SystemType.鉱業: return Occupation.鉱員;
                case SystemType.居住: return Occupation.官吏;
                default: return Occupation.工員; // 工業
            }
        }

        /// <summary>勢力のセクター別企業をデモ生成（専制=国有/民主=私有で所有形態が分かれる＝国有は雇用を守る）。</summary>
        private System.Collections.Generic.List<Enterprise> SeedEnterprises(Faction fac)
        {
            FactionState st = StateOf(fac);
            bool democratic = st != null && GovernmentFormRules.IsDemocratic(st.governmentForm);
            Ownership own = democratic ? Ownership.私有 : Ownership.国有;
            var list = new System.Collections.Generic.List<Enterprise>();
            SystemType[] sectors = { SystemType.工業, SystemType.農業, SystemType.鉱業, SystemType.居住 };
            for (int i = 0; i < sectors.Length; i++)
                list.Add(new Enterprise(fac, sectors[i], employees: 100f, capital: 1000f, productivity: 1f, wageRate: 1f,
                    name: $"{fac}{sectors[i]}{(own == Ownership.国有 ? "公社" : "社")}", ownership: own));
            return list;
        }

        /// <summary>所有惑星のうちセクターが雇う職業のPOP工員総数（採用の供給上限の素）。</summary>
        private static float SectorLaborPool(System.Collections.Generic.List<Province> owned, SystemType sector)
        {
            Occupation occ = OccForSector(sector);
            float sum = 0f;
            for (int i = 0; i < owned.Count; i++) sum += OccupationRules.Workers(owned[i], occ);
            return sum;
        }

        /// <summary>星系の生産チェーン在庫（森林→木材→建材→住宅・#2091）。未生成なら null。観測層（生産流通）専用＝read-only。</summary>
        public ChainStock GetChainStock(int systemId)
            => chainStocks.TryGetValue(systemId, out var cs) ? cs : null;

        /// <summary>星系のBOM消費財在庫（食品/衣類等・#2098）。未生成なら null。観測層（生産流通）専用＝read-only。</summary>
        public CommodityStock GetCommodityStock(int systemId)
            => bomStocks.TryGetValue(systemId, out var cs) ? cs : null;

        /// <summary>星系ごとの造船所一覧（建艦キュー/進捗・#884）。観測層（造船オブザーバ）専用＝read-only。</summary>
        public System.Collections.Generic.IReadOnlyList<Shipyard> Shipyards => shipyards;

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

                // 希少資源（#178 配線）：鉱床のある所有惑星だけが偏って産出する＝勢力ごとの希少資源備蓄へ蓄積。
                if (!strategicStockpiles.TryGetValue(fac, out var rare) || rare == null)
                {
                    rare = new StrategicResourceStockpile();
                    strategicStockpiles[fac] = rare;
                }
                for (int i = 0; i < owned.Count; i++)
                    StrategicResourceRules.ProduceFromProvince(rare, owned[i], 1f);

                // 市場（M-1 #179 配線）：供給＝産出率の合計／需要＝人口比例。価格を均衡へ収束させる＝
                // 供給>需要で下落・需要>供給で高騰（少量で創発）。経済(財政E)・生活水準と独立の価格レイヤー。
                float supSupplies = 0f, supFuel = 0f, supAmmo = 0f, popTotal = 0f;
                for (int i = 0; i < owned.Count; i++)
                {
                    supSupplies += ResourceProductionRules.ProvinceRate(owned[i], ResourceType.物資);
                    supFuel     += ResourceProductionRules.ProvinceRate(owned[i], ResourceType.燃料);
                    supAmmo     += ResourceProductionRules.ProvinceRate(owned[i], ResourceType.弾薬);
                    popTotal    += owned[i].population;
                }
                float supLuxury = supSupplies * 0.15f; // 余剰物資が奢侈品へ回る proxy
                if (!markets.TryGetValue(fac, out var mk) || mk == null)
                {
                    mk = new Market[]
                    {
                        new Market(GoodType.物資,   supSupplies, popTotal * DemandSupplies, MarketGoods[0].basePrice),
                        new Market(GoodType.燃料,   supFuel,     popTotal * DemandFuel,     MarketGoods[1].basePrice),
                        new Market(GoodType.弾薬,   supAmmo,     popTotal * DemandAmmo,     MarketGoods[2].basePrice),
                        new Market(GoodType.奢侈品, supLuxury,   popTotal * DemandLuxury,   MarketGoods[3].basePrice),
                    };
                    markets[fac] = mk;
                }
                mk[0].supply = supSupplies; mk[0].demand = popTotal * DemandSupplies;
                mk[1].supply = supFuel;     mk[1].demand = popTotal * DemandFuel;
                mk[2].supply = supAmmo;     mk[2].demand = popTotal * DemandAmmo;
                mk[3].supply = supLuxury;   mk[3].demand = popTotal * DemandLuxury;
                // 物価→市場（#179↔物価）：通貨のインフレ（priceLevel）で名目の基準価格が上がる＝市場価格に物価が乗る。
                float priceLevel = PriceLevelOf(fac);
                for (int g = 0; g < mk.Length; g++)
                {
                    var scaledGood = new Good(MarketGoods[g].goodType, MarketGoods[g].basePrice * priceLevel);
                    MarketRules.Tick(mk[g], scaledGood, MarketRules.MarketParams.Default, 1f);
                }

                // 企業（#1022 配線）：セクター別の企業が市場価格で生産・利潤→資本蓄積、POP工員供給を上限に雇用調整。
                // 国有企業の利潤は国庫へ（私有は民間に残り観測のみ）。市場価格（直上で更新）を読む＝企業↔市場が連動。
                if (!enterprises.TryGetValue(fac, out var firms) || firms == null)
                {
                    firms = SeedEnterprises(fac);
                    enterprises[fac] = firms;
                }
                FactionState fstate = StateOf(fac);
                for (int e = 0; e < firms.Count; e++)
                {
                    Enterprise firm = firms[e];
                    if (firm == null) continue;
                    Market gm = mk[(int)GoodForSector(firm.sector)];
                    float price = gm != null ? gm.price : 1f;
                    float laborSupply = SectorLaborPool(owned, firm.sector) * 0.1f; // 採用余地（供給の一部）
                    float profit = EnterpriseRules.Tick(firm, price, laborSupply, 1f);
                    if (firm.ownership == Ownership.国有 && profit > 0f && fstate != null)
                        fstate.treasury += profit * 0.5f; // 国有企業利潤の一部を国庫へ
                }

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
