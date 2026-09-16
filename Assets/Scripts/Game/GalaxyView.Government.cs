using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace Ginei
{
    public partial class GalaxyView
    {
        // --- 人事の空席補充（#152）と捕虜の処遇（#154）の配線 ---
        private Office[] commandOffices; // 勢力ごとの要職（DemoFactions と並行・null=未設定）
        private Office[] civilOffices;   // 勢力ごとの文官要職＝宰相（銓衡で配属・DemoFactions と並行）
        private Office[] governorOffices; // 勢力ごとの総督職（OfficeScope.星系・scopeKey=星系id で星系別に配属）
        private const CourtRank PremierRequiredRank = CourtRank.従五位下; // 宰相の官位相当＝五位以上（貴族）
        private const CourtRank GovernorRequiredRank = CourtRank.正六位上; // 総督（受領/国司）の官位相当＝六位以上
        private const int MaxGovernedSystems = 16;   // 総督を置く星系の上限（PERF＝無制限配属を防ぐ）
        private const float CentralOversightShare = 0.3f; // 中央（宰相）が地方へ及ぼす監督の効き（薄く全土へ）
        private List<Ministry>[] ministries;          // 勢力ごとの省庁ツリー（二官八省・DemoFactions と並行）
        private int[] ministryTopId;                  // 勢力ごとの太政官（最上位省）id

        // --- 官僚の年次人事（#141 配線）：省庁ツリーは一時データ・人事台帳は FactionState.civilService（保存）が単一の出所 ---
        private static readonly CivilServicePostParams CivilServicePrm = CivilServicePostParams.Default;
        private static readonly CivilServiceAnnualParams CivilServiceAnnualPrm = CivilServiceAnnualParams.Default;
        private const int MaxCivilServiceChangeNotices = 5; // 通知に載せる変更明細の上限（残りは件数に丸める）
        private const int MaxCivilServiceSkipNotices = 3;   // 通知に載せる見送り理由の上限（同じ理由はまとめる）

        /// <summary>文官要職（観測用・人物名鑑が在任を表示）。</summary>
        public IReadOnlyList<Office> CivilOffices => civilOffices;

        /// <summary>その勢力の宰相職（内政・国家）。未編成/非デモ勢力は null。任命は <see cref="GovernmentRegistry"/> を通す。</summary>
        public Office PremierOfficeOf(Faction f)
        {
            int idx = FactionIndex(f);
            return (civilOffices != null && idx >= 0 && idx < civilOffices.Length) ? civilOffices[idx] : null;
        }

        /// <summary>その勢力の総督（知事）職（内政・星系スコープ）。未編成/非デモ勢力は null。在任は scopeKey=星系ID。</summary>
        public Office GovernorOfficeOf(Faction f)
        {
            int idx = FactionIndex(f);
            return (governorOffices != null && idx >= 0 && idx < governorOffices.Length) ? governorOffices[idx] : null;
        }

        /// <summary>宰相の官位相当（年次の銓衡 <see cref="RunCivilAppointmentTick"/> と同じ要求位階）。</summary>
        public static CourtRank PremierRank => PremierRequiredRank;

        /// <summary>勢力の省庁ツリー（二官八省・GOV-5 #158）。未配線/非デモ勢力は null。観測層（政府オブザーバ）専用＝read-only。</summary>
        public IReadOnlyList<Ministry> MinistriesOf(Faction f)
        {
            if (ministries == null) return null;
            int idx = FactionIndex(f);
            return (idx >= 0 && idx < ministries.Length) ? ministries[idx] : null;
        }

        /// <summary>勢力の太政官（最上位省）id。未配線/非デモ勢力は −1。観測層専用＝read-only。</summary>
        public int TopMinistryIdOf(Faction f)
        {
            if (ministryTopId == null) return -1;
            int idx = FactionIndex(f);
            return (idx >= 0 && idx < ministryTopId.Length) ? ministryTopId[idx] : -1;
        }

        /// <summary>DemoFactions 内の番号（非デモ勢力は −1）。</summary>
        private int FactionIndex(Faction f)
        {
            for (int i = 0; i < DemoFactions.Length; i++) if (DemoFactions[i] == f) return i;
            return -1;
        }

        /// <summary>その文官が就いている文官官職名（宰相＝中央 or ◯◯総督＝地方）。無ければ空（観測用・人物名鑑が読む）。</summary>
        public string CivilPostOf(Person p)
        {
            if (p == null) return "";
            if (civilOffices != null)
                for (int f = 0; f < civilOffices.Length; f++)
                    if (civilOffices[f] != null && GovernmentRegistry.GetHolder(civilOffices[f]) is Person h && h.id == p.id)
                        return civilOffices[f].officeName;
            if (governorOffices != null && map != null)
                for (int i = 0; i < map.systems.Count; i++)
                {
                    StarSystem s = map.systems[i];
                    if (s == null) continue;
                    int fIdx = FactionIndex(s.owner);
                    if (fIdx < 0 || governorOffices[fIdx] == null) continue;
                    if (GovernmentRegistry.GetHolder(governorOffices[fIdx], s.id) is Person g && g.id == p.id)
                        return $"{s.systemName}総督";
                }
            return MinistryOf(p); // 要職に無ければ省庁の配属を返す（無ければ空）
        }

        /// <summary>勢力の現役（生存・自由・現役）司令を後任候補として集める。</summary>
        private System.Collections.Generic.List<ICharacter> ActiveCommanders(Faction f)
        {
            var list = new System.Collections.Generic.List<ICharacter>();
            if (commanders == null) return list;
            for (int i = 0; i < commanders.Count; i++)
            {
                Person c = commanders[i];
                if (c != null && c.faction == f && c.IsAvailable && c.serviceStatus == ServiceStatus.現役)
                    list.Add(c);
            }
            return list;
        }

        // ===== 誰が操作しているか（GitHub #67 の権限判定の入口）=====

        /// <summary>
        /// いま操作している人物（主人公）。特定できなければ null。
        /// 決裁の権限判定（<see cref="DecisionAuthorityRules"/>）と、会戦の指揮系統の判定に使う。
        /// <b>役職・階級はこの人物のものを見る</b>＝プレイヤーを全能の裁可者にしない。
        /// </summary>
        public Person PlayerCharacter()
        {
            if (qaPlayerCharacter != null) return qaPlayerCharacter; // 試験入口（BindPlayerCharacterForQa）で固定したときだけ
            var career = FindAnyObjectByType<ProtagonistCareerDirector>();
            return career != null ? career.Protagonist : null;
        }

        /// <summary>その勢力の軍政型（文民統制／君主統帥など）。権限判定の独立した制約。</summary>
        public CivilianControlType CivilianControlOf(Faction f) => FactionControl(f);

        /// <summary>人物IDから人物を引く（上申先の在否・資質の確認に使う）。居なければ null。</summary>
        public Person FindPersonById(int personId)
        {
            if (personId <= 0) return null;
            Person p = FindIn(commanders, personId);
            return p ?? FindIn(civilians, personId);
        }

        private static Person FindIn(System.Collections.Generic.List<Person> roster, int id)
        {
            if (roster == null) return null;
            for (int i = 0; i < roster.Count; i++)
                if (roster[i] != null && roster[i].id == id) return roster[i];
            return null;
        }

        /// <summary>
        /// その所掌を決裁できる役職に就いている人物を探す（上申先）。
        /// <paramref name="exclude"/>（本人）は除く。見つからなければ null＝<b>代行を勝手に作らない</b>。
        /// </summary>
        public Person FindOfficeHolder(Faction faction, OfficeDomain domain, Person exclude = null)
        {
            Person best = null;
            best = SearchHolder(commanders, faction, domain, exclude, best);
            best = SearchHolder(civilians, faction, domain, exclude, best);
            return best;
        }

        private static Person SearchHolder(System.Collections.Generic.List<Person> roster, Faction faction,
                                           OfficeDomain domain, Person exclude, Person best)
        {
            if (roster == null) return best;
            for (int i = 0; i < roster.Count; i++)
            {
                Person p = roster[i];
                if (p == null || p.IsDeceased || p.faction != faction) continue;
                if (exclude != null && p.id == exclude.id) continue;
                if (!OfficeRules.CanPropose(GovernmentRegistry.GetOffices(p), domain, OfficeScope.国家)) continue;
                // 同条件なら階級の高いほう＝同位は id の小さいほう（決定論）。
                if (best == null || p.rankTier > best.rankTier
                    || (p.rankTier == best.rankTier && p.id < best.id)) best = p;
            }
            return best;
        }

        /// <summary>勢力の軍政型を現在の政体形態から導く（捕虜処遇 DefaultDisposition 等が政体に追従＝共産化で処断的に等）。</summary>
        private static CivilianControlType FactionControl(Faction f)
        {
            var camp = StrategySession.Campaign;
            FactionState s = camp != null ? CampaignRules.GetState(camp, f) : null;
            if (s != null) return GovernmentFormRules.ControlTypeOf(s.governmentForm);
            return f == Faction.帝国 ? CivilianControlType.君主統帥 : CivilianControlType.文民統制; // フォールバック
        }

        private static Faction EnemyOf(Faction f) => f == Faction.帝国 ? Faction.同盟 : Faction.帝国;

        /// <summary>
        /// 戦役開始時の初期政府編成（政府オブザーバ Alt+G を開幕から実データで満たす）：要職（宇宙艦隊司令長官）を任命し、
        /// 省庁（二官八省）を編成して文民を配属する。<b>従来は最初の年境界（年次ティック）まで「要職の任命なし／省庁 未配線」</b>
        /// だったのを、開幕に前倒しする。宰相/総督/首班は位階の叙位・政党結成が要るため年次の銓衡（<see cref="RunCivilAppointmentTick"/> 等）で
        /// 追って埋まる。いずれも冪等（年次ティックと二重編成しない・通知を撒かない静かなシード）。</summary>
        private void SeedGovernment()
        {
            SeedCommandOffices();          // 要職＝司令長官を最先任へ任命（GovernmentRegistry を初期化して任命・静か）
            SeedMinistries();              // 二官八省の編成だけは常に冪等シード（省庁ツリーは保存されない一時データ）
            RestoreCivilServiceStaffing(); // 人事台帳（保存）があれば在任者を配属へ写す＝台帳が正（#141）
            RunMinistryStaffingTick();     // 台帳の無い勢力だけ従来のシード配属（文民を能力順・位階ゲートなし・静か）
            RestoreElectedOffices();       // 保存/在席の選挙結果（首相・知事）を役職へ戻す（選挙はしない・人物不在なら空席＋理由）
        }

        /// <summary>
        /// 読込/開幕時に人事台帳（<see cref="FactionState.civilService"/>＝保存データ）の在任者を省庁の配属へ写す（#141）。
        /// 省庁ツリーは保存されない一時データなので、台帳を正として <see cref="Ministry.staffIds"/> を復元する
        /// ＝在任・段・就任年・履歴を失わず、次の年次人事が同じ人物を重ねて配属しない。台帳に無い配属は外さない。
        /// </summary>
        private void RestoreCivilServiceStaffing()
        {
            if (ministries == null) return;
            for (int f = 0; f < DemoFactions.Length; f++)
            {
                CivilServiceState st = CivilServiceOf(DemoFactions[f]);
                if (st == null || ministries[f] == null) continue;
                CivilServicePostRules.NormalizeLoaded(st); // 旧セーブ/壊れた記録の穴埋め（冪等・任命も解任もしない）
                CivilServicePostRules.SyncStaffing(ministries[f], st);
            }
        }

        /// <summary>勢力の人事台帳（未初期化＝null。初期化は年次人事 <see cref="RunCivilServiceAnnualTick"/> が1回だけ行う）。</summary>
        private static CivilServiceState CivilServiceOf(Faction f)
        {
            FactionState s = StateOf(f);
            return s != null ? s.civilService : null;
        }

        /// <summary>要職をシード（冪等）：勢力ごとに「宇宙艦隊司令長官」を1つ作り、最先任の現役へ任命。</summary>
        private void SeedCommandOffices()
        {
            if (commandOffices != null) return;
            GovernmentRegistry.Clear();
            commandOffices = new Office[DemoFactions.Length];
            for (int f = 0; f < DemoFactions.Length; f++)
            {
                Faction fac = DemoFactions[f];
                var office = new Office(900 + f, $"{fac}宇宙艦隊司令長官", OfficeScope.国家, OfficeDomain.軍事)
                { militaryOnly = true, requiredTier = 8 };
                commandOffices[f] = office;
                VacancyRules.FillVacancy(fac, office, ActiveCommanders(fac)); // 初任命
            }
            // 文官要職＝宰相（内政・文民専用）。位階の要求は官位相当（PremierRequiredRank）で別途効かせる＝requiredTier=0。
            // 初任は空席のまま（文民は年を追って卒業・叙位される）。年次の RunCivilAppointmentTick が銓衡で埋める。
            civilOffices = new Office[DemoFactions.Length];
            for (int f = 0; f < DemoFactions.Length; f++)
            {
                Faction fac = DemoFactions[f];
                civilOffices[f] = new Office(910 + f, $"{fac}宰相", OfficeScope.国家, OfficeDomain.内政)
                { civilianOnly = true, requiredTier = 0 };
            }
            // 文官の地方官＝総督（受領/国司・OfficeScope.星系）。同一 Office を scopeKey=星系id で星系別に使う。
            governorOffices = new Office[DemoFactions.Length];
            for (int f = 0; f < DemoFactions.Length; f++)
            {
                Faction fac = DemoFactions[f];
                governorOffices[f] = new Office(920 + f, $"{fac}総督", OfficeScope.星系, OfficeDomain.内政)
                { civilianOnly = true, requiredTier = 0 };
            }
        }

        /// <summary>勢力の文民ネームドを集める（銓衡候補）。</summary>
        private List<Person> CiviliansOf(Faction f)
        {
            var list = new List<Person>();
            if (civilians == null) return list;
            for (int i = 0; i < civilians.Count; i++)
                if (civilians[i] != null && civilians[i].faction == f) list.Add(civilians[i]);
            return list;
        }

        /// <summary>
        /// 文官の銓衡配属（官僚制基盤＝<see cref="CivilAppointmentRules"/> へ委譲）。死亡/捕虜・官位相当を割った在任者を解任し、
        /// 叙位された文官から考課＋位階で最適者を宰相へ任命する（式部省の選叙）。就任は人事通知へ。
        /// </summary>
        private void RunCivilAppointmentTick()
        {
            SeedCommandOffices(); // 冪等＝文官要職もここで用意される
            if (civilOffices == null || civilians == null) return;
            // 名実の乖離を選抜にも効かせる＝権威が低いほど門閥人事（位階＝家柄）が実績を上書きする。
            var prm = CivilServiceRules.ParamsForAuthority(
                courtAuthority != null ? courtAuthority.authority : 0f, CivilServiceRules.AppointmentParams.Default);
            for (int f = 0; f < DemoFactions.Length; f++)
            {
                Office office = civilOffices[f];
                if (office == null) continue;
                Faction fac = DemoFactions[f];
                // 民主政で国政選挙が回っている勢力は、首相（＝この職）を選挙で決める＝官位の銓衡で上書きしない。
                if (UsesElectedPremier(fac)) { MaintainElectedPremier(fac); continue; }
                var holder = GovernmentRegistry.GetHolder(office) as Person;
                if (holder != null && (!holder.IsAvailable
                    || JapaneseCourtRankRules.Compare(holder.courtRank, PremierRequiredRank) < 0))
                    GovernmentRegistry.Dismiss(office, holder); // 官位相当を割った（位階喪失）／死亡・捕虜
                ICharacter before = GovernmentRegistry.GetHolder(office);
                Person appointed = CivilAppointmentRules.FillVacancy(
                    fac, office, PremierRequiredRank, CiviliansOf(fac), prm);
                if (appointed != null && appointed != before)
                    NotificationCenter.Push(NotificationCategory.人事, NotificationSeverity.情報,
                        $"{fac} {office.officeName} に {appointed.name}（{JapaneseCourtRankRules.Name(appointed.courtRank)}）が就任");
            }
        }

        /// <summary>勢力の文民から、既に他の官職に就いている者（<paramref name="assigned"/>）を除いた銓衡候補。一人一職を保つ。</summary>
        private List<Person> CiviliansOfExcluding(Faction f, HashSet<int> assigned)
        {
            var list = new List<Person>();
            if (civilians == null) return list;
            for (int i = 0; i < civilians.Count; i++)
            {
                Person c = civilians[i];
                if (c != null && c.faction == f && (assigned == null || !assigned.Contains(c.id))) list.Add(c);
            }
            return list;
        }

        /// <summary>
        /// 総督（地方官）の銓衡配属（官僚制基盤）。所有星系ごとに、官位相当（六位以上）の文官を考課＋位階で配属する
        /// ＝受領/国司。中央の宰相とは別人（一人一職）。PERF＝<see cref="MaxGovernedSystems"/> 件で打ち止め。
        /// </summary>
        private void RunGovernorAppointmentTick()
        {
            SeedCommandOffices();
            if (governorOffices == null || civilians == null || map == null) return;

            // 名実の乖離を選抜にも効かせる＝権威が低いほど門閥人事（位階＝家柄）が実績を上書きする。
            var prm = CivilServiceRules.ParamsForAuthority(
                courtAuthority != null ? courtAuthority.authority : 0f, CivilServiceRules.AppointmentParams.Default);
            var assigned = new HashSet<int>();
            if (civilOffices != null) // 宰相（中央）は総督に重ねない
                for (int f = 0; f < civilOffices.Length; f++)
                    if (civilOffices[f] != null && GovernmentRegistry.GetHolder(civilOffices[f]) is Person pm) assigned.Add(pm.id);

            // 選挙で知事を選ぶ勢力は、銓衡の前に在任を現況へ合わせる（政治 Tick 後の死亡・占領・兼任で権限を残さない）。
            for (int f = 0; f < DemoFactions.Length; f++)
                if (UsesElectedGovernors(DemoFactions[f])) RefreshElectedGovernors(DemoFactions[f]);

            int governed = 0;
            for (int i = 0; i < map.systems.Count; i++)
            {
                if (governed >= MaxGovernedSystems) break;
                StarSystem s = map.systems[i];
                if (s == null) continue;
                DismissForeignGovernors(s); // 占領・離反などで所有が変わった星系の旧勢力の総督/知事の権限を外す
                int fIdx = FactionIndex(s.owner);
                if (fIdx < 0) continue; // デモ勢力の領のみ
                Office office = governorOffices[fIdx];
                if (office == null) continue;
                // 民主政で知事選が回っている勢力は、知事を選挙で決める＝官位の銓衡で上書きしない（政治 Tick が反映済み）。
                if (UsesElectedGovernors(s.owner))
                {
                    if (GovernmentRegistry.GetHolder(office, s.id) is Person elected) assigned.Add(elected.id);
                    continue;
                }

                var holder = GovernmentRegistry.GetHolder(office, s.id) as Person;
                if (holder != null && (!holder.IsAvailable
                    || JapaneseCourtRankRules.Compare(holder.courtRank, GovernorRequiredRank) < 0))
                {
                    GovernmentRegistry.Dismiss(office, holder, s.id);
                    holder = null;
                }
                ICharacter before = holder;
                Person gov = CivilAppointmentRules.FillVacancy(
                    s.owner, office, GovernorRequiredRank, CiviliansOfExcluding(s.owner, assigned),
                    prm, scopeKey: s.id);
                if (gov == null) continue;

                assigned.Add(gov.id);
                governed++;
                if (gov != before)
                    NotificationCenter.Push(NotificationCategory.人事, NotificationSeverity.情報,
                        $"{s.owner} {s.systemName}総督 に {gov.name}（{JapaneseCourtRankRules.Name(gov.courtRank)}）が就任");
            }
        }

        /// <summary>その星系の所有勢力以外の総督/知事職に在任者が残っていれば外す（旧所有勢力の権限を残さない）。</summary>
        private void DismissForeignGovernors(StarSystem s)
        {
            if (s == null || governorOffices == null) return;
            for (int f = 0; f < governorOffices.Length; f++)
            {
                if (DemoFactions[f] == s.owner || governorOffices[f] == null) continue;
                ICharacter stale = GovernmentRegistry.GetHolder(governorOffices[f], s.id);
                if (stale != null) GovernmentRegistry.Dismiss(governorOffices[f], stale, s.id);
            }
        }

        /// <summary>
        /// 星系の内政に効く文官行政寄与＝<b>総督（地方・その星系）＋宰相（中央・薄く監督）</b>。いずれも名実の乖離で
        /// 朝廷の権威ぶん減衰（<see cref="AdministrationRules"/>）。総督が空席なら中央の監督のみが薄く届く。
        /// </summary>
        private float SystemAdminBonus(StarSystem s)
        {
            if (s == null) return 0f;
            float authority = courtAuthority != null ? courtAuthority.authority : 0f;
            float gov = 0f;
            int fIdx = FactionIndex(s.owner);
            if (fIdx >= 0 && governorOffices != null && governorOffices[fIdx] != null)
            {
                var governor = GovernmentRegistry.GetHolder(governorOffices[fIdx], s.id) as Person;
                gov = AdministrationRules.StabilityContribution(governor, authority, AdministrationRules.AdminParams.Default);
            }
            // 中央＝宰相＋省庁（民部省/太政官の行政）が監督として薄く全土へ及ぶ。
            float central = PremierAdminBonus(s.owner) + MinistryCentralBonus(s.owner);
            return gov + central * CentralOversightShare;
        }

        // ===== 省庁ツリー（二官八省・GOV-5 #158 配線） =====

        /// <summary>勢力ごとの省庁ツリーをシード（冪等）：太政官 ⊃ 式部省/民部省/大蔵省/兵部省。</summary>
        private void SeedMinistries()
        {
            if (ministries != null) return;
            ministries = new List<Ministry>[DemoFactions.Length];
            ministryTopId = new int[DemoFactions.Length];
            for (int f = 0; f < DemoFactions.Length; f++)
            {
                Faction fac = DemoFactions[f];
                int baseId = 1000 + f * 10;
                var tree = new List<Ministry>
                {
                    new Ministry(baseId + 0, $"{fac}太政官", OfficeDomain.内政) { staffSlots = 2 },
                    new Ministry(baseId + 1, $"{fac}式部省", OfficeDomain.内政) { staffSlots = 4 }, // 人事
                    new Ministry(baseId + 2, $"{fac}民部省", OfficeDomain.内政) { staffSlots = 4 }, // 内政
                    new Ministry(baseId + 3, $"{fac}大蔵省", OfficeDomain.財政) { staffSlots = 3 }, // 財政
                    new Ministry(baseId + 4, $"{fac}兵部省", OfficeDomain.軍事) { staffSlots = 3 }, // 軍政
                };
                for (int c = 1; c <= 4; c++) MinistryRules.AttachChild(tree, baseId + 0, baseId + c);
                ministries[f] = tree;
                ministryTopId[f] = baseId + 0;
            }
        }

        /// <summary>
        /// 省庁の配属の<b>開幕/読込シード</b>（官僚制基盤）：死亡/捕虜の官僚を外し、空き定員を勢力の文民で埋める（有能な順・一人一省＝兼任しない）。
        /// 数値ロジックは <see cref="MinistryRules"/>/<see cref="MinistryAdminRules"/> へ委譲。
        /// <para><b>年次からは呼ばない（#141）</b>＝これは内閣人事局の承認を通さず空席を埋めるため、年次の人事は
        /// <see cref="RunCivilServiceAnnualTick"/>（承認つき）が担う。人事台帳を持つ勢力はこのシードの対象外＝台帳の配属を上書きしない。</para>
        /// </summary>
        private void RunMinistryStaffingTick()
        {
            SeedMinistries();
            if (ministries == null || civilians == null) return;
            for (int f = 0; f < DemoFactions.Length; f++)
            {
                List<Ministry> tree = ministries[f];
                if (tree == null) continue;
                Faction fac = DemoFactions[f];
                if (CivilServiceOf(fac) != null) continue; // 台帳のある勢力は年次人事の領分＝旧自動配属で上書きしない

                // 死亡/捕虜の官僚を一掃
                PurgeUnavailableStaff(tree, null);

                // 既配属を除いた候補（有能順）
                var staffed = new HashSet<int>(MinistryRules.AllOfficialsUnder(tree, ministryTopId[f]));
                var pool = new List<Person>();
                for (int i = 0; i < civilians.Count; i++)
                {
                    Person c = civilians[i];
                    if (c != null && c.faction == fac && c.IsAvailable && !staffed.Contains(c.id)) pool.Add(c);
                }
                pool.Sort((a, b) => b.CivilAptitude.CompareTo(a.CivilAptitude));

                int next = 0;
                for (int m = 0; m < tree.Count && next < pool.Count; m++)
                {
                    var mn = tree[m];
                    if (mn == null) continue;
                    while (mn.HasVacancy && next < pool.Count)
                        if (MinistryRules.AssignOfficial(tree, mn.id, pool[next++].id)) { } // 単一所属は MinistryRules が保証
                }
            }
        }

        /// <summary>
        /// 死亡/捕虜/名簿から消えた官僚を省庁の配属（<see cref="Ministry.staffIds"/>）から外す（裁量の人事ではない＝承認を要さない後始末）。
        /// <paramref name="st"/> を渡すと台帳の在任者は触らない＝彼らの整理は年次人事の失職整理（<see cref="CivilServicePostRules.RetireIfIneligible"/>）
        /// が理由と履歴つきで行う＝台帳と配属を食い違わせない。
        /// </summary>
        private void PurgeUnavailableStaff(List<Ministry> tree, CivilServiceState st)
        {
            if (tree == null || civilians == null) return; // 名簿が未配線のときに配属を消さない
            for (int m = 0; m < tree.Count; m++)
            {
                Ministry mn = tree[m];
                if (mn == null || mn.staffIds == null) continue;
                for (int i = mn.staffIds.Count - 1; i >= 0; i--)
                {
                    int pid = mn.staffIds[i];
                    if (st != null && CivilServicePostRules.FindServing(st, pid) != null) continue; // 台帳の在任者は年次整理に任せる
                    Person held = FindCivilian(pid);
                    if (held == null || !held.IsAvailable) mn.staffIds.RemoveAt(i);
                }
            }
        }

        // ===== 官僚の年次人事（#141 配線） =====

        /// <summary>
        /// 省内職位の年次人事を勢力ごとに1回だけ回す（<see cref="CivilServiceAnnualRules.TickYear"/> が唯一の入口・年次の
        /// <c>RunBureaucracyTick</c>＝官位と考課の更新の後に呼ぶ）。失職整理→昇任→入省を<b>内閣人事局の承認つき</b>で通し、
        /// 承認権者（事務次官級＝首相／局長級以下＝所管大臣）が不在なら埋めずに見送る＝自動処理が権限を迂回しない。
        /// <para>台帳（<see cref="FactionState.civilService"/>）が無い新規/旧セーブの勢力は、ここで1回だけ台帳を作り、
        /// 現在の <see cref="Ministry.staffIds"/> を<b>同じ省の一般官僚として</b>移行する（<see cref="CivilServicePostRules.MigrateExistingStaff"/>
        /// ＝新規採用・異動・昇任はしない）。台帳がある勢力は台帳を正として配属を同期してから回す＝読込後も重複配属しない。</para>
        /// <para>年は選挙・委任と同じ暦年（<see cref="ElectionYear"/>）＝シーンを組み直しても在職年がずれない。
        /// 動くのは人事台帳と <see cref="Ministry.staffIds"/> だけ（内閣・<see cref="GovernmentRegistry"/>・軍・国庫には触れない）。</para>
        /// </summary>
        private void RunCivilServiceAnnualTick()
        {
            SeedMinistries();
            if (ministries == null || StrategySession.Campaign == null) return;
            int year = ElectionYear();
            List<Person> roster = ElectionRoster();

            for (int f = 0; f < DemoFactions.Length; f++)
            {
                Faction fac = DemoFactions[f];
                List<Ministry> tree = ministries[f];
                FactionState s = StateOf(fac);
                if (tree == null || s == null) continue;

                bool created = s.civilService == null;
                if (created) s.civilService = new CivilServiceState(); // 新規/旧セーブの初期化は1回だけ
                CivilServiceState st = s.civilService;

                if (!created) CivilServicePostRules.SyncStaffing(tree, st); // 台帳を正として配属を復元（読込後の同期）
                PurgeUnavailableStaff(tree, st);                            // 台帳に無い配属の死亡/捕虜だけ後始末

                var reasons = new List<string>();
                var counts = new List<int>();
                int migrated = created ? MigrateExistingMinistryStaff(fac, tree, st, roster, year, reasons, counts) : 0;

                CivilServiceAnnualReport rep = CivilServiceAnnualRules.TickYear(
                    s.politics, fac, tree, roster, year, st, CivilServicePrm, CivilServiceAnnualPrm);
                NotifyCivilServiceAnnual(fac, rep, migrated, reasons, counts);
            }
        }

        /// <summary>
        /// 台帳を持たなかった勢力の既存の配属を、同じ省の一般官僚として台帳へ写す（移行専用の入口へ委譲・1勢力1回）。
        /// 登録できない人物（死亡・拘束・他勢力・在野・軍人・政治家）は配属を触らずに理由だけ集める＝状態の一部だけを壊さない。
        /// </summary>
        /// <returns>写した人数。</returns>
        private int MigrateExistingMinistryStaff(Faction fac, List<Ministry> tree, CivilServiceState st,
            List<Person> roster, int year, List<string> reasons, List<int> counts)
        {
            int migrated = 0;
            for (int m = 0; m < tree.Count; m++)
            {
                Ministry mn = tree[m];
                if (mn == null || mn.staffIds == null) continue;
                for (int i = 0; i < mn.staffIds.Count; i++) // 移行は staffIds を変えない＝そのまま前から走査してよい
                {
                    if (CivilServicePostRules.MigrateExistingStaff(tree, fac, mn.id, mn.staffIds[i], roster, year,
                            "既存の配属を人事台帳へ移行", st, out string problem))
                        migrated++;
                    else if (problem != null)
                        BumpReason(reasons, counts, problem);
                }
            }
            return migrated;
        }

        /// <summary>同じ理由はまとめて数える（通知を理由の羅列で氾濫させない）。</summary>
        private static void BumpReason(List<string> reasons, List<int> counts, string reason)
        {
            if (string.IsNullOrEmpty(reason)) return;
            for (int i = 0; i < reasons.Count; i++)
                if (reasons[i] == reason) { counts[i]++; return; }
            reasons.Add(reason);
            counts.Add(1);
        }

        /// <summary>
        /// 年次人事の結果を人事通知へ1件だけ流す（勢力ごと）。要約（退職/昇任/配属/見送りの件数）＋変更明細（人物名・省名・職位を
        /// <see cref="MaxCivilServiceChangeNotices"/> 件まで）＋見送りの理由（同じ理由はまとめ <see cref="MaxCivilServiceSkipNotices"/> 種まで）。
        /// 載せなかったぶんは件数に丸める（黙って捨てない）。変化も見送りも無ければ通知しない。
        /// </summary>
        private void NotifyCivilServiceAnnual(Faction fac, CivilServiceAnnualReport rep, int migrated,
            List<string> reasons, List<int> counts)
        {
            if (rep == null) return;
            int migrationSkips = 0;
            for (int i = 0; i < counts.Count; i++) migrationSkips += counts[i];
            if (rep.TotalChanges == 0 && rep.skippedCount == 0 && migrated == 0 && migrationSkips == 0) return;

            var sb = new StringBuilder();
            sb.Append(fac).Append(" 官僚の年次人事：退職").Append(rep.retiredCount)
              .Append("・昇任").Append(rep.promotedCount)
              .Append("・配属").Append(rep.assignedCount)
              .Append("・見送り").Append(rep.skippedCount + migrationSkips);
            if (migrated > 0) sb.Append("（既存の配属を台帳へ移行 ").Append(migrated).Append("名）");

            // 変更明細（退職・昇任・配属）
            var detail = new StringBuilder();
            int shown = 0, changes = 0;
            for (int i = 0; i < rep.entries.Count; i++)
            {
                CivilServiceAnnualEntry e = rep.entries[i];
                if (e == null) continue;
                if (e.kind == CivilServiceAnnualKind.見送り) { BumpReason(reasons, counts, e.reason); continue; }
                changes++;
                if (shown >= MaxCivilServiceChangeNotices) continue;
                if (shown > 0) detail.Append('・');
                detail.Append(ElectionPersonName(e.personId)).Append('（').Append(e.ministryName).Append(' ')
                      .Append(e.grade).Append('）').Append(e.kind);
                shown++;
            }
            if (shown > 0)
            {
                sb.Append('／').Append(detail);
                if (changes > shown) sb.Append(" ほか").Append(changes - shown).Append('件');
            }

            // 見送りの理由（対応が要るもの＝承認権者の空席・候補なし・資格不足）
            if (reasons.Count > 0)
            {
                sb.Append("／見送り：");
                int listed = 0, covered = 0;
                for (int i = 0; i < reasons.Count && listed < MaxCivilServiceSkipNotices; i++)
                {
                    if (listed > 0) sb.Append('・');
                    sb.Append(reasons[i]);
                    if (counts[i] > 1) sb.Append('×').Append(counts[i]);
                    covered += counts[i];
                    listed++;
                }
                int omitted = rep.skippedCount + migrationSkips - covered; // 明細に載らなかった見送り（打切りぶんを含む）
                if (omitted > 0) sb.Append(" ほか").Append(omitted).Append('件');
            }

            NotificationCenter.Push(NotificationCategory.人事,
                rep.retiredCount > 0 ? NotificationSeverity.注意 : NotificationSeverity.情報, sb.ToString());
        }

        /// <summary>試験用：年次の官僚人事（台帳の初期化・移行・同期を含む本番と同じ経路）。</summary>
        public void RunCivilServiceAnnualTickForQa() => RunCivilServiceAnnualTick();

        /// <summary>勢力の省庁（太政官ツリー）の内政寄与＝名実の乖離で朝廷の権威ぶん減衰（<see cref="MinistryAdminRules"/>）。</summary>
        private float MinistryCentralBonus(Faction owner)
        {
            int f = FactionIndex(owner);
            if (f < 0 || ministries == null || ministries[f] == null) return 0f;
            float authority = courtAuthority != null ? courtAuthority.authority : 0f;
            Ministry top = MinistryRules.Get(ministries[f], ministryTopId[f]);
            return MinistryAdminRules.AdministrativeBonus(top, ministries[f], FindCivilian, authority, MinistryAdminRules.MinistryParams.Default);
        }

        /// <summary>その文官が配属されている省庁名（無ければ空・観測用）。</summary>
        private string MinistryOf(Person p)
        {
            if (p == null || ministries == null) return "";
            for (int f = 0; f < ministries.Length; f++)
            {
                List<Ministry> tree = ministries[f];
                if (tree == null) continue;
                for (int m = 0; m < tree.Count; m++)
                    if (tree[m] != null && tree[m].staffIds.Contains(p.id)) return tree[m].ministryName;
            }
            return "";
        }

        /// <summary>
        /// 後任補充（VacancyRules・#152）＋捕虜の処遇（CaptivityRules・#154）を年次で回す。数式/状態遷移は Core 窓口へ委譲。
        /// </summary>
        private void RunPersonnelTurnoverTick()
        {
            if (commanders == null) return;
            ResolveCaptives();   // 既存捕虜を処遇（解放/登用/処断）
            MaybeCapture();      // 敵対勢力により低確率で捕虜化
            FillCommandVacancies(); // 要職の空席を後任補充
        }

        /// <summary>捕虜を捕獲側の政体に従って処遇：登用（寝返り・稀）→さもなくば解放/処断。</summary>
        private void ResolveCaptives()
        {
            for (int i = 0; i < commanders.Count; i++)
            {
                Person c = commanders[i];
                if (c == null || c.captiveStatus != CaptiveStatus.捕虜) continue;
                Faction captor = c.heldBy;

                // まず登用（寝返り＝調略）を試みる（思想差・処遇で決まる稀な成立）。
                float recruitChance = CaptivityRules.RecruitChance(0.5f, 0.5f);
                if (DetRoll(campaignYear, c.id) < recruitChance && CaptivityRules.Recruit(c, captor))
                {
                    NotificationCenter.Push(NotificationCategory.人事, NotificationSeverity.注意, $"{c.name} {captor} へ登用（寝返り）");
                    continue;
                }

                // さもなくば捕獲側の政体の既定処遇（処断 or 解放）。
                CaptiveDisposition dispo = CaptivityRules.DefaultDisposition(FactionControl(captor));
                if (dispo == CaptiveDisposition.処断 && CaptivityRules.Execute(c, campaignYear))
                    NotificationCenter.Push(NotificationCategory.人事, NotificationSeverity.警告, $"{c.name} 処断（捕虜）");
                else if (CaptivityRules.Release(c))
                    NotificationCenter.Push(NotificationCategory.人事, NotificationSeverity.情報, $"{c.name} 解放され帰還");
            }
        }

        /// <summary>敵対勢力により低確率で中堅以下の現役将校を捕虜化（前線での捕獲のデモ）。</summary>
        private void MaybeCapture()
        {
            if (DetRoll(campaignYear, NextRollSeed()) > 0.15f) return; // 年あたりの捕獲生起（控えめ）
            var pool = new System.Collections.Generic.List<Person>();
            for (int i = 0; i < commanders.Count; i++)
            {
                Person c = commanders[i];
                if (c != null && c.IsAvailable && c.serviceStatus == ServiceStatus.現役 && c.rankTier < 8)
                    pool.Add(c); // 最高位は捕らえにくい＝中堅以下
            }
            if (pool.Count == 0) return;
            Person target = pool[(int)(DetRoll(campaignYear, NextRollSeed()) * pool.Count) % pool.Count];
            Faction captor = EnemyOf(target.faction);
            if (CaptivityRules.Capture(target, captor, campaignYear))
                NotificationCenter.Push(NotificationCategory.人事, NotificationSeverity.注意, $"{target.faction} {target.name} {captor} の捕虜に");
        }

        /// <summary>要職の保持者が死亡/捕虜/退役なら解任し、現役の有資格者で後任補充（VacancyRules・#152）。</summary>
        private void FillCommandVacancies()
        {
            SeedCommandOffices();
            for (int f = 0; f < DemoFactions.Length; f++)
            {
                Office office = commandOffices[f];
                if (office == null) continue;
                Faction fac = DemoFactions[f];
                var holder = GovernmentRegistry.GetHolder(office) as Person;
                if (holder != null && (!holder.IsAvailable || holder.serviceStatus == ServiceStatus.退役))
                    GovernmentRegistry.Dismiss(office, holder);
                ICharacter before = GovernmentRegistry.GetHolder(office);
                VacancyRules.FillVacancy(fac, office, ActiveCommanders(fac));
                ICharacter after = GovernmentRegistry.GetHolder(office);
                if (after != null && after != before)
                    NotificationCenter.Push(NotificationCategory.人事, NotificationSeverity.情報, $"{fac} {office.officeName} に {after.CharacterName} が就任");
            }
        }

    }
}
