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
        /// 星系ごとの統治状態(Province)を用意する。Battle 往復時は StrategySession から復元し安定度/統合を引き継ぐ。
        /// デモ用に勢力へ思想を持たせ、住民の思想＝（初回は）開始所有勢力とする。
        /// </summary>
        private void SetupGovernance()
        {
            // デモ用の勢力データ（思想を持たせて内政の手応えを出す。実運用は Resources/Factions の FactionData）。
            demoFactions[Faction.帝国] = MakeDemoFaction("帝国", "専制", Faction.帝国);
            demoFactions[Faction.同盟] = MakeDemoFaction("同盟", "民主", Faction.同盟);

            provinces.Clear();
            // 永続化済みの内政状態があれば引き継ぐ（Battle 往復で安定度/統合を失わない）。
            if (StrategySession.Provinces != null)
                foreach (var kv in StrategySession.Provinces)
                    if (kv.Value != null) provinces[kv.Key] = kv.Value;

            foreach (var s in map.systems)
            {
                if (s == null) continue;
                // 復元に無い星系（初回・新規）だけ作る。住民の思想＝開始所有勢力（占領されても変わらない＝燻りの源）。
                if (!provinces.ContainsKey(s.id))
                {
                    var prov = new Province(s.id, IdeologyOf(s.owner), 100f);
                    SeedStrategicDeposit(prov, s.id); // #178：希少資源の鉱床を決定論で偏在配置（一部星系のみ）
                    provinces[s.id] = prov;
                }
                // 復帰時点の所有を基準に（往復直後に誤って OnOccupied しないため）。
                prevOwners[s.id] = s.owner;
            }

            StrategySession.Provinces = provinces; // 以後この参照を永続化（static が生き続ける間）

            // 国家状態（#817 旗幟の基準忠誠の出所）：Battle 往復で失わないよう StrategySession に持たせる。
            if (StrategySession.Campaign == null) StrategySession.Campaign = new CampaignState(map);
            CampaignRules.EnsureStates(StrategySession.Campaign);

            AnnounceCampaignObjective(); // 遊べる縦スライス：目標と初手をプレイヤーに提示（オンボーディング）
        }

        /// <summary>
        /// 希少資源の鉱床を星系IDから決定論で偏在配置する（#178 配線）。約4割の星系のみ鉱床を持ち、
        /// 種類（レアメタル/反応物質/超伝導体/希少結晶）と豊富さ（0.4〜1.0）も決定論で決まる＝再生成・往復で安定。
        /// </summary>
        private static void SeedStrategicDeposit(Province p, int systemId)
        {
            if (p == null || p.hasStrategicResource) return;
            unchecked
            {
                uint h = (uint)systemId * 2654435761u + 1013904223u;
                if (h % 5u >= 2u) return; // ~40% の星系のみ鉱床あり（偏在＝偏った産出）
                p.hasStrategicResource = true;
                p.strategicResource = (StrategicResourceType)(int)((h >> 11) % 4u);
                p.strategicAbundance = 0.4f + (h >> 7) % 7u / 10f; // 0.4..1.0
            }
        }

        private FactionData MakeDemoFaction(string name, string ideology, Faction legacy)
        {
            var f = ScriptableObject.CreateInstance<FactionData>();
            f.factionName = name; f.ideology = ideology; f.legacyFaction = legacy;
            return f;
        }

        private string IdeologyOf(Faction f) => demoFactions.TryGetValue(f, out var fd) && fd != null ? fd.ideology : "";

        /// <summary>各星系の内政を1tick進める：所有変化で OnOccupied（不安定化）、以降は目標安定度へ収束。</summary>
        private void TickGovernance(float dt)
        {
            if (dt <= 0f || map == null) return;
            foreach (var s in map.systems)
            {
                if (s == null) continue;
                if (!provinces.TryGetValue(s.id, out var prov) || prov == null) continue;

                // 所有が変わった＝占領 → 統合リセットで不安定化
                if (prevOwners.TryGetValue(s.id, out var prev) && prev != s.owner)
                {
                    GovernanceRules.OnOccupied(prov);
                    prevOwners[s.id] = s.owner;
                }

                FactionData owner = demoFactions.TryGetValue(s.owner, out var fd) ? fd : null;
                // 文官行政（総督＝地方＋宰相＝中央）が安定度目標を押し上げる＝名実の乖離で朝廷の権威ぶん減衰（権威0なら効かない）。
                // ＋経済・民心（創発ループ配線）：高税/債務スパイラル/民心崩壊が安定度を下げ反乱を誘発、繁栄は安定を支える。
                bool supplyOk = SupplyReadinessOf(s.owner) >= MilSupplyLowReadiness;
                GovernanceRules.Tick(prov, owner, supplyOk, atWar: HasHostileFleetAt(s),
                    deltaTime: dt, policy: prov.governancePolicy, adminBonus: SystemAdminBonus(s) + EconomyStabilityBonus(s.owner));
            }
        }

        /// <summary>
        /// マウス直下の自領星系について次の統治政策を上申する（<see cref="GameAction.統治政策上申"/>）。
        /// 戦略・政治の決定は直接変更せず、既存の稟議→決裁→執行を通す（#67）。
        /// </summary>
        private void CycleGovernancePolicyAtMouse()
        {
            // 文字入力中はゲーム操作として解釈しない（Alt+T を入力欄の打鍵で誤発火させない）。
            if (IsTextInputFocused()) return;
            if (cam == null || map == null || Mouse.current == null) return;

            // 窓（観測層・星系情報パネル等）の上では、その下の星系へ上申しない＝見えていない星を対象にしない。
            if (PointerOverUI())
            {
                NotificationCenter.Push(NotificationCategory.内政, NotificationSeverity.情報,
                    $"統治政策の上申（{GameInput.KeyLabel(GameAction.統治政策上申)}）：カーソルが窓の上にあります。星系に合わせるか、星系情報パネルのボタンを使ってください");
                return;
            }

            // 拾う範囲は星系情報（I キー）と同じ＝当たり判定（要塞は大きい）と最小半径の大きいほう。
            Vector2 w = WorldMouse();
            int systemId = NearestSystemDist(w, out float distance);
            if (systemId < 0 || !GovernanceProposalRules.AcceptsPointerDistance(distance, ClickRadiusFor(systemId)))
            {
                NotificationCenter.Push(NotificationCategory.内政, NotificationSeverity.注意,
                    GovernanceProposalRules.RejectionText(GovernanceProposalRejection.星系なし, ""));
                return;
            }

            // 受付判定・上申・結果の通知はボタンと同じ入口（RingiDirector.ProposeNextGovernancePolicy）。
            RingiDirector.ProposeNextGovernancePolicy(systemId, out _, out _);
        }

        /// <summary>所有勢力の在任宰相による安定度寄与（名実の乖離＝朝廷の権威で減衰・<see cref="AdministrationRules"/>）。空席/非デモ勢力は0。</summary>
        private float PremierAdminBonus(Faction owner)
        {
            if (civilOffices == null) return 0f;
            for (int f = 0; f < DemoFactions.Length; f++)
            {
                if (DemoFactions[f] != owner) continue;
                Office office = civilOffices[f];
                if (office == null) return 0f;
                var premier = GovernmentRegistry.GetHolder(office) as Person;
                float authority = courtAuthority != null ? courtAuthority.authority : 0f;
                return AdministrationRules.StabilityContribution(premier, authority, AdministrationRules.AdminParams.Default);
            }
            return 0f;
        }

        /// <summary>所有勢力の経済・民心が安定度へ与える±補正（創発ループ＝高税/債務/低民心で反乱を誘発・<see cref="GovernanceEconomyRules"/>）。国家状態が無ければ0。</summary>
        private float EconomyStabilityBonus(Faction owner)
        {
            var camp = StrategySession.Campaign;
            if (camp == null) return 0f;
            FactionState fs = CampaignRules.GetState(camp, owner);
            return fs == null ? 0f : GovernanceEconomyRules.StabilityModifier(fs);
        }

        /// <summary>その星系に所有勢力と敵対する戦略艦隊が停泊しているか（戦時ペナルティ判定）。</summary>
        private bool HasHostileFleetAt(StarSystem s)
        {
            var here = reg.FleetsAt(s.id);
            for (int i = 0; i < here.Count; i++)
            {
                StrategicFleet f = here[i];
                if (f != null && FactionRelations.IsHostile(null, f.faction, s.ownerData, s.owner)) return true;
            }
            return false;
        }

        /// <summary>
        /// 星系の座標だけを <see cref="GalaxyLayoutRules"/> で決め直す（#戦略MAP刷新）。
        /// <b>星系id・所有・回廊接続・艦隊の所在には一切触れない</b>＝セーブ整合と進行への影響なし。
        /// fresh=true は新規生成（陣営の帯へ層化して配る）、false は既存座標を活かした調整（読み込み後）。
        /// </summary>
        private void ApplyGalaxyLayout(bool fresh, System.Func<float> roll)
        {
            if (map == null || map.systems == null || map.systems.Count == 0) return;

            var nodes = new List<LayoutNode>(map.systems.Count);
            for (int i = 0; i < map.systems.Count; i++)
            {
                StarSystem s = map.systems[i];
                if (s == null) continue;
                nodes.Add(new LayoutNode(s.id, LayoutSideOf(s.owner), s.position));
            }
            if (nodes.Count == 0) return;

            GalaxyLayoutParams p = GalaxyLayoutParams.Default;
            if (fresh) GalaxyLayoutRules.Layout(nodes, p, roll);
            else GalaxyLayoutRules.Refine(nodes, p);

            for (int i = 0; i < nodes.Count; i++)
            {
                StarSystem s = map.GetSystem(nodes[i].id);
                if (s != null) s.position = nodes[i].position;
            }
        }

        /// <summary>
        /// 航路の交差を座標だけで解く（#航路が交錯する）。<b>回廊は読むだけ＝接続も本数も変えない</b>
        /// （消すと進軍経路が失われる）。解けたら true。非平面などで解けなければ false を返し、
        /// 残った交差数を通知に出す＝黙って諦めない。
        /// </summary>
        private bool UntangleCorridors(System.Func<float> roll)
        {
            if (map == null || map.systems == null || map.corridors == null) return true;

            var nodes = new List<LayoutNode>(map.systems.Count);
            for (int i = 0; i < map.systems.Count; i++)
            {
                StarSystem s = map.systems[i];
                if (s == null) continue;
                nodes.Add(new LayoutNode(s.id, LayoutSideOf(s.owner), s.position));
            }
            if (nodes.Count == 0) return true;

            var edges = new List<LayoutEdge>(map.corridors.Count);
            for (int i = 0; i < map.corridors.Count; i++)
            {
                Corridor c = map.corridors[i];
                if (c != null) edges.Add(new LayoutEdge(c.aId, c.bId));
            }

            bool ok = GalaxyPlanarityRules.Untangle(
                nodes, edges, GalaxyLayoutParams.Default, GalaxyPlanarityParams.Default, roll, out int remaining);

            // 座標だけ書き戻す（id で引く＝並び順に依存しない）。
            for (int i = 0; i < nodes.Count; i++)
            {
                StarSystem s = map.GetSystem(nodes[i].id);
                if (s != null) s.position = nodes[i].position;
            }

            if (!ok)
            {
                // 平面に描けないグラフ（K5/K3,3 を含む等）。辺は消さずに最小の交差で描く＝経路は全て残る。
                Debug.LogWarning($"[GalaxyView] 航路の交差を解ききれませんでした（残り {remaining}）。" +
                                 "平面に描けないグラフの可能性があります。航路は削除していません。");
                NotificationCenter.Push(NotificationCategory.システム, NotificationSeverity.注意,
                    $"航路の交差が {remaining} 箇所残りました（経路は保持）");
            }
            return ok;
        }

        /// <summary>
        /// 盤面から決まる種（星系idと回廊の端点から作る）。読み込みのたびに同じ結果になるようにする＝
        /// 同じセーブを何度開いても同じ配置になり、プレイヤーが位置を覚えられる。
        /// </summary>
        private int SeedFromMap()
        {
            unchecked
            {
                int h = 17;
                if (map?.systems != null)
                    for (int i = 0; i < map.systems.Count; i++)
                        if (map.systems[i] != null) h = h * 31 + map.systems[i].id;
                if (map?.corridors != null)
                    for (int i = 0; i < map.corridors.Count; i++)
                        if (map.corridors[i] != null) h = h * 31 + map.corridors[i].aId * 7 + map.corridors[i].bId;
                return h;
            }
        }

        /// <summary>決定論的な roll(0..1) を作る（同じ種なら毎回同じ列）。</summary>
        private static System.Func<float> DeterministicRoll(int seed)
        {
            var rng = new System.Random(seed);
            return () => (float)rng.NextDouble();
        }

        /// <summary>回廊長を現在の座標から作り直す（新規生成のみ）。見た目の距離とワープ所要時間を一致させる。</summary>
        private void RecomputeCorridorLengths()
        {
            if (map == null || map.corridors == null) return;
            for (int i = 0; i < map.corridors.Count; i++)
            {
                Corridor c = map.corridors[i];
                if (c == null) continue;
                StarSystem a = map.GetSystem(c.aId), b = map.GetSystem(c.bId);
                if (a == null || b == null) continue;
                c.length = Mathf.Max(2f, Vector2.Distance(a.position, b.position));
            }
        }

        /// <summary>所有勢力を配置の帯へ写す（同盟＝左／帝国＝右／その他＝中央の係争帯）。</summary>
        private static int LayoutSideOf(Faction owner)
        {
            if (owner == Faction.同盟) return -1;
            if (owner == Faction.帝国) return +1;
            return 0;
        }

        private void BuildDemoGalaxy()
        {
            // 戦略↔実会戦の往復で世界状態を保持（あれば再利用）
            if (StrategySession.HasState)
            {
                map = StrategySession.Map; reg = StrategySession.Reg;
                // セーブから戻った盤面にも新しい見た目を適用する（#戦略MAP刷新）。
                // Refine は「重なりを解く＋枠いっぱいへ寄せる」だけで相対の位置関係を保ち、
                // **保存済みの回廊長には触れない**＝ワープ所要時間と航路接続は完全に不変。
                // 収束済みの盤面では何も動かないため、会戦との往復で毎回呼ばれても座標は流れない（冪等）。
                ApplyGalaxyLayout(fresh: false, roll: null);
                // 航路の交差も座標だけで解く（#航路が交錯する）。回廊の接続・保存済みの長さ・星系idは不変＝
                // 進軍中の艦隊の参照（currentSystemId/destinationSystemId と回廊長）に影響しない。
                // 既に交差0なら1mmも動かさない（冪等）＝会戦との往復や再読み込みで座標が流れない。
                UntangleCorridors(DeterministicRoll(SeedFromMap()));
                return;
            }

            // 新規戦役ごとに銀河マップを手続き生成して多様化する（#いろんなマップ）。
            // 不変条件：両勢力の星系数を等しくして約50:50（支配率しきい値70%＝開幕で決着しない）／
            //           各クラスタを連結／最低1本の前線回廊（敵対端点）。生成結果は StrategySession/セーブに
            //           保存されるため決定論的再生成は不要（毎回違ってよい）。
            GenerateGalaxy();

            // 帝国星系は惑星（制空権持ち）で防衛＝同盟は停泊だけでは占領できず攻城が要る（#131）。
            // 同盟星系は無防備（planet 無し）＝従来どおり停泊で占領（両方の挙動をデモ）。
            // PB-6 デモ：帝国星系の最初の2つを要塞・コロニーにして「同枠攻略」を見せる。残りは従来の惑星。
            int siegeVariety = 0;
            foreach (var s in map.systems)
                if (s != null && s.owner == Faction.帝国)
                {
                    if (siegeVariety == 0)
                        s.planet = PlanetSiegeRules.CreateTarget(s.id, Faction.帝国, Planet.SiegeTargetKind.要塞);
                    else if (siegeVariety == 1)
                        s.planet = PlanetSiegeRules.CreateTarget(s.id, Faction.帝国, Planet.SiegeTargetKind.コロニー);
                    else
                        s.planet = new Planet(s.id, Faction.帝国, demoPlanetDefense, demoPlanetDefense);
                    siegeVariety++;
                }

            // 梯団編成（デモ）：艦隊 ⊂ 軍団(corpsId) ⊂ 軍集団(armyGroupId)。生成マップの後方星系へ配置する。
            PopulateDemoFleets();

            // 難易度の開始戦力傾き（易しい＝自軍強め/敵弱め）。プレイヤー勢力以外を敵として倍率を掛ける（基準は等倍＝普通）。
            CampaignDifficulty diff = GameSettings.Instance != null ? GameSettings.Instance.campaignDifficulty : CampaignDifficulty.普通;
            Faction pf = GameSettings.Instance != null ? GameSettings.Instance.playerFaction : Faction.同盟;
            float pFac = CampaignDifficultyRules.PlayerStrengthFactor(diff);
            float eFac = CampaignDifficultyRules.EnemyStrengthFactor(diff);
            foreach (var f in reg.fleets)
                if (f != null)
                    f.strength = Mathf.Max(1, Mathf.RoundToInt(f.strength * (f.faction == pf ? pFac : eFac)));

            // ★難易度補正で兵力が確定したこの時点で、各艦隊の初期艦艇数を<b>引き直して確定</b>させる。
            // 盤面へ加えた時点（reg.Add）でも一度確定しているが、それは補正前の兵力に基づく値なので、
            // 補正後の兵力で上書きする。ここで確定しておけば以後は毎回の導出に頼らない
            //（導出は旧セーブ・旧データを読むときだけの後方互換）。
            foreach (var f in reg.fleets)
                if (f != null) f.SetShips(FleetShipCountRules.FromStrength(f.strength));

            StrategySession.Set(map, reg);
        }

        // 戦略マップの星系名プール（世界の名峰ベース・日本除く・#星系名）。実体は Core の MountainSystemNames。
        private static string[] SystemNamePool => MountainSystemNames.Names;

        /// <summary>
        /// 新規戦役の銀河マップを手続き生成する（#いろんなマップ）。複数のトポロジ原型（0=二大陣営対峙／
        /// 1=中央ハブ争奪／2=長い前線）をランダムに選び、星系数(各勢力3〜5)・配置・回廊を毎回変える。
        /// 不変条件：両勢力の星系数を等しく約50:50（しきい値70%＝開幕で決着しない）、各クラスタを連結、
        /// 最低1本の前線回廊（敵対端点＝会戦が生起する）。決定論不要（保存されるため毎回違ってよい）。
        /// </summary>
        private void GenerateGalaxy()
        {
            map = new GalaxyMap();
            var rng = new System.Random();

            // 名前をシャッフルして重複なく引く。
            var names = new List<string>(SystemNamePool);
            for (int i = names.Count - 1; i > 0; i--) { int j = rng.Next(i + 1); (names[i], names[j]) = (names[j], names[i]); }
            int nameIdx = 0;
            string NextName() => nameIdx < names.Count ? names[nameIdx++] : ("星系" + (++nameIdx));

            float Jit() => (float)(rng.NextDouble() * 2.0 - 1.0);
            int perSide = 3 + rng.Next(3);   // 各勢力 3〜5 星系（合計6〜10＝終盤ラグ規律：少数に保つ）
            int archetype = rng.Next(3);     // 0=対峙 / 1=中央ハブ争奪 / 2=長い前線

            var ally = new List<int>();      // 同盟（左）
            var imp = new List<int>();       // 帝国（右）
            int id = 0;

            // --- 星系の配置（原型ごとに形を変える） ---
            if (archetype == 2)
            {
                // 長い前線：左右2列に同数を並べ、各行で前線回廊を張る。
                for (int r = 0; r < perSide; r++)
                {
                    float t = perSide == 1 ? 0.5f : (float)r / (perSide - 1);
                    float y = Mathf.Lerp(-4.5f, 4.5f, t) + Jit() * 0.4f;
                    map.AddSystem(new StarSystem(id, NextName(), new Vector2(-3.2f + Jit() * 0.6f, y), Faction.同盟)); ally.Add(id++);
                    map.AddSystem(new StarSystem(id, NextName(), new Vector2(3.2f + Jit() * 0.6f, y), Faction.帝国)); imp.Add(id++);
                }
            }
            else
            {
                // 左クラスタ＝同盟／右クラスタ＝帝国（散らす）。
                for (int i = 0; i < perSide; i++)
                { map.AddSystem(new StarSystem(id, NextName(), new Vector2(-6f + Jit() * 1.6f, Jit() * 4f), Faction.同盟)); ally.Add(id++); }
                for (int i = 0; i < perSide; i++)
                { map.AddSystem(new StarSystem(id, NextName(), new Vector2(6f + Jit() * 1.6f, Jit() * 4f), Faction.帝国)); imp.Add(id++); }
            }

            // 中央ハブ（archetype 1）：中央に争奪星系を1つ。所有はランダム（僅差は許容＝しきい値70%に届かない）。
            int hub = -1;
            if (archetype == 1)
            {
                Faction ho = rng.Next(2) == 0 ? Faction.帝国 : Faction.同盟;
                map.AddSystem(new StarSystem(id, NextName(), new Vector2(Jit() * 0.8f, Jit() * 1.2f), ho));
                hub = id; (ho == Faction.帝国 ? imp : ally).Add(id++);
            }

            // --- 配置の確定（#戦略MAP刷新）---
            // ここまでの座標は「どのクラスタに属するか」を決めるための仮置き。回廊を張る前に本配置へ均し、
            // 陣営の帯・最小星間距離・画面いっぱいの使用を満たす。**回廊長はこの後の Link で確定座標から作る**
            // ＝見た目の距離とワープ所要時間が一致する。枠の尺は従来と同程度なので所要時間の水準は変わらない。
            ApplyGalaxyLayout(fresh: true, roll: () => (float)rng.NextDouble());

            // 近傍解決（位置が近い候補id。from は除外）。
            int Nearest(int from, List<int> cand)
            {
                StarSystem f = map.GetSystem(from);
                int best = from; float bd = float.MaxValue;
                if (f == null) return best;
                foreach (int c in cand)
                {
                    if (c == from) continue;
                    StarSystem s = map.GetSystem(c);
                    if (s == null) continue;
                    float d = Vector2.Distance(f.position, s.position);
                    if (d < bd) { bd = d; best = c; }
                }
                return best;
            }

            // --- 回廊（連結＋前線） ---
            var edges = new HashSet<long>();
            void Link(int a, int b, CorridorType t = CorridorType.通商)
            {
                if (a == b) return;
                long key = Mathf.Min(a, b) * 1000L + Mathf.Max(a, b);
                if (!edges.Add(key)) return;
                StarSystem sa = map.GetSystem(a), sb = map.GetSystem(b);
                float len = sa != null && sb != null ? Mathf.Max(2f, Vector2.Distance(sa.position, sb.position)) : 3f;
                map.AddCorridor(new Corridor(a, b, len, t));
            }

            // 候補の航路が既存の航路と交差するか（#航路が交錯する＝張る前に弾く）。
            bool WouldCross(int a, int b)
            {
                StarSystem sa = map.GetSystem(a), sb = map.GetSystem(b);
                if (sa == null || sb == null) return false;
                for (int i = 0; i < map.corridors.Count; i++)
                {
                    Corridor c = map.corridors[i];
                    if (c == null) continue;
                    StarSystem ca = map.GetSystem(c.aId), cb = map.GetSystem(c.bId);
                    if (ca == null || cb == null) continue;
                    if (GalaxyPlanarityRules.SegmentsCross(sa.position, sb.position, ca.position, cb.position, 1e-4f))
                        return true;
                }
                return false;
            }

            // 各クラスタを連結（鎖＋ランダムな弦を0〜2本＝形に変化）。
            // 鎖は<b>位置順（下から上）</b>に張る＝生成順のままだと鎖自身が折り返して交差するため。
            // 弦は交差しない対だけを採る（連結性は鎖が担保済みなので、張れなければ諦めてよい）。
            void Wire(List<int> cluster)
            {
                var ordered = new List<int>(cluster);
                ordered.Sort((x, y) =>
                {
                    StarSystem sx = map.GetSystem(x), sy = map.GetSystem(y);
                    if (sx == null || sy == null) return 0;
                    int cmp = sx.position.y.CompareTo(sy.position.y);
                    return cmp != 0 ? cmp : sx.position.x.CompareTo(sy.position.x);
                });
                for (int i = 1; i < ordered.Count; i++) Link(ordered[i - 1], ordered[i]);

                int chords = ordered.Count >= 4 ? 1 + rng.Next(2) : 0;
                for (int c = 0; c < chords && ordered.Count > 2; c++)
                {
                    // 交差しない弦を何回か探し、見つからなければこの弦は張らない。
                    for (int attempt = 0; attempt < 8; attempt++)
                    {
                        int a = ordered[rng.Next(ordered.Count)];
                        int b = ordered[rng.Next(ordered.Count)];
                        if (a == b || WouldCross(a, b)) continue;
                        Link(a, b, CorridorType.要衝);
                        break;
                    }
                }
            }
            Wire(ally); Wire(imp);

            // y の低い順に並べ替えた写し（前線の張り方を「順位どうし」に揃えて交差を防ぐ）。
            List<int> ByHeight(List<int> src)
            {
                var o = new List<int>(src);
                o.Sort((x, y) =>
                {
                    StarSystem sx = map.GetSystem(x), sy = map.GetSystem(y);
                    if (sx == null || sy == null) return 0;
                    return sx.position.y.CompareTo(sy.position.y);
                });
                return o;
            }

            // 交差しない相手を優先して選ぶ（見つからなければ最近傍＝連結性を優先し、後段の untangle が解く）。
            int NearestNonCrossing(int from, List<int> cand)
            {
                int best = -1; float bd = float.MaxValue;
                StarSystem f = map.GetSystem(from);
                if (f == null) return Nearest(from, cand);
                for (int i = 0; i < cand.Count; i++)
                {
                    int c = cand[i];
                    if (c == from) continue;
                    StarSystem s = map.GetSystem(c);
                    if (s == null || WouldCross(from, c)) continue;
                    float d = Vector2.Distance(f.position, s.position);
                    if (d < bd) { bd = d; best = c; }
                }
                return best >= 0 ? best : Nearest(from, cand);
            }

            // 前線：敵対クラスタ間を橋渡し（最低1本＝会戦が生起する）。
            if (archetype == 2)
            {
                // 各行で前線。**双方を y 順に並べて同順位どうしを結ぶ**＝生成順のままだと橋が互いに交差する。
                List<int> a2 = ByHeight(ally), i2 = ByHeight(imp);
                int rows = Mathf.Min(a2.Count, i2.Count);
                for (int r = 0; r < rows; r++) Link(a2[r], i2[r], CorridorType.要衝);
            }
            else if (hub >= 0)
            {
                Link(hub, NearestNonCrossing(hub, ally), CorridorType.要衝); // ハブ＝前線（両側へ）
                Link(hub, NearestNonCrossing(hub, imp), CorridorType.要衝);
                if (rng.Next(2) == 0) { int a = ally[rng.Next(ally.Count)]; Link(a, NearestNonCrossing(a, imp), CorridorType.要衝); }
            }
            else
            {
                int bridges = 1 + rng.Next(2); // 対峙：内側どうしを1〜2本
                for (int b = 0; b < bridges; b++)
                { int a = ally[rng.Next(ally.Count)]; Link(a, NearestNonCrossing(a, imp), CorridorType.要衝); }
            }

            // 航路の交差を座標だけで解く（#航路が交錯する）。回廊を張った後に位置を詰めるので、
            // このあと長さを作り直して「見た目の距離＝ワープ所要時間」を一致させる。
            UntangleCorridors(() => (float)rng.NextDouble());
            RecomputeCorridorLengths();

            // 新配置では陣営の帯が中央へ寄るぶん、前線回廊の見た目の距離が旧配置（左右 ±6＝約12）より短い。
            // 素通りだと開幕から数日で接敵してしまうため、敵対どうしを結ぶ回廊にだけ長さの下限を課し、
            // 「前線へ出るまでに時間がかかる」という従来のテンポを保つ（陣営内の移動時間は従来の振れ幅の内）。
            EnforceFrontCorridorLength();

            // #40 戦略ノード：前線の要衝回廊を1本だけ要塞で封鎖する（帝国側）。同盟は撃破/制圧しないと通れない。
            PlaceDemoFortress();
        }

        /// <summary>
        /// 敵対勢力どうしを結ぶ回廊（＝前線）の長さに下限を課す（#戦略MAP刷新）。
        /// 長さはワープ所要時間の素なので、配置を締めても開幕の接敵タイミングが早くなりすぎない。
        /// 陣営内の回廊には触れない＝territory 内の機動は見た目どおりの時間で動く。
        /// </summary>
        private void EnforceFrontCorridorLength()
        {
            if (map == null || map.corridors == null) return;
            for (int i = 0; i < map.corridors.Count; i++)
            {
                Corridor c = map.corridors[i];
                if (c == null) continue;
                StarSystem a = map.GetSystem(c.aId), b = map.GetSystem(c.bId);
                if (a == null || b == null) continue;
                if (!FactionRelations.IsHostile(null, a.owner, null, b.owner)) continue;
                if (c.length < frontCorridorMinLength) c.length = frontCorridorMinLength;
            }
        }

        /// <summary>#40 デモ：敵対勢力をつなぐ前線の要衝回廊を1本だけ要塞で封鎖する（要塞所有は帝国側）。</summary>
        private void PlaceDemoFortress()
        {
            if (map == null || map.corridors == null) return;
            for (int i = 0; i < map.corridors.Count; i++)
            {
                Corridor c = map.corridors[i];
                if (c == null || c.type != CorridorType.要衝 || c.fortress != null) continue;
                StarSystem a = map.GetSystem(c.aId), b = map.GetSystem(c.bId);
                if (a == null || b == null) continue;
                if (!FactionRelations.IsHostile(null, a.owner, null, b.owner)) continue; // 前線（敵対）のみ
                Faction owner = a.owner == Faction.帝国 ? a.owner : b.owner;             // 帝国側を要塞所有者に
                c.fortress = new Fortress(1200f, 740f, 1f, true) { owner = owner, fortressName = "イゼルローン要塞" };
                return; // デモは1つだけ
            }
        }

        /// <summary>
        /// 生成マップ上に両勢力の艦隊（軍団 ⊂ 軍集団）を配置する（デモ）。各勢力の後方星系に2個軍団を置き、
        /// 軍団は軍団旗艦（★）＋随伴艦隊で構成する。位置は星系idに依存せず生成結果から解決する。
        /// 帝国は1星系に集結（軍集団の入れ子枠を開幕から見せる）、同盟は別星系に分散して両方の見た目をデモ。
        /// </summary>
        private void PopulateDemoFleets()
        {
            reg = new StrategicFleetRegistry(map);
            var rng = new System.Random();
            int fleetId = 1, corpsId = 1;

            void Deploy(Faction fac, int armyGroupId, bool concentrate)
            {
                string fp = fac == Faction.帝国 ? "帝国" : "同盟";
                string agName = fp + "第1軍集団";

                // この勢力の星系を「後方ほど先」（前線=x≈0 から遠い順）に並べる。
                var owned = new List<StarSystem>();
                foreach (var s in map.systems) if (s != null && s.owner == fac) owned.Add(s);
                if (owned.Count == 0) return;
                owned.Sort((a, b) => Mathf.Abs(b.position.x).CompareTo(Mathf.Abs(a.position.x)));

                int corpsCount = Mathf.Min(2, owned.Count);
                for (int c = 0; c < corpsCount; c++)
                {
                    StarSystem at = concentrate ? owned[0] : owned[Mathf.Min(c, owned.Count - 1)];
                    int cid = corpsId++;
                    string cname = $"{fp}第{c + 1}軍団";
                    reg.Add(new StrategicFleet(fleetId++, at.id, fac, 1.3f + (float)rng.NextDouble() * 0.3f)
                    { strength = 200 + rng.Next(120), corpsId = cid, corpsName = cname, isCorpsFlagship = true, armyGroupId = armyGroupId, armyGroupName = agName });
                    reg.Add(new StrategicFleet(fleetId++, at.id, fac, 1.1f + (float)rng.NextDouble() * 0.2f)
                    { strength = 130 + rng.Next(80), corpsId = cid, corpsName = cname, armyGroupId = armyGroupId, armyGroupName = agName });
                }
            }

            Deploy(Faction.帝国, 1, concentrate: true);
            Deploy(Faction.同盟, 2, concentrate: false);
        }

        // ===== 描画 =====

    }
}
