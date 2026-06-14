using System.Collections.Generic;
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

        /// <summary>文官要職（観測用・人物名鑑が在任を表示）。</summary>
        public IReadOnlyList<Office> CivilOffices => civilOffices;

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

        /// <summary>勢力の軍政型を現在の政体形態から導く（捕虜処遇 DefaultDisposition 等が政体に追従＝共産化で処断的に等）。</summary>
        private static CivilianControlType FactionControl(Faction f)
        {
            var camp = StrategySession.Campaign;
            FactionState s = camp != null ? CampaignRules.GetState(camp, f) : null;
            if (s != null) return GovernmentFormRules.ControlTypeOf(s.governmentForm);
            return f == Faction.帝国 ? CivilianControlType.君主統帥 : CivilianControlType.文民統制; // フォールバック
        }

        private static Faction EnemyOf(Faction f) => f == Faction.帝国 ? Faction.同盟 : Faction.帝国;

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

            int governed = 0;
            for (int i = 0; i < map.systems.Count; i++)
            {
                if (governed >= MaxGovernedSystems) break;
                StarSystem s = map.systems[i];
                if (s == null) continue;
                int fIdx = FactionIndex(s.owner);
                if (fIdx < 0) continue; // デモ勢力の領のみ
                Office office = governorOffices[fIdx];
                if (office == null) continue;

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
        /// 省庁の配属（年次・官僚制基盤）：死亡/捕虜の官僚を外し、空き定員を勢力の文民で埋める（有能な順・一人一省＝兼任しない）。
        /// 数値ロジックは <see cref="MinistryRules"/>/<see cref="MinistryAdminRules"/> へ委譲。
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

                // 死亡/捕虜の官僚を一掃
                for (int m = 0; m < tree.Count; m++)
                {
                    var mn = tree[m];
                    if (mn == null) continue;
                    for (int i = mn.staffIds.Count - 1; i >= 0; i--)
                    {
                        Person held = FindCivilian(mn.staffIds[i]);
                        if (held == null || !held.IsAvailable) mn.staffIds.RemoveAt(i);
                    }
                }

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
                if (UnityEngine.Random.value < recruitChance && CaptivityRules.Recruit(c, captor))
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
            if (UnityEngine.Random.value > 0.15f) return; // 年あたりの捕獲生起（控えめ）
            var pool = new System.Collections.Generic.List<Person>();
            for (int i = 0; i < commanders.Count; i++)
            {
                Person c = commanders[i];
                if (c != null && c.IsAvailable && c.serviceStatus == ServiceStatus.現役 && c.rankTier < 8)
                    pool.Add(c); // 最高位は捕らえにくい＝中堅以下
            }
            if (pool.Count == 0) return;
            Person target = pool[UnityEngine.Random.Range(0, pool.Count)];
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
