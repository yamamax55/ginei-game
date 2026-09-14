using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 選挙の保存（CampaignSerializer 経由）を固定する：政党・議席・日程・直近開票・政府・知事選の往復、
    /// 旧セーブ（政治なし）は null のまま、JsonUtility が作る空の既定オブジェクトは「未設定」に戻ること、読込だけでは選挙が進まないこと。
    /// </summary>
    public class ElectionSaveRoundTripTests
    {
        static Person Pol(int id)
            => new Person(id, "政治家" + id, Faction.同盟, PersonRole.文民) { isPolitician = true, birthYear = 760 };

        static CampaignState ElectedCampaign()
        {
            var map = new GalaxyMap();
            map.AddSystem(new StarSystem(0, "ハイネセン", new Vector2(0f, 0f), Faction.同盟));
            var c = new CampaignState(map);
            var s = new FactionState(Faction.同盟) { governmentForm = GovernmentForm.共和制, politics = new PoliticsState() };
            s.politics.parties.Add(new Party(1, "民政党", Faction.同盟) { support = 0.5f });
            s.politics.parties.Add(new Party(2, "進歩党", Faction.同盟) { support = 0.375f });
            s.politics.parties.Add(new Party(3, "急進党", Faction.同盟) { support = 0.125f });
            var roster = new List<Person> { Pol(10), Pol(11), Pol(12), Pol(13) };

            var tick = default(PoliticsTickRules.PoliticsTickResult);
            tick.upperClassUp = -1;
            ElectionCycleRules.RunNationalYear(s, 800, tick, null, roster, ElectionCycleParams.Default);
            var owned = new List<LocalConstituency> { new LocalConstituency(0, 100f, 0.5f, "") };
            LocalElectionRules.Reconcile(s.politics, owned, 800, LocalElectionParams.Default);
            LocalElectionRules.RunDue(s.politics, Faction.同盟, 800, owned, roster, LocalElectionParams.Default);
            c.states.Add(s);
            return c;
        }

        [Test]
        public void RoundTrip_PreservesSeatsGovernmentSchedulesAndGovernors()
        {
            CampaignState before = ElectedCampaign();
            PoliticsState b = before.states[0].politics;
            int governor = LocalElectionRules.Find(b, 0).governorPersonId;
            Assert.GreaterOrEqual(governor, 0);

            CampaignState after = CampaignSerializer.FromJson(CampaignSerializer.ToJson(before));
            PoliticsState a = after.states[0].politics;

            Assert.IsNotNull(a);
            Assert.AreEqual(3, a.parties.Count);
            Assert.AreEqual(b.parties[0].leaderId, a.parties[0].leaderId);
            CollectionAssert.AreEqual(b.parties[0].memberIds, a.parties[0].memberIds);
            Assert.IsTrue(ElectionCycleRules.IsSeated(a));
            Assert.AreEqual(150, ElectionCycleRules.SeatsOf(a.lowerSeats, 1));
            Assert.AreEqual(113, ElectionCycleRules.SeatsOf(a.lowerSeats, 2));
            Assert.AreEqual(46, ElectionCycleRules.SeatsOf(a.upperSeats, 2));
            Assert.AreEqual(804, a.lowerHouse.nextElectionYear);
            Assert.AreEqual(803, a.upperHouse.nextElectionYear);
            Assert.AreEqual(LegislativeChamber.上院, a.upperHouse.chamber);
            Assert.AreEqual(b.government.premierPersonId, a.government.premierPersonId);
            Assert.AreEqual(CabinetStatus.少数政権, a.government.status);
            Assert.AreEqual(b.government.sourceElectionId, a.government.sourceElectionId);
            Assert.AreEqual(2, a.recentResults.Count);
            Assert.AreEqual(b.recentResults[0].electionId, a.recentResults[0].electionId);
            Assert.IsTrue(a.localsSeeded);
            LocalElectionState rec = LocalElectionRules.Find(a, 0);
            Assert.AreEqual(governor, rec.governorPersonId);
            Assert.AreEqual(804, rec.termEndYear);
            Assert.AreEqual(800, rec.lastAttemptYear);
        }

        [Test]
        public void LoadedState_SameYearTick_DoesNotRecount()
        {
            CampaignState after = CampaignSerializer.FromJson(CampaignSerializer.ToJson(ElectedCampaign()));
            FactionState s = after.states[0];
            var roster = new List<Person> { Pol(10), Pol(11), Pol(12), Pol(13) };
            var tick = default(PoliticsTickRules.PoliticsTickResult);
            tick.upperClassUp = -1;

            NationalYearOutcome o = ElectionCycleRules.RunNationalYear(s, 800, tick, null, roster, ElectionCycleParams.Default);
            Assert.IsFalse(o.inaugural);
            Assert.IsNull(o.lowerRecord);
            Assert.IsFalse(o.governmentChanged);
            var owned = new List<LocalConstituency> { new LocalConstituency(0, 100f, 0.5f, "") };
            Assert.AreEqual(0, LocalElectionRules.Reconcile(s.politics, owned, 800, LocalElectionParams.Default).Count);
            Assert.AreEqual(0, LocalElectionRules.RunDue(s.politics, Faction.同盟, 800, owned, roster, LocalElectionParams.Default).Count);
            Assert.AreEqual(2, s.politics.recentResults.Count);
        }

        [Test]
        public void OldSave_WithoutPolitics_LoadsAsNull()
        {
            var map = new GalaxyMap();
            var c = new CampaignState(map);
            c.states.Add(new FactionState(Faction.同盟) { governmentForm = GovernmentForm.共和制 });
            CampaignState after = CampaignSerializer.FromJson(CampaignSerializer.ToJson(c));
            Assert.IsNull(after.states[0].politics);

            // 旗が false なら中身があっても読まない（旧セーブを JsonUtility が既定オブジェクトで埋めた場合）
            var save = CampaignSerializer.ToSaveData(c);
            save.states[0].hasPolitics = false;
            save.states[0].politics = new PoliticsState();
            Assert.IsNull(CampaignSerializer.FromSaveData(save).states[0].politics);
        }

        [Test]
        public void EmptyDefaultObjects_AreNormalizedToUnset()
        {
            var save = new CampaignSaveData();
            save.states.Add(new FactionStateSave
            {
                faction = (int)Faction.同盟,
                hasPolitics = true,
                politics = new PoliticsState
                {
                    lowerHouse = new ChamberSchedule(),
                    upperHouse = new ChamberSchedule(),
                    lowerSeats = new ChamberSeats(),
                    upperSeats = new ChamberSeats(),
                    government = new GovernmentFormation(),
                    recentResults = null,
                    locals = null,
                },
            });
            PoliticsState p = CampaignSerializer.FromSaveData(save).states[0].politics;
            Assert.IsNotNull(p);
            Assert.IsNull(p.lowerHouse, "年0の日程は未設定");
            Assert.IsNull(p.upperHouse);
            Assert.IsNull(p.lowerSeats, "定数0の議院は未構成");
            Assert.IsNull(p.upperSeats);
            Assert.IsNull(p.government, "中身の無い政府は未組閣");
            Assert.IsNotNull(p.recentResults);
            Assert.IsNotNull(p.locals);
            Assert.IsFalse(ElectionCycleRules.IsSeated(p));
        }
    }
}
