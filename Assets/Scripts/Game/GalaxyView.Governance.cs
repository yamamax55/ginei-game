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
                GovernanceRules.Tick(prov, owner, supplyOk: true, atWar: HasHostileFleetAt(s),
                    deltaTime: dt, policy: GovernancePolicy.民生, adminBonus: SystemAdminBonus(s) + EconomyStabilityBonus(s.owner));
            }
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

        private void BuildDemoGalaxy()
        {
            // 戦略↔実会戦の往復で世界状態を保持（あれば再利用）
            if (StrategySession.HasState) { map = StrategySession.Map; reg = StrategySession.Reg; return; }

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

            StrategySession.Set(map, reg);
        }

        // 戦略マップの星系名プール（手続き生成で重複なく引く）。
        private static readonly string[] SystemNamePool =
        {
            "アスタ","ベガ","ケレス","ドラコ","エリス","フェニクス","オリオン","シリウス",
            "リゲル","アルタイル","デネブ","アンタレス","ポラリス","カノープス","スピカ","プロキオン",
            "カペラ","アルデバラン","レグルス","ミラ","ベラトリクス","ハダル","ミザール","アルゴル",
            "タロス","ニケ","ヘスティア","ネメシス","ガイア","クロノス","セレネ","ヘリオス",
        };

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

            // 各クラスタを連結（鎖＋ランダムな弦を0〜2本＝形に変化）。
            void Wire(List<int> cluster)
            {
                for (int i = 1; i < cluster.Count; i++) Link(cluster[i - 1], cluster[i]);
                int chords = cluster.Count >= 4 ? 1 + rng.Next(2) : 0;
                for (int c = 0; c < chords && cluster.Count > 2; c++)
                    Link(cluster[rng.Next(cluster.Count)], cluster[rng.Next(cluster.Count)], CorridorType.要衝);
            }
            Wire(ally); Wire(imp);

            // 前線：敵対クラスタ間を橋渡し（最低1本＝会戦が生起する）。
            if (archetype == 2)
            {
                for (int r = 0; r < perSide; r++) Link(ally[r], imp[r], CorridorType.要衝); // 各行で前線
            }
            else if (hub >= 0)
            {
                Link(hub, Nearest(hub, ally), CorridorType.要衝); // ハブ＝前線（両側へ）
                Link(hub, Nearest(hub, imp), CorridorType.要衝);
                if (rng.Next(2) == 0) { int a = ally[rng.Next(ally.Count)]; Link(a, Nearest(a, imp), CorridorType.要衝); }
            }
            else
            {
                int bridges = 1 + rng.Next(2); // 対峙：内側どうしを1〜2本
                for (int b = 0; b < bridges; b++)
                { int a = ally[rng.Next(ally.Count)]; Link(a, Nearest(a, imp), CorridorType.要衝); }
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
