using System.Collections.Generic;
using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 国政選挙（ElectionCycleRules）を固定する：初回選挙の議席総数と端数・上院の非改選保持・同じ年の二重開票なし・
    /// 組閣（単独過半／少数政権／組閣未成立）・党首の補充・首相欠缺の再組閣・非民主での停止・資格判定。
    /// 既定値＝下院300・上院120（A60/B60）。支持率は2進で割り切れる値にして端数を厳密に固定する。
    /// </summary>
    public class ElectionCycleRulesTests
    {
        static readonly ElectionCycleParams Prm = ElectionCycleParams.Default;

        static Person Pol(int id, Faction f = Faction.同盟)
            => new Person(id, "政治家" + id, f, PersonRole.文民) { isPolitician = true, birthYear = 760 };

        static FactionState State(params float[] supports)
        {
            var s = new FactionState(Faction.同盟) { governmentForm = GovernmentForm.共和制 };
            s.politics = new PoliticsState();
            for (int i = 0; i < supports.Length; i++)
                s.politics.parties.Add(new Party(i + 1, "党" + (i + 1), Faction.同盟) { support = supports[i] });
            return s;
        }

        static PoliticsTickRules.PoliticsTickResult NoElection()
        {
            var t = default(PoliticsTickRules.PoliticsTickResult);
            t.upperClassUp = -1;
            return t;
        }

        static PartySeatCount Seat(ChamberSeats cs, int partyId)
        {
            foreach (var e in cs.parties) if (e.partyId == partyId) return e;
            return null;
        }

        [Test]
        public void Inaugural_AllSeatsElected_TotalsConserved_MinorityAtExactHalf()
        {
            var s = State(0.5f, 0.375f, 0.125f);
            var roster = new List<Person> { Pol(10), Pol(11), Pol(12) };

            NationalYearOutcome o = ElectionCycleRules.RunNationalYear(s, 800, NoElection(), null, roster, Prm);
            PoliticsState pol = s.politics;

            Assert.IsTrue(o.inaugural);
            Assert.IsTrue(ElectionCycleRules.IsSeated(pol));
            // 下院 150/112.5/37.5 → 150/113/37（剰余同点は得票の多い党2）
            Assert.AreEqual(150, ElectionCycleRules.SeatsOf(pol.lowerSeats, 1));
            Assert.AreEqual(113, ElectionCycleRules.SeatsOf(pol.lowerSeats, 2));
            Assert.AreEqual(37, ElectionCycleRules.SeatsOf(pol.lowerSeats, 3));
            // 上院 A/B それぞれ 30/23/7
            Assert.AreEqual(30, Seat(pol.upperSeats, 1).classA); Assert.AreEqual(30, Seat(pol.upperSeats, 1).classB);
            Assert.AreEqual(23, Seat(pol.upperSeats, 2).classA); Assert.AreEqual(23, Seat(pol.upperSeats, 2).classB);
            Assert.AreEqual(7, Seat(pol.upperSeats, 3).classA);  Assert.AreEqual(7, Seat(pol.upperSeats, 3).classB);
            Assert.AreEqual(300, o.lowerRecord.seatsUp);
            Assert.AreEqual(120, o.upperRecord.seatsUp);
            Assert.AreEqual(2, pol.recentResults.Count);

            // 日程は初回選挙の年から数える（下院4年・上院3年ごと）
            Assert.AreEqual(804, pol.lowerHouse.nextElectionYear);
            Assert.AreEqual(803, pol.upperHouse.nextElectionYear);

            // 党首は無所属の政治家から（党員の少ない党へ ID 順に入党）
            Assert.AreEqual(10, pol.parties[0].leaderId);
            Assert.AreEqual(11, pol.parties[1].leaderId);
            Assert.AreEqual(12, pol.parties[2].leaderId);

            // 150/300 はちょうど半分＝過半数（151）に届かない＝少数政権
            Assert.IsTrue(o.governmentFormed);
            Assert.AreEqual(10, pol.government.premierPersonId);
            Assert.AreEqual(CabinetStatus.少数政権, pol.government.status);
            StringAssert.Contains("少数政権", pol.government.reason);
            Assert.AreEqual(o.lowerRecord.electionId, pol.government.sourceElectionId);
        }

        [Test]
        public void LowerElection_Majority_IsSingleParty()
        {
            var s = State(0.75f, 0.125f, 0.125f);
            var roster = new List<Person> { Pol(10), Pol(11), Pol(12) };
            ElectionCycleRules.RunNationalYear(s, 800, NoElection(), null, roster, Prm);
            // 225/37.5/37.5 → 剰余・得票とも同点は党ID小（党2）
            Assert.AreEqual(225, ElectionCycleRules.SeatsOf(s.politics.lowerSeats, 1));
            Assert.AreEqual(38, ElectionCycleRules.SeatsOf(s.politics.lowerSeats, 2));
            Assert.AreEqual(37, ElectionCycleRules.SeatsOf(s.politics.lowerSeats, 3));
            Assert.AreEqual(CabinetStatus.単独過半, s.politics.government.status);
            Assert.AreEqual("", s.politics.government.reason);
        }

        [Test]
        public void UpperHalfElection_KeepsOtherClass_AndSameYearIsNotRecounted()
        {
            var s = State(0.5f, 0.375f, 0.125f);
            var roster = new List<Person> { Pol(10), Pol(11), Pol(12) };
            ElectionCycleRules.RunNationalYear(s, 800, NoElection(), null, roster, Prm);
            PoliticsState pol = s.politics;

            pol.parties[0].support = 0.25f; pol.parties[1].support = 0.5f; pol.parties[2].support = 0.25f;
            var tick = NoElection();
            tick.upperHouseElection = true;
            tick.upperClassUp = 0;

            NationalYearOutcome o = ElectionCycleRules.RunNationalYear(s, 803, tick, null, roster, Prm);
            Assert.IsNotNull(o.upperRecord);
            Assert.IsNull(o.lowerRecord);
            Assert.AreEqual(60, o.upperRecord.seatsUp);
            // 区分A だけ 15/30/15 に入れ替わり、区分B（800年の 30/23/7）は残る
            Assert.AreEqual(15, Seat(pol.upperSeats, 1).classA); Assert.AreEqual(30, Seat(pol.upperSeats, 1).classB);
            Assert.AreEqual(30, Seat(pol.upperSeats, 2).classA); Assert.AreEqual(23, Seat(pol.upperSeats, 2).classB);
            Assert.AreEqual(15, Seat(pol.upperSeats, 3).classA); Assert.AreEqual(7, Seat(pol.upperSeats, 3).classB);
            Assert.AreEqual(120, Seat(pol.upperSeats, 1).Total + Seat(pol.upperSeats, 2).Total + Seat(pol.upperSeats, 3).Total);
            Assert.AreEqual(803, pol.upperSeats.lastElectionYearA);
            Assert.AreEqual(800, pol.upperSeats.lastElectionYearB);
            // 上院選挙では首相は変わらない
            Assert.AreEqual(10, pol.government.premierPersonId);

            int records = pol.recentResults.Count;
            NationalYearOutcome again = ElectionCycleRules.RunNationalYear(s, 803, tick, null, roster, Prm);
            Assert.IsNull(again.upperRecord, "同じ年・同じ区分は二重に開票しない");
            Assert.IsFalse(again.governmentChanged);
            Assert.AreEqual(records, pol.recentResults.Count);
            Assert.AreEqual(15, Seat(pol.upperSeats, 1).classA);
        }

        [Test]
        public void FormGovernment_SeatTie_SmallerPartyIdLeads()
        {
            var s = State(0.4f, 0.3f, 0.3f);
            var roster = new List<Person> { Pol(10), Pol(11), Pol(12) };
            ElectionCycleRules.OrganizeParties(s.politics.parties, roster, Faction.同盟);
            ElectionCycleRules.EnsureSeats(s.politics, Prm);
            var cs = s.politics.lowerSeats;
            cs.parties.Add(new PartySeatCount(3) { classA = 100 });
            cs.parties.Add(new PartySeatCount(2) { classA = 100 });
            cs.parties.Add(new PartySeatCount(1) { classA = 100 });
            cs.lastElectionYearA = 800; cs.seated = true;

            GovernmentFormation g = ElectionCycleRules.FormGovernment(s.politics, Faction.同盟, 800, "x", roster);
            Assert.AreEqual(1, g.partyId);
            Assert.AreEqual(10, g.premierPersonId);
            Assert.AreEqual(CabinetStatus.少数政権, g.status);
        }

        [Test]
        public void FirstPartyWithoutEligibleLeader_CabinetNotFormed_NoFabrication()
        {
            var s = State(0.5f, 0.375f, 0.125f);
            var roster = new List<Person> { Pol(10), Pol(11), Pol(12) };
            ElectionCycleRules.RunNationalYear(s, 800, NoElection(), null, roster, Prm);

            roster[0].deathYear = 801; // 第一党の唯一の党員＝首相が死亡
            bool changed = ElectionCycleRules.MaintainGovernment(s.politics, Faction.同盟, 801, roster, out int prev);
            Assert.IsTrue(changed);
            Assert.AreEqual(10, prev);
            Assert.AreEqual(-1, s.politics.government.premierPersonId);
            Assert.AreEqual(CabinetStatus.組閣未成立, s.politics.government.status);
            StringAssert.Contains("第一党", s.politics.government.reason);
            Assert.AreEqual(-1, s.politics.parties[0].leaderId, "党首を捏造しない");

            // 同じ状況で再度呼んでも変化なし（通知の二重化を防ぐ）
            Assert.IsFalse(ElectionCycleRules.MaintainGovernment(s.politics, Faction.同盟, 802, roster, out _));
            Assert.AreEqual(801, s.politics.government.formedYear);
        }

        [Test]
        public void PremierDies_PartyElectsNewLeader_Reformed()
        {
            var s = State(0.5f, 0.375f, 0.125f);
            var roster = new List<Person> { Pol(10), Pol(11), Pol(12), Pol(13) }; // 13 は党1へ（党員数同数→党ID小）
            ElectionCycleRules.RunNationalYear(s, 800, NoElection(), null, roster, Prm);
            CollectionAssert.AreEquivalent(new[] { 10, 13 }, s.politics.parties[0].memberIds);
            Assert.AreEqual(10, s.politics.government.premierPersonId); // 同得点は ID 順の先頭

            roster[0].deathYear = 802;
            Assert.IsTrue(ElectionCycleRules.MaintainGovernment(s.politics, Faction.同盟, 802, roster, out _));
            Assert.AreEqual(13, s.politics.parties[0].leaderId);
            Assert.AreEqual(13, s.politics.government.premierPersonId);
            Assert.AreEqual(CabinetStatus.少数政権, s.politics.government.status);
            StringAssert.Contains("再組閣", s.politics.government.reason);
        }

        [Test]
        public void Eligibility_RequiresLivingFreeSameFactionCivilianPolitician()
        {
            Assert.IsTrue(ElectionCycleRules.IsEligiblePolitician(Pol(1), Faction.同盟));
            Assert.IsFalse(ElectionCycleRules.IsEligiblePolitician(null, Faction.同盟));
            Assert.IsFalse(ElectionCycleRules.IsEligiblePolitician(Pol(1, Faction.帝国), Faction.同盟));
            var soldier = Pol(2); soldier.role = PersonRole.軍人;
            Assert.IsFalse(ElectionCycleRules.IsEligiblePolitician(soldier, Faction.同盟));
            var bureaucrat = Pol(3); bureaucrat.isPolitician = false;
            Assert.IsFalse(ElectionCycleRules.IsEligiblePolitician(bureaucrat, Faction.同盟));
            var captive = Pol(4); captive.captiveStatus = CaptiveStatus.捕虜;
            Assert.IsFalse(ElectionCycleRules.IsEligiblePolitician(captive, Faction.同盟));
            Assert.IsTrue(ElectionCycleRules.IsValidPartyMember(captive, Faction.同盟), "拘束中は党籍を保つ");
            var ronin = Pol(5); ronin.isFreeAgent = true;
            Assert.IsFalse(ElectionCycleRules.IsEligiblePolitician(ronin, Faction.同盟));
            // 官位（位階）は問わない＝無位でも資格あり、人物は書き換えない
            Assert.AreEqual(CourtRank.無位, Pol(6).courtRank);
            Assert.IsTrue(ElectionCycleRules.IsEligiblePolitician(Pol(6), Faction.同盟));
        }

        [Test]
        public void NationalVotes_StableRegionFavorsRulingParty()
        {
            var parties = new List<Party>
            {
                new Party(1, "与党", Faction.同盟) { support = 0.5f },
                new Party(2, "野党", Faction.同盟) { support = 0.5f },
            };
            var stable = new List<RegionalElectorate> { new RegionalElectorate(1, 100f, 1f) };
            var unrest = new List<RegionalElectorate> { new RegionalElectorate(1, 100f, 0f) };

            List<VoteTally> a = ElectionCycleRules.NationalVotes(parties, stable, 1, 0.2f);
            Assert.AreEqual(60f, a[0].votes, 1e-3f);
            Assert.AreEqual(40f, a[1].votes, 1e-3f);
            List<VoteTally> b = ElectionCycleRules.NationalVotes(parties, unrest, 1, 0.2f);
            Assert.AreEqual(40f, b[0].votes, 1e-3f);
            Assert.AreEqual(60f, b[1].votes, 1e-3f);
            // 有権者のいない地域だけなら支持率そのもの
            List<VoteTally> c = ElectionCycleRules.NationalVotes(parties, new List<RegionalElectorate> { new RegionalElectorate(1, 0f, 1f) }, 1, 0.2f);
            Assert.AreEqual(0.5f, c[0].votes, 1e-4f);
        }

        [Test]
        public void NoParties_NoSeats_RetryLater()
        {
            var s = new FactionState(Faction.同盟) { governmentForm = GovernmentForm.共和制, politics = new PoliticsState() };
            NationalYearOutcome o = ElectionCycleRules.RunNationalYear(s, 800, NoElection(), null, new List<Person>(), Prm);
            Assert.IsNull(o.lowerRecord);
            Assert.IsFalse(ElectionCycleRules.IsSeated(s.politics));
        }

        [Test]
        public void SuspendNational_OnlyOnce_AndOnlyIfElectionsHappened()
        {
            Assert.IsFalse(ElectionCycleRules.SuspendNational(new PoliticsState(), 800, "x", out _));

            var s = State(0.5f, 0.375f, 0.125f);
            var roster = new List<Person> { Pol(10), Pol(11), Pol(12) };
            ElectionCycleRules.RunNationalYear(s, 800, NoElection(), null, roster, Prm);

            Assert.IsTrue(ElectionCycleRules.SuspendNational(s.politics, 801, "政体移行", out int prev));
            Assert.AreEqual(10, prev);
            Assert.AreEqual(CabinetStatus.対象外, s.politics.government.status);
            Assert.AreEqual(-1, s.politics.government.premierPersonId);
            Assert.IsFalse(ElectionCycleRules.SuspendNational(s.politics, 802, "政体移行", out _));
            Assert.AreEqual(150, ElectionCycleRules.SeatsOf(s.politics.lowerSeats, 1), "議席は履歴として残す");
        }

        [Test]
        public void RecentResults_AreCapped()
        {
            var s = State(0.5f, 0.5f);
            var roster = new List<Person> { Pol(10), Pol(11) };
            ElectionCycleRules.RunNationalYear(s, 800, NoElection(), null, roster, Prm);
            var tick = NoElection();
            tick.lowerHouseElection = true;
            for (int y = 801; y <= 812; y++) ElectionCycleRules.RunNationalYear(s, y, tick, null, roster, Prm);
            Assert.AreEqual(Prm.maxNationalRecords, s.politics.recentResults.Count);
            Assert.AreEqual(812, s.politics.recentResults[s.politics.recentResults.Count - 1].year);
        }
    }
}
