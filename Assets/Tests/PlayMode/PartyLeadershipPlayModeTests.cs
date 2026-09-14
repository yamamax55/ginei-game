using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 総裁選の Game 接続（<see cref="GalaxyView"/> の年次の政治 Tick）を固定条件で通す PlayMode 試験：
    /// 初年に党ごとの総裁選が1回だけ行われ記録が残る／同じ年の再処理で増えない／党首が替わっても首相・宰相職は変わらず、
    /// 新党首に政府役職・艦隊の指揮権が付かない／JSON 往復→再構築で記録と任期が戻り、読込だけで総裁選をしない／
    /// 政治オブザーバに総裁選・派閥・党内序列・党首不在（選出待ち）が出る。
    /// GalaxyView は無効な GameObject に載せて Start を走らせない。セーブファイルは読み書きしない（JSON は文字列の往復だけ）。
    /// </summary>
    public class PartyLeadershipPlayModeTests
    {
        private const int YearSeconds = 60 * 30 * 12; // GameDate.DateParams.Default の1年（game-秒）
        private const int FirstYear = 797;
        private const int CapitalId = 1, FrontierId = 2;

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
            var go = new GameObject("GalaxyView_LeadershipQa");
            go.SetActive(false);
            spawned.Add(go);
            GalaxyView view = go.AddComponent<GalaxyView>();
            view.BindElectionQaWorld(map, provinces, new List<Person>(), civilians);
            return view;
        }

        private static int HolderId(Office office, int scopeKey = 0)
        {
            ICharacter h = GovernmentRegistry.GetHolder(office, scopeKey);
            return h != null ? h.Id : -1;
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

        [UnityTest]
        public IEnumerator AnnualTick_HoldsLeadershipElections_SeparateFromPremier_SurvivesSave_AndOverlayShows()
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

            // --- 1. 初年：党ごとに総裁選1回→その党首から組閣 ---
            long seq = NotificationCenter.LastSeq;
            view.RunPoliticsTickForQa();
            yield return null;
            PoliticsState pol = alliance.politics;
            int held = 0;
            foreach (Party p in pol.parties)
            {
                Assert.IsTrue(p.leadership.managed, p.partyName + " が総裁選の管理下にない");
                if (p.memberIds.Count == 0) continue;
                LeadershipElectionRecord rec = p.leadership.Latest;
                Assert.IsNotNull(rec, p.partyName + " の総裁選の記録がない");
                Assert.AreEqual(FirstYear, rec.year);
                StringAssert.Contains("総裁選", rec.electionId, "国政選挙と別のID");
                Assert.AreEqual(rec.winnerId, p.leaderId);
                Assert.AreEqual(FirstYear + 3, p.leadership.termEndYear);
                held++;
            }
            Assert.Greater(held, 0);
            Assert.AreEqual(held, CountMessages(seq, "総裁選"), "党ごとに1通");
            GovernmentFormation g = pol.government;
            Assert.IsNotNull(g);
            Party ruling = ElectionCycleRules.FindParty(pol.parties, g.partyId);
            int premier = g.premierPersonId;
            Assert.AreEqual(ruling.leaderId, premier, "下院選挙後の組閣で第一党の党首が首相");
            Office premierOffice = view.PremierOfficeOf(Faction.同盟);
            Assert.AreEqual(premier, HolderId(premierOffice));

            // --- 2. 同じ年の再処理：記録・任期・通知が増えない ---
            var counts = new Dictionary<int, int>();
            foreach (Party p in pol.parties) counts[p.id] = p.leadership.records.Count;
            long seq2 = NotificationCenter.LastSeq;
            view.RunPoliticsTickForQa();
            foreach (Party p in pol.parties)
            {
                Assert.AreEqual(counts[p.id], p.leadership.records.Count, "同じ年に総裁選を再実施した");
                if (p.memberIds.Count > 0) Assert.AreEqual(FirstYear + 3, p.leadership.termEndYear);
            }
            Assert.AreEqual(0, CountMessages(seq2, "総裁選"));

            // --- 3. JSON 往復→再構築（読込だけで総裁選をしない） ---
            var winners = new Dictionary<int, int>();
            var ids = new Dictionary<int, string>();
            foreach (Party p in pol.parties)
            {
                winners[p.id] = p.leaderId;
                ids[p.id] = p.leadership.Latest != null ? p.leadership.Latest.electionId : "";
            }
            var wins = new Dictionary<int, int>();
            foreach (LegislatorRecord r in pol.legislators) wins[r.personId] = r.TotalWins;
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
            foreach (Party p in after.parties)
            {
                Assert.AreEqual(winners[p.id], p.leaderId, p.partyName + " の党首が復元されない");
                Assert.AreEqual(ids[p.id], p.leadership.Latest != null ? p.leadership.Latest.electionId : "");
                Assert.AreEqual(counts[p.id], p.leadership.records.Count);
            }
            Assert.AreEqual(0, CountMessages(seqLoad, "総裁選"), "読込だけで総裁選をした");
            rebuilt.RunPoliticsTickForQa();
            foreach (Party p in after.parties) Assert.AreEqual(counts[p.id], p.leadership.records.Count, "読込後の同じ年に総裁選をした");
            foreach (LegislatorRecord r in after.legislators) Assert.AreEqual(wins[r.personId], r.TotalWins, "総裁選・読込で当選回数が変わった");
            premierOffice = rebuilt.PremierOfficeOf(Faction.同盟);
            Assert.AreEqual(premier, HolderId(premierOffice));

            // --- 4. 翌年：首相が党を離れ党首が空席→総裁選で新党首。首相・宰相職は変わらず、新党首に政府役職も軍の指揮権も付かない ---
            Party afterRuling = ElectionCycleRules.FindParty(after.parties, after.government.partyId);
            Assert.IsTrue(PartyMembershipRules.Leave(after, premier, "試験：党を離れる").ok);
            Assert.AreEqual(-1, afterRuling.leaderId);
            StrategySession.Clock = new GameClock { elapsedSeconds = YearSeconds * 2d + 1d };
            Assert.AreEqual(FirstYear + 1, rebuilt.ElectionYearForQa);
            var officesBefore = new Dictionary<int, int>();
            foreach (Person person in people) officesBefore[person.id] = OfficesOf(person.id).Count;
            long seq3 = NotificationCenter.LastSeq;
            rebuilt.RunPoliticsTickForQa();
            int newLeader = afterRuling.leaderId;
            Assert.GreaterOrEqual(newLeader, 0, "党首の空席で総裁選が行われない");
            Assert.AreNotEqual(premier, newLeader);
            Assert.AreEqual(FirstYear + 1, afterRuling.leadership.Latest.year);
            StringAssert.Contains("空席", afterRuling.leadership.Latest.trigger);
            Assert.AreEqual(1, CountMessages(seq3, "総裁選"));
            Assert.AreEqual(premier, after.government.premierPersonId, "党首交代で首相が変わった");
            Assert.AreEqual(premier, HolderId(premierOffice), "党首交代で宰相職が動いた");
            List<Office> held2 = OfficesOf(newLeader);
            Assert.AreEqual(officesBefore[newLeader], held2.Count, "党首に就いただけで政府役職が増えた");
            Assert.IsFalse(held2.Contains(premierOffice), "新党首が宰相職に就いた");
            Assert.IsFalse(OfficeRules.CanPropose(held2, OfficeDomain.軍事, OfficeScope.国家), "新党首が全軍の指揮権を持った");
            rebuilt.RunCivilAppointmentTickForQa();
            Assert.AreEqual(premier, HolderId(premierOffice), "年次の宰相銓衡で党首へ首相が移った");

            // --- 5. 政治オブザーバ：総裁選・派閥・党内序列、党首不在＝選出待ち ---
            Assert.IsTrue(PartyMembershipRules.Leave(after, newLeader, "試験：党首も離党").ok);
            var overlay = new GameObject("PoliticsObserverOverlay").AddComponent<PoliticsObserverOverlay>();
            spawned.Add(overlay.gameObject);
            yield return null;
            string text = overlay.DumpTextForTest;
            StringAssert.Contains("党首（総裁）", text);
            StringAssert.Contains("直近の総裁選", text);
            StringAssert.Contains("選挙ID " + afterRuling.leadership.Latest.electionId, text);
            StringAssert.Contains("seed " + afterRuling.leadership.Latest.seed, text);
            StringAssert.Contains("党内序列", text);
            StringAssert.Contains("無派閥", text);
            StringAssert.Contains("不在＝選出待ち", text, "党首不在の表示がない");
            StringAssert.Contains("首相は組閣の手続き", text);

            // 同じ年のうちは空席のまま（次の年次で総裁選）
            long seq4 = NotificationCenter.LastSeq;
            rebuilt.RunPoliticsTickForQa();
            Assert.AreEqual(-1, afterRuling.leaderId, "管理下の党で旧来の党首補充が走った");
            Assert.AreEqual(0, CountMessages(seq4, "総裁選"));
        }
    }
}
