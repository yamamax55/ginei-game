using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace Ginei
{
    public partial class GalaxyView
    {
        // 反乱（内政→戦略の創発ループ）：星系ごとの不穏スコア累積と「兆し」警告の既出フラグ。
        private readonly Dictionary<int, float> rebellionScore = new Dictionary<int, float>();
        private readonly HashSet<int> rebellionWarned = new HashSet<int>();

        /// <summary>
        /// 反乱を年次で解決する：所有星系の不穏スコアを更新し、閾値超過で<b>離反</b>（隣接する敵対勢力へ寝返り／無ければ対勢力へ）。
        /// 高税/債務/占領直後の低統合/補給切れ → 安定度低下 → 反乱 → 星系喪失、という台本なしの因果を作る（数値は `RebellionRules` へ委譲）。
        /// 兆し域では一度だけ警告して猶予を与える（プレイヤーは G の国策などで安定を立て直せる）。
        /// </summary>
        private void RunRebellionTick()
        {
            if (map == null || provinces == null) return;
            foreach (var s in map.systems)
            {
                if (s == null || !provinces.TryGetValue(s.id, out var prov) || prov == null) continue;
                rebellionScore.TryGetValue(s.id, out float score);
                score = RebellionRules.NextScore(score, prov);

                if (RebellionRules.ShouldRevolt(score))
                {
                    Faction old = s.owner;
                    Faction rebel = RebelTargetFaction(s);
                    s.owner = rebel;
                    if (s.planet != null) s.planet.owner = rebel; // 惑星防衛も新所有者へ
                    rebellionScore[s.id] = 0f;
                    rebellionWarned.Remove(s.id);
                    NotificationCenter.Push(NotificationCategory.政治, NotificationSeverity.警告,
                        $"{s.systemName} が離反！（{old}→{rebel}）内政の乱れが反乱を招いた");
                    // 占領扱い：次の TickGovernance が所有変化を検知し OnOccupied で新所有者にも不安定を課す。
                }
                else
                {
                    rebellionScore[s.id] = score;
                    if (RebellionRules.IsBrewing(score) && rebellionWarned.Add(s.id))
                        NotificationCenter.Push(NotificationCategory.政治, NotificationSeverity.注意,
                            $"{s.systemName} で反乱の兆し（安定度を立て直さねば離反する）");
                    else if (!RebellionRules.IsBrewing(score))
                        rebellionWarned.Remove(s.id);
                }
            }
        }

        /// <summary>離反先の勢力：隣接する敵対勢力があればそこへ寝返る（無ければ legacy の対勢力）。</summary>
        private Faction RebelTargetFaction(StarSystem s)
        {
            if (map != null && s != null)
                foreach (int nid in map.Neighbors(s.id))
                {
                    StarSystem n = map.GetSystem(nid);
                    if (n != null && n.owner != s.owner) return n.owner;
                }
            return (s != null && s.owner == Faction.帝国) ? Faction.同盟 : Faction.帝国;
        }

        /// <summary>
        /// 政体進化を年次で回す（#117）：初期形態をシード（帝国=君主制/同盟=共和制/他=首長制）し、社会シグナル
        /// （正統性/腐敗/合意/希望/包摂）から `GovernmentFormRules.NextForm` で年1回1遷移を進めて通知する。数式は Core へ委譲。
        /// </summary>
        private void RunRegimeEvolutionTick()
        {
            var camp = StrategySession.Campaign;
            if (camp == null || camp.states == null) return;

            if (!regimeFormsSeeded)
            {
                for (int i = 0; i < camp.states.Count; i++)
                {
                    FactionState s = camp.states[i];
                    if (s == null || s.governmentForm != GovernmentForm.首長制) continue;
                    s.governmentForm = s.faction == Faction.帝国 ? GovernmentForm.君主制
                                     : s.faction == Faction.同盟 ? GovernmentForm.共和制
                                     : GovernmentForm.首長制; // 他勢力は首長制スタート
                }
                regimeFormsSeeded = true;
            }

            for (int i = 0; i < camp.states.Count; i++)
            {
                FactionState s = camp.states[i];
                if (s == null) continue;

                // (1) 政変（C1 Tier A）：統制が弱いとクーデター/革命が発火し、成功で政体が転換する。
                CoupContext ctx = PoliticalUpheavalRules.ContextOf(s);
                UpheavalResult up = PoliticalUpheavalRules.ResolveUpheaval(s.governmentForm, ctx, UnityEngine.Random.value);
                if (up.attempted)
                {
                    if (s.regime != null) s.regime.legitimacy = up.newLegitimacy; // 事後正統性（成功/粛清/内戦）
                    if (up.formChanged)
                    {
                        GovernmentForm from = s.governmentForm;
                        GovernmentFormRules.Apply(s, up.newForm);
                        NotificationCenter.Push(NotificationCategory.政治, NotificationSeverity.警告, $"{s.faction} {up.type}クーデター成功＝政体が {from} → {up.newForm} へ");
                    }
                    else
                    {
                        string note = up.outcome == CoupOutcome.内戦 ? "内戦化" : "未遂（鎮圧）";
                        NotificationCenter.Push(NotificationCategory.政治, NotificationSeverity.注意, $"{s.faction} {up.type}クーデター {note}");
                    }
                    continue; // 政変があった年は緩やかな進化はスキップ
                }

                // (2) 緩やかな進化：社会シグナルで合法な遷移を1段進める。
                RegimeSignals signals = GovernmentFormRules.SignalsOf(s);
                GovernmentForm next = GovernmentFormRules.NextForm(s.governmentForm, signals);
                if (next != s.governmentForm)
                {
                    GovernmentForm prev = s.governmentForm;
                    GovernmentFormRules.Apply(s, next);
                    NotificationCenter.Push(NotificationCategory.政治, NotificationSeverity.注意, $"{s.faction} 政体が {prev} → {next} へ移行");
                }
            }
        }

        /// <summary>
        /// 政党政治の年次 Tick（#159 配線）：民主政治の勢力ごとに、成熟度に応じて政党制を二大政党へ収束させ、
        /// 衆参の選挙日程を回し、分断危機の立ち上がりを通知する。数値は <see cref="PoliticsTickRules"/>（→PartySystemRules/ElectionScheduleRules）へ委譲。
        /// </summary>
        private void RunPoliticsTick()
        {
            var camp = StrategySession.Campaign;
            if (camp == null || camp.states == null) return;

            for (int i = 0; i < camp.states.Count; i++)
            {
                FactionState s = camp.states[i];
                if (s == null) continue;
                if (!ElectoralSystemRules.IsElectoral(s.governmentForm)) continue; // 民主政治のみ（寡頭/君主/独裁は選挙なし）

                if (s.politics == null || s.politics.parties.Count == 0) SeedDemoParties(s);

                var r = PoliticsTickRules.TickYear(s, campaignYear);

                if (r.lowerHouseElection)
                {
                    Party ruling = PartyRules.RulingParty(s.politics.parties);
                    string rn = ruling != null ? ruling.partyName : "—";
                    NotificationCenter.Push(NotificationCategory.政治, NotificationSeverity.情報,
                        $"{s.faction} 下院総選挙（衆議院相当・任期4年）＝第一党 {rn}");
                }
                if (r.upperHouseElection)
                    NotificationCenter.Push(NotificationCategory.政治, NotificationSeverity.情報,
                        $"{s.faction} 上院通常選挙（参議院相当・半数改選）");
                if (r.dividedCrisisOnset)
                    NotificationCenter.Push(NotificationCategory.政治, NotificationSeverity.警告,
                        $"{s.faction} 二大政党化で社会の分断が深刻化（有効政党数 {r.effectiveParties:0.0}）");
            }
        }

        /// <summary>デモ用の政党シード：多党乱立から出発させる（成熟が上がると二大政党へ収束する＝#159）。</summary>
        private void SeedDemoParties(FactionState s)
        {
            if (s == null) return;
            if (s.politics == null) s.politics = new PoliticsState();
            if (s.politics.parties.Count > 0) return;

            string[] names = s.faction == Faction.帝国
                ? new[] { "立憲党", "自由党", "国民党", "革新党" }
                : new[] { "民政党", "進歩党", "中道党", "急進党" };
            int baseId = (int)s.faction * 100;
            float share = 1f / names.Length;
            for (int i = 0; i < names.Length; i++)
            {
                Party p = PartyOrganizationRules.Create(baseId + i + 1, names[i], s.faction, founderId: -1);
                p.support = share;
                s.politics.parties.Add(p);
            }
        }

        // --- 外交（DIPLO・#2119 配線） ---
        /// <summary>勢力ペアの外交を年次で回す＝関係ドリフト→AIが宣戦/講和/同盟を決定し通知。</summary>
        private void RunDiplomacyTick()
        {
            if (map == null) return;
            // セッション初期化＋FactionRelations.ActiveDiplomacy 配線（冪等）。
            var names = new System.Collections.Generic.List<string>();
            for (int f = 0; f < DemoFactions.Length; f++) names.Add(DemoFactions[f].ToString());
            var state = DiplomacySession.Ensure(names);

            var dp = DiplomacyRules.DiplomacyParams.Default;
            var ai = DiplomacyAiRules.DiploAiParams.Default;
            var wp = WarGoalRules.WarGoalParams.Default;
            // プレイヤー勢力の外交はプレイヤーが操作する（AIに乗っ取らせない・#2119 操作化）。
            Faction player = GameSettings.Instance != null ? GameSettings.Instance.playerFaction : Faction.同盟;

            for (int i = 0; i < DemoFactions.Length; i++)
                for (int j = i + 1; j < DemoFactions.Length; j++)
                {
                    Faction fa = DemoFactions[i], fb = DemoFactions[j];
                    if (fa == player || fb == player) continue; // プレイヤー絡みのペアはAI判断しない
                    string a = fa.ToString(), b = fb.ToString();
                    // 国力＝所有惑星の人口合計、思想親和＝デモは異勢力で険悪、国境接触ありとみなす。
                    float strA = FactionPopulation(fa), strB = FactionPopulation(fb);
                    var factors = new DiplomacyRules.OpinionFactors(-0.5f, 0.2f, true, 0f, false);
                    var ev = DiplomacyTickRules.TickPair(state, a, b, factors, strA, strB, campaignYear, dp, ai, wp);
                    switch (ev)
                    {
                        case DiplomacyEvent.宣戦布告:
                            NotificationCenter.Push(NotificationCategory.外交, NotificationSeverity.警告, $"{a} が {b} に宣戦布告");
                            break;
                        case DiplomacyEvent.講和:
                            NotificationCenter.Push(NotificationCategory.外交, NotificationSeverity.情報, $"{a} と {b} が講和");
                            break;
                        case DiplomacyEvent.同盟締結:
                            NotificationCenter.Push(NotificationCategory.外交, NotificationSeverity.情報, $"{a} と {b} が同盟締結");
                            break;
                    }
                }

            // 失効した条約を整理（status系は平時へ）。
            TreatyManagementRules.ExpireDue(state, campaignYear);
        }

        /// <summary>
        /// プレイヤー勢力の外交コマンドを発令（UI/キーから呼ぶ・#2119 操作化の入口）。
        /// 検証/適用は <see cref="DiplomacyCommandRules"/> へ委譲。成功で外交カテゴリへ通知し true。
        /// </summary>
        public bool IssuePlayerDiplomacy(Faction target, DiplomaticAction action)
        {
            var state = DiplomacySession.State;
            if (state == null) return false;
            Faction player = GameSettings.Instance != null ? GameSettings.Instance.playerFaction : Faction.同盟;
            if (target == player) return false;
            string a = player.ToString(), b = target.ToString();
            bool ok = DiplomacyCommandRules.Issue(state, a, b, action, DiplomacyRules.DiplomacyParams.Default);
            if (ok)
                NotificationCenter.Push(NotificationCategory.外交, NotificationSeverity.情報, $"{a} → {b}：{action} を発令");
            return ok;
        }

        /// <summary>勢力の国力 proxy＝所有星系の人口合計。</summary>
        private float FactionPopulation(Faction faction)
        {
            if (map == null || provinces == null) return 0f;
            float pop = 0f;
            foreach (var s in map.systems)
                if (s != null && s.owner == faction && provinces.TryGetValue(s.id, out var prov) && prov != null)
                    pop += prov.population;
            return pop;
        }

        // --- 法の支配と法と秩序（LAW・#2126 配線） ---
        /// <summary>勢力の法の支配（デモ法体系）＋惑星の治安（犯罪→秩序）を年次で解き、安定へ反映・抑圧を通知。</summary>
        private void RunLawTick()
        {
            if (map == null || provinces == null) return;
            var cp = CrimeRules.CrimeParams.Default;
            for (int f = 0; f < DemoFactions.Length; f++)
            {
                Faction fac = DemoFactions[f];
                // デモ法体系：同盟＝法の支配（権力も法に従う）／帝国＝法治どまり（権力制約が低い）。
                LegalSystem legal = fac == Faction.同盟
                    ? new LegalSystem(0.7f, 0.7f, 0.7f, 0.7f)
                    : new LegalSystem(0.7f, 0.4f, 0.25f, 0.6f);
                float rol = RuleOfLawRules.RuleOfLawIndex(legal);
                const float enforcement = 0.6f; // デモ警察力
                int repressed = 0;
                foreach (var s in map.systems)
                {
                    if (s == null || s.owner != fac) continue;
                    if (!provinces.TryGetValue(s.id, out var prov) || prov == null) continue;
                    float unemployment = UnityEngine.Mathf.Clamp01(OccupationRules.UnemploymentPressure(prov));
                    float poverty = UnityEngine.Mathf.Clamp01(1f - prov.livingStandard);
                    var r = LawTickRules.TickProvince(rol, unemployment, poverty, 0.3f, enforcement, cp);
                    // 秩序で安定度を緩やかに補正（GovernanceRules 収束と競合させない）。
                    prov.stability = UnityEngine.Mathf.Clamp(prov.stability + r.stabilityDelta * 0.1f, 0f, 100f);
                    if (r.repression > 0.4f) repressed++;
                }
                if (RuleOfLawRules.IsRuleByLawOnly(legal) && repressed > 0)
                    NotificationCenter.Push(NotificationCategory.政治, NotificationSeverity.注意,
                        $"{fac} 法治体制で取締りが抑圧化（{repressed} 星系）＝正統性を蝕む");
            }
        }

        /// <summary>対立勢力（プレイヤー以外の最初のデモ勢力）へ外交コマンドを発令。発令不可なら通知。</summary>
        private void IssueDiplomacyToRival(DiplomaticAction action)
        {
            Faction player = GameSettings.Instance != null ? GameSettings.Instance.playerFaction : Faction.同盟;
            Faction rival = player;
            for (int i = 0; i < DemoFactions.Length; i++)
                if (DemoFactions[i] != player) { rival = DemoFactions[i]; break; }
            if (rival == player) return; // 対立勢力なし
            if (!IssuePlayerDiplomacy(rival, action))
                NotificationCenter.Push(NotificationCategory.外交, NotificationSeverity.情報, $"{action} は今は発令できません（{rival} との現状態）");
        }

        /// <summary>
        /// ミッションコマンド（任務戦術）：マウス直下の敵対星系へ「攻略せよ」と任務を下す。
        /// 参謀本部（自勢力の最有能指揮官の文才）が必要兵力を見積もり、遊休艦隊から必要十分を自動動員して進軍させる。
        /// 必要規模は参謀本部の実力で可変＝有能なら無駄なく軍団/軍集団を、無能なら過小動員のまま発動する。
        /// </summary>
        private void IssueMissionAtMouse()
        {
            if (cam == null || map == null || reg == null) return;
            Vector2 w = WorldMouse();
            int sysId = NearestSystemDist(w, out float d);
            if (sysId < 0 || d > 1.2f) return;
            ExecuteMission(map.GetSystem(sysId));
        }

        /// <summary>
        /// ミッションコマンド（任務戦術）：「◯◯勢力を攻略せよ」＝対立勢力を相手に攻撃目標を参謀本部が選定し任務を下す。
        /// 避実撃虚で到達可能な最も攻めやすい敵星系を選び（兵力を分散させず一点に集中）、`ExecuteMission` で自動動員・進軍させる。
        /// </summary>
        private void IssueCampaignAgainstRival()
        {
            if (map == null || reg == null) return;
            Faction player = GameSettings.Instance != null ? GameSettings.Instance.playerFaction : Faction.同盟;
            Faction rival = player;
            for (int i = 0; i < DemoFactions.Length; i++)
                if (DemoFactions[i] != player) { rival = DemoFactions[i]; break; }
            if (rival == player) return; // 対立勢力なし

            // 敵勢力の星系を攻撃目標候補に。守備兵力＝在席敵対艦隊／到達可否＝自勢力星系から経路あり。
            var targets = new List<CampaignTarget>();
            for (int i = 0; i < map.systems.Count; i++)
            {
                StarSystem s = map.systems[i];
                if (s == null) continue;
                if (!FactionRelations.IsHostile(null, player, s.ownerData, s.owner)) continue; // 敵対星系のみ
                float garrison = 0f;
                var here = reg.FleetsAt(s.id);
                if (here != null)
                    for (int k = 0; k < here.Count; k++)
                        if (here[k] != null && FactionRelations.IsHostile(null, player, null, here[k].faction)) garrison += here[k].strength;
                bool defended = s.planet != null && !s.planet.Captured;
                targets.Add(new CampaignTarget(s.id, garrison, defended, ReachableByFaction(player, s.id)));
            }

            int targetId = MissionCommandRules.SelectCampaignTarget(targets);
            if (targetId < 0)
            {
                NotificationCenter.Push(NotificationCategory.占領, NotificationSeverity.情報,
                    $"{rival} 攻略：到達可能な攻撃目標がありません");
                return;
            }
            ExecuteMission(map.GetSystem(targetId));
        }

        /// <summary>その勢力のいずれかの所有星系から目標星系へ回廊経路で到達可能か（避実撃虚の到達可否）。</summary>
        private bool ReachableByFaction(Faction faction, int goalId)
        {
            for (int i = 0; i < map.systems.Count; i++)
            {
                StarSystem s = map.systems[i];
                if (s == null || s.owner != faction) continue;
                if (s.id == goalId) return true;
                if (GalaxyPathfinder.FindPath(map, s.id, goalId).Count > 0) return true;
            }
            return false;
        }

        /// <summary>
        /// 任務の実行：参謀本部が必要兵力を見積もり、遊休艦隊から必要十分を自動動員して進軍させる。
        /// 必要規模は参謀本部の実力で可変。<b>戦力の集中が満たせなければ逐次投入せず「集中待機」する（孫子）</b>。
        /// </summary>
        private void ExecuteMission(StarSystem s)
        {
            if (s == null) return;
            Faction player = GameSettings.Instance != null ? GameSettings.Instance.playerFaction : Faction.同盟;

            // 自国/友軍星系には攻略任務を出さない（敵対星系のみ）。
            if (!FactionRelations.IsHostile(null, player, s.ownerData, s.owner))
            {
                NotificationCenter.Push(NotificationCategory.占領, NotificationSeverity.情報,
                    $"{s.systemName} は攻略対象外（自国/友軍）");
                return;
            }

            // 敵戦力＝目標星系に在席する敵対艦隊の合計。防衛惑星があれば攻者三倍の対象（defended）。
            float enemyStrength = 0f;
            var here = reg.FleetsAt(s.id);
            if (here != null)
                for (int i = 0; i < here.Count; i++)
                {
                    StrategicFleet g = here[i];
                    if (g != null && FactionRelations.IsHostile(null, player, null, g.faction)) enemyStrength += g.strength;
                }
            bool defended = s.planet != null && !s.planet.Captured;

            // 参謀本部の実力（0..1）＝自勢力の最有能指揮官の文才（運営/情報）。
            float staff = StaffCompetence(player);

            // 動員候補＝自勢力の遊休（停泊中・非交戦）艦隊。
            var avail = new List<MissionForce>();
            for (int i = 0; i < reg.fleets.Count; i++)
            {
                StrategicFleet f = reg.fleets[i];
                if (f == null || f.faction != player) continue;
                if (f.IsOnCorridor || f.engaged) continue;          // 移動中/交戦中は動員しない
                if (f.currentSystemId == s.id) continue;             // 既に目標星系に居る艦は除く
                avail.Add(new MissionForce(f.id, f.strength));
            }

            MissionPlan plan = MissionCommandRules.PlanMission(
                s.id, MissionType.星系攻略, player, enemyStrength, defended, staff, avail);

            if (plan.fleetIds.Count == 0)
            {
                NotificationCenter.Push(NotificationCategory.占領, NotificationSeverity.注意,
                    $"{s.systemName} 攻略任務：動員可能な遊休艦隊がありません");
                return;
            }

            // 兵力の集中（孫子＝戦力の逐次投入をしない）：集中が満たせない有能な参謀本部は発動せず待機する。
            if (!plan.launched)
            {
                NotificationCenter.Push(NotificationCategory.占領, NotificationSeverity.注意,
                    $"任務：{s.systemName} 攻略は戦力集中まで待機（逐次投入を避ける）。動員可能{plan.committedStrength:0}/必要{plan.requiredStrength:0}");
                return;
            }

            // 動員した艦隊を目標星系へ進軍させる（どう動くかは各艦の経路探索に委ねる＝任務戦術）。
            for (int i = 0; i < plan.fleetIds.Count; i++)
            {
                StrategicFleet f = reg.GetFleet(plan.fleetIds[i]);
                if (f != null) f.WarpTo(map, s.id);
            }

            string scale = plan.echelon.ToString();
            string note = plan.piecemeal ? "（逐次投入＝兵力不足のまま発動）" : "";
            NotificationCenter.Push(NotificationCategory.占領,
                plan.piecemeal ? NotificationSeverity.注意 : NotificationSeverity.情報,
                $"任務：{s.systemName} 攻略。{scale}を集中動員（{plan.fleetIds.Count}隊・兵力{plan.committedStrength:0}/{plan.requiredStrength:0}）{note}");
        }

        /// <summary>参謀本部の実力（0..1）＝その勢力の最有能指揮官の文才（運営/情報の平均）を正規化。指揮官不在は中庸0.5。</summary>
        private float StaffCompetence(Faction faction)
        {
            if (commanders == null) return 0.5f;
            float best = -1f;
            for (int i = 0; i < commanders.Count; i++)
            {
                Person c = commanders[i];
                if (c == null || c.faction != faction || c.IsDeceased) continue;
                if (c.CivilAptitude > best) best = c.CivilAptitude;
            }
            return best < 0f ? 0.5f : Mathf.Clamp01(best / 100f);
        }

    }
}
