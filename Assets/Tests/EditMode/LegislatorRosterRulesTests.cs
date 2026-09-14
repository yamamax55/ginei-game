using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 国政議員の名簿と個人の当選履歴（LegislatorRosterRules）を固定する：党別議席以下の実在議員・集計議席、一院のみ、
    /// 下院再選の加算と連続当選、上院の非改選保持、同じ開票の二重計上なし、落選・死亡・離反・拘束・知事就任、
    /// 開始前の経歴の扱い（捏造しない）、旧データの正規化と JSON 往復。
    /// 小さい議院（下院2・上院4＝A2/B2）で優先順位を厳密に固定する。
    /// </summary>
    public class LegislatorRosterRulesTests
    {
        static readonly ElectionCycleParams Small = new ElectionCycleParams(2, 4, 8, 0.2f);
        static readonly LegislatorRosterParams Prm = LegislatorRosterParams.Default;

        static Person Pol(int id, int charisma)
            => new Person(id, "政治家" + id, Faction.同盟, PersonRole.文民) { isPolitician = true, birthYear = 760, charisma = charisma };

        static PoliticsTickRules.PoliticsTickResult Tick(bool lower, int upperClass)
        {
            var t = default(PoliticsTickRules.PoliticsTickResult);
            t.lowerHouseElection = lower;
            t.upperHouseElection = upperClass >= 0;
            t.upperClassUp = upperClass;
            return t;
        }

        /// <summary>単一政党（党首10）に党員10..15（人望は ID が小さいほど高い）。</summary>
        static FactionState OnePartyState(out List<Person> roster)
        {
            var s = new FactionState(Faction.同盟) { governmentForm = GovernmentForm.共和制, politics = new PoliticsState() };
            var party = new Party(1, "民政党", Faction.同盟) { support = 1f, leaderId = 10 };
            roster = new List<Person>();
            for (int i = 0; i < 6; i++)
            {
                roster.Add(Pol(10 + i, 90 - i * 10));
                party.memberIds.Add(10 + i);
            }
            s.politics.parties.Add(party);
            return s;
        }

        /// <summary>初回選挙を開票して名簿へ反映（下院→上院）。</summary>
        static NationalYearOutcome RunYear(FactionState s, int year, PoliticsTickRules.PoliticsTickResult tick,
            List<Person> roster, ElectionCycleParams prm, IList<RegionalElectorate> regions = null)
        {
            NationalYearOutcome o = ElectionCycleRules.RunNationalYear(s, year, tick, regions, roster, prm);
            LegislatorRosterRules.AssignElection(s.politics, s.faction, o.lowerRecord, roster, regions, Prm);
            LegislatorRosterRules.AssignElection(s.politics, s.faction, o.upperRecord, roster, regions, Prm);
            return o;
        }

        static void AssertSeatedIn(PoliticsState pol, int personId, LegislativeChamber chamber, int seatClass)
        {
            LegislatorRecord r = LegislatorRosterRules.Find(pol, personId);
            Assert.IsNotNull(r, "人物#" + personId + " の記録が無い");
            Assert.IsTrue(r.seated, "人物#" + personId + " が議員でない");
            Assert.AreEqual(chamber, r.seatChamber);
            Assert.AreEqual(seatClass, r.seatClass);
        }

        // ===== 議席数以下・集計議席・優先順位 =====

        [Test]
        public void FewCandidates_NamedNeverExceedSeats_RestIsAggregate_SeatTotalsUnchanged()
        {
            var s = new FactionState(Faction.同盟) { governmentForm = GovernmentForm.共和制, politics = new PoliticsState() };
            s.politics.parties.Add(new Party(1, "民政党", Faction.同盟) { support = 1f });
            var roster = new List<Person> { Pol(10, 60), Pol(11, 50), Pol(12, 40) };

            NationalYearOutcome o = ElectionCycleRules.RunNationalYear(s, 800, Tick(false, -1), null, roster, ElectionCycleParams.Default);
            LegislatorAssignment lower = LegislatorRosterRules.AssignElection(s.politics, s.faction, o.lowerRecord, roster, null, Prm);
            LegislatorAssignment upper = LegislatorRosterRules.AssignElection(s.politics, s.faction, o.upperRecord, roster, null, Prm);
            PoliticsState pol = s.politics;

            Assert.AreEqual(3, lower.named);
            Assert.AreEqual(297, lower.aggregate);
            Assert.AreEqual(0, upper.named, "下院議員は上院に充てない（候補は3名だけ）");
            Assert.AreEqual(120, upper.aggregate);
            Assert.AreEqual(300, ElectionCycleRules.SeatsOf(pol.lowerSeats, 1), "党別の確定議席は減らない");
            Assert.AreEqual(120, ElectionCycleRules.SeatsOf(pol.upperSeats, 1));
            Assert.AreEqual(3, LegislatorRosterRules.NamedSeats(pol, LegislativeChamber.下院, 1));
            Assert.AreEqual(297, LegislatorRosterRules.AggregateSeats(pol, LegislativeChamber.下院, 1));
            Assert.AreEqual(120, LegislatorRosterRules.AggregateSeats(pol, LegislativeChamber.上院, 1));
            Assert.AreEqual(3, pol.legislators.Count, "候補の数を超える人物を作った");
        }

        [Test]
        public void Inaugural_LeaderThenScore_LowerFirst_UpperClassAThenB_OneChamberEach()
        {
            FactionState s = OnePartyState(out List<Person> roster);
            RunYear(s, 800, Tick(false, -1), roster, Small);
            PoliticsState pol = s.politics;

            AssertSeatedIn(pol, 10, LegislativeChamber.下院, -1);
            AssertSeatedIn(pol, 11, LegislativeChamber.下院, -1);
            AssertSeatedIn(pol, 12, LegislativeChamber.上院, 0);
            AssertSeatedIn(pol, 13, LegislativeChamber.上院, 0);
            AssertSeatedIn(pol, 14, LegislativeChamber.上院, 1);
            AssertSeatedIn(pol, 15, LegislativeChamber.上院, 1);
            Assert.AreEqual(2, LegislatorRosterRules.SeatedMembers(pol, LegislativeChamber.下院).Count);
            Assert.AreEqual(4, LegislatorRosterRules.SeatedMembers(pol, LegislativeChamber.上院).Count);
            Assert.AreEqual(pol.government.premierPersonId, 10, "首相（第一党党首）は下院議員を兼ねてよい");

            for (int id = 10; id <= 15; id++)
            {
                LegislatorRecord r = LegislatorRosterRules.Find(pol, id);
                Assert.AreEqual(1, r.TotalWins, "人物#" + id + " は1回だけ当選");
                Assert.AreEqual(1, r.consecutiveWins);
                Assert.AreEqual(800, r.firstWinYear);
                Assert.AreEqual(800, r.lastWinYear);
                Assert.AreEqual(800, r.recordStartYear, "記録開始は初当選の年（年齢から過去歴を作らない）");
                Assert.IsFalse(r.priorKnown);
            }
            Assert.AreEqual(1, LegislatorRosterRules.Find(pol, 10).lowerWins);
            Assert.AreEqual(0, LegislatorRosterRules.Find(pol, 10).upperWins);
            Assert.AreEqual(1, LegislatorRosterRules.Find(pol, 12).upperWins);
            Assert.AreEqual(800, pol.legislatorHistorySinceYear);
        }

        [Test]
        public void HomeRegion_BeatsSlightlyMorePopularCandidate()
        {
            var s = new FactionState(Faction.同盟) { governmentForm = GovernmentForm.共和制, politics = new PoliticsState() };
            var party = new Party(1, "民政党", Faction.同盟) { support = 1f, leaderId = 10 };
            party.memberIds.AddRange(new[] { 10, 11, 12 });
            s.politics.parties.Add(party);
            Person home = Pol(12, 50);
            home.birthSystemId = 5;
            var roster = new List<Person> { Pol(10, 90), Pol(11, 60), home };
            var regions = new List<RegionalElectorate> { new RegionalElectorate(5, 100f, 0.5f) };

            NationalYearOutcome o = ElectionCycleRules.RunNationalYear(s, 800, Tick(false, -1), regions, roster, Small);
            LegislatorRosterRules.AssignElection(s.politics, s.faction, o.lowerRecord, roster, regions, Prm);

            Assert.IsTrue(LegislatorRosterRules.IsSeated(s.politics, 10), "党首が最優先");
            Assert.IsTrue(LegislatorRosterRules.IsSeated(s.politics, 12), "地盤のある候補が勝つ");
            Assert.IsFalse(LegislatorRosterRules.IsSeated(s.politics, 11));
        }

        // ===== 再選・非改選・二重計上 =====

        [Test]
        public void UpperClassA_Election_ReelectsClassA_KeepsClassBWithoutCounting()
        {
            FactionState s = OnePartyState(out List<Person> roster);
            RunYear(s, 800, Tick(false, -1), roster, Small);
            NationalYearOutcome o = RunYear(s, 803, Tick(false, 0), roster, Small);
            PoliticsState pol = s.politics;

            Assert.IsNotNull(o.upperRecord);
            Assert.AreEqual(0, o.upperRecord.classUp);
            foreach (int id in new[] { 12, 13 })
            {
                LegislatorRecord r = LegislatorRosterRules.Find(pol, id);
                AssertSeatedIn(pol, id, LegislativeChamber.上院, 0);
                Assert.AreEqual(2, r.upperWins, "改選区分Aの現職は再選で加算");
                Assert.AreEqual(2, r.consecutiveWins);
                Assert.AreEqual(800, r.firstWinYear);
                Assert.AreEqual(803, r.lastWinYear);
            }
            foreach (int id in new[] { 14, 15 })
            {
                LegislatorRecord r = LegislatorRosterRules.Find(pol, id);
                AssertSeatedIn(pol, id, LegislativeChamber.上院, 1);
                Assert.AreEqual(1, r.upperWins, "非改選の区分Bは当選回数を増やさない");
                Assert.AreEqual(800, r.seatElectedYear);
            }
            Assert.AreEqual(1, LegislatorRosterRules.Find(pol, 10).lowerWins, "下院は改選なし");
        }

        [Test]
        public void LowerReelection_IncrementsWinsAndConsecutive()
        {
            FactionState s = OnePartyState(out List<Person> roster);
            RunYear(s, 800, Tick(false, -1), roster, Small);
            RunYear(s, 804, Tick(true, -1), roster, Small);
            PoliticsState pol = s.politics;

            foreach (int id in new[] { 10, 11 })
            {
                LegislatorRecord r = LegislatorRosterRules.Find(pol, id);
                AssertSeatedIn(pol, id, LegislativeChamber.下院, -1);
                Assert.AreEqual(2, r.lowerWins);
                Assert.AreEqual(2, r.consecutiveWins);
                Assert.AreEqual(804, r.lastWinYear);
                Assert.AreEqual(2, LegislatorRosterRules.TotalWins(pol, id));
            }
        }

        [Test]
        public void SameElectionId_CountsOnce_EvenIfLogIsLostOrYearIsRerun()
        {
            FactionState s = OnePartyState(out List<Person> roster);
            NationalYearOutcome o = RunYear(s, 800, Tick(false, -1), roster, Small);
            PoliticsState pol = s.politics;

            LegislatorAssignment again = LegislatorRosterRules.AssignElection(pol, s.faction, o.lowerRecord, roster, null, Prm);
            Assert.IsTrue(again.alreadyAssigned);
            Assert.AreEqual(1, LegislatorRosterRules.Find(pol, 10).lowerWins);

            // 反映ログが失われても個人の最終選挙IDで二重に数えない（議席は同じ結果に戻る）
            pol.legislatorAssignedElectionIds.Clear();
            LegislatorAssignment third = LegislatorRosterRules.AssignElection(pol, s.faction, o.lowerRecord, roster, null, Prm);
            Assert.IsFalse(third.alreadyAssigned);
            Assert.AreEqual(0, third.newlyElected + third.reelected, "同じ開票を数え直した");
            Assert.AreEqual(1, LegislatorRosterRules.Find(pol, 10).lowerWins);
            Assert.AreEqual(1, LegislatorRosterRules.Find(pol, 10).consecutiveWins);
            AssertSeatedIn(pol, 10, LegislativeChamber.下院, -1);

            // 同じ年の国政処理の再実行は開票しない＝名簿も動かない
            NationalYearOutcome rerun = RunYear(s, 800, Tick(false, -1), roster, Small);
            Assert.IsNull(rerun.lowerRecord);
            Assert.AreEqual(1, LegislatorRosterRules.Find(pol, 11).TotalWins);

            // 党首選・組閣では増えない
            ElectionCycleRules.OrganizeParties(pol.parties, roster, s.faction);
            ElectionCycleRules.MaintainGovernment(pol, s.faction, 801, roster, out _);
            Assert.AreEqual(1, LegislatorRosterRules.Find(pol, 10).TotalWins);
        }

        // ===== 落選・死亡・離反・拘束・知事 =====

        [Test]
        public void Defeat_KeepsCumulativeHistory_ResetsConsecutive_AndPartyWithoutCandidatesIsAggregate()
        {
            var s = new FactionState(Faction.同盟) { governmentForm = GovernmentForm.共和制, politics = new PoliticsState() };
            var p1 = new Party(1, "民政党", Faction.同盟) { support = 0.5f, leaderId = 10 };
            p1.memberIds.AddRange(new[] { 10, 12 });
            var p2 = new Party(2, "進歩党", Faction.同盟) { support = 0.5f, leaderId = 11 };
            p2.memberIds.AddRange(new[] { 11, 13 });
            s.politics.parties.Add(p1);
            s.politics.parties.Add(p2);
            var roster = new List<Person> { Pol(10, 90), Pol(11, 80), Pol(12, 70), Pol(13, 60) };

            RunYear(s, 800, Tick(false, -1), roster, Small);
            PoliticsState pol = s.politics;
            AssertSeatedIn(pol, 10, LegislativeChamber.下院, -1);
            AssertSeatedIn(pol, 11, LegislativeChamber.下院, -1);

            p1.support = 1f;
            p2.support = 0f;
            RunYear(s, 804, Tick(true, -1), roster, Small);

            Assert.AreEqual(2, ElectionCycleRules.SeatsOf(pol.lowerSeats, 1));
            Assert.AreEqual(0, ElectionCycleRules.SeatsOf(pol.lowerSeats, 2));
            LegislatorRecord loser = LegislatorRosterRules.Find(pol, 11);
            Assert.IsFalse(loser.seated, "議席を失った現職");
            Assert.AreEqual(1, loser.lowerWins, "落選しても累積は残る");
            Assert.AreEqual(0, loser.consecutiveWins);
            Assert.AreEqual(800, loser.lastWinYear);
            StringAssert.Contains("議席を得られなかった", loser.statusReason);
            Assert.AreEqual(2, LegislatorRosterRules.Find(pol, 10).lowerWins);
            Assert.AreEqual(1, LegislatorRosterRules.NamedSeats(pol, LegislativeChamber.下院, 1), "12 は上院議員なので下院に充てない");
            Assert.AreEqual(1, LegislatorRosterRules.AggregateSeats(pol, LegislativeChamber.下院, 1));
        }

        [Test]
        public void Reconcile_DeathAndDefectionVacate_CaptiveKeepsSeat_WinsUnchanged()
        {
            FactionState s = OnePartyState(out List<Person> roster);
            RunYear(s, 800, Tick(false, -1), roster, Small);
            PoliticsState pol = s.politics;

            roster.Find(p => p.id == 14).deathYear = 801;
            roster.Find(p => p.id == 13).faction = Faction.帝国;
            roster.Find(p => p.id == 15).captiveStatus = CaptiveStatus.捕虜;

            List<LegislatorVacancy> vac = LegislatorRosterRules.Reconcile(pol, s.faction, roster, 801);
            Assert.AreEqual(2, vac.Count);
            LegislatorRecord dead = LegislatorRosterRules.Find(pol, 14);
            Assert.IsFalse(dead.seated);
            StringAssert.Contains("死去", dead.statusReason);
            Assert.AreEqual(1, dead.upperWins, "死亡しても履歴は残る");
            LegislatorRecord defector = LegislatorRosterRules.Find(pol, 13);
            Assert.IsFalse(defector.seated);
            StringAssert.Contains("離反", defector.statusReason);
            Assert.AreEqual(1, defector.upperWins);
            AssertSeatedIn(pol, 15, LegislativeChamber.上院, 1);
            Assert.AreEqual(2, LegislatorRosterRules.AggregateSeats(pol, LegislativeChamber.上院, 1), "外れた議席は集計へ戻る");
            Assert.AreEqual(4, ElectionCycleRules.SeatsOf(pol.upperSeats, 1));

            Assert.AreEqual(0, LegislatorRosterRules.Reconcile(pol, s.faction, roster, 801).Count, "再実行で重ねて外さない");
        }

        [Test]
        public void Governor_VacatesSeat_AndIsNotCandidate_ShortfallBecomesAggregate()
        {
            FactionState s = OnePartyState(out List<Person> roster);
            RunYear(s, 800, Tick(false, -1), roster, Small);
            PoliticsState pol = s.politics;
            pol.locals.Add(new LocalElectionState(1) { governorPersonId = 12, status = LocalElectionStatus.当選 });

            List<LegislatorVacancy> vac = LegislatorRosterRules.Reconcile(pol, s.faction, roster, 800);
            Assert.AreEqual(1, vac.Count);
            Assert.AreEqual(12, vac[0].personId);
            StringAssert.Contains("知事", LegislatorRosterRules.Find(pol, 12).statusReason);

            NationalYearOutcome o = ElectionCycleRules.RunNationalYear(s, 803, Tick(false, 0), null, roster, Small);
            LegislatorAssignment a = LegislatorRosterRules.AssignElection(pol, s.faction, o.upperRecord, roster, null, Prm);
            Assert.AreEqual(1, a.named, "知事と他院議員を除くと候補は13のみ");
            Assert.AreEqual(1, a.aggregate);
            Assert.AreEqual(1, a.reelected);
            Assert.IsFalse(LegislatorRosterRules.IsSeated(pol, 12));
            Assert.AreEqual(1, LegislatorRosterRules.Find(pol, 12).upperWins);
            Assert.AreEqual(2, LegislatorRosterRules.Find(pol, 13).upperWins);
        }

        [Test]
        public void SuspendAll_ClearsSeats_KeepsHistory()
        {
            FactionState s = OnePartyState(out List<Person> roster);
            RunYear(s, 800, Tick(false, -1), roster, Small);
            Assert.AreEqual(6, LegislatorRosterRules.SuspendAll(s.politics, "政体移行"));
            Assert.AreEqual(0, LegislatorRosterRules.SeatedMembers(s.politics, LegislativeChamber.下院).Count);
            Assert.AreEqual(1, LegislatorRosterRules.TotalWins(s.politics, 10));
        }

        // ===== 開始前の経歴 =====

        [Test]
        public void PriorHistory_OnlyWhenExplicit_AddsToTotals_WithoutSeat()
        {
            var pol = new PoliticsState();
            Assert.AreEqual(0, LegislatorRosterRules.TotalWins(pol, 20), "記録なし＝不明/ゲーム内0");

            LegislatorRecord r = LegislatorRosterRules.SeedPriorHistory(pol, 20, 3, -2, 780, 796);
            Assert.IsTrue(r.priorKnown);
            Assert.AreEqual(3, r.TotalLowerWins);
            Assert.AreEqual(0, r.TotalUpperWins, "負数は0");
            Assert.AreEqual(0, r.lowerWins, "ゲーム内の当選とは別");
            Assert.AreEqual(780, r.firstWinYear);
            Assert.AreEqual(796, r.lastWinYear);
            Assert.IsFalse(r.seated, "経歴だけで議席は与えない");
            Assert.IsNull(LegislatorRosterRules.SeedPriorHistory(null, 20, 1, 1, 0, 0));
        }

        // ===== 旧データ・JSON =====

        [Test]
        public void OldData_NullLists_NormalizeToEmpty_AllSeatsAggregate()
        {
            FactionState s = OnePartyState(out List<Person> roster);
            ElectionCycleRules.RunNationalYear(s, 800, Tick(false, -1), null, roster, Small);
            PoliticsState pol = s.politics;
            pol.legislators = null;
            pol.legislatorAssignedElectionIds = null;

            ElectionCycleRules.NormalizeLoaded(pol);
            Assert.IsNotNull(pol.legislators);
            Assert.IsNotNull(pol.legislatorAssignedElectionIds);
            Assert.AreEqual(2, LegislatorRosterRules.AggregateSeats(pol, LegislativeChamber.下院, 1));
            Assert.AreEqual(4, LegislatorRosterRules.AggregateSeats(pol, LegislativeChamber.上院, 1));
            Assert.AreEqual(0, LegislatorRosterRules.Reconcile(pol, s.faction, roster, 800).Count);
        }

        [Test]
        public void Normalize_TrimsNamedAboveSeats_AndRemovesBrokenRecords()
        {
            FactionState s = OnePartyState(out List<Person> roster);
            RunYear(s, 800, Tick(false, -1), roster, Small);
            PoliticsState pol = s.politics;
            // 食い違い：上院議員15を下院にも載せる＝下院の実在議員3 > 定数2
            LegislatorRecord r15 = LegislatorRosterRules.Find(pol, 15);
            r15.seatChamber = LegislativeChamber.下院;
            r15.seatClass = -1;
            pol.legislators.Add(null);
            pol.legislators.Add(new LegislatorRecord(10));

            ElectionCycleRules.NormalizeLoaded(pol);
            Assert.AreEqual(6, pol.legislators.Count, "null と重複が除かれる");
            Assert.IsFalse(LegislatorRosterRules.IsSeated(pol, 15), "定数超過は人物ID大の方から外す");
            Assert.AreEqual(2, LegislatorRosterRules.NamedSeats(pol, LegislativeChamber.下院, 1));
            Assert.AreEqual(1, LegislatorRosterRules.Find(pol, 15).upperWins, "履歴は残す");
        }

        [Test]
        public void JsonRoundTrip_PreservesRecords_AndLoadDoesNotCount()
        {
            FactionState s = OnePartyState(out List<Person> roster);
            RunYear(s, 800, Tick(false, -1), roster, Small);
            NationalYearOutcome o803 = RunYear(s, 803, Tick(false, 0), roster, Small);
            LegislatorRosterRules.SeedPriorHistory(s.politics, 14, 2, 0, 790, 0);
            var map = new GalaxyMap();
            map.AddSystem(new StarSystem(0, "ハイネセン", new Vector2(0f, 0f), Faction.同盟));
            var c = new CampaignState(map);
            c.states.Add(s);

            CampaignState after = CampaignSerializer.FromJson(CampaignSerializer.ToJson(c));
            PoliticsState a = after.states[0].politics;
            PoliticsState b = s.politics;

            Assert.AreEqual(b.legislators.Count, a.legislators.Count);
            Assert.AreEqual(b.legislatorHistorySinceYear, a.legislatorHistorySinceYear);
            CollectionAssert.AreEqual(b.legislatorAssignedElectionIds, a.legislatorAssignedElectionIds);
            for (int id = 10; id <= 15; id++)
            {
                LegislatorRecord x = LegislatorRosterRules.Find(b, id), y = LegislatorRosterRules.Find(a, id);
                Assert.AreEqual(x.lowerWins, y.lowerWins);
                Assert.AreEqual(x.upperWins, y.upperWins);
                Assert.AreEqual(x.consecutiveWins, y.consecutiveWins);
                Assert.AreEqual(x.firstWinYear, y.firstWinYear);
                Assert.AreEqual(x.lastWinYear, y.lastWinYear);
                Assert.AreEqual(x.recordStartYear, y.recordStartYear);
                Assert.AreEqual(x.seated, y.seated);
                Assert.AreEqual(x.seatChamber, y.seatChamber);
                Assert.AreEqual(x.seatClass, y.seatClass);
                Assert.AreEqual(x.seatPartyId, y.seatPartyId);
                Assert.AreEqual(x.lastUpperElectionId, y.lastUpperElectionId);
                Assert.AreEqual(x.priorKnown, y.priorKnown);
                Assert.AreEqual(x.priorLowerWins, y.priorLowerWins);
            }
            Assert.AreEqual(3, LegislatorRosterRules.Find(a, 14).TotalWins, "明示の経歴（下院2）＋ゲーム内（上院1）が往復で保たれる");
            Assert.AreEqual(790, LegislatorRosterRules.Find(a, 14).firstWinYear);

            // 読込後に同じ年を回しても・同じ開票を反映し直しても数えない
            NationalYearOutcome rerun = RunYear(after.states[0], 803, Tick(false, 0), roster, Small);
            Assert.IsNull(rerun.upperRecord);
            Assert.IsTrue(LegislatorRosterRules.AssignElection(a, Faction.同盟, o803.upperRecord, roster, null, Prm).alreadyAssigned);
            Assert.AreEqual(2, LegislatorRosterRules.Find(a, 12).upperWins);
        }
    }
}
