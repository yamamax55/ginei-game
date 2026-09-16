using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 官僚の年次人事（#141）の Game 接続（<see cref="GalaxyView"/> の年次 Tick と開幕/読込のシード）を固定条件で通す PlayMode 試験：
    /// 台帳の無い状態（新規/旧セーブ）で既存の省庁配属を<b>同じ省の一般官僚として1回だけ</b>移行する（配属は動かさない・政治家や資格のない人物は
    /// 登録せず理由を通知へ集約）／同じ年に二度回しても台帳が増えない（冪等）／在職年を満たすと所管大臣の承認で昇任する／
    /// 保存往復→再構築で在任・段・就任年・履歴が戻り、省庁の配属は台帳から復元される（旧自動配属で上書きしない）／
    /// <b>承認権者（所管大臣）が空席の省は空きがあっても埋めない</b>＝旧 <c>RunMinistryStaffingTick</c> の無承認配属が年次に走らない／
    /// 通知は勢力ごと1件・変更明細は上限つき。
    /// <para>GalaxyView は無効な GameObject に載せて Start を走らせない（盤面は <c>BindElectionQaWorld</c> で差し込む）。
    /// 内閣は選挙の乱数に依らせず、Core の共通入口（<see cref="CabinetAppointmentRules"/>）で決め打ちに組む＝承認権者の在否だけを操る。
    /// セーブファイルは読み書きしない（JSON は文字列の往復だけ）。</para>
    /// </summary>
    public class CivilServiceAnnualIntegrationPlayModeTests
    {
        private const int YearSeconds = 60 * 30 * 12; // GameDate.DateParams.Default の1年（game-秒）
        private const int FirstYear = 797;
        private const int CapitalId = 1, FrontierId = 2;
        private const int PremierId = 11;             // 政治家 11..20（11=首相・12〜=大臣）
        private const int PoliticianCount = 10;
        private const int BureaucratId = 31;          // 職業官僚 31..36（政治家でない文民）
        private const int BureaucratCount = 6;
        private const int NewcomerId = 40;            // 後から名簿に加わる職業官僚（入省の候補）
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

        /// <summary>職業官僚（政治家でない文民）：課長級の資格（正七位上・考課6.0）を満たす。文才は政治家より低い＝旧シードで後ろの省に入る。</summary>
        private static Person NewBureaucrat(int id, int aptitude)
        {
            var p = new Person(id, "試験官僚" + id, Faction.同盟, PersonRole.文民)
            { birthYear = 750, courtRank = CourtRank.正七位上, operation = aptitude, intelligence = aptitude };
            p.merit = new OfficialMerit(id) { evaluations = 4, cumulativeScore = 24f }; // 平均6.0
            return p;
        }

        /// <summary>政治家10名（文才 高＝太政官/式部省/民部省へ）＋職業官僚6名（文才 低＝大蔵省/兵部省へ）＝旧シードで16の定員が埋まる。</summary>
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
            var go = new GameObject("GalaxyView_CivilServiceQa");
            go.SetActive(false);
            spawned.Add(go);
            GalaxyView view = go.AddComponent<GalaxyView>();
            view.BindElectionQaWorld(map, provinces, new List<Person>(), civilians);
            return view;
        }

        /// <summary>同盟（共和制）を張り、政府をシードした GalaxyView を返す（省庁は旧シードで満員＝台帳はまだ無い＝旧セーブ相当）。</summary>
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

        /// <summary>与党（政治家全員）と首相を置き、太政官の下の各省へ大臣（＝局長級以下の承認権者）を任命する（選挙の乱数に依らない決め打ち）。</summary>
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
                if (tree[i].id == top) continue; // 太政官には大臣を置かない（承認権者が不在の省として残す）
                AppointmentResult r = CabinetAppointmentRules.TryAppoint(pol, Faction.同盟, PremierId, tree, top,
                    tree[i].id, CabinetPostKind.大臣, next, roster, year, "試験：承認権者を置く", CabPrm);
                Assert.IsTrue(r.ok, tree[i].ministryName + " に大臣を置けない：" + r.reason);
                next++;
            }
        }

        // ===== 参照 =====

        private static List<Ministry> Tree(GalaxyView view) => new List<Ministry>(view.MinistriesOf(Faction.同盟));

        private static Ministry MinistryNamed(GalaxyView view, string suffix)
        {
            List<Ministry> tree = Tree(view);
            for (int i = 0; i < tree.Count; i++)
                if (tree[i] != null && tree[i].ministryName.EndsWith(suffix)) return tree[i];
            return null;
        }

        private static CivilServiceState LedgerOf(CampaignState campaign)
            => CampaignRules.GetState(campaign, Faction.同盟).civilService;

        private static int CountAtGrade(CivilServiceState st, BureaucratGrade g)
        {
            int n = 0;
            for (int i = 0; i < st.records.Count; i++) if (st.records[i].grade == g) n++;
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

        /// <summary>台帳と省庁の配属が食い違わない：在任者は当該省に配属され、人物は1省1職位、同じ人物が2つの省に居ない。</summary>
        private static void AssertLedgerConsistent(GalaxyView view, CivilServiceState st, string when)
        {
            List<Ministry> tree = Tree(view);
            var serving = new HashSet<int>();
            for (int i = 0; i < st.records.Count; i++)
            {
                CivilServiceRecord r = st.records[i];
                Assert.IsTrue(r.IsServing, when + "：在任でない記録が残っている");
                Assert.IsTrue(serving.Add(r.personId), when + "：人物#" + r.personId + " が二重に在任");
                Ministry m = MinistryRules.Get(tree, r.ministryId);
                Assert.IsNotNull(m, when + "：台帳の省#" + r.ministryId + " が無い");
                Assert.IsTrue(m.staffIds.Contains(r.personId), when + "：" + m.ministryName + " の在任者#" + r.personId + " が配属されていない");
            }
            var staffed = new HashSet<int>();
            for (int i = 0; i < tree.Count; i++)
                for (int k = 0; k < tree[i].staffIds.Count; k++)
                    Assert.IsTrue(staffed.Add(tree[i].staffIds[k]), when + "：人物#" + tree[i].staffIds[k] + " が2つの省に配属されている");
        }

        private static List<string> MessagesSince(long seq, string needle)
        {
            var hits = new List<string>();
            List<Notification> list = NotificationCenter.Since(seq);
            for (int i = 0; i < list.Count; i++)
                if (list[i].message.Contains(needle)) hits.Add(list[i].message);
            return hits;
        }

        private static int Occurrences(string text, string needle)
        {
            int n = 0, at = 0;
            while ((at = text.IndexOf(needle, at, System.StringComparison.Ordinal)) >= 0) { n++; at += needle.Length; }
            return n;
        }

        /// <summary>統一クロックを指定の宇宙暦の年へ進める（開始年＝<see cref="FirstYear"/>−1・1年＝<see cref="YearSeconds"/>）。</summary>
        private static void SetYear(int year)
            => StrategySession.Clock = new GameClock { elapsedSeconds = YearSeconds * (double)(year - (FirstYear - 1)) + 1d };

        // ===== 1. 移行 → 冪等 → 昇任 → 保存往復 =====

        [UnityTest]
        public IEnumerator AnnualTick_MigratesExistingStaff_PromotesWithApproval_AndSurvivesSaveLoad()
        {
            GalaxyView view = StartAlliance(out CampaignState campaign, out FactionState alliance, out List<Person> civilians);
            FormCabinet(alliance, view, civilians, FirstYear);
            yield return null;
            Assert.AreEqual(FirstYear, view.ElectionYearForQa);
            Assert.IsNull(alliance.civilService, "前提：人事台帳を持たない（新規/旧セーブ相当）");
            Dictionary<int, List<int>> seeded = StaffSnapshot(view);
            Assert.AreEqual(BureaucratCount, CountStaffedBureaucrats(view), "前提：旧シードで職業官僚が省庁に配属されている");

            // --- 1. 初回：台帳を作り、既存の配属を同じ省の一般官僚として写す（配属は動かさない） ---
            long seq = NotificationCenter.LastSeq;
            view.RunCivilServiceAnnualTickForQa();
            CivilServiceState st = alliance.civilService;
            Assert.IsNotNull(st, "年次人事で台帳が初期化されない");
            AssertStaffUnchanged(view, seeded, "移行");
            AssertLedgerConsistent(view, st, "移行");
            Assert.AreEqual(BureaucratCount, st.records.Count, "職業官僚だけが台帳へ載る");
            for (int i = 0; i < st.records.Count; i++)
            {
                CivilServiceRecord r = st.records[i];
                Assert.GreaterOrEqual(r.personId, BureaucratId, "政治家を台帳へ載せた（人物#" + r.personId + "）");
                Assert.AreEqual(BureaucratGrade.一般官僚, r.grade, "移行は段を上げない");
                Assert.AreEqual(FirstYear, r.appointedYear);
                Assert.AreEqual(-1, r.appointedById, "移行は誰の任用でもない");
                StringAssert.Contains("移行", r.reason);
            }
            List<string> notices = MessagesSince(seq, "官僚の年次人事");
            Assert.AreEqual(1, notices.Count, "年次人事の通知は勢力ごと1件");
            StringAssert.Contains("同盟 官僚の年次人事", notices[0]);
            StringAssert.Contains("既存の配属を台帳へ移行 " + BureaucratCount + "名", notices[0]);
            StringAssert.Contains("政治家", notices[0], "登録できない理由が集約されない");

            // --- 2. 同じ年に二度回しても台帳は増えない（冪等） ---
            long seq2 = NotificationCenter.LastSeq;
            view.RunCivilServiceAnnualTickForQa();
            Assert.AreEqual(BureaucratCount, alliance.civilService.records.Count, "同じ年の再処理で台帳が増えた");
            AssertLedgerConsistent(view, st, "再処理");
            AssertStaffUnchanged(view, seeded, "再処理");
            List<string> again = MessagesSince(seq2, "官僚の年次人事");
            Assert.AreEqual(1, again.Count);
            StringAssert.DoesNotContain("台帳へ移行", again[0], "移行は1回だけ");

            // --- 3. 在職年を満たした年：所管大臣の承認で昇任する（配属は動かない） ---
            SetYear(FirstYear + 3);
            Assert.AreEqual(FirstYear + 3, view.ElectionYearForQa);
            long seq3 = NotificationCenter.LastSeq;
            view.RunCivilServiceAnnualTickForQa();
            int promoted = CountAtGrade(st, BureaucratGrade.課長級);
            Assert.AreEqual(4, promoted, "大臣のいる2省で各2名（年の上限）が課長級へ昇任する");
            AssertStaffUnchanged(view, seeded, "昇任");
            AssertLedgerConsistent(view, st, "昇任");
            List<string> promo = MessagesSince(seq3, "官僚の年次人事");
            Assert.AreEqual(1, promo.Count);
            StringAssert.Contains("昇任4", promo[0]);
            StringAssert.Contains("課長級）昇任", promo[0], "明細に省名・職位・事由が出ない");
            for (int i = 0; i < st.records.Count; i++)
            {
                if (st.records[i].grade != BureaucratGrade.課長級) continue;
                CabinetPost minister = CabinetAppointmentRules.FindPost(alliance.politics.cabinet, st.records[i].ministryId, CabinetPostKind.大臣);
                Assert.IsNotNull(minister);
                Assert.AreEqual(minister.holderId, st.records[i].appointedById, "承認したのは所管大臣でない");
            }

            // --- 4. 保存往復→再構築：在任・段・履歴が戻り、配属は台帳から復元される ---
            CampaignSaveData save = CampaignSerializer.Parse(CampaignSerializer.ToJson(campaign, civilians));
            CampaignState loaded = CampaignSerializer.FromSaveData(save);
            List<Person> people = CampaignSerializer.ReadPeople(save);
            Object.DestroyImmediate(view.gameObject);
            yield return null;
            GalaxyMap map2 = loaded.map != null && loaded.map.systems.Count > 0 ? loaded.map : NewMap();
            Dictionary<int, Province> provinces2 = NewProvinces();
            StrategySession.Campaign = loaded;
            StrategySession.Map = map2;
            StrategySession.Provinces = provinces2;
            long seqLoad = NotificationCenter.LastSeq;
            GalaxyView rebuilt = NewView(map2, provinces2, people);
            rebuilt.SeedGovernmentForQa();

            CivilServiceState st2 = LedgerOf(loaded);
            Assert.IsNotNull(st2, "台帳が保存されない");
            Assert.AreEqual(BureaucratCount, st2.records.Count);
            Assert.AreEqual(4, CountAtGrade(st2, BureaucratGrade.課長級), "段が戻らない");
            AssertLedgerConsistent(rebuilt, st2, "読込");
            Assert.AreEqual(BureaucratCount, CountStaffedBureaucrats(rebuilt), "台帳の在任者が配属へ復元されない");
            Assert.AreEqual(0, MessagesSince(seqLoad, "官僚の年次人事").Count, "読込だけで年次人事の通知が出た");

            // 読込後の年次：台帳を正として同期するだけ＝同じ人物を重ねて配属しない
            rebuilt.RunCivilServiceAnnualTickForQa();
            Assert.AreEqual(BureaucratCount, st2.records.Count, "読込後の年次で台帳が増えた");
            AssertLedgerConsistent(rebuilt, st2, "読込後の年次");
        }

        /// <summary>省庁に配属されている職業官僚（id 31 以上）の人数。</summary>
        private static int CountStaffedBureaucrats(GalaxyView view)
        {
            int n = 0;
            List<Ministry> tree = Tree(view);
            for (int i = 0; i < tree.Count; i++)
                for (int k = 0; k < tree[i].staffIds.Count; k++)
                    if (tree[i].staffIds[k] >= BureaucratId) n++;
            return n;
        }

        // ===== 2. 承認権者のいない省は埋めない／通知の上限 =====

        [UnityTest]
        public IEnumerator AnnualTick_LeavesVacancyWithoutApprover_FillsApprovedMinistry_AndCapsNoticeDetails()
        {
            GalaxyView view = StartAlliance(out CampaignState campaign, out FactionState alliance, out List<Person> civilians);
            FormCabinet(alliance, view, civilians, FirstYear);
            yield return null;
            view.RunCivilServiceAnnualTickForQa(); // 初回＝移行
            CivilServiceState st = alliance.civilService;
            Assert.AreEqual(BureaucratCount, st.records.Count);

            // 大蔵省＝大臣を解任して空き1（承認権者なし）／兵部省＝大臣は在任のまま空き1（承認権者あり）
            Ministry okura = MinistryNamed(view, "大蔵省");
            Ministry hyobu = MinistryNamed(view, "兵部省");
            Assert.IsNotNull(okura); Assert.IsNotNull(hyobu);
            Assert.Less(okura.id, hyobu.id, "前提：承認権者のいない省を先に走査する（省ID昇順）");
            Assert.IsTrue(CabinetAppointmentRules.Dismiss(alliance.politics, Faction.同盟, PremierId, okura.id,
                CabinetPostKind.大臣, civilians, FirstYear, "試験：承認権者を空席にする", CabPrm).ok);
            okura.staffSlots++;
            hyobu.staffSlots++;
            civilians.Add(NewBureaucrat(NewcomerId, 20)); // 卒業した文官（未配属の入省候補）

            SetYear(FirstYear + 1);
            long seq = NotificationCenter.LastSeq;
            view.RunCivilServiceAnnualTickForQa();

            Assert.IsFalse(okura.staffIds.Contains(NewcomerId), "承認権者のいない省へ無承認で配属した");
            Assert.AreEqual(3, okura.staffIds.Count, "空きは埋めないまま（定員を増やしただけ）");
            Assert.AreEqual(3, CivilServicePostRules.ServingCount(st, okura.id), "大蔵省の台帳に無承認の在任が書かれた");
            CivilServiceRecord hired = CivilServicePostRules.FindServing(st, NewcomerId);
            Assert.IsNotNull(hired, "承認権者のいる省へも入省できていない");
            Assert.AreEqual(hyobu.id, hired.ministryId, "承認権者のいない省へ入省させた");
            Assert.AreEqual(BureaucratGrade.一般官僚, hired.grade);
            CabinetPost hyobuMinister = CabinetAppointmentRules.FindPost(alliance.politics.cabinet, hyobu.id, CabinetPostKind.大臣);
            Assert.AreEqual(hyobuMinister.holderId, hired.appointedById, "承認したのは所管大臣でない");
            Assert.IsTrue(hyobu.staffIds.Contains(NewcomerId));
            AssertLedgerConsistent(view, st, "入省");

            List<string> notices = MessagesSince(seq, "官僚の年次人事");
            Assert.AreEqual(1, notices.Count);
            StringAssert.Contains("配属1", notices[0]);
            StringAssert.Contains("見送り", notices[0]);
            StringAssert.Contains("所管大臣", notices[0], "見送りの理由（承認権者の空席）が出ない");

            // --- 通知の明細は上限つき（6名の退職＝5件まで載せて残りは件数へ丸める） ---
            for (int i = 0; i < BureaucratCount; i++)
            {
                Person p = civilians.Find(x => x.id == BureaucratId + i);
                p.deathYear = FirstYear + 1;
            }
            SetYear(FirstYear + 2);
            long seq2 = NotificationCenter.LastSeq;
            view.RunCivilServiceAnnualTickForQa();

            Assert.AreEqual(1, st.records.Count, "死亡した在任者が整理されない");
            Assert.AreEqual(NewcomerId, st.records[0].personId);
            Assert.AreEqual(BureaucratCount, st.history.Count, "退任の履歴が残らない");
            for (int i = 0; i < st.history.Count; i++)
                Assert.AreEqual(CivilServiceStatus.退職, st.history[i].status);
            AssertLedgerConsistent(view, st, "退職");

            List<string> retired = MessagesSince(seq2, "官僚の年次人事");
            Assert.AreEqual(1, retired.Count, "退職が6件でも通知は1件");
            StringAssert.Contains("退職" + BureaucratCount, retired[0]);
            Assert.AreEqual(5, Occurrences(retired[0], "）退職"), "変更明細は5件まで");
            StringAssert.Contains("ほか1件", retired[0], "載せなかった明細を件数へ丸めない");
            List<Notification> all = NotificationCenter.Since(seq2);
            for (int i = 0; i < all.Count; i++)
                if (all[i].message == retired[0])
                {
                    Assert.AreEqual(NotificationCategory.人事, all[i].category);
                    Assert.AreEqual(NotificationSeverity.注意, all[i].severity, "退職を含む年は注意");
                }
        }
    }
}
