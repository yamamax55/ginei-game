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
                UpheavalResult up = PoliticalUpheavalRules.ResolveUpheaval(s.governmentForm, ctx, DetRoll(campaignYear, NextRollSeed()));
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
        /// 政党政治と選挙の年次 Tick（#159 配線）：民主政治の勢力ごとに、成熟度に応じて政党制を二大政党へ収束させ、
        /// 衆参の日程どおりに国政選挙を開票して議席を確定し、下院選挙の後に組閣（首相＝宰相職へ就任）し、
        /// 星系知事選を行って知事職へ就ける。非民主へ移った勢力は選挙を止め、選出された首相/知事の権限を外す（任命制へ戻す）。
        /// 数値は <see cref="PoliticsTickRules"/>／<see cref="ElectionCycleRules"/>／<see cref="LocalElectionRules"/> へ委譲。
        /// 年は統一クロックの暦（<see cref="ElectionYear"/>）＝同じ年に二度呼ばれても議席・役職・通知は二重に動かない。
        /// </summary>
        private void RunPoliticsTick()
        {
            var camp = StrategySession.Campaign;
            if (camp == null || camp.states == null) return;
            int year = ElectionYear();
            List<Person> roster = ElectionRoster();

            for (int i = 0; i < camp.states.Count; i++)
            {
                FactionState s = camp.states[i];
                if (s == null) continue;
                if (!ElectoralSystemRules.IsElectoral(s.governmentForm))
                {
                    SuspendElections(s, year); // 寡頭/君主/独裁は選挙なし（以前に選挙をしていた勢力だけ止める）
                    continue;
                }

                if (s.politics == null || s.politics.parties.Count == 0) SeedDemoParties(s);

                var r = PoliticsTickRules.TickYear(s, year);

                // 国政：開票→確定議席→組閣→宰相職（首相）へ反映→党別議席へ実在の議員を充てる（下院→上院）
                List<RegionalElectorate> electorate = ElectorateOf(s.faction);
                NationalYearOutcome national = ElectionCycleRules.RunNationalYear(
                    s, year, r, electorate, roster, NationalElectionParams);
                NotifyNational(s, national);
                ApplyElectedPremier(s);
                AssignLegislators(s, national.lowerRecord, roster, electorate);
                AssignLegislators(s, national.upperRecord, roster, electorate);

                // 地方：所有星系と台帳を突き合わせ（占領/編入）→期日の知事選→知事職へ反映
                List<LocalConstituency> owned = ConstituenciesOf(s.faction);
                List<LocalElectionEvent> localEvents = LocalElectionRules.Reconcile(s.politics, owned, year, GovernorElectionParams);
                localEvents.AddRange(LocalElectionRules.RunDue(s.politics, s.faction, year, owned, roster, GovernorElectionParams));
                ApplyLocalElectionEvents(s, localEvents);
                SyncElectedGovernors(s);
                ReconcileLegislators(s, roster, year, true); // 知事に就いた人・欠缺の議席を集計へ戻す

                if (r.dividedCrisisOnset)
                    NotificationCenter.Push(NotificationCategory.政治, NotificationSeverity.警告,
                        $"{s.faction} 二大政党化で社会の分断が深刻化（有効政党数 {r.effectiveParties:0.0}）");
            }
        }

        // ===== 選挙の配線（国政・地方） =====

        private static readonly ElectionCycleParams NationalElectionParams = ElectionCycleParams.Default;
        private static readonly LocalElectionParams GovernorElectionParams = LocalElectionParams.Default;
        private static readonly LegislatorRosterParams LegislatorParams = LegislatorRosterParams.Default;

        /// <summary>開票結果を議員名簿へ反映し、実在議員と集計議席の内訳を1通だけ通知する（同じ選挙IDは反映済みなら何もしない）。</summary>
        private void AssignLegislators(FactionState s, NationalElectionRecord rec, List<Person> roster, List<RegionalElectorate> electorate)
        {
            if (s == null || s.politics == null || rec == null) return;
            LegislatorAssignment a = LegislatorRosterRules.AssignElection(s.politics, s.faction, rec, roster, electorate, LegislatorParams);
            if (a.alreadyAssigned) return;
            NotificationCenter.Push(NotificationCategory.政治, NotificationSeverity.情報,
                $"{s.faction} {rec.chamber}の当選議員（SE{rec.year}）：人物 {a.named}名（新 {a.newlyElected}・再 {a.reelected}）・集計議席 {a.aggregate}" +
                (a.defeated > 0 ? $"・議席を失った現職 {a.defeated}名" : ""));
        }

        /// <summary>議員資格を現況へ合わせる（当選回数は変えない）。<paramref name="notify"/> が false なら通知しない（読込時）。</summary>
        private void ReconcileLegislators(FactionState s, List<Person> roster, int year, bool notify)
        {
            if (s == null || s.politics == null) return;
            List<LegislatorVacancy> vacated = LegislatorRosterRules.Reconcile(s.politics, s.faction, roster, year);
            if (!notify) return;
            for (int i = 0; i < vacated.Count; i++)
                NotificationCenter.Push(NotificationCategory.人事, NotificationSeverity.注意,
                    $"{s.faction} {vacated[i].chamber}議員 {ElectionPersonName(vacated[i].personId)} 失職（{vacated[i].reason}）");
        }

        /// <summary>
        /// 選挙の暦年＝統一クロックの宇宙暦（画面の日付と同じ）。<c>campaignYear</c> はシーンを組み直すと開始年へ戻るため、
        /// 保存した選挙日程とずれないようクロックから求める。
        /// </summary>
        private int ElectionYear()
        {
            GameClock clock = StrategySession.Clock;
            GameDate.DateParams dp = policyCalendar != null ? policyCalendar.Params : GameDate.DateParams.Default;
            return GameDate.FromSeconds(clock != null ? clock.ElapsedSeconds : 0d, TimeDisplay.StartYear, dp).year;
        }

        /// <summary>選挙の資格判定に使う全人物（軍人＋文民）。</summary>
        private List<Person> ElectionRoster()
        {
            var list = new List<Person>();
            if (commanders != null) list.AddRange(commanders);
            if (civilians != null) list.AddRange(civilians);
            return list;
        }

        /// <summary>その勢力が選挙で首相を選んでいるか（民主政で両院が構成済み）＝年次の宰相銓衡を行わない。</summary>
        private bool UsesElectedPremier(Faction f)
        {
            FactionState s = StateOf(f);
            return s != null && ElectoralSystemRules.IsElectoral(s.governmentForm) && ElectionCycleRules.IsSeated(s.politics);
        }

        /// <summary>その勢力が選挙で知事を選んでいるか（民主政で知事選の日程を組み済み）＝年次の総督銓衡を行わない。</summary>
        private bool UsesElectedGovernors(Faction f)
        {
            FactionState s = StateOf(f);
            return s != null && ElectoralSystemRules.IsElectoral(s.governmentForm) && s.politics != null && s.politics.localsSeeded;
        }

        /// <summary>国政選挙の地域票（所有星系の人口と安定度）。</summary>
        private List<RegionalElectorate> ElectorateOf(Faction f)
        {
            var list = new List<RegionalElectorate>();
            if (map == null || provinces == null) return list;
            for (int i = 0; i < map.systems.Count; i++)
            {
                StarSystem s = map.systems[i];
                if (s == null || s.owner != f || !provinces.TryGetValue(s.id, out Province prov) || prov == null) continue;
                list.Add(new RegionalElectorate(s.id, prov.population, prov.stability / 100f));
            }
            return list;
        }

        /// <summary>知事選の選挙区＝所有星系（内政データのある星系。人口・安定度・土着思想）。</summary>
        private List<LocalConstituency> ConstituenciesOf(Faction f)
        {
            var list = new List<LocalConstituency>();
            if (map == null || provinces == null) return list;
            for (int i = 0; i < map.systems.Count; i++)
            {
                StarSystem s = map.systems[i];
                if (s == null || s.owner != f || !provinces.TryGetValue(s.id, out Province prov) || prov == null) continue;
                list.Add(new LocalConstituency(s.id, prov.population, prov.stability / 100f, prov.nativeIdeology));
            }
            return list;
        }

        private string ElectionSystemName(int systemId)
        {
            StarSystem s = map != null ? map.GetSystem(systemId) : null;
            return s != null ? s.systemName : "星系#" + systemId;
        }

        private string ElectionPersonName(int personId)
        {
            Person p = FindPersonById(personId);
            return p != null ? p.name : "人物#" + personId;
        }

        private static string ElectionPartyName(PoliticsState pol, int partyId)
        {
            Party p = pol != null ? ElectionCycleRules.FindParty(pol.parties, partyId) : null;
            return p != null ? p.partyName : "無所属";
        }

        /// <summary>国政選挙と組閣の通知（開票・組閣があった年だけ＝同年の再処理では出ない）。</summary>
        private void NotifyNational(FactionState s, NationalYearOutcome o)
        {
            PoliticsState pol = s.politics;
            if (pol == null) return;
            if (o.lowerRecord != null)
                NotificationCenter.Push(NotificationCategory.政治, NotificationSeverity.情報,
                    $"{s.faction} {(o.inaugural ? "初の下院選挙" : "下院総選挙")}（SE{o.lowerRecord.year}・{o.lowerRecord.seatsUp}議席）＝{TopPartyText(o.lowerRecord)}");
            if (o.upperRecord != null)
                NotificationCenter.Push(NotificationCategory.政治, NotificationSeverity.情報,
                    $"{s.faction} {(o.inaugural ? "初の上院選挙（全議席）" : "上院通常選挙（半数改選）")}（SE{o.upperRecord.year}・{o.upperRecord.seatsUp}議席）＝{TopPartyText(o.upperRecord)}");

            GovernmentFormation g = pol.government;
            if (g == null || !(o.governmentFormed || o.governmentChanged)) return;
            if (g.premierPersonId >= 0)
                NotificationCenter.Push(NotificationCategory.政治, NotificationSeverity.情報,
                    $"{s.faction} 首相に {ElectionPersonName(g.premierPersonId)}（{ElectionPartyName(pol, g.partyId)} {g.partySeats}/{g.totalSeats}議席・{g.status}）");
            else
                NotificationCenter.Push(NotificationCategory.政治, NotificationSeverity.警告,
                    $"{s.faction} 組閣未成立：{g.reason}");
        }

        private static string TopPartyText(NationalElectionRecord rec)
        {
            PartyVoteResult top = null;
            for (int i = 0; i < rec.results.Count; i++)
            {
                PartyVoteResult r = rec.results[i];
                if (r == null) continue;
                if (top == null || r.seatsAfter > top.seatsAfter || (r.seatsAfter == top.seatsAfter && r.partyId < top.partyId)) top = r;
            }
            return top == null ? "議席配分なし"
                : $"第一党 {top.partyName} {top.seatsAfter}/{rec.totalSeats}議席（今回 {top.seatsWon}・得票 {top.voteShare * 100f:0}%）";
        }

        /// <summary>
        /// 選出された首相を宰相職（<see cref="GovernmentRegistry"/> の既存の文官要職）へ反映する。首相が空席なら職も空ける。
        /// 政治任用なので官位（位階）のゲートは課さない。軍団・艦隊の指揮権は与えない（内政所掌の役職のみ）。
        /// </summary>
        private void ApplyElectedPremier(FactionState s)
        {
            if (s == null || !ElectionCycleRules.IsSeated(s.politics)) return;
            Office office = PremierOfficeOf(s.faction);
            if (office == null) return;
            GovernmentFormation g = s.politics.government;
            Person premier = g != null && g.premierPersonId >= 0 ? FindPersonById(g.premierPersonId) : null;

            ICharacter holder = GovernmentRegistry.GetHolder(office);
            if (holder != null && (premier == null || holder.Id != premier.id))
                GovernmentRegistry.Dismiss(office, holder); // 任命の宰相・前首相を外す
            if (premier != null && GovernmentRegistry.GetHolder(office) == null
                && !GovernmentRegistry.TryAppoint(s.faction, office, premier))
            {
                g.reason = $"首相 {premier.name} を{office.officeName}に就けられなかった（役職の資格を満たさない）";
                g.premierPersonId = -1;
                g.status = CabinetStatus.組閣未成立;
            }
        }

        /// <summary>知事選の出来事を知事職へ反映して通知する（失職は権限を外す。不成立は勢力ごとに1通へまとめる）。</summary>
        private void ApplyLocalElectionEvents(FactionState s, List<LocalElectionEvent> events)
        {
            if (s == null || events == null || events.Count == 0) return;
            Office office = GovernorOfficeOf(s.faction);
            int failed = 0, retryYear = 0;
            string failReason = "";
            for (int i = 0; i < events.Count; i++)
            {
                LocalElectionEvent e = events[i];
                string sys = ElectionSystemName(e.systemId);
                switch (e.kind)
                {
                    case LocalElectionEventKind.失職:
                        DismissHolderById(office, e.systemId, e.previousPersonId);
                        NotificationCenter.Push(NotificationCategory.人事, NotificationSeverity.注意,
                            $"{s.faction} {sys}知事 {ElectionPersonName(e.previousPersonId)} 失職（{e.reason}）");
                        break;
                    case LocalElectionEventKind.当選:
                    case LocalElectionEventKind.再選:
                        NotificationCenter.Push(NotificationCategory.政治, NotificationSeverity.情報,
                            $"{s.faction} {sys}知事選：{ElectionPersonName(e.personId)}（{ElectionPartyName(s.politics, e.partyId)}）が{e.kind}（任期〜SE{e.nextElectionYear}）");
                        break;
                    case LocalElectionEventKind.不成立:
                        failed++;
                        retryYear = e.nextElectionYear;
                        if (failReason.Length == 0) failReason = e.reason;
                        break;
                }
            }
            if (failed > 0)
                NotificationCenter.Push(NotificationCategory.政治, NotificationSeverity.注意,
                    $"{s.faction} 知事選 不成立 {failed}星系（{failReason}）→ 再実施 SE{retryYear}");
        }

        /// <summary>知事選の台帳どおりに知事職（星系スコープ）の在任者を合わせる（当選者を就け、それ以外の在任者を外す）。</summary>
        private void SyncElectedGovernors(FactionState s)
        {
            if (s == null || s.politics == null || s.politics.locals == null) return;
            Office office = GovernorOfficeOf(s.faction);
            if (office == null) return;
            int premierId = s.politics.government != null ? s.politics.government.premierPersonId : -1;
            for (int i = 0; i < s.politics.locals.Count; i++)
            {
                LocalElectionState rec = s.politics.locals[i];
                if (rec == null) continue;
                // 兼任の拒否（首相と知事・複数星系の知事）：保存データの食い違いや選挙の無い年の再組閣で生じても就けない。
                string conflict = GovernorConflictReason(s.politics, rec, premierId);
                if (conflict != null)
                {
                    int previous = rec.governorPersonId;
                    VacateGovernorRecord(rec, conflict);
                    NotificationCenter.Push(NotificationCategory.人事, NotificationSeverity.注意,
                        $"{s.faction} {ElectionSystemName(rec.systemId)}知事 {ElectionPersonName(previous)} 失職（{conflict}）");
                }
                Person gov = rec.governorPersonId >= 0 ? FindPersonById(rec.governorPersonId) : null;
                ICharacter holder = GovernmentRegistry.GetHolder(office, rec.systemId);
                if (holder != null && (gov == null || holder.Id != gov.id))
                    GovernmentRegistry.Dismiss(office, holder, rec.systemId); // 任命の総督・前知事を外す
                if (gov != null && GovernmentRegistry.GetHolder(office, rec.systemId) == null
                    && !GovernmentRegistry.TryAppoint(s.faction, office, gov, rec.systemId))
                {
                    rec.reason = $"当選者 {gov.name} を知事職に就けられなかった（役職の資格を満たさない）";
                    rec.governorPersonId = -1;
                    rec.governorPartyId = -1;
                    rec.status = LocalElectionStatus.失職;
                }
            }
        }

        /// <summary>
        /// 非民主へ移った勢力の選挙を止める：政府を対象外にして選出首相を宰相職から外し、選出知事を失職させる
        /// （以後は既存の任命/銓衡の経路が埋める）。一度も選挙をしていない勢力では何もしない。
        /// </summary>
        private void SuspendElections(FactionState s, int year)
        {
            if (s == null || s.politics == null) return;
            string reason = $"政体が{s.governmentForm}へ移行＝選挙なし（首相・知事は任命制）";
            if (ElectionCycleRules.SuspendNational(s.politics, year, reason, out int previousPremier))
            {
                DismissHolderById(PremierOfficeOf(s.faction), 0, previousPremier);
                NotificationCenter.Push(NotificationCategory.政治, NotificationSeverity.警告, $"{s.faction} 国政選挙を停止：{reason}");
            }
            ApplyLocalElectionEvents(s, LocalElectionRules.Suspend(s.politics, reason));
            LegislatorRosterRules.SuspendAll(s.politics, reason); // 議員資格を外す（当選履歴は残す）
        }

        /// <summary>
        /// 読込・シーン再構築の直後に、保存された選挙結果（首相・知事）を政府役職へ戻す（選挙はしない）。
        /// 人物が名簿に居ない・死亡・拘束・離反なら空席にして理由を残す（次の年次で再組閣／補欠選挙）。
        /// </summary>
        private void RestoreElectedOffices()
        {
            var camp = StrategySession.Campaign;
            if (camp == null || camp.states == null) return;
            int year = ElectionYear();
            for (int i = 0; i < camp.states.Count; i++)
            {
                FactionState s = camp.states[i];
                if (s == null || s.politics == null || !ElectoralSystemRules.IsElectoral(s.governmentForm)) continue;
                PoliticsState pol = s.politics;

                GovernmentFormation g = pol.government;
                if (g != null && g.premierPersonId >= 0
                    && !ElectionCycleRules.IsEligiblePolitician(FindPersonById(g.premierPersonId), s.faction))
                {
                    g.reason = $"首相（人物#{g.premierPersonId}）が不在・死亡などで職務を続けられないため空席（次の年次で再組閣）";
                    g.premierPersonId = -1;
                    g.status = CabinetStatus.組閣未成立;
                }
                ApplyElectedPremier(s);

                VacateUnavailableGovernors(s, year); // 読込時は通知しない（空席と理由は台帳に残る）
                SyncElectedGovernors(s);
                ReconcileLegislators(s, ElectionRoster(), year, false); // 議員資格だけ整える（当選回数は数えない）
            }
        }

        /// <summary>
        /// 死亡・拘束・離反・不在の知事を空席にして理由と補欠選挙の年を残す（選挙はしない）。空席にした星系ぶんの失職の出来事を返す。
        /// </summary>
        private List<LocalElectionEvent> VacateUnavailableGovernors(FactionState s, int year)
        {
            var events = new List<LocalElectionEvent>();
            if (s == null || s.politics == null || s.politics.locals == null) return events;
            for (int k = 0; k < s.politics.locals.Count; k++)
            {
                LocalElectionState rec = s.politics.locals[k];
                if (rec == null || rec.governorPersonId < 0) continue;
                if (ElectionCycleRules.IsEligiblePolitician(FindPersonById(rec.governorPersonId), s.faction)) continue;
                int previous = rec.governorPersonId;
                int by = year + GovernorElectionParams.retryYears;
                string reason = $"知事（人物#{previous}）が不在・死亡などのため空席（補欠選挙 SE{by}）";
                VacateGovernorRecord(rec, reason);
                events.Add(new LocalElectionEvent
                {
                    systemId = rec.systemId, kind = LocalElectionEventKind.失職,
                    personId = -1, previousPersonId = previous, partyId = -1,
                    nextElectionYear = rec.nextElectionYear, reason = reason,
                });
            }
            return events;
        }

        /// <summary>知事選の台帳の知事を空席にする（失職・理由・補欠選挙の年＝次の知事選より遅らせない）。</summary>
        private void VacateGovernorRecord(LocalElectionState rec, string reason)
        {
            if (rec == null) return;
            int by = ElectionYear() + GovernorElectionParams.retryYears;
            rec.reason = reason ?? "";
            rec.governorPersonId = -1;
            rec.governorPartyId = -1;
            rec.termEndYear = 0;
            rec.status = LocalElectionStatus.失職;
            rec.nextElectionYear = rec.nextElectionYear > 0 ? Mathf.Min(rec.nextElectionYear, by) : by;
        }

        /// <summary>
        /// 知事が兼任になっていれば理由を返す（兼任でなければ null）：首相本人／より小さい星系IDの知事を既に務めている。
        /// 同じ人物が複数星系に載っていれば星系ID最小の1つだけを残す（決定論）。
        /// </summary>
        private string GovernorConflictReason(PoliticsState pol, LocalElectionState rec, int premierId)
        {
            if (pol == null || rec == null || rec.governorPersonId < 0) return null;
            int by = ElectionYear() + GovernorElectionParams.retryYears;
            if (rec.governorPersonId == premierId)
                return $"知事（人物#{rec.governorPersonId}）は首相と兼任できないため空席（補欠選挙 SE{by}）";
            int other = LocalElectionRules.GovernedSystemOf(pol, rec.governorPersonId, rec.systemId);
            if (other >= 0 && other < rec.systemId)
                return $"知事（人物#{rec.governorPersonId}）は{ElectionSystemName(other)}の知事と兼任できないため空席（補欠選挙 SE{by}）";
            return null;
        }

        /// <summary>
        /// 年次の総督銓衡の時点で、選挙で選んだ知事の在任を現況へ合わせる（選挙はしない）：手放した星系の知事を失職させ、
        /// 死亡・拘束・離反した知事を空席にし、兼任を外して知事職を台帳どおりにする。
        /// 政治 Tick の後に起きた文民の老衰・離反・占領・再組閣で、権限が次の年まで残らないようにする。
        /// </summary>
        private void RefreshElectedGovernors(Faction f)
        {
            FactionState s = StateOf(f);
            if (s == null || s.politics == null || !UsesElectedGovernors(f)) return;
            int year = ElectionYear();
            ApplyLocalElectionEvents(s, LocalElectionRules.Reconcile(s.politics, ConstituenciesOf(f), year, GovernorElectionParams));
            ApplyLocalElectionEvents(s, VacateUnavailableGovernors(s, year));
            SyncElectedGovernors(s);
            ReconcileLegislators(s, ElectionRoster(), year, true); // 政治 Tick 後の死去・離反・知事就任で議席を残さない
        }

        /// <summary>選挙の無い年に首相が欠けたら現議席で組み直して宰相職へ反映する（年次の文官銓衡から呼ぶ）。</summary>
        private void MaintainElectedPremier(Faction f)
        {
            FactionState s = StateOf(f);
            if (s == null || s.politics == null) return;
            if (ElectionCycleRules.MaintainGovernment(s.politics, f, ElectionYear(), ElectionRoster(), out _))
            {
                GovernmentFormation g = s.politics.government;
                if (g != null && g.premierPersonId >= 0)
                    NotificationCenter.Push(NotificationCategory.政治, NotificationSeverity.注意,
                        $"{f} 首相交代：{ElectionPersonName(g.premierPersonId)}（{ElectionPartyName(s.politics, g.partyId)}・{g.status}）");
                else if (g != null)
                    NotificationCenter.Push(NotificationCategory.政治, NotificationSeverity.警告, $"{f} 首相空席：{g.reason}");
            }
            ApplyElectedPremier(s);
        }

        /// <summary>役職の在任者がその人物なら外す（別人なら触らない）。</summary>
        private static void DismissHolderById(Office office, int scopeKey, int personId)
        {
            if (office == null || personId < 0) return;
            ICharacter holder = GovernmentRegistry.GetHolder(office, scopeKey);
            if (holder != null && holder.Id == personId) GovernmentRegistry.Dismiss(office, holder, scopeKey);
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
        // 厭戦が関係値の親和へ寄与する重み（厭戦1.0で親和へ最大 +この値）＝民の疲弊が講和を後押しする強さ。
        private const float WarWearinessPeaceWeight = 0.5f;
        // 1年あたり、戦争損害(0..1)に比例して削られる民心(community.hope)の最大量＝戦争の銃後コスト。
        private const float WarHopeDrainRate = 0.02f;

        // 戦争の銃後コスト：損害に比例して民心(community.hope)を bounded に削る＝戦争は民を疲れさせる
        // （その疲弊を HomefrontWeariness が拾い講和を後押しする＝内政⇄外交の閉ループ）。null安全。
        private static void DrainWarHope(FactionState s, float casualties)
        {
            if (s == null || s.community == null) return;
            s.community.hope = Mathf.Clamp01(s.community.hope - Mathf.Clamp01(casualties) * WarHopeDrainRate);
        }

        // 銃後の厭戦（0..1）。交戦中の勢力 f について、民心(community.hope)を銃後の支持・兵糧は中立(1)として
        // WarWearinessModifiersRules で測る（基礎厭戦＝戦争ターン/損害に、民心低下と長期化の修飾を足す）。非交戦・null は 0。
        private float HomefrontWeariness(Faction f, WarState war)
        {
            if (war == null) return 0f;
            FactionState s = StateOf(f);
            float homeSupport = (s != null && s.community != null) ? Mathf.Clamp01(s.community.hope) : 1f;
            return WarWearinessModifiersRules.AdjustedWeariness(war, homeSupport, 1f);
        }

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
                    // 軍産複合体の戦争バイアス（MCN-4 #1389・CAP-3 #204 配線）：どちらかに複合体が成立すると思想親和をさらに険悪化＝開戦を促し講和を遠ざける。
                    float warBias = Mathf.Max(MilitaryIndustrialRules.WarBias(GetMilitaryIndustrialPressure(fa)),
                                              MilitaryIndustrialRules.WarBias(GetMilitaryIndustrialPressure(fb)));
                    WarState preWar = WarLedger.Get(a, b);                  // 講和前の戦況（領土移転の勝者判定＋銃後の厭戦に使う）
                    float preScore = preWar != null ? preWar.warScore : 0f;
                    // 内政⇄外交の配線：交戦中なら双方の「銃後の厭戦」（民心低下＋長期化＋損害＝WarWearinessModifiersRules）を測り、
                    // 厭戦が高いほど関係値の親和へ正の補正を足す＝民が疲れた国ほど講和へ傾く（既存の戦費/賠償とは別系統の追加効果）。
                    float weariness = Mathf.Max(HomefrontWeariness(fa, preWar), HomefrontWeariness(fb, preWar));
                    var factors = new DiplomacyRules.OpinionFactors(-0.5f - warBias * 0.3f + weariness * WarWearinessPeaceWeight, 0.2f, true, 0f, false);
                    var ev = DiplomacyTickRules.TickPair(state, a, b, factors, strA, strB, campaignYear, dp, ai, wp);

                    // 外交アクションAI（P1 配線）：険悪×国力優位なら制裁＝相手の国庫を bounded に削る（効果額は DiplomaticEffectRules 委譲）。
                    float op = state.Opinion(a, b);
                    FactionState saa = StateOf(fa), sbb = StateOf(fb);
                    if (sbb != null && DiplomaticActionAiRules.ShouldSanction(op, strA, strB))
                        sbb.treasury = Mathf.Max(0f, sbb.treasury - Mathf.Min(sbb.treasury * 0.05f, DiplomaticEffectRules.SanctionEconomicHit(CampaignRules.EconomyBase(sbb), 0.2f)));
                    if (saa != null && DiplomaticActionAiRules.ShouldSanction(op, strB, strA))
                        saa.treasury = Mathf.Max(0f, saa.treasury - Mathf.Min(saa.treasury * 0.05f, DiplomaticEffectRules.SanctionEconomicHit(CampaignRules.EconomyBase(saa), 0.2f)));

                    // 諜報の工作（P1 配線）：険悪なら高能力側が相手にサボタージュ＝相手の国庫を bounded に削る（防諜で守る）。
                    if (op < -20f)
                    {
                        IntelState ia = intelStates.TryGetValue(fa, out var iav) ? iav : null;
                        IntelState ib = intelStates.TryGetValue(fb, out var ibv) ? ibv : null;
                        if (ia != null && sbb != null && IntelligenceTickRules.SabotageSuccess(ia.capability, ib != null ? ib.counterIntel : 0f, SabotageRoll(fa, fb)))
                        {
                            sbb.treasury = Mathf.Max(0f, sbb.treasury - Mathf.Min(sbb.treasury * 0.03f, IntelligenceTickRules.SabotageEffect(ia.capability) * CampaignRules.EconomyBase(sbb) * 0.05f));
                            NotificationCenter.Push(NotificationCategory.外交, NotificationSeverity.注意, $"{fb} で {fa} の諜報工作（妨害）が発覚"); // Tier1 通知
                        }
                        if (ib != null && saa != null && IntelligenceTickRules.SabotageSuccess(ib.capability, ia != null ? ia.counterIntel : 0f, SabotageRoll(fb, fa)))
                        {
                            saa.treasury = Mathf.Max(0f, saa.treasury - Mathf.Min(saa.treasury * 0.03f, IntelligenceTickRules.SabotageEffect(ib.capability) * CampaignRules.EconomyBase(saa) * 0.05f));
                            NotificationCenter.Push(NotificationCategory.外交, NotificationSeverity.注意, $"{fa} で {fb} の諜報工作（妨害）が発覚"); // Tier1 通知
                        }
                    }
                    switch (ev)
                    {
                        case DiplomacyEvent.宣戦布告:
                            NotificationCenter.Push(NotificationCategory.外交, NotificationSeverity.警告, $"{a} が {b} に宣戦布告");
                            DeclareAlliedWar(state, fa, fb); // P1：宣戦側の同盟国も標的へ参戦（共同戦）
                            break;
                        case DiplomacyEvent.講和:
                            NotificationCenter.Push(NotificationCategory.外交, NotificationSeverity.情報, $"{a} と {b} が講和");
                            // P1：決定的な戦況での講和は勝者が敗者の国境星系を1つ併合（敗者複数星系のみ＝滅亡させない）。
                            if (Mathf.Abs(preScore) >= 0.3f)
                            {
                                Faction winner = preScore > 0f ? fa : fb, loser = preScore > 0f ? fb : fa;
                                if (TransferBorderSystem(winner, loser))
                                    NotificationCenter.Push(NotificationCategory.占領, NotificationSeverity.情報, $"{winner} が講和で {loser} の星系を割譲させた");
                            }
                            break;
                        case DiplomacyEvent.同盟締結:
                            NotificationCenter.Push(NotificationCategory.外交, NotificationSeverity.情報, $"{a} と {b} が同盟締結");
                            break;
                    }
                }

            // 賠償（P1 配線）：進行中の戦争で戦況の不利な側（敗勢）が有利な側へ年次賠償を払う＝戦争が経済を消耗させる。
            var wars = WarLedger.All;
            if (wars != null)
                for (int w = 0; w < wars.Count; w++)
                {
                    WarState ws = wars[w];
                    if (ws == null) continue;
                    // 戦争の財政コスト（配線ループ#5）：損害が大きいほど双方の国庫が消耗する＝戦争は高くつく（全戦争・bounded）。
                    float warCost = Mathf.Clamp01(ws.casualties) * 0.02f;
                    // 戦費は国庫を、損害は銃後の民心(community.hope)を削る＝戦争は民を疲れさせ、それが上の HomefrontWeariness 経由で講和を促す（内政⇄外交の閉ループ）。
                    if (System.Enum.TryParse(ws.factionA, out Faction caf)) { var sc = StateOf(caf); if (sc != null) { sc.treasury = Mathf.Max(0f, sc.treasury * (1f - warCost)); DrainWarHope(sc, ws.casualties); } }
                    if (System.Enum.TryParse(ws.factionB, out Faction cbf)) { var sc = StateOf(cbf); if (sc != null) { sc.treasury = Mathf.Max(0f, sc.treasury * (1f - warCost)); DrainWarHope(sc, ws.casualties); } }
                    if (Mathf.Abs(ws.warScore) < 0.2f) continue; // 拮抗は賠償なし（戦費は上で課済み）
                    bool aWinning = ws.warScore > 0f;
                    if (!System.Enum.TryParse(aWinning ? ws.factionA : ws.factionB, out Faction wf)) continue;
                    if (!System.Enum.TryParse(aWinning ? ws.factionB : ws.factionA, out Faction lf)) continue;
                    FactionState winner = StateOf(wf), loser = StateOf(lf);
                    if (winner == null || loser == null) continue;
                    float rep = Mathf.Min(loser.treasury * 0.05f,
                        DiplomaticActionAiRules.ProposedReparations(Mathf.Abs(ws.warScore), CampaignRules.EconomyBase(loser)));
                    if (rep > 0f)
                    {
                        loser.treasury = Mathf.Max(0f, loser.treasury - rep); winner.treasury += rep;
                        NotificationCenter.Push(NotificationCategory.外交, NotificationSeverity.情報, $"{lf} が {wf} へ賠償を支払（{rep:0}）"); // Tier1 通知
                    }
                }

            // 条約効果（P1 配線）：締結中の条約は毎年 opinion を補強する（DiplomaticEffectRules→DiplomacyRules）。
            var treaties = TreatyLedger.All;
            if (treaties != null)
                for (int t = 0; t < treaties.Count; t++)
                {
                    ActiveTreaty tr = treaties[t];
                    if (tr == null) continue;
                    float delta = DiplomaticEffectRules.TreatyOpinionDelta(tr.type) * 0.1f; // 年次の控えめな補強
                    if (Mathf.Abs(delta) > 0.0001f) DiplomacyRules.AdjustOpinion(state, tr.factionA, tr.factionB, delta);
                }

            // 失効した条約を整理（status系は平時へ）。
            TreatyManagementRules.ExpireDue(state, campaignYear);
        }

        // 継承危機（P2 配線）：勢力ごとの内乱リスク状態（観測/通知のエッジ検出用）。
        private readonly System.Collections.Generic.Dictionary<Faction, bool> successionCrisis
            = new System.Collections.Generic.Dictionary<Faction, bool>();

        /// <summary>勢力が継承危機（内乱リスク）中か（観測層専用＝read-only）。</summary>
        public bool IsSuccessionCrisis(Faction faction)
            => successionCrisis.TryGetValue(faction, out var v) && v;

        /// <summary>
        /// 継承・内乱（P2 配線）：正統性と派閥対立から継承紛争リスクを解き、内乱の閾値を超えたら通知する
        /// （`SuccessionCrisisRules` 委譲＝後継者不明＋低正統性で危機）。年次（`RunAnnualLifecycleTick`）から呼ぶ。
        /// </summary>
        private void RunSuccessionTick()
        {
            var camp = StrategySession.Campaign;
            if (camp == null || camp.states == null) return;
            for (int i = 0; i < camp.states.Count; i++)
            {
                FactionState s = camp.states[i];
                if (s == null || s.regime == null) continue;
                float legitimacy = s.regime.legitimacy;
                float factionalism = 1f - s.inclusiveness;            // 収奪的(低包摂)ほど派閥対立
                bool hasHeir = legitimacy > 0.5f;                      // proxy：正統性が高い＝後継明確
                float risk = SuccessionCrisisRules.SuccessionDisputeRisk(hasHeir, legitimacy, factionalism);
                bool now = SuccessionCrisisRules.WouldEruptCivilWar(risk, 0.6f);
                bool was = successionCrisis.TryGetValue(s.faction, out var pc) && pc;
                successionCrisis[s.faction] = now;
                if (now && !was)
                {
                    NotificationCenter.Push(NotificationCategory.政治, NotificationSeverity.警告,
                        $"{s.faction} 継承危機＝内乱勃発（正統性 {legitimacy:0.00}）");
                    // 内乱の帰結（P2 配線・bounded）：正統性失墜・軍備損耗・所有星系の安定度低下。
                    s.regime.legitimacy = Mathf.Clamp01(s.regime.legitimacy - 0.1f);
                    int pool = FleetPool.Get(s.faction);
                    if (pool > 0) FleetPool.Add(s.faction, -(int)(pool * 0.1f)); // 内乱で軍の1割を喪失
                    if (map != null && provinces != null)
                        foreach (var sys in map.systems)
                            if (sys != null && sys.owner == s.faction && provinces.TryGetValue(sys.id, out var prov) && prov != null)
                                prov.stability = Mathf.Max(0f, prov.stability - 10f);
                    // 政体転換（P0 配線・継承ループの出口）：正統性が地に落ちた内乱では強権が台頭しうる（移行可能な政体のみ）。
                    if (legitimacy < 0.25f && GovernmentFormRules.CanTransition(s.governmentForm, GovernmentForm.指導者独裁))
                    {
                        s.governmentForm = GovernmentForm.指導者独裁;
                        NotificationCenter.Push(NotificationCategory.政治, NotificationSeverity.警告,
                            $"{s.faction} 内乱の混乱に乗じ強権体制（指導者独裁）が成立");
                    }
                }
            }
        }

        /// <summary>同盟→共同参戦（P1）：宣戦布告した側の同盟国も標的へ宣戦する（プレイヤー絡み・既交戦は除く）。</summary>
        private void DeclareAlliedWar(DiplomacyState state, Faction declarer, Faction target)
        {
            if (state == null) return;
            Faction player = GameSettings.Instance != null ? GameSettings.Instance.playerFaction : Faction.同盟;
            var dpar = DiplomacyRules.DiplomacyParams.Default;
            for (int k = 0; k < DemoFactions.Length; k++)
            {
                Faction ally = DemoFactions[k];
                if (ally == declarer || ally == target || ally == player) continue;
                if (state.Status(declarer.ToString(), ally.ToString()) == DiplomacyState.DiplomaticStatus.同盟
                    && state.Status(ally.ToString(), target.ToString()) != DiplomacyState.DiplomaticStatus.交戦)
                {
                    DiplomacyRules.DeclareWar(state, ally.ToString(), target.ToString(), dpar);
                    NotificationCenter.Push(NotificationCategory.外交, NotificationSeverity.警告, $"{ally} が同盟に従い {target} へ参戦");
                }
            }
        }

        /// <summary>講和の領土移転（P1）：勝者が敗者の国境星系を1つ併合する。敗者が複数星系を持つ場合のみ（滅亡させない）。成功で true。</summary>
        private bool TransferBorderSystem(Faction winner, Faction loser)
        {
            if (map == null) return false;
            int loserCount = 0;
            foreach (var s in map.systems) if (s != null && s.owner == loser) loserCount++;
            if (loserCount <= 1) return false; // 最後の1星系は割譲しない
            foreach (var s in map.systems)
            {
                if (s == null || s.owner != loser) continue;
                var nb = map.Neighbors(s.id);
                for (int n = 0; n < nb.Count; n++)
                {
                    var ns = map.GetSystem(nb[n]);
                    if (ns != null && ns.owner == winner) // 勝者領に隣接する敗者星系を割譲
                    {
                        s.owner = winner;
                        if (provinces != null && provinces.TryGetValue(s.id, out var pv) && pv != null) GovernanceRules.OnOccupied(pv);
                        return true;
                    }
                }
            }
            return false;
        }

        /// <summary>諜報工作の決定論 roll（年×攻撃側×標的のハッシュ→[0,1)）。乱数なし＝セーブ往復で再現。</summary>
        private float SabotageRoll(Faction attacker, Faction target)
        {
            unchecked
            {
                uint h = (uint)campaignYear * 2654435761u + (uint)((int)attacker * 73856093) + (uint)((int)target * 19349663) + 0x9E3779B9u;
                h ^= h >> 13; h *= 0x85EBCA6Bu; h ^= h >> 16;
                return (h & 0xFFFFFFu) / (float)0x1000000;
            }
        }

        // 開示エンジン（P3 配線・#495 物語の背骨）：秘史→真相→エンディングの連鎖開示。
        private DisclosureLedger disclosureLedger;
        private SampleDisclosures.Chronicle chronicle;

        /// <summary>開示の進捗（0..1・観測層専用＝read-only）。</summary>
        public float DisclosureProgress() => disclosureLedger != null ? disclosureLedger.Progress() : 0f;
        /// <summary>指定 id の秘史が開示済みか（観測層専用）。</summary>
        public bool IsDisclosureRevealed(string id) => disclosureLedger != null && disclosureLedger.IsRevealed(id);

        /// <summary>
        /// 開示（P3 配線）：`DisclosureLedger` に秘史連鎖（`SampleDisclosures`）を登録し、年次で `Evaluate`＝
        /// 条件・前提が満ちた秘史を不動点まで連鎖開示して通知する。デモは開幕から一定年で断片が見つかる
        /// （探索#119 配線までの仮トリガ）。年次（`RunAnnualLifecycleTick`）から呼ぶ。
        /// </summary>
        private void RunDisclosureTick()
        {
            if (disclosureLedger == null)
            {
                disclosureLedger = new DisclosureLedger();
                disclosureLedger.Register(SampleDisclosures.SecretFragment());
                disclosureLedger.Register(SampleDisclosures.AncientTruth());
                disclosureLedger.Register(SampleDisclosures.EndingUnlock());
                chronicle = new SampleDisclosures.Chronicle();
            }
            if (!chronicle.fragmentFound && campaignYear >= TimeDisplay.StartYear + 5)
                chronicle.fragmentFound = true; // 仮トリガ（探索が実装されたらそこから立てる）
            Faction pf = GameSettings.Instance != null ? GameSettings.Instance.playerFaction : Faction.同盟;
            var revealed = disclosureLedger.Evaluate(new EventContext(pf, -1, chronicle));
            if (revealed != null)
                for (int i = 0; i < revealed.Count; i++)
                    NotificationCenter.Push(NotificationCategory.システム, NotificationSeverity.情報,
                        $"【{revealed[i].category}】{revealed[i].title}");
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
            // ★プレイヤーへ出す規模は<b>艦艇数（隻）</b>だけ（内部の抽象兵力は画面に出さない）。
            // 動員ぶんは実在艦隊の Ships の合計、必要ぶんは計画値なので FleetShipCountRules で換算する。
            if (!plan.launched)
            {
                NotificationCenter.Push(NotificationCategory.占領, NotificationSeverity.注意,
                    $"任務：{s.systemName} 攻略は戦力集中まで待機（逐次投入を避ける）。" +
                    $"動員可能{MissionShips(plan):N0}隻／必要{RequiredShips(plan):N0}隻");
                return;
            }

            // 動員した艦隊を目標星系へ進軍させる（どう動くかは各艦の経路探索に委ねる＝任務戦術）。
            for (int i = 0; i < plan.fleetIds.Count; i++)
            {
                StrategicFleet f = reg.GetFleet(plan.fleetIds[i]);
                if (f != null) f.WarpTo(map, s.id);
            }

            string scale = plan.echelon.ToString();
            string note = plan.piecemeal ? "（逐次投入＝戦力不足のまま発動）" : "";
            NotificationCenter.Push(NotificationCategory.占領,
                plan.piecemeal ? NotificationSeverity.注意 : NotificationSeverity.情報,
                $"任務：{s.systemName} 攻略。{scale}を集中動員（{plan.fleetIds.Count}隊・" +
                $"{MissionShips(plan):N0}隻／必要{RequiredShips(plan):N0}隻）{note}");
        }

        /// <summary>動員した艦隊の<b>実艦艇数</b>の合計（プレイヤー向けの規模表示・抽象兵力は出さない）。</summary>
        private int MissionShips(MissionPlan plan)
        {
            if (reg == null || plan.fleetIds == null) return 0;
            long total = 0;
            for (int i = 0; i < plan.fleetIds.Count; i++)
            {
                StrategicFleet f = reg.GetFleet(plan.fleetIds[i]);
                if (f != null) total += Mathf.Max(0, f.Ships);
            }
            return total > int.MaxValue ? int.MaxValue : (int)total;
        }

        /// <summary>
        /// 任務に必要な規模を艦艇数へ換算する。必要量は計画値（実在艦隊の裏付けが無い）なので、
        /// 換算は <see cref="FleetShipCountRules.FromStrength"/> の一本の窓口を通す。
        /// </summary>
        private static int RequiredShips(MissionPlan plan)
            => FleetShipCountRules.FromStrength(Mathf.RoundToInt(Mathf.Max(0f, plan.requiredStrength)));

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
