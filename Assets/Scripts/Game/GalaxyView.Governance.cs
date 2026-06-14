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
                    provinces[s.id] = new Province(s.id, IdeologyOf(s.owner), 100f);
                // 復帰時点の所有を基準に（往復直後に誤って OnOccupied しないため）。
                prevOwners[s.id] = s.owner;
            }

            StrategySession.Provinces = provinces; // 以後この参照を永続化（static が生き続ける間）

            // 国家状態（#817 旗幟の基準忠誠の出所）：Battle 往復で失わないよう StrategySession に持たせる。
            if (StrategySession.Campaign == null) StrategySession.Campaign = new CampaignState(map);
            CampaignRules.EnsureStates(StrategySession.Campaign);

            AnnounceCampaignObjective(); // 遊べる縦スライス：目標と初手をプレイヤーに提示（オンボーディング）
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

            // 開始は帝国3:同盟3＝50:50（勝利/敗北しきい値70%＝開始時はどちらも未達＝開幕で決着しない）。
            // 帝国＝右クラスタ{0,2,3}／同盟＝左クラスタ{1,4,5}。中央ドラコ(3)が唯一の前線ハブ。
            map = new GalaxyMap();
            map.AddSystem(new StarSystem(0, "アスタ", new Vector2(0f, 3f), Faction.帝国));
            map.AddSystem(new StarSystem(1, "ベガ", new Vector2(-5f, -3f), Faction.同盟));
            map.AddSystem(new StarSystem(2, "ケレス", new Vector2(5f, 3f), Faction.帝国));
            map.AddSystem(new StarSystem(3, "ドラコ", new Vector2(0f, -0.5f), Faction.帝国));
            map.AddSystem(new StarSystem(4, "エリス", new Vector2(-2.5f, 1f), Faction.同盟));
            map.AddSystem(new StarSystem(5, "フェニクス", new Vector2(-3.5f, -2.5f), Faction.同盟));

            map.AddCorridor(new Corridor(2, 0, 4f, CorridorType.要衝));
            map.AddCorridor(new Corridor(0, 3, 5f));
            map.AddCorridor(new Corridor(3, 1, 4f));  // 前線：帝国ドラコ ⟷ 同盟ベガ
            map.AddCorridor(new Corridor(3, 4, 3f));  // 前線：帝国ドラコ ⟷ 同盟エリス
            map.AddCorridor(new Corridor(4, 1, 2f));
            map.AddCorridor(new Corridor(1, 5, 2f));
            map.AddCorridor(new Corridor(4, 5, 3f));

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

            reg = new StrategicFleetRegistry(map);
            reg.Add(new StrategicFleet(1, 2, Faction.帝国, 1.5f) { strength = 250 });
            reg.Add(new StrategicFleet(2, 1, Faction.同盟, 1.5f) { strength = 300 });
            reg.Add(new StrategicFleet(3, 4, Faction.同盟, 1.2f) { strength = 150 });
            reg.Add(new StrategicFleet(4, 3, Faction.帝国, 1.3f) { strength = 200 }); // ドラコ防衛・前線で衝突用

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

        // ===== 描画 =====

    }
}
