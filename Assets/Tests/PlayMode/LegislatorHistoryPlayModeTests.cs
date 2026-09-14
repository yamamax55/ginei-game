using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 国政議員の名簿（<see cref="LegislatorRosterRules"/>）の Game 接続を固定条件で通す PlayMode 試験。
    /// <see cref="GalaxyView"/> の年次の政治 Tick で党別議席へ実在の議員が充てられ、知事・首相の兼任整理と両立し、
    /// JSON 往復→再構築で履歴が戻り、読込・同年の再処理で当選回数が増えず、死去で議席が外れ、政治オブザーバに出ること。
    /// GalaxyView は無効な GameObject に載せて Start を走らせず、試験用入口から本番の private 経路を呼ぶ。セーブファイルは触らない。
    /// </summary>
    public class LegislatorHistoryPlayModeTests
    {
        private const int YearSeconds = 60 * 30 * 12; // GameDate.DateParams.Default の1年（game-秒）
        private const int ElectionYear = 797;
        private const int CapitalId = 1, FrontierId = 2, BorderId = 3, ImperialId = 4;

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

        /// <summary>同盟の文民政治家6名（ID 11..16）。</summary>
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
            return list;
        }

        private GalaxyView NewView(GalaxyMap map, Dictionary<int, Province> provinces, List<Person> civilians)
        {
            var go = new GameObject("GalaxyView_LegislatorQa");
            go.SetActive(false); // Start（盤面構築）を走らせない
            spawned.Add(go);
            GalaxyView view = go.AddComponent<GalaxyView>();
            view.BindElectionQaWorld(map, provinces, new List<Person>(), civilians);
            return view;
        }

        private CampaignState NewCampaign(GalaxyMap map, Dictionary<int, Province> provinces)
        {
            var campaign = new CampaignState(map);
            var alliance = new FactionState(Faction.同盟) { governmentForm = GovernmentForm.共和制, politics = new PoliticsState() };
            alliance.politics.parties.Add(new Party(1, "試験民政党", Faction.同盟) { support = 0.6f });
            alliance.politics.parties.Add(new Party(2, "試験進歩党", Faction.同盟) { support = 0.4f });
            campaign.states.Add(alliance);
            campaign.states.Add(new FactionState(Faction.帝国) { governmentForm = GovernmentForm.君主制 });
            StrategySession.Map = map;
            StrategySession.Campaign = campaign;
            StrategySession.Provinces = provinces;
            StrategySession.Clock = new GameClock { elapsedSeconds = YearSeconds + 1d };
            return campaign;
        }

        private static int CountMessages(long afterSeq, string needle)
        {
            int n = 0;
            List<Notification> list = NotificationCenter.Since(afterSeq);
            for (int i = 0; i < list.Count; i++)
                if (list[i].message.Contains(needle)) n++;
            return n;
        }

        /// <summary>党ごとに実在議員が確定議席以下／一人一院／知事は議員でない、を確かめる。</summary>
        private static void AssertRosterInvariants(PoliticsState pol, string when)
        {
            foreach (LegislativeChamber ch in new[] { LegislativeChamber.下院, LegislativeChamber.上院 })
            {
                ChamberSeats cs = ch == LegislativeChamber.下院 ? pol.lowerSeats : pol.upperSeats;
                foreach (PartySeatCount e in cs.parties)
                {
                    int named = LegislatorRosterRules.NamedSeats(pol, ch, e.partyId);
                    Assert.LessOrEqual(named, e.Total, when + "：" + ch + " 党#" + e.partyId + " の実在議員が議席を超えた");
                    Assert.AreEqual(e.Total, named + LegislatorRosterRules.AggregateSeats(pol, ch, e.partyId), when + "：議席総数が合わない");
                }
            }
            var ids = new HashSet<int>();
            foreach (LegislatorRecord r in pol.legislators)
                Assert.IsTrue(ids.Add(r.personId), when + "：人物#" + r.personId + " の記録が重複");
            foreach (LocalElectionState l in pol.locals)
                if (l.governorPersonId >= 0)
                    Assert.IsFalse(LegislatorRosterRules.IsSeated(pol, l.governorPersonId), when + "：知事#" + l.governorPersonId + " が議員のまま");
        }

        [UnityTest]
        public IEnumerator AnnualTick_SeatsNamedLegislators_RoundTrip_NoRecount_DeathVacates_OverlayShows()
        {
            GalaxyMap map = NewMap();
            Dictionary<int, Province> provinces = NewProvinces();
            List<Person> civilians = NewCivilians();
            CampaignState campaign = NewCampaign(map, provinces);
            GalaxyView view = NewView(map, provinces, civilians);
            view.SeedGovernmentForQa();
            Assert.AreEqual(ElectionYear, view.ElectionYearForQa);

            long seq = NotificationCenter.LastSeq;
            view.RunPoliticsTickForQa();
            view.RunCivilAppointmentTickForQa();
            view.RunGovernorAppointmentTickForQa();
            yield return null;

            PoliticsState pol = CampaignRules.GetState(campaign, Faction.同盟).politics;
            int lower1 = ElectionCycleRules.SeatsOf(pol.lowerSeats, 1), lower2 = ElectionCycleRules.SeatsOf(pol.lowerSeats, 2);
            Assert.AreEqual(300, lower1 + lower2, "党別の確定議席の総数は変わらない");
            AssertRosterInvariants(pol, "初回選挙後");
            Assert.AreEqual(ElectionYear, pol.legislatorHistorySinceYear);
            Assert.AreEqual(1, CountMessages(seq, "下院の当選議員"), "下院の名簿通知は1通");

            int premier = pol.government.premierPersonId;
            Assert.IsTrue(LegislatorRosterRules.IsSeated(pol, premier), "首相（第一党党首）は下院議員");
            Assert.AreEqual(1, LegislatorRosterRules.Find(pol, premier).lowerWins);
            int governors = 0;
            foreach (LocalElectionState l in pol.locals)
            {
                if (l.governorPersonId < 0) continue;
                governors++;
                LegislatorRecord g = LegislatorRosterRules.Find(pol, l.governorPersonId);
                Assert.IsNotNull(g, "知事は下院に当選してから知事に就いた（履歴は残る）");
                Assert.AreEqual(1, g.lowerWins);
                StringAssert.Contains("知事", g.statusReason);
            }
            Assert.Greater(governors, 0);
            foreach (LegislatorRecord r in pol.legislators)
            {
                Assert.LessOrEqual(r.TotalWins, 1, "初回選挙で2回以上数えた");
                Assert.IsFalse(r.priorKnown, "開始前の経歴を捏造した");
                Assert.AreEqual(ElectionYear, r.recordStartYear);
            }

            // 政治オブザーバに実在議員・院・当選回数・記録開始・集計議席が出る
            var overlay = new GameObject("PoliticsObserverOverlay").AddComponent<PoliticsObserverOverlay>();
            spawned.Add(overlay.gameObject);
            yield return null;
            string text = overlay.DumpTextForTest;
            // 人物名は有効な GalaxyView から引く（試験の GalaxyView は無効なので ID 表示になりうる）
            Assert.IsTrue(text.Contains("・試験政治家" + premier + "（") || text.Contains("・人物#" + premier + "（"), "首相の議員行が表示されない");
            StringAssert.Contains("当選1回", text);
            StringAssert.Contains("記録開始SE" + ElectionYear, text);
            StringAssert.Contains("集計", text);

            // 同じ年の再処理で増えない
            view.RunPoliticsTickForQa();
            Assert.AreEqual(1, LegislatorRosterRules.Find(pol, premier).TotalWins, "同じ年の再処理で当選回数が増えた");

            // JSON 往復→再構築（読込で数えない）
            var before = new Dictionary<int, LegislatorRecord>();
            foreach (LegislatorRecord r in pol.legislators) before[r.personId] = r;
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

            Assert.AreEqual(before.Count, after.legislators.Count);
            foreach (LegislatorRecord r in after.legislators)
            {
                LegislatorRecord b = before[r.personId];
                Assert.AreEqual(b.lowerWins, r.lowerWins);
                Assert.AreEqual(b.upperWins, r.upperWins);
                Assert.AreEqual(b.seated, r.seated, "人物#" + r.personId + " の議員資格が復元されない");
                Assert.AreEqual(b.seatChamber, r.seatChamber);
                Assert.AreEqual(b.seatPartyId, r.seatPartyId);
            }
            Assert.AreEqual(0, CountMessages(seqLoad, "当選議員"), "読込で名簿通知が出た");
            rebuilt.RunPoliticsTickForQa();
            rebuilt.RunCivilAppointmentTickForQa();
            rebuilt.RunGovernorAppointmentTickForQa();
            Assert.AreEqual(1, LegislatorRosterRules.Find(after, premier).TotalWins, "読込後の同じ年に数え直した");
            AssertRosterInvariants(after, "読込後");

            // 首相でも知事でもない現職議員が死去→総督銓衡の時点で議席が外れ、履歴は残る
            LegislatorRecord victim = null;
            foreach (LegislatorRecord r in LegislatorRosterRules.SeatedMembers(after, LegislativeChamber.下院))
                if (r.personId != premier) { victim = r; break; }
            Assert.IsNotNull(victim, "首相以外の現職議員がいない");
            people.Find(p => p.id == victim.personId).deathYear = ElectionYear;
            long seqDeath = NotificationCenter.LastSeq;
            rebuilt.RunGovernorAppointmentTickForQa();
            Assert.IsFalse(victim.seated, "死去した議員の議席が残った");
            Assert.AreEqual(1, victim.lowerWins, "死去で履歴が消えた");
            StringAssert.Contains("死去", victim.statusReason);
            Assert.AreEqual(1, CountMessages(seqDeath, "死去により議席を失った"));
            AssertRosterInvariants(after, "死去後");
            Assert.AreEqual(300, ElectionCycleRules.SeatsOf(after.lowerSeats, 1) + ElectionCycleRules.SeatsOf(after.lowerSeats, 2));
        }
    }
}
