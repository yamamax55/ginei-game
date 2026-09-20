using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Ginei;

namespace Ginei.Tests
{
    public class PartyLeadershipOperationRulesTests
    {
        const Faction F = Faction.同盟;
        const int Year = 820;

        static Person Politician(int id) => new Person(id, "候補" + id, F, PersonRole.文民)
        { isPolitician = true, birthYear = 780, charisma = 50 };

        static PoliticsState World(out Party party, out List<Person> roster)
        {
            party = new Party(1, "共和党", F);
            party.memberIds.AddRange(new[] { 1, 2, 3, 4 });
            roster = new List<Person> { Politician(1), Politician(2), Politician(3), Politician(4) };
            var pol = new PoliticsState();
            pol.parties.Add(party);
            pol.lowerSeats = new ChamberSeats(LegislativeChamber.下院, 4, 0) { seated = true };
            pol.lowerSeats.parties.Add(new PartySeatCount(1) { classA = 4 });
            for (int i = 1; i <= 4; i++) pol.legislators.Add(new LegislatorRecord(i)
            { lowerWins = i, seated = true, seatChamber = LegislativeChamber.下院, seatPartyId = 1 });
            return pol;
        }

        [Test]
        public void ManualElection_AnnounceDeclareWithdrawCloseConduct_PersistsStages()
        {
            PoliticsState pol = World(out Party party, out List<Person> roster);
            Assert.IsTrue(PartyLeadershipOperationRules.Announce(party, Year, "任期満了", out string why), why);
            Assert.AreEqual(LeadershipElectionPhase.立候補受付, party.leadership.process.phase);
            Assert.IsTrue(PartyLeadershipOperationRules.Declare(party, F, 1, Year, roster, out why), why);
            Assert.IsTrue(PartyLeadershipOperationRules.Declare(party, F, 2, Year, roster, out why), why);
            Assert.IsTrue(PartyLeadershipOperationRules.Withdraw(party, 2, out why), why);
            Assert.IsTrue(PartyLeadershipOperationRules.Declare(party, F, 3, Year, roster, out why), why);
            Assert.IsTrue(PartyLeadershipOperationRules.CloseNominations(party, out why), why);
            Assert.AreEqual(LeadershipElectionPhase.投開票待ち, party.leadership.process.phase);

            LeadershipElectionRecord rec = PartyLeadershipOperationRules.Conduct(pol, F, party, roster, null,
                PartyLeadershipParams.Default.With(noiseWeight: 0f), out why);
            Assert.IsNotNull(rec, why);
            Assert.AreEqual(LeadershipElectionPhase.完了, party.leadership.process.phase);
            Assert.IsNotNull(rec.Candidate(1));
            Assert.IsNotNull(rec.Candidate(3));
            Assert.IsNull(rec.Candidate(2), "撤回済み候補は投開票へ進まない");

            var campaign = new CampaignState(new GalaxyMap());
            campaign.states.Add(new FactionState(F) { politics = pol });
            Party restored = CampaignSerializer.FromJson(CampaignSerializer.ToJson(campaign)).states[0].politics.parties[0];
            Assert.AreEqual(LeadershipElectionPhase.完了, restored.leadership.process.phase);
            Assert.AreEqual(3, restored.leadership.process.candidacies.Count);
            Assert.IsTrue(restored.leadership.process.candidacies[1].withdrawn);
        }

        [Test]
        public void FactionOperations_CreateMoveLeaveAndManualSupport_AffectElectionRecord()
        {
            PoliticsState pol = World(out Party party, out List<Person> roster);
            Assert.IsTrue(PartyLeadershipOperationRules.CreateFaction(party, 1, "改革派", "", out string why), why);
            Assert.IsTrue(PartyLeadershipOperationRules.CreateFaction(party, 2, "穏健派", "", out why), why);
            int reform = party.factions[0].id, moderate = party.factions[1].id;
            Assert.IsTrue(PartyLeadershipOperationRules.JoinFaction(party, 3, reform, out why), why);
            Assert.IsTrue(PartyLeadershipOperationRules.JoinFaction(party, 3, moderate, out why), why);
            Assert.AreEqual(moderate, PartyLeadershipRules.FactionOf(party, 3).id);
            Assert.IsTrue(PartyLeadershipOperationRules.LeaveFaction(party, 3, out why), why);
            Assert.IsNull(PartyLeadershipRules.FactionOf(party, 3));

            Assert.IsTrue(PartyLeadershipOperationRules.Announce(party, Year, "任期満了", out why), why);
            Assert.IsTrue(PartyLeadershipOperationRules.Declare(party, F, 1, Year, roster, out why), why);
            Assert.IsTrue(PartyLeadershipOperationRules.Declare(party, F, 2, Year, roster, out why), why);
            Assert.IsTrue(PartyLeadershipOperationRules.SetFactionSupport(party, moderate, 1, out why), why);
            Assert.IsTrue(PartyLeadershipOperationRules.CloseNominations(party, out why), why);
            LeadershipElectionRecord rec = PartyLeadershipOperationRules.Conduct(pol, F, party, roster, null,
                PartyLeadershipParams.Default.With(noiseWeight: 0f, minEndorsers: 0, endorsementRatio: 0f), out why);
            Assert.IsNotNull(rec, why);
            LeadershipFactionStance stance = rec.factions.Find(x => x.factionId == moderate);
            Assert.IsNotNull(stance);
            Assert.AreEqual(1, stance.endorsedRound1);
            StringAssert.Contains("事前決定", stance.reason);
        }

        [Test]
        public void InvalidOperations_DoNotMutate()
        {
            World(out Party party, out List<Person> roster);
            Assert.IsFalse(PartyLeadershipOperationRules.Declare(party, F, 1, Year, roster, out _));
            Assert.IsFalse(PartyLeadershipOperationRules.CreateFaction(party, 99, "部外者", "", out _));
            Assert.IsTrue(PartyLeadershipOperationRules.Announce(party, Year, "", out _));
            Assert.IsFalse(PartyLeadershipOperationRules.Declare(party, F, 99, Year, roster, out _));
        }
    }
}
