using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.TestTools;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 官僚機構オブザーバ（Alt+K）が<b>省内職位（#141）</b>を観測できるか：配属官僚の行に職位名・在任年・官位・考課が出る／
    /// 台帳に無い旧配属者は「台帳未登録」＋理由（政治家など）を隠さない／省ごとの段別 在任/定員が出る／承認権者の状態
    /// （任命可能・承認者空席・上申先）が出る／直近の退任履歴が上限つきで出て超過は件数になる／台帳が無い勢力は
    /// 「人事台帳 未初期化」と出しつつ省庁ツリーは続く。あわせて<b>観測専用</b>（何度呼んでも台帳・配属が変わらない）と
    /// 既存のUI（縦スクロールバー・表示切替）を壊していないことを確かめる。
    /// <para>GalaxyView は無効な GameObject に載せて Start を走らせない（盤面は <c>BindElectionQaWorld</c> で差し込む）。
    /// 内閣は選挙の乱数に依らせず Core の共通入口（<see cref="CabinetAppointmentRules"/>）で決め打ちに組み、台帳は固定データを
    /// 直に置く＝表示だけを問う。セーブ・シーンには触れない。</para>
    /// </summary>
    public class BureaucracyObserverCareerPlayModeTests
    {
        private const int YearSeconds = 60 * 30 * 12; // GameDate.DateParams.Default の1年（game-秒）
        private const int FirstYear = 797;
        private const int CapitalId = 1, FrontierId = 2;
        private const int PremierId = 11;             // 政治家 11..20（11=首相・12〜=大臣）
        private const int PoliticianCount = 10;
        private const int BureaucratId = 31;          // 職業官僚 31..36（政治家でない文民）
        private const int BureaucratCount = 6;
        private const int TenureYears = 4;            // 課長級の在任年（就任年＝FirstYear−4）
        private const int MinisterId = PremierId + 1; // 委任の試験で1省に置く大臣
        private const int ViceId = PremierId + 2;     // 同じ省の副大臣（委任先）
        private const int DelegationYears = 1;        // 委任の期限（CabinetParams.Default の最長2年以内）
        private static readonly CabinetParams CabPrm = CabinetParams.Default;

        private readonly List<GameObject> spawned = new List<GameObject>();
        private CampaignState savedCampaign;
        private GalaxyMap savedMap;
        private Dictionary<int, Province> savedProvinces;
        private GameClock savedClock;
        private List<GovernmentRegistry.Appointment> savedAppointments;
        private GalaxyView savedActive;

        [SetUp]
        public void SetUp()
        {
            savedActive = GalaxyView.Active;
            savedCampaign = StrategySession.Campaign;
            savedMap = StrategySession.Map;
            savedProvinces = StrategySession.Provinces;
            savedClock = StrategySession.Clock;
            savedAppointments = new List<GovernmentRegistry.Appointment>(GovernmentRegistry.Appointments);
        }

        [TearDown]
        public void TearDown()
        {
            for (int i = 0; i < spawned.Count; i++)
                if (spawned[i] != null) Object.DestroyImmediate(spawned[i]);
            spawned.Clear();
            GalaxyView.SwapActiveForQa(savedActive);
            StrategySession.Campaign = savedCampaign;
            StrategySession.Map = savedMap;
            StrategySession.Provinces = savedProvinces;
            StrategySession.Clock = savedClock;
            GovernmentRegistry.Clear();
            for (int i = 0; i < savedAppointments.Count; i++)
            {
                GovernmentRegistry.Appointment a = savedAppointments[i];
                GovernmentRegistry.TryAppoint(a.faction, a.office, a.holder, a.scopeKey);
            }
        }

        // ===== 盤面 =====

        private static GalaxyMap NewMap()
        {
            var map = new GalaxyMap();
            map.AddSystem(new StarSystem(CapitalId, "試験首都星", Vector2.zero, Faction.同盟));
            map.AddSystem(new StarSystem(FrontierId, "試験辺境星", new Vector2(1f, 0f), Faction.同盟));
            return map;
        }

        private static Dictionary<int, Province> NewProvinces()
            => new Dictionary<int, Province>
            {
                { CapitalId, new Province(CapitalId, "", 1000f) },
                { FrontierId, new Province(FrontierId, "", 600f) },
            };

        /// <summary>職業官僚（政治家でない文民）：正七位上・考課 平均6.0（4回）。文才は政治家より低い＝旧シードで後ろの省に入る。</summary>
        private static Person NewBureaucrat(int id, int aptitude)
        {
            var p = new Person(id, "試験官僚" + id, Faction.同盟, PersonRole.文民)
            { birthYear = 750, courtRank = CourtRank.正七位上, operation = aptitude, intelligence = aptitude };
            p.merit = new OfficialMerit(id) { evaluations = 4, cumulativeScore = 24f }; // 平均6.0
            return p;
        }

        private static List<Person> NewCivilians()
        {
            var list = new List<Person>();
            for (int i = 0; i < PoliticianCount; i++)
                list.Add(new Person(PremierId + i, "試験政治家" + (PremierId + i), Faction.同盟, PersonRole.文民)
                { isPolitician = true, birthYear = 760, charisma = 60, operation = 90 - i, intelligence = 90 });
            for (int i = 0; i < BureaucratCount; i++) list.Add(NewBureaucrat(BureaucratId + i, 30 - i));
            return list;
        }

        private GalaxyView NewView(GalaxyMap map, Dictionary<int, Province> provinces, List<Person> civilians)
        {
            var go = new GameObject("GalaxyView_BureaucracyObserverQa");
            go.SetActive(false); // Start を走らせない（盤面は差し込む）
            spawned.Add(go);
            GalaxyView view = go.AddComponent<GalaxyView>();
            view.BindElectionQaWorld(map, provinces, new List<Person>(), civilians);
            return view;
        }

        /// <summary>同盟（共和制）を張り、政府をシードした GalaxyView を返す（省庁は旧シードで満員＝台帳はまだ無い）。</summary>
        private GalaxyView StartAlliance(out CampaignState campaign, out FactionState alliance, out List<Person> civilians)
        {
            GalaxyMap map = NewMap();
            Dictionary<int, Province> provinces = NewProvinces();
            civilians = NewCivilians();
            campaign = new CampaignState(map);
            alliance = new FactionState(Faction.同盟) { governmentForm = GovernmentForm.共和制, politics = new PoliticsState() };
            campaign.states.Add(alliance);
            StrategySession.Map = map;
            StrategySession.Campaign = campaign;
            StrategySession.Provinces = provinces;
            StrategySession.Clock = new GameClock { elapsedSeconds = YearSeconds + 1d };
            GalaxyView view = NewView(map, provinces, civilians);
            view.SeedGovernmentForQa();
            return view;
        }

        /// <summary>与党・首相を置き、最上位（太政官）を除く各省へ大臣を任命する＝太政官だけ承認権者が空席のまま残る。</summary>
        private static void FormCabinet(FactionState alliance, GalaxyView view, List<Person> roster, int year)
        {
            PoliticsState pol = alliance.politics;
            var ruling = new Party(1, "試験民政党", Faction.同盟) { leaderId = PremierId, support = 1f };
            for (int i = 0; i < PoliticianCount; i++) ruling.memberIds.Add(PremierId + i);
            pol.parties.Add(ruling);
            pol.government = new GovernmentFormation
            {
                status = CabinetStatus.単独過半, premierPersonId = PremierId, partyId = 1, formedYear = year, sourceElectionId = "qa",
            };

            List<Ministry> tree = Tree(view);
            int top = view.TopMinistryIdOf(Faction.同盟);
            CabinetAppointmentRules.Reconcile(pol, Faction.同盟, tree, top, roster, year, CabPrm);
            int next = PremierId + 1;
            for (int i = 0; i < tree.Count; i++)
            {
                if (tree[i].id == top) continue; // 太政官には大臣を置かない（承認権者が空席の省として残す）
                AppointmentResult r = CabinetAppointmentRules.TryAppoint(pol, Faction.同盟, PremierId, tree, top,
                    tree[i].id, CabinetPostKind.大臣, next, roster, year, "試験：承認権者を置く", CabPrm);
                Assert.IsTrue(r.ok, tree[i].ministryName + " に大臣を置けない：" + r.reason);
                next++;
            }
        }

        /// <summary>
        /// 与党・首相を置き、大臣を置く省のうち1つにだけ 大臣＋副大臣 を任命して、その省を返す（他省は承認権者が空席のまま）。
        /// 委任（所管決裁）の表示だけを問うため、内閣は Core の共通入口で決め打ちに組む。
        /// </summary>
        private static Ministry FormCabinetWithVice(FactionState alliance, GalaxyView view, List<Person> roster, int year)
        {
            PoliticsState pol = alliance.politics;
            var ruling = new Party(1, "試験民政党", Faction.同盟) { leaderId = PremierId, support = 1f };
            for (int i = 0; i < PoliticianCount; i++) ruling.memberIds.Add(PremierId + i);
            pol.parties.Add(ruling);
            pol.government = new GovernmentFormation
            {
                status = CabinetStatus.単独過半, premierPersonId = PremierId, partyId = 1, formedYear = year, sourceElectionId = "qa",
            };

            List<Ministry> tree = Tree(view);
            int top = view.TopMinistryIdOf(Faction.同盟);
            CabinetAppointmentRules.Reconcile(pol, Faction.同盟, tree, top, roster, year, CabPrm);

            List<Ministry> cabinetMinistries = CabinetAppointmentRules.CabinetMinistries(tree, top);
            Assert.Greater(cabinetMinistries.Count, 0, "前提：大臣を置く省が無い");
            Ministry host = cabinetMinistries[0];

            AppointmentResult m = CabinetAppointmentRules.TryAppoint(pol, Faction.同盟, PremierId, tree, top, host.id,
                CabinetPostKind.大臣, MinisterId, roster, year, "試験：所管大臣を置く", CabPrm);
            Assert.IsTrue(m.ok, host.ministryName + " に大臣を置けない：" + m.reason);
            AppointmentResult v = CabinetAppointmentRules.TryAppoint(pol, Faction.同盟, PremierId, tree, top, host.id,
                CabinetPostKind.副大臣, ViceId, roster, year, "試験：副大臣を置く", CabPrm);
            Assert.IsTrue(v.ok, host.ministryName + " に副大臣を置けない：" + v.reason);
            return host;
        }

        /// <summary>大臣本人が副大臣へ所管決裁を委任する（期限は SE<paramref name="until"/>）。</summary>
        private static void DelegateApproval(FactionState alliance, List<Person> roster, Ministry host, int year, int until)
        {
            AppointmentResult d = CabinetAppointmentRules.Delegate(alliance.politics, Faction.同盟, MinisterId, host.id,
                CabinetDelegation.所管決裁, until, roster, year, CabPrm);
            Assert.IsTrue(d.ok, "所管決裁を委任できない：" + d.reason);
        }

        // ===== 参照 =====

        private static List<Ministry> Tree(GalaxyView view) => new List<Ministry>(view.MinistriesOf(Faction.同盟));

        /// <summary>職業官僚が配属されている最初の省（旧シードの現況＝配属は動かさない）。</summary>
        private static Ministry MinistryWithBureaucrat(GalaxyView view, out int personId)
        {
            List<Ministry> tree = Tree(view);
            for (int i = 0; i < tree.Count; i++)
                for (int k = 0; k < tree[i].staffIds.Count; k++)
                    if (tree[i].staffIds[k] >= BureaucratId) { personId = tree[i].staffIds[k]; return tree[i]; }
            personId = -1;
            return null;
        }

        private static int CountStaffedBureaucrats(GalaxyView view)
        {
            int n = 0;
            List<Ministry> tree = Tree(view);
            for (int i = 0; i < tree.Count; i++)
                for (int k = 0; k < tree[i].staffIds.Count; k++)
                    if (tree[i].staffIds[k] >= BureaucratId) n++;
            return n;
        }

        private static Dictionary<int, List<int>> StaffSnapshot(GalaxyView view)
        {
            var snap = new Dictionary<int, List<int>>();
            List<Ministry> tree = Tree(view);
            for (int i = 0; i < tree.Count; i++) snap[tree[i].id] = new List<int>(tree[i].staffIds);
            return snap;
        }

        private static void AssertStaffUnchanged(GalaxyView view, Dictionary<int, List<int>> snap, string when)
        {
            List<Ministry> tree = Tree(view);
            Assert.AreEqual(snap.Count, tree.Count, when + "：省の数が変わった");
            for (int i = 0; i < tree.Count; i++)
                CollectionAssert.AreEqual(snap[tree[i].id], tree[i].staffIds, when + "：" + tree[i].ministryName + " の配属が変わった");
        }

        private static CivilServiceRecord Vacated(Ministry m, int personId, BureaucratGrade grade, CivilServiceStatus status,
            int appointedYear, int vacatedYear, string reason)
            => new CivilServiceRecord
            {
                ministryId = m.id,
                ministryName = m.ministryName,
                personId = personId,
                grade = grade,
                appointedYear = appointedYear,
                vacatedYear = vacatedYear,
                appointedById = PremierId,
                reason = reason,
                status = status
            };

        /// <summary>オブザーバを無効な GameObject に載せる（Awake＝UI構築を走らせず本文だけを取る）。</summary>
        private BureaucracyObserverOverlay NewDumpOverlay()
        {
            var go = new GameObject("BureaucracyObserverOverlay_Dump");
            go.SetActive(false);
            spawned.Add(go);
            return go.AddComponent<BureaucracyObserverOverlay>();
        }

        // ===== 1. 職位・在任年・官位・考課・段別定員・承認権者・履歴・台帳未登録 =====

        [UnityTest]
        public IEnumerator Observer_ShowsPostTitleTenureRankMeritSlotsApproverAndHistory()
        {
            GalaxyView view = StartAlliance(out CampaignState campaign, out FactionState alliance, out List<Person> civilians);
            FormCabinet(alliance, view, civilians, FirstYear);
            yield return null;
            Assert.AreEqual(FirstYear, view.ElectionYearForQa);
            Assert.AreEqual(BureaucratCount, CountStaffedBureaucrats(view), "前提：旧シードで職業官僚が省庁に配属されている");

            // --- 固定の台帳：1名だけ課長級（在任4年）・残りは台帳未登録のまま ---
            Ministry host = MinistryWithBureaucrat(view, out int chiefId);
            Assert.IsNotNull(host, "前提：職業官僚の配属先が見つからない");
            var st = new CivilServiceState();
            st.records.Add(new CivilServiceRecord
            {
                ministryId = host.id,
                ministryName = host.ministryName,
                personId = chiefId,
                grade = BureaucratGrade.課長級,
                appointedYear = FirstYear - TenureYears,
                appointedById = PremierId,
                reason = "試験：固定の在任記録",
                status = CivilServiceStatus.在任
            });
            // 履歴6件（上限5件＋超過1件）。退職/異動/昇任/降任/解任がすべて並ぶ
            st.history.Add(Vacated(host, BureaucratId + 5, BureaucratGrade.一般官僚, CivilServiceStatus.退職, 790, 791, "試験：死亡"));
            st.history.Add(Vacated(host, BureaucratId + 4, BureaucratGrade.一般官僚, CivilServiceStatus.異動, 790, 792, "試験：他省へ"));
            st.history.Add(Vacated(host, BureaucratId + 3, BureaucratGrade.一般官僚, CivilServiceStatus.降任, 790, 793, "試験：降任"));
            st.history.Add(Vacated(host, BureaucratId + 2, BureaucratGrade.課長級, CivilServiceStatus.解任, 790, 794, "試験：解任"));
            st.history.Add(Vacated(host, BureaucratId + 1, BureaucratGrade.一般官僚, CivilServiceStatus.退職, 790, 795, "試験：在野"));
            st.history.Add(Vacated(host, chiefId, BureaucratGrade.一般官僚, CivilServiceStatus.昇任, 793, FirstYear - TenureYears, "試験：昇任"));
            st.historyDropped = 2;
            alliance.civilService = st;

            GalaxyView.SwapActiveForQa(view);
            Dictionary<int, List<int>> seeded = StaffSnapshot(view);
            BureaucracyObserverOverlay overlay = NewDumpOverlay();
            string text = overlay.BuildDumpForQa();

            // 職位名（GradeTitle を再利用＝独自名称を持たない）・在任年・官位・考課
            string title = CivilServicePostRules.GradeTitle(host.ministryName, BureaucratGrade.課長級);
            StringAssert.Contains(title, text, "職位名が出ない");
            StringAssert.Contains("在任 " + TenureYears + "年", text, "在任年（現在年−就任年）が出ない");
            StringAssert.Contains("官位 " + CourtRank.正七位上, text, "官位が出ない");
            StringAssert.Contains("考課 6.0（4回）", text, "考課（平均・回数）が出ない");

            // 段別 在任/定員（定員は SlotsFor が出所）
            CivilServicePostParams prm = CivilServicePostParams.Default;
            StringAssert.Contains("課長 1/" + CivilServicePostRules.SlotsFor(host, BureaucratGrade.課長級, prm), text, "課長級の在任/定員が出ない");
            StringAssert.Contains("局長 0/" + CivilServicePostRules.SlotsFor(host, BureaucratGrade.局長級, prm), text, "局長級の在任/定員が出ない");
            StringAssert.Contains("次官 0/" + CivilServicePostRules.SlotsFor(host, BureaucratGrade.事務次官級, prm), text, "事務次官級の在任/定員が出ない");
            StringAssert.Contains("一般 0/" + CivilServicePostRules.SlotsFor(host, BureaucratGrade.一般官僚, prm), text,
                "一般官僚の在任/定員（省の配属定員）が出ない");

            // 承認権者：首相／所管大臣は任命可能（上申先つき）・大臣のいない太政官は承認者空席
            StringAssert.Contains("任命可能", text, "承認権者が居るのに任命可能が出ない");
            StringAssert.Contains("上申先", text, "上申先（承認権者）が出ない");
            StringAssert.Contains("承認者空席", text, "大臣のいない省の承認者空席が出ない");
            StringAssert.Contains("試験政治家" + PremierId, text, "承認権者（首相）の名前が出ない");

            // 台帳未登録（理由つき＝政治家・未移行を隠さない）
            StringAssert.Contains("台帳未登録", text, "台帳に無い旧配属者が明示されない");
            StringAssert.Contains("政治家", text, "台帳へ載せられない理由（政治家）が出ない");
            StringAssert.Contains("台帳へ未移行", text, "まだ移行していない職業官僚の理由が出ない");

            // 履歴（新しい順・最大5件・超過は件数）
            StringAssert.Contains("人事履歴", text);
            StringAssert.Contains("昇任", text, "昇任の履歴が出ない");
            StringAssert.Contains("解任", text, "解任の履歴が出ない");
            StringAssert.Contains("降任", text, "降任の履歴が出ない");
            StringAssert.Contains("異動", text, "異動の履歴が出ない");
            StringAssert.Contains("…ほか 1件", text, "上限を超えた履歴が件数で示されない");
            StringAssert.Contains("上限で捨てた 2件", text, "黙って捨てた履歴の件数が示されない");
            Assert.IsFalse(text.Contains("試験：死亡"), "6件目（最も古い履歴）まで載せている＝上限が効いていない");

            // 台帳の在任・履歴の要約
            StringAssert.Contains("在任 1名", text);
            StringAssert.Contains("履歴 6件", text);

            // --- 観測専用：何度呼んでも入力（台帳・配属）が変わらず、同じ本文になる ---
            string again = overlay.BuildDumpForQa();
            Assert.AreEqual(text, again, "同じ盤面で本文が変わった（観測が状態に触れている疑い）");
            Assert.AreEqual(1, st.records.Count, "台帳の在任記録が変わった");
            Assert.AreEqual(6, st.history.Count, "台帳の履歴が変わった");
            Assert.AreEqual(2, st.historyDropped, "捨てた件数が変わった");
            Assert.AreEqual(BureaucratGrade.課長級, st.records[0].grade, "在任記録の段が変わった");
            Assert.AreEqual(FirstYear - TenureYears, st.records[0].appointedYear, "就任年が変わった");
            AssertStaffUnchanged(view, seeded, "観測");
        }

        // ===== 2. 台帳が無い勢力（新規/旧セーブ） =====

        [UnityTest]
        public IEnumerator Observer_WithoutLedger_ShowsUninitialized_AndKeepsMinistryTree()
        {
            GalaxyView view = StartAlliance(out CampaignState campaign, out FactionState alliance, out List<Person> civilians);
            FormCabinet(alliance, view, civilians, FirstYear);
            yield return null;
            Assert.IsNull(alliance.civilService, "前提：人事台帳を持たない（新規/旧セーブ相当）");

            GalaxyView.SwapActiveForQa(view);
            Ministry host = MinistryWithBureaucrat(view, out int anyId);
            Assert.IsNotNull(host);
            Dictionary<int, List<int>> seeded = StaffSnapshot(view);
            BureaucracyObserverOverlay overlay = NewDumpOverlay();
            string text = overlay.BuildDumpForQa();

            StringAssert.Contains("人事台帳 未初期化", text, "台帳が無いことが明示されない");
            StringAssert.Contains(host.ministryName, text, "台帳が無いと省庁ツリーが出なくなった");
            StringAssert.Contains("試験官僚" + anyId, text, "配属官僚の一覧が出なくなった");
            StringAssert.Contains("台帳未登録", text, "台帳が無いときの配属者の扱いが出ない");
            StringAssert.Contains("承認者空席", text, "承認権者の状態が出ない");
            Assert.IsFalse(text.Contains("課長 0/"), "台帳が無いのに段別の在任数（0扱い）を出している");

            Assert.IsNull(alliance.civilService, "観測で台帳を作ってしまっている");
            AssertStaffUnchanged(view, seeded, "台帳なしの観測");
        }

        // ===== 3. 有効な所管決裁の委任を受けた副大臣（承認権者として隠さない） =====

        [UnityTest]
        public IEnumerator Observer_ShowsViceMinister_WithValidApprovalDelegation()
        {
            GalaxyView view = StartAlliance(out CampaignState campaign, out FactionState alliance, out List<Person> civilians);
            Ministry host = FormCabinetWithVice(alliance, view, civilians, FirstYear);
            yield return null;
            Assert.AreEqual(FirstYear, view.ElectionYearForQa);

            int until = FirstYear + DelegationYears;
            DelegateApproval(alliance, civilians, host, FirstYear, until);

            // 前提：Core は大臣・副大臣の双方に局長級の承認を認めている（表示が Core を先回りしていないことの土台）
            Assert.IsTrue(CivilServicePostRules.ApprovalAuthority(alliance.politics, Faction.同盟, MinisterId, host.id,
                BureaucratGrade.局長級, civilians, FirstYear).ok, "前提：所管大臣が承認できない");
            Assert.IsTrue(CivilServicePostRules.ApprovalAuthority(alliance.politics, Faction.同盟, ViceId, host.id,
                BureaucratGrade.局長級, civilians, FirstYear).ok, "前提：委任を受けた副大臣が承認できない");

            GalaxyView.SwapActiveForQa(view);
            Dictionary<int, List<int>> seeded = StaffSnapshot(view);
            BureaucracyObserverOverlay overlay = NewDumpOverlay();
            string text = overlay.BuildDumpForQa();

            // 大臣（任命可能・上申先）と副大臣（委任承認・期限）の両方を出す＝どちらも隠さない
            StringAssert.Contains("任命可能", text, "所管大臣が承認できるのに任命可能が出ない");
            StringAssert.Contains("試験政治家" + MinisterId, text, "上申先（所管大臣）の名前が出ない");
            StringAssert.Contains("委任承認", text, "有効な所管決裁の委任を受けた副大臣が承認権者として出ない");
            StringAssert.Contains("試験政治家" + ViceId, text, "委任を受けた副大臣の名前が出ない");
            StringAssert.Contains("SE" + until + "まで", text, "委任の期限が出ない");

            // 観測専用：何度呼んでも本文・委任の記録・配属が変わらない
            Assert.AreEqual(text, overlay.BuildDumpForQa(), "同じ盤面で本文が変わった（観測が状態に触れている疑い）");
            CabinetPost vice = CabinetAppointmentRules.FindPost(alliance.politics.cabinet, host.id, CabinetPostKind.副大臣);
            Assert.AreEqual(CabinetDelegation.所管決裁, vice.delegation, "観測で委任の範囲が変わった");
            Assert.AreEqual(until, vice.delegationEndYear, "観測で委任の期限が変わった");
            Assert.AreEqual(ViceId, vice.holderId, "観測で副大臣の在任者が変わった");
            AssertStaffUnchanged(view, seeded, "委任の観測");
        }

        // ===== 4. 期限切れの委任（可能と誤表示しない） =====

        [UnityTest]
        public IEnumerator Observer_HidesViceMinister_AfterDelegationExpired()
        {
            GalaxyView view = StartAlliance(out CampaignState campaign, out FactionState alliance, out List<Person> civilians);
            Ministry host = FormCabinetWithVice(alliance, view, civilians, FirstYear);
            yield return null;

            int until = FirstYear + DelegationYears;
            DelegateApproval(alliance, civilians, host, FirstYear, until);

            // 期限の翌年へ（内閣は整理しない＝委任の記録は残ったまま失効している状況）
            StrategySession.Clock = new GameClock { elapsedSeconds = (double)(until + 1 - TimeDisplay.StartYear) * YearSeconds + 1d };
            GalaxyView.SwapActiveForQa(view);
            int year = view.ElectionYearForQa;
            Assert.Greater(year, until, "前提：委任の期限を過ぎた暦年になっていない");
            Assert.IsFalse(CivilServicePostRules.ApprovalAuthority(alliance.politics, Faction.同盟, ViceId, host.id,
                BureaucratGrade.局長級, civilians, year).ok, "前提：Core は失効した委任を認めていない");

            BureaucracyObserverOverlay overlay = NewDumpOverlay();
            string text = overlay.BuildDumpForQa();

            Assert.IsFalse(text.Contains("委任承認"), "期限切れの委任を持つ副大臣を承認権者として出している");
            Assert.IsFalse(text.Contains("SE" + until + "まで"), "失効した委任の期限を出している");
            StringAssert.Contains("任命可能", text, "委任が切れただけで所管大臣の承認まで消えている");
            StringAssert.Contains("試験政治家" + MinisterId, text, "上申先（所管大臣）の名前が消えた");

            // 観測専用：失効した委任の記録にも触れない
            CabinetPost vice = CabinetAppointmentRules.FindPost(alliance.politics.cabinet, host.id, CabinetPostKind.副大臣);
            Assert.AreEqual(CabinetDelegation.所管決裁, vice.delegation, "観測で委任の範囲が変わった");
            Assert.AreEqual(until, vice.delegationEndYear, "観測で委任の期限が変わった");
        }

        // ===== 5. 既存UI（縦スクロールバー・表示切替）を壊していない =====

        [UnityTest]
        public IEnumerator Overlay_KeepsScrollbarAndVisibilityToggle()
        {
            var go = new GameObject("BureaucracyObserverOverlay_UI");
            spawned.Add(go);
            BureaucracyObserverOverlay overlay = go.AddComponent<BureaucracyObserverOverlay>();
            yield return null;

            Transform panel = go.transform.Find("BureaucracyObserverCanvas/ObserverPanel");
            Assert.IsNotNull(panel, "パネルの構造（Canvas/ObserverPanel）が変わった");
            Assert.IsFalse(panel.gameObject.activeSelf, "既定で開いている");

            ScrollRect scroll = go.GetComponentInChildren<ScrollRect>(true);
            Assert.IsNotNull(scroll, "スクロール領域が無い");
            Assert.IsTrue(scroll.vertical, "縦スクロールが無効になっている");
            Scrollbar bar = go.GetComponentInChildren<Scrollbar>(true);
            Assert.IsNotNull(bar, "縦スクロールバーが無い（UiScrollbars.Attach の配線が外れた）");
            Assert.IsNotNull(scroll.verticalScrollbar, "縦スクロールバーが ScrollRect に繋がっていない");

            overlay.SetVisible(true);
            Assert.IsTrue(panel.gameObject.activeSelf, "開けない");
            yield return null; // 表示中の Update（本文の更新）が例外なく回る
            overlay.Toggle();
            Assert.IsFalse(panel.gameObject.activeSelf, "閉じられない");
        }

        // ===== 6. 「官僚人事を開く」ボタン：押すと人事窓（CivilServiceAppointmentPanel）が開く =====

        [UnityTest]
        public IEnumerator Overlay_OpenAppointmentButton_OpensCivilServiceAppointmentPanel()
        {
            var go = new GameObject("BureaucracyObserverOverlay_UI2");
            spawned.Add(go);
            BureaucracyObserverOverlay overlay = go.AddComponent<BureaucracyObserverOverlay>();
            yield return null;

            Button btn = overlay.OpenAppointmentButtonForTest;
            Assert.IsNotNull(btn, "「官僚人事を開く」ボタンが無い");
            overlay.SetVisible(true);
            Assert.IsTrue(btn.gameObject.activeInHierarchy, "ボタンが表示されていない");

            btn.onClick.Invoke();
            Assert.IsTrue(CivilServiceAppointmentPanel.IsOpen, "ボタン押下で官僚人事窓が開かない");

            // 後始末：他の試験へ開いた窓を残さない（既存のパネル試験と同じく生成した窓ごと破棄する）
            CivilServiceAppointmentPanel panel = CivilServiceAppointmentPanel.InstanceForTest;
            if (panel != null) Object.DestroyImmediate(panel.gameObject);
        }
    }
}
