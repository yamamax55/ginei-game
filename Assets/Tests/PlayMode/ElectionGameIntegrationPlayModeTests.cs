using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 選挙の Game 接続（<see cref="GalaxyView"/> の実際の配線）を固定条件で通す PlayMode 試験。
    /// 初回選挙→首相（宰相職）・知事（総督職）の <see cref="GovernmentRegistry"/> 在任→年次の銓衡で上書きされない／
    /// JSON 往復→再構築で在任・議席・日程が戻り、読込だけで開票しない／死亡・拘束・所有変更で権限が残らず理由が出る／
    /// 首相と知事・複数星系の知事の兼任を拒否／選挙の職で艦隊の指揮権を得ない、を確かめる。
    /// GalaxyView は無効な GameObject に載せて Start（盤面構築・音・カメラ）を走らせず、試験用入口から本番の private 経路を呼ぶ。
    /// セーブファイルは読み書きしない（JSON は文字列の往復だけ）。変更した static は TearDown で戻す。
    /// </summary>
    public class ElectionGameIntegrationPlayModeTests
    {
        private const int YearSeconds = 60 * 30 * 12; // GameDate.DateParams.Default の1年（game-秒）
        private const int ElectionYear = 797;         // 試験の暦年＝開始年796の翌年
        private const int CapitalId = 1, FrontierId = 2, BorderId = 3, ImperialId = 4;
        private const int BureaucratId = 20;

        private readonly List<GameObject> spawned = new List<GameObject>();
        private CampaignState savedCampaign;
        private GalaxyMap savedMap;
        private Dictionary<int, Province> savedProvinces;
        private GameClock savedClock;
        private List<GovernmentRegistry.Appointment> savedAppointments;

        [SetUp]
        public void SetUp()
        {
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
            PoliticsObserverOverlay overlay = Object.FindAnyObjectByType<PoliticsObserverOverlay>();
            if (overlay != null) Object.DestroyImmediate(overlay.gameObject);

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

        // ===== 固定条件の世界 =====

        private sealed class World
        {
            public GalaxyMap map;
            public CampaignState campaign;
            public Dictionary<int, Province> provinces;
            public List<Person> civilians;
            public GalaxyView view;
            public FactionState Alliance => CampaignRules.GetState(campaign, Faction.同盟);
            public PoliticsState Pol => Alliance.politics;
        }

        private static GalaxyMap NewMap()
        {
            var map = new GalaxyMap();
            map.AddSystem(new StarSystem(CapitalId, "試験首都星", Vector2.zero, Faction.同盟));
            map.AddSystem(new StarSystem(FrontierId, "試験辺境星", new Vector2(1f, 0f), Faction.同盟));
            map.AddSystem(new StarSystem(BorderId, "試験国境星", new Vector2(2f, 0f), Faction.同盟));
            map.AddSystem(new StarSystem(ImperialId, "試験帝都", new Vector2(3f, 0f), Faction.帝国));
            return map;
        }

        private static Dictionary<int, Province> NewProvinces()
        {
            return new Dictionary<int, Province>
            {
                { CapitalId, new Province(CapitalId, "", 1000f) },
                { FrontierId, new Province(FrontierId, "", 600f) },
                { BorderId, new Province(BorderId, "", 400f) },
                { ImperialId, new Province(ImperialId, "", 800f) },
            };
        }

        /// <summary>同盟の文民政治家6名（ID 11..16）＋官位を持つ非政治家の官僚1名（旧来の銓衡なら宰相・総督に選ばれる）。</summary>
        private static List<Person> NewCivilians()
        {
            var list = new List<Person>();
            for (int i = 0; i < 6; i++)
            {
                int id = 11 + i;
                list.Add(new Person(id, "試験政治家" + id, Faction.同盟, PersonRole.文民)
                {
                    isPolitician = true, birthYear = 760, charisma = 40 + i * 7, intelligence = 50 + i * 3,
                });
            }
            list.Add(new Person(BureaucratId, "試験官僚", Faction.同盟, PersonRole.文民)
            {
                birthYear = 750, courtRank = GalaxyView.PremierRank, operation = 95, intelligence = 95,
            });
            return list;
        }

        private GalaxyView NewView(GalaxyMap map, Dictionary<int, Province> provinces, List<Person> civilians)
        {
            var go = new GameObject("GalaxyView_ElectionQa");
            go.SetActive(false); // Start（盤面構築）を走らせない
            spawned.Add(go);
            GalaxyView view = go.AddComponent<GalaxyView>();
            view.BindElectionQaWorld(map, provinces, new List<Person>(), civilians);
            return view;
        }

        /// <summary>共和制の同盟（2党）と君主制の帝国を SE797 に置き、開幕の政府シードまで行う（選挙はまだ）。</summary>
        private World NewWorld()
        {
            var w = new World { map = NewMap(), provinces = NewProvinces(), civilians = NewCivilians() };
            w.campaign = new CampaignState(w.map);
            var alliance = new FactionState(Faction.同盟) { governmentForm = GovernmentForm.共和制, politics = new PoliticsState() };
            alliance.politics.parties.Add(new Party(1, "試験民政党", Faction.同盟) { support = 0.6f });
            alliance.politics.parties.Add(new Party(2, "試験進歩党", Faction.同盟) { support = 0.4f });
            w.campaign.states.Add(alliance);
            w.campaign.states.Add(new FactionState(Faction.帝国) { governmentForm = GovernmentForm.君主制 });

            StrategySession.Map = w.map;
            StrategySession.Campaign = w.campaign;
            StrategySession.Provinces = w.provinces;
            StrategySession.Clock = new GameClock { elapsedSeconds = YearSeconds + 1d };

            w.view = NewView(w.map, w.provinces, w.civilians);
            w.view.SeedGovernmentForQa();
            return w;
        }

        private static int HolderId(Office office, int scopeKey = 0)
        {
            ICharacter h = GovernmentRegistry.GetHolder(office, scopeKey);
            return h != null ? h.Id : -1;
        }

        private static int CountAppointments(int personId, Office office)
        {
            int n = 0;
            IReadOnlyList<GovernmentRegistry.Appointment> all = GovernmentRegistry.Appointments;
            for (int i = 0; i < all.Count; i++)
                if (all[i].holder != null && all[i].holder.Id == personId && (office == null || all[i].office == office)) n++;
            return n;
        }

        private static List<Office> OfficesOf(int personId)
        {
            var list = new List<Office>();
            IReadOnlyList<GovernmentRegistry.Appointment> all = GovernmentRegistry.Appointments;
            for (int i = 0; i < all.Count; i++)
                if (all[i].holder != null && all[i].holder.Id == personId) list.Add(all[i].office);
            return list;
        }

        /// <summary>兼任が無いこと：首相は知事職に就いていない／誰も複数星系の知事職を持たない。</summary>
        private static void AssertNoDualHolding(World w, string when)
        {
            Office governor = w.view.GovernorOfficeOf(Faction.同盟);
            int premier = HolderId(w.view.PremierOfficeOf(Faction.同盟));
            if (premier >= 0)
                Assert.AreEqual(0, CountAppointments(premier, governor), when + "：首相が知事職も持っている");
            var seen = new HashSet<int>();
            foreach (int sys in new[] { CapitalId, FrontierId, BorderId, ImperialId })
            {
                int id = HolderId(governor, sys);
                if (id < 0) continue;
                Assert.IsTrue(seen.Add(id), when + "：人物#" + id + " が複数星系の知事職を持っている");
            }
        }

        /// <summary>選挙で就いた職では艦隊の指揮権（国家規模の軍事所掌）を得ない。</summary>
        private static void AssertNoFleetCommand(int personId, string label)
        {
            List<Office> held = OfficesOf(personId);
            Assert.IsFalse(OfficeRules.CanPropose(held, OfficeDomain.軍事, OfficeScope.国家), label + " が全軍の指揮権を持った");
            Assert.IsFalse(OfficeRules.CanPropose(held, OfficeDomain.軍事, OfficeScope.星系), label + " が軍事所掌を持った");
            Assert.IsFalse(BattleCommandModeRules.CampaignChain(
                OfficeRules.CanPropose(held, OfficeDomain.軍事, OfficeScope.国家), "").commandsAll, label + " の会戦系統が総司令官になった");
        }

        private static int CountMessages(long afterSeq, string needle)
        {
            int n = 0;
            List<Notification> list = NotificationCenter.Since(afterSeq);
            for (int i = 0; i < list.Count; i++)
                if (list[i].message.Contains(needle)) n++;
            return n;
        }

        // ===== 1. 初回選挙→在任→年次の銓衡で上書きされない =====

        [UnityTest]
        public IEnumerator InauguralElection_SeatsPremierAndGovernors_AndAnnualAppointmentsDoNotOverwrite()
        {
            World w = NewWorld();
            Assert.AreEqual(ElectionYear, w.view.ElectionYearForQa);
            Office premierOffice = w.view.PremierOfficeOf(Faction.同盟);
            Office governorOffice = w.view.GovernorOfficeOf(Faction.同盟);
            Assert.IsNotNull(premierOffice);
            Assert.IsNotNull(governorOffice);
            Assert.AreEqual(-1, HolderId(premierOffice), "開幕シードだけでは首相は決まらない（選挙しない）");

            long seq = NotificationCenter.LastSeq;
            w.view.RunPoliticsTickForQa();
            yield return null;

            PoliticsState pol = w.Pol;
            Assert.IsTrue(ElectionCycleRules.IsSeated(pol), "初回選挙で両院が構成される");
            Assert.AreEqual(2, pol.recentResults.Count, "下院・上院の初回開票が1回ずつ");
            Assert.AreEqual(ElectionYear + 4, pol.lowerHouse.nextElectionYear);
            Assert.GreaterOrEqual(pol.government.premierPersonId, 11, "第一党の党首が首相");
            int premier = pol.government.premierPersonId;
            Assert.AreEqual(premier, HolderId(premierOffice), "首相が宰相職に在任");
            Assert.AreEqual(1, CountMessages(seq, "初の下院選挙"));

            var governors = new Dictionary<int, int>();
            foreach (int sys in new[] { CapitalId, FrontierId, BorderId })
            {
                LocalElectionState rec = LocalElectionRules.Find(pol, sys);
                Assert.IsNotNull(rec, "所有星系ごとに知事選の台帳");
                Assert.GreaterOrEqual(rec.governorPersonId, 11, "星系#" + sys + " に当選者");
                Assert.AreEqual(rec.governorPersonId, HolderId(governorOffice, sys), "当選者が総督職（星系スコープ）に在任");
                governors[sys] = rec.governorPersonId;
            }
            Assert.IsNull(LocalElectionRules.Find(pol, ImperialId), "他勢力の星系に知事選は無い");
            Assert.IsNull(CampaignRules.GetState(w.campaign, Faction.帝国).politics, "君主制の帝国は選挙しない");
            AssertNoDualHolding(w, "初回選挙後");

            // 年次の宰相／総督銓衡（本番と同じ順）＝官位を持つ官僚がいても選挙結果を上書きしない
            w.view.RunCivilAppointmentTickForQa();
            w.view.RunGovernorAppointmentTickForQa();
            Assert.AreEqual(premier, HolderId(premierOffice), "宰相の銓衡が選出首相を上書きした");
            foreach (KeyValuePair<int, int> kv in governors)
                Assert.AreEqual(kv.Value, HolderId(governorOffice, kv.Key), "総督の銓衡が星系#" + kv.Key + " の知事を上書きした");
            Assert.AreEqual(0, CountAppointments(BureaucratId, null), "官位の銓衡で官僚が首相・知事に就いた");
            AssertNoDualHolding(w, "年次銓衡後");

            // 同じ年にもう一度回しても開票・通知は増えない
            long seq2 = NotificationCenter.LastSeq;
            w.view.RunPoliticsTickForQa();
            Assert.AreEqual(2, pol.recentResults.Count, "同じ年に再開票した");
            Assert.AreEqual(0, CountMessages(seq2, "選挙"), "同じ年の再処理で選挙通知が出た");
            Assert.AreEqual(premier, HolderId(premierOffice));

            // 選挙の職で艦隊の戦術指揮権を得ない
            AssertNoFleetCommand(premier, "首相");
            foreach (KeyValuePair<int, int> kv in governors) AssertNoFleetCommand(kv.Value, "知事#" + kv.Key);
            Assert.IsFalse(premierOffice.domain == OfficeDomain.軍事 || governorOffice.domain == OfficeDomain.軍事);
        }

        // ===== 2. JSON 往復→再構築→復元（読込だけで開票しない） =====

        [UnityTest]
        public IEnumerator SaveJsonRoundTrip_RebuildRestoresOfficesSeatsAndSchedules_WithoutRecount()
        {
            World w = NewWorld();
            w.view.RunPoliticsTickForQa();
            PoliticsState before = w.Pol;
            int premier = before.government.premierPersonId;
            CabinetStatus status = before.government.status;
            int lower1 = ElectionCycleRules.SeatsOf(before.lowerSeats, 1), lower2 = ElectionCycleRules.SeatsOf(before.lowerSeats, 2);
            int upper1 = ElectionCycleRules.SeatsOf(before.upperSeats, 1), upper2 = ElectionCycleRules.SeatsOf(before.upperSeats, 2);
            int lowerNext = before.lowerHouse.nextElectionYear, upperNext = before.upperHouse.nextElectionYear;
            var governors = new Dictionary<int, int>();
            var termEnds = new Dictionary<int, int>();
            foreach (int sys in new[] { CapitalId, FrontierId, BorderId })
            {
                governors[sys] = LocalElectionRules.Find(before, sys).governorPersonId;
                termEnds[sys] = LocalElectionRules.Find(before, sys).termEndYear;
            }

            // 文字列だけで往復（セーブファイルには触れない）
            string json = CampaignSerializer.ToJson(w.campaign, w.civilians);
            CampaignSaveData save = CampaignSerializer.Parse(json);
            Assert.IsNotNull(save);
            CampaignState loaded = CampaignSerializer.FromSaveData(save);
            List<Person> loadedPeople = CampaignSerializer.ReadPeople(save);
            Assert.AreEqual(w.civilians.Count, loadedPeople.Count);

            // シーン再構築：旧 GalaxyView を捨て、新しい盤面・人物・政府台帳で組み直す
            Object.DestroyImmediate(w.view.gameObject);
            yield return null;
            GalaxyMap map = loaded.map != null && loaded.map.systems.Count > 0 ? loaded.map : NewMap();
            Dictionary<int, Province> provinces = NewProvinces();
            StrategySession.Campaign = loaded;
            StrategySession.Map = map;
            StrategySession.Provinces = provinces;

            long seq = NotificationCenter.LastSeq;
            GalaxyView rebuilt = NewView(map, provinces, loadedPeople);
            rebuilt.SeedGovernmentForQa(); // 開幕・読込時と同じ（ここで RestoreElectedOffices）
            var w2 = new World { map = map, campaign = loaded, provinces = provinces, civilians = loadedPeople, view = rebuilt };
            PoliticsState after = w2.Pol;

            Office premierOffice = rebuilt.PremierOfficeOf(Faction.同盟);
            Office governorOffice = rebuilt.GovernorOfficeOf(Faction.同盟);
            Assert.AreEqual(premier, HolderId(premierOffice), "首相の在任が復元されない");
            Assert.AreEqual(status, after.government.status);
            foreach (KeyValuePair<int, int> kv in governors)
            {
                Assert.AreEqual(kv.Value, HolderId(governorOffice, kv.Key), "星系#" + kv.Key + " の知事の在任が復元されない");
                Assert.AreEqual(termEnds[kv.Key], LocalElectionRules.Find(after, kv.Key).termEndYear);
            }
            Assert.AreEqual(lower1, ElectionCycleRules.SeatsOf(after.lowerSeats, 1));
            Assert.AreEqual(lower2, ElectionCycleRules.SeatsOf(after.lowerSeats, 2));
            Assert.AreEqual(upper1, ElectionCycleRules.SeatsOf(after.upperSeats, 1));
            Assert.AreEqual(upper2, ElectionCycleRules.SeatsOf(after.upperSeats, 2));
            Assert.AreEqual(lowerNext, after.lowerHouse.nextElectionYear);
            Assert.AreEqual(upperNext, after.upperHouse.nextElectionYear);
            Assert.AreEqual(2, after.recentResults.Count, "読込で開票記録が増えた");
            Assert.AreEqual(0, CountMessages(seq, "選挙"), "読込だけで選挙通知が出た");
            Assert.AreEqual(0, CountMessages(seq, "首相に"), "読込だけで組閣通知が出た");
            AssertNoDualHolding(w2, "復元後");

            // 読込後の同じ年の年次処理でも開票しない・在任は変わらない
            rebuilt.RunPoliticsTickForQa();
            rebuilt.RunCivilAppointmentTickForQa();
            rebuilt.RunGovernorAppointmentTickForQa();
            Assert.AreEqual(2, after.recentResults.Count, "読込後の同じ年に再開票した");
            Assert.AreEqual(0, CountMessages(seq, "選挙"));
            Assert.AreEqual(premier, HolderId(premierOffice));
            foreach (KeyValuePair<int, int> kv in governors)
                Assert.AreEqual(kv.Value, HolderId(governorOffice, kv.Key));
            AssertNoFleetCommand(premier, "復元した首相");
        }

        // ===== 3. 死亡・拘束・所有者変更 =====

        [UnityTest]
        public IEnumerator DeathCaptivityAndOwnershipChange_RemoveAuthority_AndShowVacancyReasons()
        {
            World w = NewWorld();
            w.view.RunPoliticsTickForQa();
            PoliticsState pol = w.Pol;
            Office premierOffice = w.view.PremierOfficeOf(Faction.同盟);
            Office governorOffice = w.view.GovernorOfficeOf(Faction.同盟);
            int premier = pol.government.premierPersonId;
            int deadGovernor = LocalElectionRules.Find(pol, FrontierId).governorPersonId;
            int borderGovernor = LocalElectionRules.Find(pol, BorderId).governorPersonId;
            Person premierPerson = w.civilians.Find(p => p.id == premier);
            Person deadPerson = w.civilians.Find(p => p.id == deadGovernor);

            // 政治 Tick の後（同じ年の後段）に起きる：首相が捕虜・辺境星の知事が死去・国境星が帝国に占領
            premierPerson.captiveStatus = CaptiveStatus.捕虜;
            premierPerson.heldBy = Faction.帝国;
            deadPerson.deathYear = ElectionYear;
            w.map.GetSystem(BorderId).owner = Faction.帝国;

            long seq = NotificationCenter.LastSeq;
            w.view.RunCivilAppointmentTickForQa();
            w.view.RunGovernorAppointmentTickForQa();

            // 首相：捕虜の権限は残らず、再組閣または空席の理由が残る
            Assert.AreNotEqual(premier, HolderId(premierOffice), "捕虜の首相が宰相職に残った");
            Assert.AreEqual(0, OfficesOf(premier).Count, "捕虜の首相に役職が残った");
            GovernmentFormation g = pol.government;
            StringAssert.Contains("職務を続けられない", g.reason, "首相交代/空席の理由が無い");
            Assert.AreEqual(g.premierPersonId, HolderId(premierOffice), "台帳の首相と宰相職の在任が食い違う");

            // 死去した知事：権限なし・空席理由と補欠選挙
            Assert.AreEqual(0, OfficesOf(deadGovernor).Count, "死去した知事に役職が残った");
            Assert.AreNotEqual(deadGovernor, HolderId(governorOffice, FrontierId));
            LocalElectionState frontier = LocalElectionRules.Find(pol, FrontierId);
            Assert.AreEqual(-1, frontier.governorPersonId);
            Assert.AreEqual(LocalElectionStatus.失職, frontier.status);
            StringAssert.Contains("不在・死亡", frontier.reason);
            Assert.AreEqual(ElectionYear + 1, frontier.nextElectionYear, "補欠選挙は翌年");

            // 占領された星系：旧勢力の知事の権限なし・台帳から外れ、失職の理由を通知
            Assert.AreEqual(-1, HolderId(governorOffice, BorderId), "占領された星系に同盟の知事が残った");
            Assert.AreEqual(0, CountAppointments(borderGovernor, governorOffice), "占領星系の旧知事が知事職を持っている");
            Assert.IsNull(LocalElectionRules.Find(pol, BorderId), "手放した星系の知事選台帳が残った");
            Assert.AreEqual(1, CountMessages(seq, "星系が勢力を離れた"), "占領による失職の理由が通知されない");

            AssertNoDualHolding(w, "欠缺の処理後");
            if (g.premierPersonId >= 0) AssertNoFleetCommand(g.premierPersonId, "後任の首相");

            // 政治オブザーバに空席の理由が出る
            var overlay = new GameObject("PoliticsObserverOverlay").AddComponent<PoliticsObserverOverlay>();
            spawned.Add(overlay.gameObject);
            yield return null;
            string text = overlay.DumpTextForTest;
            StringAssert.Contains("職務を続けられない", text, "首相の交代/空席理由が表示されない");
            StringAssert.Contains("不在・死亡", text, "知事の空席理由が表示されない");
            StringAssert.Contains("知事 空席", text);
        }

        // ===== 4. 兼任の拒否（保存データの食い違い・選挙の無い年の年次処理） =====

        [UnityTest]
        public IEnumerator DualHolding_PremierAndGovernorOrTwoSystems_IsRejectedOnRestoreAndAnnualTick()
        {
            World w = NewWorld();
            w.view.RunPoliticsTickForQa();
            PoliticsState pol = w.Pol;
            int premier = pol.government.premierPersonId;
            int frontierGovernor = LocalElectionRules.Find(pol, FrontierId).governorPersonId;

            // 保存データを食い違わせる：首相を首都星の知事にも、辺境星の知事を国境星の知事にも載せる
            LocalElectionRules.Find(pol, CapitalId).governorPersonId = premier;
            LocalElectionRules.Find(pol, BorderId).governorPersonId = frontierGovernor;
            CampaignSaveData save = CampaignSerializer.Parse(CampaignSerializer.ToJson(w.campaign, w.civilians));
            CampaignState loaded = CampaignSerializer.FromSaveData(save);
            List<Person> people = CampaignSerializer.ReadPeople(save);

            Object.DestroyImmediate(w.view.gameObject);
            yield return null;
            GalaxyMap map = loaded.map != null && loaded.map.systems.Count > 0 ? loaded.map : NewMap();
            Dictionary<int, Province> provinces = NewProvinces();
            StrategySession.Campaign = loaded;
            StrategySession.Map = map;
            StrategySession.Provinces = provinces;
            GalaxyView rebuilt = NewView(map, provinces, people);
            rebuilt.SeedGovernmentForQa();
            var w2 = new World { map = map, campaign = loaded, provinces = provinces, civilians = people, view = rebuilt };
            PoliticsState after = w2.Pol;
            Office premierOffice = rebuilt.PremierOfficeOf(Faction.同盟);
            Office governorOffice = rebuilt.GovernorOfficeOf(Faction.同盟);

            Assert.AreEqual(premier, HolderId(premierOffice), "首相は宰相職に復元される");
            Assert.AreEqual(-1, HolderId(governorOffice, CapitalId), "首相が首都星の知事職にも就いた");
            StringAssert.Contains("首相と兼任できない", LocalElectionRules.Find(after, CapitalId).reason);
            Assert.AreEqual(frontierGovernor, HolderId(governorOffice, FrontierId), "星系ID最小の知事職は残る");
            Assert.AreEqual(-1, HolderId(governorOffice, BorderId), "1人が2星系の知事職に就いた");
            StringAssert.Contains("兼任できない", LocalElectionRules.Find(after, BorderId).reason);
            Assert.AreEqual(-1, LocalElectionRules.Find(after, BorderId).governorPersonId);
            AssertNoDualHolding(w2, "食い違ったデータの復元後");

            // 選挙の無い年の年次処理で兼任が生じても（例：再組閣で知事が首相に）総督銓衡の時点で外す
            LocalElectionRules.Find(after, FrontierId).governorPersonId = premier;
            rebuilt.RunCivilAppointmentTickForQa();
            rebuilt.RunGovernorAppointmentTickForQa();
            Assert.AreEqual(premier, HolderId(premierOffice));
            Assert.AreEqual(-1, HolderId(governorOffice, FrontierId), "首相と知事の兼任が年次処理で残った");
            Assert.AreEqual(0, CountAppointments(frontierGovernor, governorOffice), "台帳から外れた前知事の権限が残った");
            StringAssert.Contains("首相と兼任できない", LocalElectionRules.Find(after, FrontierId).reason);
            AssertNoDualHolding(w2, "年次処理後");
            AssertNoFleetCommand(premier, "首相");
        }
    }
}
