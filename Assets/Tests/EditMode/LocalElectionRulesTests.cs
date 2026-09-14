using System.Collections.Generic;
using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 星系知事選（LocalElectionRules）を固定する：初回日程・当選と任期・同票の決着・候補不在の不成立（捏造しない）・
    /// 同じ年の再処理なし・首相と他星系知事の兼任禁止・現職の再選・死亡による補欠選挙・占領による失職と新規編入の猶予・
    /// 非民主での停止と民主化での再開・地方の民意（安定度）が国政の与党をそのまま写さないこと。
    /// </summary>
    public class LocalElectionRulesTests
    {
        static readonly LocalElectionParams Prm = LocalElectionParams.Default;

        static Person Pol(int id, Faction f = Faction.同盟)
            => new Person(id, "政治家" + id, f, PersonRole.文民) { isPolitician = true, birthYear = 760 };

        /// <summary>党1（与党・党員10）と党2（野党・党員11）、支持は各0.5。</summary>
        static PoliticsState TwoParties(int premierId = -1)
        {
            var pol = new PoliticsState();
            var p1 = new Party(1, "与党", Faction.同盟) { support = 0.5f, leaderId = 10 };
            p1.memberIds.Add(10);
            var p2 = new Party(2, "野党", Faction.同盟) { support = 0.5f, leaderId = 11 };
            p2.memberIds.Add(11);
            pol.parties.Add(p1);
            pol.parties.Add(p2);
            pol.government = new GovernmentFormation { status = CabinetStatus.少数政権, partyId = 1, premierPersonId = premierId };
            return pol;
        }

        static List<LocalConstituency> Systems(float stability01, params int[] ids)
        {
            var list = new List<LocalConstituency>();
            foreach (int id in ids) list.Add(new LocalConstituency(id, 100f, stability01, ""));
            return list;
        }

        static List<LocalElectionEvent> Cycle(PoliticsState pol, int year, List<LocalConstituency> owned, List<Person> roster)
        {
            var ev = LocalElectionRules.Reconcile(pol, owned, year, Prm);
            ev.AddRange(LocalElectionRules.RunDue(pol, Faction.同盟, year, owned, roster, Prm));
            return ev;
        }

        [Test]
        public void FirstSeeding_ElectsSameYear_WithFourYearTerm_TieGoesToSmallerId()
        {
            var pol = TwoParties();
            var owned = Systems(0.5f, 3);
            var ev = Cycle(pol, 800, owned, new List<Person> { Pol(11), Pol(10) });

            LocalElectionState rec = LocalElectionRules.Find(pol, 3);
            Assert.IsTrue(pol.localsSeeded);
            Assert.AreEqual(LocalElectionStatus.当選, rec.status);
            Assert.AreEqual(10, rec.governorPersonId, "安定度0.5＝政権評価も現職補正も中立→同票は人物ID小");
            Assert.AreEqual(1, rec.governorPartyId);
            Assert.AreEqual(804, rec.termEndYear);
            Assert.AreEqual(804, rec.nextElectionYear);
            Assert.AreEqual(2, rec.lastResults.Count);
            Assert.AreEqual(0.5f, rec.lastResults[0].voteShare, 1e-4f);
            Assert.AreEqual(1, ev.Count);
            Assert.AreEqual(LocalElectionEventKind.当選, ev[0].kind);
        }

        [Test]
        public void CandidateVotes_DefaultProfile_FixedValue()
        {
            // 人望50→人気0.5、情報0→弁舌0 → 党員票訴求 0.35 × 人口100 × 党の地力(0.5+0.5)
            var k = new LocalConstituency(1, 100f, 0.5f, "");
            float v = LocalElectionRules.CandidateVotes(ElectionCycleRules.ProfileOf(Pol(10), 0.5f), true, 0.5f, true, false, false, k, Prm);
            Assert.AreEqual(35f, v, 1e-3f);
        }

        [Test]
        public void LocalMood_NotACopyOfNational_UnrestFavorsOpposition()
        {
            var roster = new List<Person> { Pol(10), Pol(11) };
            var unrest = TwoParties();
            Cycle(unrest, 800, Systems(0f, 1), roster);
            Assert.AreEqual(11, LocalElectionRules.Find(unrest, 1).governorPersonId, "荒れた星系では国政の与党でなく野党候補");

            var stable = TwoParties();
            Cycle(stable, 800, Systems(1f, 1), roster);
            Assert.AreEqual(10, LocalElectionRules.Find(stable, 1).governorPersonId);
        }

        [Test]
        public void NoCandidates_Fails_WithReasonAndRetryYear_NoFabrication_NoSameYearRepeat()
        {
            var pol = TwoParties();
            var owned = Systems(0.5f, 1);
            var ev = Cycle(pol, 800, owned, new List<Person>());

            LocalElectionState rec = LocalElectionRules.Find(pol, 1);
            Assert.AreEqual(LocalElectionStatus.不成立, rec.status);
            Assert.AreEqual(-1, rec.governorPersonId);
            Assert.AreEqual(LocalElectionRules.NoCandidateReason, rec.reason);
            Assert.AreEqual(801, rec.nextElectionYear);
            Assert.AreEqual(1, ev.Count);
            Assert.AreEqual(LocalElectionEventKind.不成立, ev[0].kind);

            Assert.AreEqual(0, Cycle(pol, 800, owned, new List<Person>()).Count, "同じ年は再処理しない");

            var later = Cycle(pol, 801, owned, new List<Person> { Pol(11) });
            Assert.AreEqual(LocalElectionEventKind.当選, later[0].kind);
            Assert.AreEqual(11, rec.governorPersonId);
        }

        [Test]
        public void PremierAndOtherGovernors_CannotRun()
        {
            var pol = TwoParties(premierId: 10);
            var owned = Systems(0.5f, 1, 2);
            Cycle(pol, 800, owned, new List<Person> { Pol(10), Pol(11) });

            Assert.AreEqual(11, LocalElectionRules.Find(pol, 1).governorPersonId);
            LocalElectionState second = LocalElectionRules.Find(pol, 2);
            Assert.AreEqual(LocalElectionStatus.不成立, second.status, "首相10は出馬不可・11は星系1の知事");
            Assert.AreEqual(-1, second.governorPersonId);
        }

        [Test]
        public void Incumbent_NotReelectedMidTerm_ReelectedAtTermEnd()
        {
            var pol = TwoParties();
            var owned = Systems(0.5f, 1);
            var roster = new List<Person> { Pol(11) };
            Cycle(pol, 800, owned, roster);
            Assert.AreEqual(0, Cycle(pol, 801, owned, roster).Count);

            var ev = Cycle(pol, 804, owned, roster);
            Assert.AreEqual(1, ev.Count);
            Assert.AreEqual(LocalElectionEventKind.再選, ev[0].kind);
            Assert.AreEqual(808, LocalElectionRules.Find(pol, 1).termEndYear);
        }

        [Test]
        public void GovernorDies_ByElectionSameYear()
        {
            var pol = TwoParties();
            var owned = Systems(0.5f, 1);
            var roster = new List<Person> { Pol(11), Pol(12) };
            Cycle(pol, 800, owned, roster);
            Assert.AreEqual(11, LocalElectionRules.Find(pol, 1).governorPersonId);

            roster[0].deathYear = 802;
            var ev = Cycle(pol, 802, owned, roster);
            Assert.AreEqual(LocalElectionEventKind.失職, ev[0].kind);
            Assert.AreEqual(11, ev[0].previousPersonId);
            Assert.AreEqual(LocalElectionEventKind.当選, ev[1].kind);
            Assert.AreEqual(12, LocalElectionRules.Find(pol, 1).governorPersonId);
            Assert.AreEqual(806, LocalElectionRules.Find(pol, 1).termEndYear);
        }

        [Test]
        public void GovernorBecomesPremier_ResignsAndByElection()
        {
            var pol = TwoParties();
            var owned = Systems(0.5f, 1);
            var roster = new List<Person> { Pol(10), Pol(11) };
            Cycle(pol, 800, owned, roster);
            Assert.AreEqual(10, LocalElectionRules.Find(pol, 1).governorPersonId);

            pol.government.premierPersonId = 10;
            var ev = Cycle(pol, 801, owned, roster);
            Assert.AreEqual(LocalElectionEventKind.失職, ev[0].kind);
            StringAssert.Contains("首相就任", ev[0].reason);
            Assert.AreEqual(11, LocalElectionRules.Find(pol, 1).governorPersonId);
        }

        [Test]
        public void Occupation_RemovesGovernor_NewSystemGetsGraceYear()
        {
            var pol = TwoParties();
            var roster = new List<Person> { Pol(11) };
            Cycle(pol, 800, Systems(0.5f, 1), roster);

            var ev = Cycle(pol, 801, Systems(0.5f, 2), roster); // 星系1を失い星系2を得た
            Assert.AreEqual(LocalElectionEventKind.失職, ev[0].kind);
            Assert.AreEqual(1, ev[0].systemId);
            Assert.AreEqual(11, ev[0].previousPersonId);
            Assert.IsNull(LocalElectionRules.Find(pol, 1));
            LocalElectionState gained = LocalElectionRules.Find(pol, 2);
            Assert.AreEqual(802, gained.nextElectionYear);
            Assert.AreEqual(-1, gained.governorPersonId);
            StringAssert.Contains("新規編入", gained.reason);
            Assert.AreEqual(1, ev.Count, "猶予中は選挙しない");
        }

        [Test]
        public void Suspend_ThenDemocratize_Resumes()
        {
            var pol = TwoParties();
            var owned = Systems(0.5f, 1);
            var roster = new List<Person> { Pol(11) };
            Cycle(pol, 800, owned, roster);

            var ev = LocalElectionRules.Suspend(pol, "政体移行");
            Assert.AreEqual(1, ev.Count);
            LocalElectionState rec = LocalElectionRules.Find(pol, 1);
            Assert.AreEqual(LocalElectionStatus.対象外, rec.status);
            Assert.AreEqual(-1, rec.governorPersonId);
            Assert.AreEqual(0, LocalElectionRules.Suspend(pol, "政体移行").Count);
            Assert.AreEqual(0, LocalElectionRules.RunDue(pol, Faction.同盟, 802, owned, roster, Prm).Count, "対象外は選挙しない");

            LocalElectionRules.Reconcile(pol, owned, 803, Prm);
            Assert.AreEqual(LocalElectionStatus.未実施, rec.status);
            Assert.AreEqual(804, rec.nextElectionYear);
        }

        [Test]
        public void NullSafe()
        {
            Assert.AreEqual(0, LocalElectionRules.Reconcile(null, null, 800, Prm).Count);
            Assert.AreEqual(0, LocalElectionRules.RunDue(null, Faction.同盟, 800, null, null, Prm).Count);
            Assert.AreEqual(0, LocalElectionRules.Suspend(null, "x").Count);
            Assert.IsNull(LocalElectionRules.Find(null, 1));
        }
    }
}
