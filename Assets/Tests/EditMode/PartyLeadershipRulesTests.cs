using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 総裁選・党内派閥・当選回数による序列（PartyLeadershipRules / PartySeniorityRules / LeadershipElectionRules の拡張）を固定する：
    /// 厳密な過半数と算定票の正規化、第1回の過半数当選、上位2人の決選（議員票＋地方票）の逆転、小党・候補1人・候補ゼロ・有効票ゼロ、
    /// 推薦人の一人一候補（届出の重複・順序に依らない）、派閥の重複整理と一人1票、結束が全員を強制しない造反と seed の再現、
    /// 一般党員が不明なら党員票なし、年功の逓減と若手の抜擢、任期3年と連続任期の上限、同じ年の再処理なし、首相・議席を変えない、保存往復。
    /// </summary>
    public class PartyLeadershipRulesTests
    {
        const int Year = 800;
        const Faction F = Faction.同盟;

        /// <summary>揺らぎなし・派閥の推薦が強く効く（票の行き先を固定して数える）。</summary>
        static readonly PartyLeadershipParams Det = PartyLeadershipParams.Default.With(noiseWeight: 0f, factionWeight: 10f, relationWeight: 0f, policyWeight: 0f);

        static Person Pol(int id, int charisma = 50, int birthSystemId = -1)
            => new Person(id, "政治家" + id, F, PersonRole.文民) { isPolitician = true, birthYear = 760, charisma = charisma, birthSystemId = birthSystemId };

        /// <summary>党1つ（党員＝members）と、下院に legislators だけの実在議員（議席＝人数・集計議席0）。</summary>
        static PoliticsState World(int[] members, int[] legislators, out Party party)
        {
            var pol = new PoliticsState();
            party = new Party(1, "民政党", F);
            party.memberIds.AddRange(members);
            pol.parties.Add(party);
            if (legislators.Length > 0)
            {
                pol.lowerSeats = new ChamberSeats(LegislativeChamber.下院, legislators.Length, 0) { seated = true };
                pol.lowerSeats.parties.Add(new PartySeatCount(1) { classA = legislators.Length });
                for (int i = 0; i < legislators.Length; i++)
                    pol.legislators.Add(new LegislatorRecord(legislators[i])
                    {
                        lowerWins = 1, seated = true, seatChamber = LegislativeChamber.下院, seatClass = -1, seatPartyId = 1,
                    });
            }
            return pol;
        }

        static PartyFaction Fac(int id, int boss, float cohesion, params int[] members)
        {
            var pf = new PartyFaction(id, "派" + id, boss) { cohesion = cohesion };
            pf.memberIds.AddRange(members);
            return pf;
        }

        static List<Person> Roster(int from, int to)
        {
            var list = new List<Person>();
            for (int i = from; i <= to; i++) list.Add(Pol(i));
            return list;
        }

        static void SetRegion(Party p, int systemId, long members)
            => PartyMembershipRules.SetGeneralMembership(p, systemId, members, "支部集計", Year);

        static int[] Range(int from, int to)
        {
            var a = new int[to - from + 1];
            for (int i = 0; i < a.Length; i++) a[i] = from + i;
            return a;
        }

        // ===== 票の数学 =====

        [Test]
        public void Math_StrictMajority_Conversion_Seed()
        {
            Assert.IsFalse(LeadershipElectionRules.StrictMajority(50, 100), "ちょうど50%は過半数でない");
            Assert.IsTrue(LeadershipElectionRules.StrictMajority(51, 100));
            Assert.IsTrue(LeadershipElectionRules.StrictMajority(1, 1));
            Assert.IsFalse(LeadershipElectionRules.StrictMajority(0, 0));
            Assert.AreEqual(51, LeadershipElectionRules.MajorityNeeded(100));
            Assert.AreEqual(6, LeadershipElectionRules.MajorityNeeded(11));

            var ids = new List<int> { 1, 2, 3 };
            CollectionAssert.AreEqual(new[] { 5, 3, 2 }, LeadershipElectionRules.ConvertToAllotment(ids, new long[] { 500, 300, 200 }, 10));
            CollectionAssert.AreEqual(new[] { 4, 3, 3 }, LeadershipElectionRules.ConvertToAllotment(ids, new long[] { 1, 1, 1 }, 10), "端数は同剰余なら候補ID小");
            CollectionAssert.AreEqual(new[] { 0, 0, 0 }, LeadershipElectionRules.ConvertToAllotment(ids, new long[] { 0, 0, 0 }, 10), "生票0なら算定票0");

            Assert.AreEqual(LeadershipElectionRules.SeedOf("a"), LeadershipElectionRules.SeedOf("a"));
            Assert.AreNotEqual(LeadershipElectionRules.SeedOf("1:党1:総裁選:800"), LeadershipElectionRules.SeedOf("1:党1:総裁選:803"));
            float r = LeadershipElectionRules.SeededRoll(123, 4, 5);
            Assert.AreEqual(r, LeadershipElectionRules.SeededRoll(123, 4, 5));
            Assert.IsTrue(r >= 0f && r < 1f);
            Assert.AreEqual(3, PartyLeadershipRules.RequiredEndorsers(50, PartyLeadershipParams.Default), "50人×7%＝3人");
            Assert.AreEqual(1, PartyLeadershipRules.RequiredEndorsers(10, PartyLeadershipParams.Default), "下限1人");
            Assert.AreEqual(20, PartyLeadershipRules.RequiredEndorsers(1000, PartyLeadershipParams.Default), "上限20人");
            Assert.AreEqual(0, PartyLeadershipRules.RequiredEndorsers(1, PartyLeadershipParams.Default), "一人党は推薦不要");
            Assert.AreEqual(2, PartyLeadershipRules.RequiredEndorsers(3, PartyLeadershipParams.Default.With(minEndorsers: 5)), "投票者−1を超えない");
        }

        // ===== 第1回で過半数 =====

        [Test]
        public void FirstRound_StrictMajority_WithConvertedMemberVotes_AndFactionsMainstream()
        {
            PoliticsState pol = World(Range(1, 10), Range(1, 10), out Party party);
            party.factions.Add(Fac(1, 1, 1f, 1, 2, 3, 4, 5, 6));
            party.factions.Add(Fac(2, 7, 1f, 7, 8, 9, 10));
            SetRegion(party, 100, 600);
            SetRegion(party, 200, 400);
            List<Person> roster = Roster(1, 10);
            roster[0].birthSystemId = 100; // 候補1の地盤
            roster[6].birthSystemId = 200; // 候補7の地盤

            List<LeadershipElectionRecord> held = PartyLeadershipRules.TickYear(pol, F, Year, roster, null, Det.With(maxCandidates: 2));
            Assert.AreEqual(1, held.Count);
            LeadershipElectionRecord rec = held[0];

            Assert.AreEqual(LeadershipOutcome.第1回当選, rec.outcome);
            Assert.AreEqual(1, rec.winnerId);
            Assert.AreEqual(1, party.leaderId);
            Assert.IsFalse(rec.runoffHeld);
            LeadershipCandidateResult a = rec.Candidate(1), b = rec.Candidate(7);
            Assert.AreEqual(6, a.round1Named);
            Assert.AreEqual(4, b.round1Named);
            Assert.AreEqual(10, rec.namedVoters, "ネームド議員は一人1票");
            Assert.AreEqual(1000L, rec.memberRawTotal);
            Assert.AreEqual(513L, a.round1MemberRaw);
            Assert.AreEqual(487L, b.round1MemberRaw);
            Assert.AreEqual(10, rec.memberAllotment, "算定票の総数＝議員票の総数");
            Assert.AreEqual(10, a.round1MemberConverted + b.round1MemberConverted);
            Assert.AreEqual(5, a.round1MemberConverted);
            Assert.AreEqual(5, b.round1MemberConverted);
            Assert.AreEqual(20, rec.round1Total);
            Assert.AreEqual(11, a.round1Total, "11/20 は厳密な過半数");
            StringAssert.Contains("過半数", rec.reason);
            Assert.AreEqual(LeadershipCandidateStatus.当選, a.status);
            Assert.AreEqual(LeadershipCandidateStatus.第1回落選, b.status);

            Assert.AreEqual(Year, rec.termStartYear);
            Assert.AreEqual(Year + 3, rec.termEndYear, "任期3年");
            Assert.AreEqual(Year + 3, party.leadership.nextElectionYear);
            Assert.IsTrue(party.factions[0].mainstream);
            Assert.IsFalse(party.factions[1].mainstream);
            Assert.AreEqual(1, party.factions[0].endorsedCandidateId);
            Assert.AreEqual(2, rec.factions.Count);
            Assert.AreEqual(6, rec.factions[0].membersVoted);
            Assert.AreEqual(6, rec.factions[0].membersFollowed);
            Assert.AreEqual(4, rec.factions[1].membersVoted);
            Assert.AreEqual(1, LegislatorRosterRules.Find(pol, 1).lowerWins, "党首選の勝利は当選回数に数えない");
        }

        // ===== 決選 =====

        /// <summary>候補1＝党員に人気（派閥2人）、候補3＝最大派閥の領袖（5人）、候補8＝3人派閥の領袖（候補3に忠誠）。</summary>
        static PoliticsState RunoffWorld(out Party party, out List<Person> roster)
        {
            PoliticsState pol = World(Range(1, 10), Range(1, 10), out party);
            party.factions.Add(Fac(1, 1, 1f, 1, 2));
            party.factions.Add(Fac(2, 3, 1f, 3, 4, 5, 6, 7));
            party.factions.Add(Fac(3, 8, 1f, 8, 9, 10));
            roster = Roster(1, 10);
            roster[0].charisma = 100;
            roster[2].charisma = 0;
            roster[7].charisma = 0;
            roster[7].loyaltyTargetId = 3;
            return pol;
        }

        [Test]
        public void NoMajority_RunoffTopTwo_LegislatorsAndRegions_ReversesFirstRound()
        {
            PoliticsState pol = RunoffWorld(out Party party, out List<Person> roster);
            SetRegion(party, 1, 100);
            SetRegion(party, 2, 100);
            SetRegion(party, 3, 100);

            LeadershipElectionRecord rec = PartyLeadershipRules.TickYear(pol, F, Year, roster, null, Det.With(relationWeight: 5f))[0];

            LeadershipCandidateResult a = rec.Candidate(1), b = rec.Candidate(3), c = rec.Candidate(8);
            Assert.AreEqual(162L, a.round1MemberRaw);
            Assert.AreEqual(69L, b.round1MemberRaw);
            Assert.AreEqual(69L, c.round1MemberRaw);
            Assert.AreEqual(6, a.round1MemberConverted);
            Assert.AreEqual(8, a.round1Total);
            Assert.AreEqual(7, b.round1Total);
            Assert.AreEqual(5, c.round1Total);
            Assert.AreEqual(20, rec.round1Total);

            Assert.IsTrue(rec.runoffHeld, "最多8/20は過半数でない＝決選");
            Assert.AreEqual(LeadershipCandidateStatus.第1回落選, c.status);
            Assert.AreEqual(3, rec.regions.Count, "党員集計のある星系ごと1票");
            for (int i = 0; i < rec.regions.Count; i++) Assert.AreEqual(1, rec.regions[i].voteFor, "各星系の第1回生票は候補1が多い");
            Assert.AreEqual(2, a.runoffNamed);
            Assert.AreEqual(3, a.runoffRegional);
            Assert.AreEqual(8, b.runoffNamed, "候補8の派閥が候補3へ支持を変更");
            Assert.AreEqual(5, a.runoffTotal);
            Assert.AreEqual(8, b.runoffTotal);
            Assert.AreEqual(13, rec.runoffTotal);
            Assert.AreEqual(LeadershipOutcome.決選当選, rec.outcome);
            Assert.AreEqual(3, rec.winnerId, "第1回2位が決選で逆転");
            Assert.AreEqual(3, party.leaderId);
            Assert.AreEqual(LeadershipCandidateStatus.決選落選, a.status);

            LeadershipFactionStance moved = rec.factions[2];
            Assert.AreEqual(8, moved.endorsedRound1);
            Assert.AreEqual(3, moved.endorsedRunoff);
            StringAssert.Contains("支持を変更", moved.reason);
            Assert.IsTrue(moved.mainstream);
            Assert.IsFalse(rec.factions[0].mainstream);
        }

        [Test]
        public void NationalMembersOnly_RunoffHasNoRegionalVotes_Restricted()
        {
            PoliticsState pol = RunoffWorld(out Party party, out List<Person> roster);
            PartyMembershipRules.SetGeneralMembership(party, PartyMembershipTally.NationalScope, 1000, "党公表", Year);

            LeadershipElectionRecord rec = PartyLeadershipRules.TickYear(pol, F, Year, roster, null, Det.With(relationWeight: 5f))[0];

            Assert.IsTrue(rec.runoffHeld);
            Assert.AreEqual(0, rec.regions.Count);
            Assert.AreEqual(0, rec.Candidate(1).runoffRegional);
            Assert.IsTrue(HasRestriction(rec, "地方票なし"));
            Assert.AreEqual(3, rec.winnerId);
        }

        [Test]
        public void UnknownMembers_NoMemberVotes_NotDerivedFromNamedCount()
        {
            PoliticsState pol = World(Range(1, 10), Range(1, 10), out Party party);
            party.factions.Add(Fac(1, 1, 1f, 1, 2, 3, 4, 5, 6));
            party.factions.Add(Fac(2, 7, 1f, 7, 8, 9, 10));

            LeadershipElectionRecord rec = PartyLeadershipRules.TickYear(pol, F, Year, Roster(1, 10), null, Det.With(maxCandidates: 2))[0];

            Assert.IsFalse(rec.memberVotesKnown);
            Assert.AreEqual(0L, rec.memberRawTotal, "ネームド党員10名を一般党員の票にしない");
            Assert.AreEqual(0, rec.memberAllotment);
            Assert.AreEqual(0, rec.Candidate(1).round1MemberConverted);
            Assert.AreEqual(10, rec.round1Total, "議員票だけ");
            Assert.IsTrue(HasRestriction(rec, "一般党員数が不明"));
            Assert.AreEqual(LeadershipOutcome.第1回当選, rec.outcome);
        }

        // ===== 小党・候補1人・候補ゼロ・有効票ゼロ =====

        [Test]
        public void SmallParty_NamedMembersVote_EndorsementsCollapseToUncontested()
        {
            PoliticsState pol = World(new[] { 21, 22, 23 }, new int[0], out Party party);
            var roster = new List<Person> { Pol(21, 80), Pol(22, 60), Pol(23, 40) };

            LeadershipElectionRecord rec = PartyLeadershipRules.TickYear(pol, F, Year, roster, null, PartyLeadershipParams.Default)[0];

            Assert.AreEqual(PartyLeadershipRules.VoterBasisSmallParty, rec.voterBasis);
            Assert.IsTrue(HasRestriction(rec, "ネームド党員"));
            Assert.AreEqual(1, rec.requiredEndorsers);
            Assert.AreEqual(LeadershipOutcome.無投票当選, rec.outcome);
            Assert.AreEqual(21, party.leaderId);
            Assert.AreEqual(LeadershipCandidateStatus.推薦人不足で撤回, rec.Candidate(22).status);
            Assert.AreEqual(LeadershipCandidateStatus.推薦人不足で撤回, rec.Candidate(23).status);
            CollectionAssert.AreEquivalent(new[] { 22, 23 }, rec.Candidate(21).endorserIds, "撤回した候補が推薦人に回る");
            Assert.AreEqual(Year + 3, party.leadership.termEndYear);

            PoliticsState solo = World(new[] { 31 }, new int[0], out Party soloParty);
            LeadershipElectionRecord one = PartyLeadershipRules.TickYear(solo, F, Year, new List<Person> { Pol(31) }, null, PartyLeadershipParams.Default)[0];
            Assert.AreEqual(0, one.requiredEndorsers);
            Assert.AreEqual(LeadershipOutcome.無投票当選, one.outcome);
            Assert.AreEqual(31, soloParty.leaderId);
        }

        [Test]
        public void NoEligibleMembers_Or_NoValidVotes_LeaderStaysVacant_NoFabrication()
        {
            PoliticsState pol = World(new[] { 41 }, new int[0], out Party party);
            Person dead = Pol(41);
            dead.deathYear = Year - 1;
            var roster = new List<Person> { dead };

            LeadershipElectionRecord rec = PartyLeadershipRules.TickYear(pol, F, Year, roster, null, PartyLeadershipParams.Default)[0];
            Assert.AreEqual(LeadershipOutcome.選出不能, rec.outcome);
            Assert.AreEqual(-1, rec.winnerId);
            Assert.AreEqual(-1, party.leaderId);
            StringAssert.Contains("選出待ち", party.leadership.pendingReason);
            Assert.AreEqual(Year + 1, party.leadership.nextElectionYear);
            Assert.AreEqual(0, PartyLeadershipRules.TickYear(pol, F, Year + 1, roster, null, PartyLeadershipParams.Default).Count,
                "選出不能が続く党で毎年の空記録を作らない");
            Assert.AreEqual(1, party.leadership.records.Count);
            Assert.AreEqual(Year + 1, party.leadership.lastElectionYear);

            // 投票者なし（小党例外を切る）・党員票不明＝候補が2人いても有効票0
            PoliticsState noVotes = World(new[] { 51, 52 }, new int[0], out Party p2);
            LeadershipElectionRecord zero = PartyLeadershipRules.TickYear(noVotes, F, Year, new List<Person> { Pol(51), Pol(52) }, null,
                PartyLeadershipParams.Default.With(smallPartyNamedMemberVotes: false))[0];
            Assert.AreEqual(PartyLeadershipRules.VoterBasisNone, zero.voterBasis);
            Assert.AreEqual(2, zero.candidates.Count);
            Assert.AreEqual(LeadershipOutcome.選出不能, zero.outcome);
            StringAssert.Contains("有効票がない", zero.reason);
            Assert.AreEqual(-1, p2.leaderId, "ゼロ票で当選を捏造しない");
        }

        [Test]
        public void DeclaredCandidacy_NoCandidates_IncumbentContinuesOrActingLeader()
        {
            // 届出の候補が資格なし（他勢力）＝候補ゼロ・現党首なし → 候補評価の最上位を暫定党首
            PoliticsState pol = World(new[] { 61, 62 }, new int[0], out Party party);
            var roster = new List<Person> { Pol(61, 90), Pol(62, 40) };
            var declared = new List<LeadershipCandidacy> { new LeadershipCandidacy(99, new List<int> { 61 }) };
            LeadershipElectionRecord rec = PartyLeadershipRules.RunElection(pol, F, party, Year, roster, null, PartyLeadershipParams.Default, "臨時", declared);
            Assert.AreEqual(LeadershipOutcome.暫定選出, rec.outcome);
            Assert.AreEqual(61, party.leaderId);
            Assert.AreEqual(Year + 1, party.leadership.termEndYear, "暫定は1年");
            Assert.IsTrue(HasRestriction(rec, "資格がない"));
            Assert.IsNull(PartyLeadershipRules.RunElection(pol, F, party, Year, roster, null, PartyLeadershipParams.Default, "臨時", declared),
                "同じ年に二度行わない");
        }

        // ===== 推薦人の一人一候補 =====

        static LeadershipElectionRecord DeclaredElection(bool reversed)
        {
            PoliticsState pol = World(Range(1, 6), Range(1, 6), out Party party);
            var roster = Roster(1, 6);
            roster[0].charisma = 80;
            var c1 = new LeadershipCandidacy(1, new List<int> { 3, 4, 5, 1, 99 });
            var c2 = new LeadershipCandidacy(2, new List<int> { 5, 6, 2, 6 });
            var declared = reversed ? new List<LeadershipCandidacy> { c2, c1 } : new List<LeadershipCandidacy> { c1, c2 };
            return PartyLeadershipRules.RunElection(pol, F, party, Year, roster, null, Det, "臨時", declared);
        }

        [Test]
        public void DeclaredEndorsers_OnePersonOneCandidate_DuplicatesAndInvalidNotCounted_OrderIndependent()
        {
            LeadershipElectionRecord rec = DeclaredElection(false);
            List<int> e1 = rec.Candidate(1).endorserIds, e2 = rec.Candidate(2).endorserIds;
            CollectionAssert.AreEquivalent(new[] { 3, 4, 5 }, e1, "二重に届けた推薦人5は支持の高い候補1だけに数える");
            CollectionAssert.AreEquivalent(new[] { 6 }, e2);
            Assert.AreEqual(4, e1.Count + e2.Count);
            Assert.IsTrue(HasRestriction(rec, "無効"), "候補本人・投票資格のない人の推薦は無効");

            LeadershipElectionRecord rev = DeclaredElection(true);
            CollectionAssert.AreEquivalent(e1, rev.Candidate(1).endorserIds, "届出の順序に依らない");
            CollectionAssert.AreEquivalent(e2, rev.Candidate(2).endorserIds);
            Assert.AreEqual(rec.winnerId, rev.winnerId);
            Assert.AreEqual(rec.seed, rev.seed);
        }

        // ===== 派閥の重複・一人1票 =====

        [Test]
        public void Factions_DuplicatesNormalized_VotesCountedOncePerPerson()
        {
            PoliticsState pol = World(Range(1, 6), Range(1, 6), out Party party);
            party.factions.Add(Fac(1, 1, 1f, 1, 2, 3));
            party.factions.Add(Fac(2, 4, 1f, 3, 4, 5, 1, 77));
            party.factions.Add(new PartyFaction(1, "重複ID"));
            party.factions.Add(null);

            var tally = LeadershipElectionRules.TallyLegislatorVotesByFaction(party.factions, new Dictionary<int, int> { { 1, 100 }, { 2, 200 } });
            Assert.AreEqual(3, tally[100]);
            Assert.AreEqual(3, tally[200], "二重に載る1と3は派閥ID小の派閥で1回だけ（4・5・77）");
            Assert.AreEqual(5, party.factions[1].Weight, "Weight は重複・負のIDを数えない所属者数");

            Assert.Greater(PartyLeadershipRules.NormalizeFactions(party), 0);
            Assert.AreEqual(2, party.factions.Count, "null と重複IDの派閥を除く");
            CollectionAssert.AreEqual(new[] { 1, 2, 3 }, party.factions[0].memberIds);
            CollectionAssert.AreEqual(new[] { 4, 5 }, party.factions[1].memberIds, "他派閥の領袖・二重所属・党外の人を除く");
            Assert.AreEqual(1, PartyLeadershipRules.UnaffiliatedCount(party), "6は無派閥");
            Assert.AreEqual(0, PartyLeadershipRules.NormalizeFactions(party), "2回目は変更なし");

            LeadershipElectionRecord rec = PartyLeadershipRules.TickYear(pol, F, Year, Roster(1, 6), null, Det.With(maxCandidates: 2))[0];
            Assert.AreEqual(6, rec.namedVoters, "議員6人＝6票");
            Assert.AreEqual(6, rec.round1Total, "党員票不明＝議員6票だけ");
            int voted = rec.unaffiliatedVoters;
            for (int i = 0; i < rec.factions.Count; i++) voted += rec.factions[i].membersVoted;
            Assert.AreEqual(6, voted, "派閥票と個人票を二重に数えない");
        }

        // ===== 結束は強制しない・seed の再現 =====

        static LeadershipElectionRecord CohesionElection(int year)
        {
            PoliticsState pol = World(Range(1, 8), Range(1, 8), out Party party);
            party.factions.Add(Fac(1, 1, 0.2f, 1, 2, 3, 4, 5));
            party.factions.Add(Fac(2, 6, 0.9f, 6, 7, 8));
            List<Person> roster = Roster(1, 8);
            roster[4].loyaltyTargetId = 6; // 5 は他派閥の領袖に忠誠
            return PartyLeadershipRules.TickYear(pol, F, year, roster, null, PartyLeadershipParams.Default.With(maxCandidates: 2))[0];
        }

        [Test]
        public void Cohesion_DoesNotForceAllMembers_DefectionReproducibleBySeed()
        {
            LeadershipElectionRecord rec = CohesionElection(Year);
            LeadershipFactionStance weak = rec.factions[0];
            Assert.AreEqual(1, weak.endorsedRound1);
            Assert.AreEqual(5, weak.membersVoted);
            Assert.LessOrEqual(weak.membersFollowed, 4, "結束の弱い派閥で忠誠の対象へ造反する人がいる");

            LeadershipElectionRecord again = CohesionElection(Year);
            Assert.AreEqual(rec.seed, again.seed);
            Assert.AreEqual(rec.electionId, again.electionId);
            Assert.AreEqual(rec.winnerId, again.winnerId);
            Assert.AreEqual(rec.Candidate(1).round1Named, again.Candidate(1).round1Named, "同じ選挙IDは同じ票");
            Assert.AreEqual(weak.membersFollowed, again.factions[0].membersFollowed);
            Assert.AreNotEqual(rec.seed, CohesionElection(Year + 3).seed, "選挙IDが違えば seed も違う");
        }

        // ===== 当選回数と年功 =====

        [Test]
        public void Seniority_DiminishingCapped_YoungAbleCanWin_NotAuthority()
        {
            PartySeniorityParams sp = PartySeniorityParams.Default;
            Assert.AreEqual(0f, PartySeniorityRules.Standing(0, sp));
            Assert.AreEqual(0.3125f, PartySeniorityRules.Standing(1, sp), 1e-4f);
            Assert.Less(PartySeniorityRules.Standing(3, sp), PartySeniorityRules.Standing(6, sp));
            Assert.Less(PartySeniorityRules.Standing(6, sp) - PartySeniorityRules.Standing(3, sp),
                        PartySeniorityRules.Standing(3, sp) - PartySeniorityRules.Standing(0, sp), "逓減");
            Assert.AreEqual(1f, PartySeniorityRules.Standing(12, sp), 1e-4f);
            Assert.AreEqual(1f, PartySeniorityRules.Standing(30, sp), 1e-4f, "頭打ち");
            Assert.AreEqual(SeniorityTier.履歴未登録, PartySeniorityRules.TierOf(false, 5, sp));
            Assert.AreEqual(SeniorityTier.新人, PartySeniorityRules.TierOf(true, 1, sp));
            Assert.AreEqual(SeniorityTier.中堅, PartySeniorityRules.TierOf(true, 3, sp));
            Assert.AreEqual(SeniorityTier.ベテラン, PartySeniorityRules.TierOf(true, 6, sp));

            var pol = new PoliticsState();
            pol.legislators.Add(new LegislatorRecord(1) { lowerWins = 10, consecutiveWins = 4, firstWinYear = 770 });
            pol.legislators.Add(new LegislatorRecord(2) { lowerWins = 1, consecutiveWins = 1, firstWinYear = 797 });
            Person veteran = Pol(1);
            Person young = Pol(2, 100);
            young.intelligence = 100;
            young.operation = 100;
            Person sameYoung = Pol(2);
            PartyLeadershipParams d = PartyLeadershipParams.Default;

            Assert.AreEqual(0.3935f, PartyLeadershipRules.CandidateStrength(pol, veteran, d), 1e-3f);
            Assert.Greater(PartyLeadershipRules.CandidateStrength(pol, young, d), PartyLeadershipRules.CandidateStrength(pol, veteran, d),
                "能力・実績の高い若手はベテランに勝てる");
            Assert.Greater(PartyLeadershipRules.CandidateStrength(pol, veteran, d), PartyLeadershipRules.CandidateStrength(pol, sameYoung, d),
                "同じ条件ならベテランが上");
            PartyLeadershipParams merit = d.With(seniorityCulture: 0f);
            Assert.AreEqual(PartyLeadershipRules.CandidateStrength(pol, sameYoung, merit), PartyLeadershipRules.CandidateStrength(pol, veteran, merit), 1e-5f,
                "実績重視の党文化では年功を見ない");
            Assert.AreEqual(10, LegislatorRosterRules.Find(pol, 1).lowerWins, "評価で当選回数を変えない");
            Assert.AreEqual(0, veteran.rankTier, "年功で階級を与えない");

            var party = new Party(1, "民政党", F);
            party.memberIds.AddRange(new[] { 3, 2, 1 });
            List<SeniorityInfo> rank = PartySeniorityRules.Ranking(pol, party, sp);
            Assert.AreEqual(1, rank[0].personId);
            Assert.AreEqual(2, rank[1].personId);
            Assert.AreEqual(3, rank[2].personId);
            Assert.IsFalse(rank[2].historyRegistered, "記録のない人は履歴未登録（0回を捏造しない）");
            Assert.AreEqual(SeniorityTier.履歴未登録, rank[2].tier);
        }

        // ===== 任期・連続任期・同じ年の再処理 =====

        [Test]
        public void Term_ThreeYears_SameYearNoChange_TermLimit_LegacyLeaderOnlyStartsTerm()
        {
            PoliticsState pol = World(new[] { 31, 32 }, new int[0], out Party party);
            var roster = new List<Person> { Pol(31, 90), Pol(32, 50) };
            PartyLeadershipParams one = PartyLeadershipParams.Default.With(maxConsecutiveTerms: 1);

            Assert.AreEqual(1, PartyLeadershipRules.TickYear(pol, F, Year, roster, null, one).Count);
            Assert.AreEqual(31, party.leaderId);
            Assert.AreEqual(1, party.leadership.consecutiveTerms);
            Assert.AreEqual(0, PartyLeadershipRules.TickYear(pol, F, Year, roster, null, one).Count, "同じ年の再処理はしない");
            Assert.AreEqual(Year + 3, party.leadership.termEndYear);
            Assert.AreEqual(1, party.leadership.records.Count);
            Assert.AreEqual(0, PartyLeadershipRules.TickYear(pol, F, Year + 1, roster, null, one).Count);
            Assert.AreEqual(0, PartyLeadershipRules.TickYear(pol, F, Year + 2, roster, null, one).Count);

            LeadershipElectionRecord next = PartyLeadershipRules.TickYear(pol, F, Year + 3, roster, null, one)[0];
            StringAssert.Contains("任期満了", next.trigger);
            Assert.IsTrue(HasRestriction(next, "上限"), "連続任期の上限で現党首は立てない");
            Assert.IsNull(next.Candidate(31));
            Assert.AreEqual(32, party.leaderId, "無限再選にならない");
            Assert.AreEqual(1, party.leadership.consecutiveTerms);

            // 就任年不明の現党首：任期を起算するだけ（総裁選も記録もしない）
            PoliticsState legacy = World(new[] { 31, 32 }, new int[0], out Party lp);
            lp.leaderId = 31;
            Assert.AreEqual(0, PartyLeadershipRules.TickYear(legacy, F, Year, roster, null, PartyLeadershipParams.Default).Count);
            Assert.AreEqual(31, lp.leaderId);
            Assert.AreEqual(Year + 3, lp.leadership.termEndYear);
            StringAssert.Contains("起算", lp.leadership.termNote);
            Assert.AreEqual(0, lp.leadership.records.Count);
        }

        [Test]
        public void OnlyTermLimitedIncumbent_ContinuesProvisionally()
        {
            PoliticsState pol = World(new[] { 31 }, new int[0], out Party party);
            var roster = new List<Person> { Pol(31) };
            PartyLeadershipParams one = PartyLeadershipParams.Default.With(maxConsecutiveTerms: 1);
            PartyLeadershipRules.TickYear(pol, F, Year, roster, null, one);
            LeadershipElectionRecord rec = PartyLeadershipRules.TickYear(pol, F, Year + 3, roster, null, one)[0];
            Assert.AreEqual(LeadershipOutcome.暫定続投, rec.outcome);
            Assert.AreEqual(31, party.leaderId);
            Assert.AreEqual(Year + 4, party.leadership.termEndYear, "暫定1年");
            StringAssert.Contains("暫定続投", party.leadership.pendingReason);
        }

        // ===== 首相・議席と分離 =====

        [Test]
        public void LeaderDefects_ElectionHeld_PremierSeatsAndWinsUnchanged()
        {
            var s = new FactionState(F) { governmentForm = GovernmentForm.共和制, politics = new PoliticsState() };
            var p1 = new Party(1, "民政党", F) { support = 0.5f, leaderId = 10 };
            p1.memberIds.AddRange(new[] { 10, 12, 14 });
            var p2 = new Party(2, "進歩党", F) { support = 0.5f, leaderId = 11 };
            p2.memberIds.AddRange(new[] { 11, 13, 15 });
            s.politics.parties.Add(p1);
            s.politics.parties.Add(p2);
            var roster = new List<Person> { Pol(10, 90), Pol(11, 80), Pol(12, 70), Pol(13, 60), Pol(14, 50), Pol(15, 40) };
            var tick = default(PoliticsTickRules.PoliticsTickResult);
            tick.upperClassUp = -1;
            var small = new ElectionCycleParams(2, 4, 8, 0.2f);
            NationalYearOutcome o = ElectionCycleRules.RunNationalYear(s, Year, tick, null, roster, small);
            LegislatorRosterRules.AssignElection(s.politics, F, o.lowerRecord, roster, null, LegislatorRosterParams.Default);
            LegislatorRosterRules.AssignElection(s.politics, F, o.upperRecord, roster, null, LegislatorRosterParams.Default);
            PoliticsState pol = s.politics;
            Assert.AreEqual(10, pol.government.premierPersonId);
            var wins = new Dictionary<int, int>();
            foreach (LegislatorRecord r in pol.legislators) wins[r.personId] = r.TotalWins;
            int seats1 = ElectionCycleRules.SeatsOf(pol.lowerSeats, 1);

            roster[0].faction = Faction.帝国; // 党首（首相）が離反
            List<LeadershipElectionRecord> held = PartyLeadershipRules.TickYear(pol, F, Year + 1, roster, null, PartyLeadershipParams.Default);

            LeadershipElectionRecord rec = held.Find(x => x.partyId == 1);
            Assert.IsNotNull(rec);
            StringAssert.Contains("離反", rec.trigger);
            Assert.AreEqual(10, rec.previousLeaderId);
            Assert.IsTrue(p1.leaderId == 12 || p1.leaderId == 14, "残る適格な党員から");
            Assert.IsNull(held.Find(x => x.partyId == 2), "党2は就任年の起算だけ");
            Assert.AreEqual(11, p2.leaderId);
            Assert.AreEqual(10, pol.government.premierPersonId, "総裁選は首相を変えない（組閣は別の手続き）");
            Assert.AreEqual(seats1, ElectionCycleRules.SeatsOf(pol.lowerSeats, 1), "議席を変えない");
            foreach (LegislatorRecord r in pol.legislators) Assert.AreEqual(wins[r.personId], r.TotalWins, "当選回数を変えない");

            // 管理下の党は旧来の党首補充をしない（総裁選を待つ）
            p1.leaderId = -1;
            ElectionCycleRules.OrganizeParties(pol, roster, F);
            Assert.AreEqual(-1, p1.leaderId, "管理下の党は OrganizeParties で党首を補充しない");
        }

        // ===== 保存 =====

        [Test]
        public void SaveRoundTrip_KeepsLeadershipRecordsAndFactions_LoadHoldsNoElection()
        {
            PoliticsState pol = RunoffWorld(out Party party, out List<Person> roster);
            SetRegion(party, 1, 100);
            SetRegion(party, 2, 100);
            SetRegion(party, 3, 100);
            LeadershipElectionRecord rec = PartyLeadershipRules.TickYear(pol, F, Year, roster, null, Det.With(relationWeight: 5f))[0];
            var legacy = new Party(2, "旧党", F) { leaderId = 50 };
            legacy.memberIds.Add(50);
            pol.parties.Add(legacy);

            var map = new GalaxyMap();
            map.AddSystem(new StarSystem(0, "ハイネセン", new Vector2(0f, 0f), F));
            var c = new CampaignState(map);
            var s = new FactionState(F) { governmentForm = GovernmentForm.共和制, politics = pol };
            c.states.Add(s);
            PoliticsState a = CampaignSerializer.FromJson(CampaignSerializer.ToJson(c)).states[0].politics;

            Party ap = a.parties[0];
            Assert.IsTrue(ap.leadership.managed);
            Assert.AreEqual(3, ap.leaderId);
            Assert.AreEqual(Year + 3, ap.leadership.termEndYear);
            Assert.AreEqual(1, ap.leadership.records.Count);
            LeadershipElectionRecord ar = ap.leadership.Latest;
            Assert.AreEqual(rec.electionId, ar.electionId);
            Assert.AreEqual(rec.seed, ar.seed);
            Assert.AreEqual(rec.winnerId, ar.winnerId);
            Assert.AreEqual(rec.outcome, ar.outcome);
            Assert.AreEqual(rec.reason, ar.reason);
            Assert.AreEqual(3, ar.candidates.Count);
            Assert.AreEqual(8, ar.Candidate(1).round1Total);
            Assert.AreEqual(8, ar.Candidate(3).runoffTotal);
            Assert.AreEqual(3, ar.regions.Count);
            Assert.AreEqual(3, ar.factions.Count);
            Assert.AreEqual(3, ar.factions[2].endorsedRunoff);
            Assert.AreEqual(3, ap.factions[2].endorsedCandidateId);
            Assert.IsTrue(ap.factions[2].mainstream);
            Assert.AreEqual(1f, ap.factions[0].cohesion, 1e-6f);
            Assert.IsFalse(a.parties[1].leadership.managed, "旧データの党は読込で管理下にしない");
            Assert.AreEqual(50, a.parties[1].leaderId);
            Assert.AreEqual(0, a.parties[1].leadership.records.Count);

            Assert.IsNull(PartyLeadershipRules.RunElection(a, F, ap, Year, roster, null, Det, "再処理", null), "同じ選挙イベントを再処理しない");
            Assert.IsNull(PartyLeadershipRules.TickYear(a, F, Year, roster, null, Det).Find(x => x.partyId == 1), "読込後の同じ年に総裁選をしない");
            Assert.AreEqual(1, ap.leadership.records.Count);
            Assert.AreEqual(3, ap.leaderId);
            Assert.AreEqual(1, LegislatorRosterRules.Find(a, 3).lowerWins, "読込で当選回数を増やさない");
        }

        static bool HasRestriction(LeadershipElectionRecord rec, string needle)
        {
            for (int i = 0; i < rec.restrictions.Count; i++)
                if (rec.restrictions[i].Contains(needle)) return true;
            return false;
        }
    }
}
