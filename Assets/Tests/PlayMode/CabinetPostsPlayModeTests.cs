using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 内閣の政治任用と党三役の Game 接続（<see cref="GalaxyView"/> の年次の政治 Tick・宰相の維持・開幕/読込の復元）を固定条件で通す PlayMode 試験：
    /// 初年の組閣で首相が大臣・副大臣・政務官を任命し党首が三役を任命する（資格・兼任・理由・空席理由）／職業官僚の配属と GovernmentRegistry を変えず艦隊の指揮権も付かない／
    /// 同じ年の再処理で増えない／JSON 往復→再構築で在任・履歴が戻り読込だけで任命しない／閣僚の死亡で失職／首相の死亡で職務執行→翌年の新首相で総辞職と新組閣＝旧権限が残らない／
    /// 政府・政治オブザーバに表示。GalaxyView は無効な GameObject に載せて Start を走らせない。セーブファイルは読み書きしない（JSON は文字列の往復だけ）。
    /// </summary>
    public class CabinetPostsPlayModeTests
    {
        private const int YearSeconds = 60 * 30 * 12; // GameDate.DateParams.Default の1年（game-秒）
        private const int FirstYear = 797;
        private const int CapitalId = 1, FrontierId = 2;
        private const int BureaucratId = 30;

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
            PoliticsObserverOverlay po = Object.FindAnyObjectByType<PoliticsObserverOverlay>();
            if (po != null) Object.DestroyImmediate(po.gameObject);
            GovernmentObserverOverlay go = Object.FindAnyObjectByType<GovernmentObserverOverlay>();
            if (go != null) Object.DestroyImmediate(go.gameObject);

            GalaxyView.SwapActiveForQa(savedActive); // 観測用に差し替えた Active を戻す（無効な GameObject は OnDestroy で解除しない）
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

        private static GalaxyMap NewMap()
        {
            var map = new GalaxyMap();
            map.AddSystem(new StarSystem(CapitalId, "試験首都星", Vector2.zero, Faction.同盟));
            map.AddSystem(new StarSystem(FrontierId, "試験辺境星", new Vector2(1f, 0f), Faction.同盟));
            return map;
        }

        private static Dictionary<int, Province> NewProvinces()
        {
            return new Dictionary<int, Province>
            {
                { CapitalId, new Province(CapitalId, "", 1000f) },
                { FrontierId, new Province(FrontierId, "", 600f) },
            };
        }

        /// <summary>同盟の文民政治家14名（ID 11..24）と政治家でない職業官僚1名（ID 30）。</summary>
        private static List<Person> NewCivilians()
        {
            var list = new List<Person>();
            for (int i = 0; i < 14; i++)
            {
                int id = 11 + i;
                list.Add(new Person(id, "試験政治家" + id, Faction.同盟, PersonRole.文民)
                {
                    isPolitician = true, birthYear = 760, charisma = 40 + i * 3, intelligence = 45 + i * 2, operation = 60 - i,
                });
            }
            list.Add(new Person(BureaucratId, "試験官僚", Faction.同盟, PersonRole.文民) { birthYear = 770, operation = 95, intelligence = 95 });
            return list;
        }

        private GalaxyView NewView(GalaxyMap map, Dictionary<int, Province> provinces, List<Person> civilians)
        {
            var go = new GameObject("GalaxyView_CabinetQa");
            go.SetActive(false);
            spawned.Add(go);
            GalaxyView view = go.AddComponent<GalaxyView>();
            view.BindElectionQaWorld(map, provinces, new List<Person>(), civilians);
            return view;
        }

        private static List<Office> OfficesOf(int personId)
        {
            var list = new List<Office>();
            IReadOnlyList<GovernmentRegistry.Appointment> all = GovernmentRegistry.Appointments;
            for (int i = 0; i < all.Count; i++)
                if (all[i].holder != null && all[i].holder.Id == personId) list.Add(all[i].office);
            return list;
        }

        private static int CountMessages(long afterSeq, string needle)
        {
            int n = 0;
            List<Notification> list = NotificationCenter.Since(afterSeq);
            for (int i = 0; i < list.Count; i++)
                if (list[i].message.Contains(needle)) n++;
            return n;
        }

        private static Dictionary<int, List<int>> StaffSnapshot(GalaxyView view)
        {
            var snap = new Dictionary<int, List<int>>();
            IReadOnlyList<Ministry> mins = view.MinistriesOf(Faction.同盟);
            for (int i = 0; i < mins.Count; i++) snap[mins[i].id] = new List<int>(mins[i].staffIds);
            return snap;
        }

        private static void AssertStaffUnchanged(GalaxyView view, Dictionary<int, List<int>> snap, string when)
        {
            IReadOnlyList<Ministry> mins = view.MinistriesOf(Faction.同盟);
            Assert.AreEqual(snap.Count, mins.Count, when + "：省の数が変わった");
            for (int i = 0; i < mins.Count; i++)
                CollectionAssert.AreEqual(snap[mins[i].id], mins[i].staffIds, when + "：" + mins[i].ministryName + " の職業官僚の配属が変わった");
        }

        private static Dictionary<string, int> Holders(CabinetState cab)
        {
            var d = new Dictionary<string, int>();
            foreach (CabinetPost p in cab.posts) d[p.ministryId + ":" + p.kind] = p.holderId;
            return d;
        }

        /// <summary>在任の整合：首相でない・一人一職・知事でない・与党か無所属・首相の任命・理由あり、空席には理由。艦隊の指揮権・政府役職なし。</summary>
        private static void AssertCabinetConsistent(PoliticsState pol, List<Person> people, string when)
        {
            CabinetState cab = pol.cabinet;
            GovernmentFormation g = pol.government;
            var seen = new HashSet<int>();
            foreach (CabinetPost p in cab.posts)
            {
                if (p.holderId < 0)
                {
                    Assert.IsFalse(string.IsNullOrEmpty(p.vacancyReason), when + "：" + CabinetAppointmentRules.PostTitle(p) + " の空席理由がない");
                    continue;
                }
                Assert.IsTrue(seen.Add(p.holderId), when + "：人物#" + p.holderId + " が閣僚職を兼任");
                Assert.AreNotEqual(g.premierPersonId, p.holderId, when + "：首相が閣僚職を兼任");
                Person person = people.Find(x => x.id == p.holderId);
                Assert.IsNull(CabinetAppointmentRules.PersonProblem(person, Faction.同盟), when + "：資格のない閣僚");
                Assert.Less(LocalElectionRules.GovernedSystemOf(pol, p.holderId, -1), 0, when + "：知事が閣僚");
                Party own = ElectionCycleRules.PartyOf(pol.parties, p.holderId);
                Assert.IsTrue(own == null || own.id == g.partyId, when + "：野党の党員が閣僚");
                Assert.AreEqual(cab.premierPersonId, p.appointedById, when + "：現首相でない任命者");
                StringAssert.Contains("選定理由", p.appointmentReason);
                Assert.AreEqual(0, OfficesOf(p.holderId).Count, when + "：閣僚に GovernmentRegistry の役職が付いた");
                Assert.IsFalse(CabinetAppointmentRules.Authority(pol, Faction.同盟, p.holderId, p.ministryId, CabinetAction.艦隊作戦指揮, people, 0).ok);
            }
        }

        [UnityTest]
        public IEnumerator AnnualTick_FormsCabinetAndPartyExecutives_SurvivesSave_DeathAndPremierChangeClearAuthority()
        {
            GalaxyMap map = NewMap();
            Dictionary<int, Province> provinces = NewProvinces();
            List<Person> civilians = NewCivilians();
            var campaign = new CampaignState(map);
            var alliance = new FactionState(Faction.同盟) { governmentForm = GovernmentForm.共和制, politics = new PoliticsState() };
            alliance.politics.parties.Add(new Party(1, "試験民政党", Faction.同盟) { support = 0.6f });
            alliance.politics.parties.Add(new Party(2, "試験進歩党", Faction.同盟) { support = 0.4f });
            campaign.states.Add(alliance);
            StrategySession.Map = map;
            StrategySession.Campaign = campaign;
            StrategySession.Provinces = provinces;
            StrategySession.Clock = new GameClock { elapsedSeconds = YearSeconds + 1d };

            GalaxyView view = NewView(map, provinces, civilians);
            view.SeedGovernmentForQa();
            Assert.AreEqual(FirstYear, view.ElectionYearForQa);
            Dictionary<int, List<int>> staff = StaffSnapshot(view);

            // --- 1. 初年：組閣と党三役 ---
            long seq = NotificationCenter.LastSeq;
            view.RunPoliticsTickForQa();
            yield return null;
            PoliticsState pol = alliance.politics;
            GovernmentFormation g = pol.government;
            Assert.IsNotNull(g);
            int premier = g.premierPersonId;
            Assert.GreaterOrEqual(premier, 11, "組閣で首相が決まる");
            CabinetState cab = pol.cabinet;
            Assert.IsNotNull(cab, "内閣の台帳がない");
            Assert.AreEqual(premier, cab.premierPersonId);
            Assert.AreEqual(12, cab.posts.Count, "太政官の下の4省×3職");
            Assert.GreaterOrEqual(CabinetAppointmentRules.FilledCount(cab, CabinetPostKind.大臣), 1, "大臣が任命されない");
            AssertCabinetConsistent(pol, civilians, "初年");
            AssertStaffUnchanged(view, staff, "初年");
            Assert.AreEqual(1, CountMessages(seq, "内閣の任命"), "組閣の通知は1通");
            Assert.AreEqual(0, OfficesOf(BureaucratId).Count, "職業官僚が政治任用された");
            Assert.IsNull(CabinetAppointmentRules.PostHeldBy(cab, BureaucratId), "政治家でない官僚が閣僚に");

            CabinetPost minister = null;
            foreach (CabinetPost p in cab.posts) if (p.kind == CabinetPostKind.大臣 && p.holderId >= 0) { minister = p; break; }
            Assert.IsTrue(CabinetAppointmentRules.Authority(pol, Faction.同盟, minister.holderId, minister.ministryId, CabinetAction.所管決裁, civilians, FirstYear).ok);
            Assert.IsFalse(OfficeRules.CanPropose(OfficesOf(minister.holderId), OfficeDomain.軍事, OfficeScope.国家), "大臣が全軍の指揮権を持った");

            Party ruling = ElectionCycleRules.FindParty(pol.parties, g.partyId);
            Assert.AreEqual(premier, ruling.leaderId);
            int executives = 0;
            foreach (PartyPost post in PartyExecutiveRules.ExecutivePosts)
            {
                PartyAppointment a = PartyExecutiveRules.AppointmentOf(ruling, post);
                if (a == null)
                {
                    Assert.IsFalse(string.IsNullOrEmpty(PartyExecutiveRules.VacancyReason(ruling, post)), post + " の空席理由がない");
                    continue;
                }
                executives++;
                Assert.IsTrue(PartyOrganizationRules.IsMember(ruling, a.holderId));
                Assert.AreNotEqual(ruling.leaderId, a.holderId);
                Assert.AreEqual(ruling.leaderId, a.appointedById);
                Assert.IsNull(CabinetAppointmentRules.PostHeldBy(cab, a.holderId), "党三役と閣僚を兼任");
                Assert.IsFalse(PartyExecutiveRules.Authority(ruling, Faction.同盟, a.holderId, PartyExecutiveAction.政府決裁, civilians, FirstYear).ok);
                Assert.Less(LocalElectionRules.GovernedSystemOf(pol, a.holderId, -1), 0, "現職知事（人物#" + a.holderId + "）が党三役に就いた");
                Assert.AreEqual(0, OfficesOf(a.holderId).Count, "党三役に政府役職が付いた");
            }

            // --- 2. 同じ年の再処理：履歴・通知が増えない ---
            int cabHistory = cab.history.Count;
            int partyHistory = ruling.postHistory.Count;
            Dictionary<string, int> holders = Holders(cab);
            long seq2 = NotificationCenter.LastSeq;
            view.RunPoliticsTickForQa();
            view.RunCivilAppointmentTickForQa();
            Assert.AreEqual(cabHistory, cab.history.Count, "同じ年に任免をやり直した");
            Assert.AreEqual(partyHistory, ruling.postHistory.Count);
            CollectionAssert.AreEquivalent(holders, Holders(cab));
            Assert.AreEqual(0, CountMessages(seq2, "内閣"));
            Assert.AreEqual(0, CountMessages(seq2, "党三役"));

            // --- 3. JSON 往復→再構築（読込だけで任命しない） ---
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
            PoliticsState after = CampaignRules.GetState(loaded, Faction.同盟).politics;
            CabinetState cab2 = after.cabinet;
            Party ruling2 = ElectionCycleRules.FindParty(after.parties, ruling.id);
            CollectionAssert.AreEquivalent(holders, Holders(cab2), "在任が復元されない");
            Assert.AreEqual(cabHistory, cab2.history.Count, "読込で内閣の履歴が変わった");
            Assert.AreEqual(partyHistory, ruling2.postHistory.Count, "読込で党三役の履歴が変わった");
            Assert.AreEqual(executives, CountExecutives(ruling2));
            Assert.AreEqual(0, CountMessages(seqLoad, "内閣"), "読込だけで内閣の通知が出た");
            Assert.AreEqual(0, CountMessages(seqLoad, "党三役"));
            rebuilt.RunPoliticsTickForQa();
            Assert.AreEqual(cabHistory, cab2.history.Count, "読込後の同じ年に任免をやり直した");
            CollectionAssert.AreEquivalent(holders, Holders(cab2));
            AssertCabinetConsistent(after, people, "復元後");
            Dictionary<int, List<int>> staff2 = StaffSnapshot(rebuilt);

            // --- 4. 閣僚の死亡：失職（理由・権限なし） ---
            CabinetPost deadPost = null;
            foreach (CabinetPost p in cab2.posts) if (p.kind == CabinetPostKind.大臣 && p.holderId >= 0) { deadPost = p; break; }
            int deadId = deadPost.holderId, deadMinistry = deadPost.ministryId;
            people.Find(x => x.id == deadId).deathYear = FirstYear;
            long seq4 = NotificationCenter.LastSeq;
            rebuilt.RunPoliticsTickForQa();
            Assert.AreNotEqual(deadId, deadPost.holderId, "死亡した大臣が在任のまま");
            Assert.IsNull(CabinetAppointmentRules.PostHeldBy(cab2, deadId));
            Assert.IsTrue(HasHistory(cab2, "失職", deadId), "失職の履歴がない");
            Assert.IsFalse(CabinetAppointmentRules.Authority(after, Faction.同盟, deadId, deadMinistry, CabinetAction.所管決裁, people, FirstYear).ok);
            Assert.AreEqual(1, CountMessages(seq4, "失職（死亡"), "閣僚の失職の通知");
            AssertCabinetConsistent(after, people, "閣僚の死亡後");

            // --- 5. 首相の死亡：職務執行（または新首相で総辞職） ---
            Person premierPerson = people.Find(x => x.id == premier);
            premierPerson.deathYear = FirstYear;
            rebuilt.RunCivilAppointmentTickForQa();
            if (after.government.premierPersonId < 0)
            {
                Assert.IsTrue(cab2.caretaker, "首相不在で職務執行内閣にならない");
                Assert.AreEqual(FirstYear + 1, cab2.caretakerUntilYear);
                foreach (CabinetPost p in cab2.posts)
                {
                    if (p.holderId < 0) continue;
                    Assert.IsFalse(CabinetAppointmentRules.Authority(after, Faction.同盟, p.holderId, p.ministryId, CabinetAction.所管政策決定, people, FirstYear).ok, "職務執行で政策決定");
                    Assert.AreEqual(CabinetDelegation.なし, p.delegation);
                }
                Assert.IsFalse(CabinetAppointmentRules.Authority(after, Faction.同盟, premier, deadMinistry, CabinetAction.閣僚任免, people, FirstYear).ok, "死亡した首相の任免権が残った");
            }

            // 翌年：総裁選→新首相→前内閣の総辞職と新組閣
            StrategySession.Clock = new GameClock { elapsedSeconds = YearSeconds * 2d + 1d };
            Assert.AreEqual(FirstYear + 1, rebuilt.ElectionYearForQa);
            rebuilt.RunPoliticsTickForQa();
            int newPremier = after.government.premierPersonId;
            Assert.GreaterOrEqual(newPremier, 11, "翌年に新しい首相が決まらない");
            Assert.AreNotEqual(premier, newPremier);
            Assert.AreEqual(newPremier, cab2.premierPersonId);
            Assert.IsFalse(cab2.caretaker);
            Assert.IsTrue(HasHistory(cab2, "総辞職", -1), "前内閣の総辞職の履歴がない");
            foreach (CabinetPost p in cab2.posts)
                if (p.holderId >= 0) Assert.AreEqual(newPremier, p.appointedById, "旧首相の任命が残った");
            foreach (AppointmentHistoryEntry e in cab2.history)
                if (e.action == "退任" && e.year == FirstYear + 1 && CabinetAppointmentRules.PostHeldBy(cab2, e.personId) == null)
                    Assert.IsFalse(CabinetAppointmentRules.Authority(after, Faction.同盟, e.personId, e.ministryId, CabinetAction.所管決裁, people, FirstYear + 1).ok,
                        "退任した閣僚の権限が残った");
            AssertCabinetConsistent(after, people, "新組閣後");
            AssertStaffUnchanged(rebuilt, staff2, "政権交代後");
            Assert.AreEqual(0, OfficesOf(BureaucratId).Count);

            // --- 6. オブザーバ ---
            // 本番は GalaxyView.Start が Active を張る（観測層は省庁・人物名をそこから読む）。Start を走らせない試験では同じ参照を明示で張る（TearDown で戻す）。
            GalaxyView.SwapActiveForQa(rebuilt);
            var gov = new GameObject("GovernmentObserverOverlay").AddComponent<GovernmentObserverOverlay>();
            spawned.Add(gov.gameObject);
            var polOverlay = new GameObject("PoliticsObserverOverlay").AddComponent<PoliticsObserverOverlay>();
            spawned.Add(polOverlay.gameObject);
            yield return null;
            string govText = gov.DumpTextForTest;
            StringAssert.Contains("内閣（政治任用＝首相が任免）", govText);
            StringAssert.Contains("副大臣", govText);
            StringAssert.Contains("大臣政務官", govText);
            StringAssert.Contains("職業官僚", govText);
            StringAssert.DoesNotContain("省庁 未配線", govText, "政府オブザーバが GalaxyView の省庁を読めていない");
            // 省別の職業官僚（在籍/定員）＝本番の省庁台帳と同じ値
            IReadOnlyList<Ministry> govMins = rebuilt.MinistriesOf(Faction.同盟);
            foreach (CabinetPost p in cab2.posts)
            {
                if (p.kind != CabinetPostKind.大臣) continue;
                Ministry m = null;
                for (int i = 0; i < govMins.Count; i++) if (govMins[i] != null && govMins[i].id == p.ministryId) m = govMins[i];
                Assert.IsNotNull(m, p.ministryName + " の省が省庁台帳にない");
                StringAssert.Contains("職業官僚</color> " + m.staffIds.Count + "/" + m.staffSlots + "名", govText, p.ministryName + " の職業官僚の在籍/定員");
            }
            // 人物名（id 表記に落ちない）：首相と在任閣僚
            StringAssert.Contains("首相 " + people.Find(x => x.id == newPremier).name, govText);
            foreach (CabinetPost p in cab2.posts)
                if (p.holderId >= 0)
                    StringAssert.Contains(CabinetAppointmentRules.PostTitle(p) + " " + people.Find(x => x.id == p.holderId).name, govText);
            StringAssert.Contains("作戦指揮権", govText);
            StringAssert.Contains("任命理由", govText);
            StringAssert.Contains("任免履歴（新しい順）", govText);
            string polText = polOverlay.DumpTextForTest;
            StringAssert.Contains("党三役（党首が任免", polText);
            StringAssert.Contains("幹事長", polText);
            StringAssert.Contains("政府の決裁", polText);
        }

        /// <summary>試験の同盟（共和制・2党）を StrategySession に張り、政府をシードした GalaxyView を返す（初年の政治 Tick の直前）。</summary>
        private GalaxyView StartAlliance(out CampaignState campaign, out FactionState alliance, out List<Person> civilians)
        {
            GalaxyMap map = NewMap();
            Dictionary<int, Province> provinces = NewProvinces();
            civilians = NewCivilians();
            campaign = new CampaignState(map);
            alliance = new FactionState(Faction.同盟) { governmentForm = GovernmentForm.共和制, politics = new PoliticsState() };
            alliance.politics.parties.Add(new Party(1, "試験民政党", Faction.同盟) { support = 0.6f });
            alliance.politics.parties.Add(new Party(2, "試験進歩党", Faction.同盟) { support = 0.4f });
            campaign.states.Add(alliance);
            StrategySession.Map = map;
            StrategySession.Campaign = campaign;
            StrategySession.Provinces = provinces;
            StrategySession.Clock = new GameClock { elapsedSeconds = YearSeconds + 1d };
            GalaxyView view = NewView(map, provinces, civilians);
            view.SeedGovernmentForQa();
            return view;
        }

        /// <summary>全党の三役が知事でなく、政府役職（GovernmentRegistry）も持たない。</summary>
        private static void AssertExecutivesAreNotGovernors(PoliticsState pol, string when)
        {
            foreach (Party party in pol.parties)
                foreach (PartyPost post in PartyExecutiveRules.ExecutivePosts)
                {
                    PartyAppointment a = PartyExecutiveRules.AppointmentOf(party, post);
                    if (a == null) continue;
                    Assert.Less(LocalElectionRules.GovernedSystemOf(pol, a.holderId, -1), 0, when + "：現職知事（人物#" + a.holderId + "）が " + party.partyName + " " + post);
                    Assert.AreEqual(0, OfficesOf(a.holderId).Count, when + "：" + party.partyName + " " + post + "（人物#" + a.holderId + "）に政府役職が付いた");
                }
        }

        /// <summary>
        /// 旧版の不整合（現職知事が党三役に在任）を再現する：その職の在任者を党首が解任し、共通入口を通さず就任を直接書く
        /// （修正前の AI 補充で起きた状態・保存データに残りうる状態）。
        /// </summary>
        private static void InjectGovernorAsExecutive(Party party, PartyPost post, int governorId, List<Person> roster, int year)
        {
            int leader = party.leaderId;
            if (PartyExecutiveRules.AppointmentOf(party, post) != null)
                Assert.IsTrue(PartyExecutiveRules.Dismiss(party, Faction.同盟, leader, post, roster, year, "試験：知事の兼任を再現", CabinetParams.Default).ok);
            Assert.IsTrue(PartyOrganizationRules.AppointPost(party, post, governorId), "前提：知事が党員");
            PartyAppointment a = PartyExecutiveRules.AppointmentOf(party, post);
            a.appointedById = leader;
            a.appointedYear = year;
            a.reason = "試験：修正前の兼任";
        }

        private static AppointmentHistoryEntry LastPartyHistory(Party party, string action, int personId)
        {
            for (int i = party.postHistory.Count - 1; i >= 0; i--)
            {
                AppointmentHistoryEntry e = party.postHistory[i];
                if (e != null && e.action == action && e.personId == personId) return e;
            }
            return null;
        }

        /// <summary>
        /// 知事と党三役の兼任（#2768 の Game 接続試験の失敗の回帰）：初年の AI 補充で現職知事を三役に選ばない（候補の理由つき拒否）／
        /// 知事が三役に在任している状態は同じ年の年次整理で三役だけ失職（理由を通知・党の権限なし・知事職は残す）／読込の整理でも同じく失職（通知・再任命なし）。
        /// </summary>
        [UnityTest]
        public IEnumerator GovernorIsNotPartyExecutive_ConflictLosesPartyPost_OnAnnualTickAndLoad()
        {
            GalaxyView view = StartAlliance(out CampaignState campaign, out FactionState alliance, out List<Person> civilians);
            view.RunPoliticsTickForQa();
            yield return null;
            PoliticsState pol = alliance.politics;

            // --- 1. 初年：知事は三役にならない（候補から理由つきで外れる） ---
            AssertExecutivesAreNotGovernors(pol, "初年");
            int governorId = -1;
            Party governorParty = null;
            foreach (LocalElectionState rec in pol.locals)
            {
                if (rec == null || rec.governorPersonId < 0 || governorParty != null) continue;
                foreach (Party party in pol.parties)
                    if (PartyOrganizationRules.IsMember(party, rec.governorPersonId) && party.leaderId != rec.governorPersonId && party.leaderId >= 0)
                    {
                        governorId = rec.governorPersonId;
                        governorParty = party;
                        break;
                    }
            }
            Assert.IsNotNull(governorParty, "前提：党首でない党員の知事が選ばれている");
            StringAssert.Contains("知事と党三役は兼任しない",
                PartyExecutiveRules.CandidateProblem(pol, Faction.同盟, governorParty, governorId, civilians, CabinetParams.Default));
            int governorOffices = OfficesOf(governorId).Count;
            Assert.AreEqual(1, governorOffices, "知事職（総督・星系スコープ）の1つだけ");

            // --- 2. 知事が三役に在任（旧版の状態）→ 同じ年の年次整理で三役を失職・理由を通知・知事職は残る ---
            InjectGovernorAsExecutive(governorParty, PartyPost.幹事長, governorId, civilians, FirstYear);
            long seq = NotificationCenter.LastSeq;
            view.RunPoliticsTickForQa();
            Assert.AreNotEqual(governorId, PartyOrganizationRules.HolderOf(governorParty, PartyPost.幹事長), "知事が幹事長に残った");
            AppointmentHistoryEntry lost = LastPartyHistory(governorParty, "失職", governorId);
            Assert.IsNotNull(lost, "知事の三役失職の履歴がない");
            StringAssert.Contains("知事に就いた", lost.reason);
            Assert.GreaterOrEqual(CountMessages(seq, "知事に就いた"), 1, "失職の理由が通知されない");
            Assert.IsFalse(PartyExecutiveRules.Authority(governorParty, Faction.同盟, governorId, PartyExecutiveAction.党運営, civilians, FirstYear).ok,
                "失職した知事に幹事長の権限が残った");
            Assert.GreaterOrEqual(LocalElectionRules.GovernedSystemOf(pol, governorId, -1), 0, "三役の整理で知事の在任を消した");
            Assert.AreEqual(governorOffices, OfficesOf(governorId).Count, "三役の整理で知事職を外した");
            AssertExecutivesAreNotGovernors(pol, "年次整理後");

            // --- 3. 読込：保存に残った兼任も読込の整理で三役だけ失職（通知・再任命なし） ---
            InjectGovernorAsExecutive(governorParty, PartyPost.政調会長, governorId, civilians, FirstYear);
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
            PoliticsState after = CampaignRules.GetState(loaded, Faction.同盟).politics;
            Party party2 = ElectionCycleRules.FindParty(after.parties, governorParty.id);
            Assert.AreNotEqual(governorId, PartyOrganizationRules.HolderOf(party2, PartyPost.政調会長), "読込後も知事が政調会長に残った");
            AppointmentHistoryEntry lostOnLoad = LastPartyHistory(party2, "失職", governorId);
            Assert.IsNotNull(lostOnLoad);
            Assert.AreEqual(PartyPost.政調会長.ToString(), lostOnLoad.postLabel);
            StringAssert.Contains("知事に就いた", PartyExecutiveRules.VacancyReason(party2, PartyPost.政調会長));
            Assert.AreEqual(0, CountMessages(seqLoad, "党三役"), "読込だけで党三役の通知が出た");
            Assert.GreaterOrEqual(LocalElectionRules.GovernedSystemOf(after, governorId, -1), 0, "読込の整理で知事の在任を消した");
            Assert.AreEqual(1, OfficesOf(governorId).Count, "読込で知事職が戻らない");
            AssertExecutivesAreNotGovernors(after, "読込後");
        }

        private static int CountExecutives(Party p)
        {
            int n = 0;
            foreach (PartyPost post in PartyExecutiveRules.ExecutivePosts)
                if (PartyExecutiveRules.AppointmentOf(p, post) != null) n++;
            return n;
        }

        private static bool HasHistory(CabinetState cab, string action, int personId)
        {
            foreach (AppointmentHistoryEntry e in cab.history)
                if (e.action == action && (personId < 0 || e.personId == personId)) return true;
            return false;
        }
    }
}
