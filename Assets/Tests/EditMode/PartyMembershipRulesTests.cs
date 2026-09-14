using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 政党の所属管理（PartyMembershipRules）を固定する：入党の資格（同勢力・生存・一人一党）、移籍・離党で党役職・派閥・議席が整合し
    /// 確定議席と当選回数は変わらないこと、党首欠缺で例外を出さないこと、重複所属の整理順（議席の党→党首の党→党ID小）、
    /// 自動補充が既存の所属を動かさず理由つきで配属すること、与党/野党を組閣と議席から導くこと、一般党員の集計が不明のままであること、
    /// 保存と旧データ。小さい議院（下院2・上院4＝A2/B2）で固定する。
    /// </summary>
    public class PartyMembershipRulesTests
    {
        static readonly ElectionCycleParams Small = new ElectionCycleParams(2, 4, 8, 0.2f);
        static readonly LegislatorRosterParams Prm = LegislatorRosterParams.Default;

        static Person Pol(int id, int charisma = 50, Faction f = Faction.同盟)
            => new Person(id, "政治家" + id, f, PersonRole.文民) { isPolitician = true, birthYear = 760, charisma = charisma };

        static PoliticsTickRules.PoliticsTickResult NoElection()
        {
            var t = default(PoliticsTickRules.PoliticsTickResult);
            t.upperClassUp = -1;
            return t;
        }

        /// <summary>2党（党1＝党首10・党員10/12/14、党2＝党首11・党員11/13/15）で初回選挙し、名簿へ反映する。</summary>
        static FactionState ElectedTwoParties(out List<Person> roster)
        {
            var s = new FactionState(Faction.同盟) { governmentForm = GovernmentForm.共和制, politics = new PoliticsState() };
            var p1 = new Party(1, "民政党", Faction.同盟) { support = 0.5f, leaderId = 10 };
            p1.memberIds.AddRange(new[] { 10, 12, 14 });
            var p2 = new Party(2, "進歩党", Faction.同盟) { support = 0.5f, leaderId = 11 };
            p2.memberIds.AddRange(new[] { 11, 13, 15 });
            s.politics.parties.Add(p1);
            s.politics.parties.Add(p2);
            roster = new List<Person> { Pol(10, 90), Pol(11, 80), Pol(12, 70), Pol(13, 60), Pol(14, 50), Pol(15, 40) };

            NationalYearOutcome o = ElectionCycleRules.RunNationalYear(s, 800, NoElection(), null, roster, Small);
            LegislatorRosterRules.AssignElection(s.politics, s.faction, o.lowerRecord, roster, null, Prm);
            LegislatorRosterRules.AssignElection(s.politics, s.faction, o.upperRecord, roster, null, Prm);
            return s;
        }

        // ===== 入党の資格・一人一党 =====

        [Test]
        public void Join_RejectsOtherFactionDeadMissingAndAlreadyAffiliated()
        {
            var pol = new PoliticsState();
            pol.parties.Add(new Party(1, "民政党", Faction.同盟));
            pol.parties.Add(new Party(2, "進歩党", Faction.同盟));
            pol.parties.Add(new Party(3, "帝政党", Faction.帝国));
            Person dead = Pol(21); dead.deathYear = 799;
            var roster = new List<Person> { Pol(20), dead, Pol(22, 50, Faction.帝国) };

            StringAssert.Contains("他勢力", PartyMembershipRules.Join(pol, Faction.同盟, roster, 20, 3, "").reason);
            StringAssert.Contains("資格", PartyMembershipRules.Join(pol, Faction.同盟, roster, 21, 1, "").reason);
            StringAssert.Contains("資格", PartyMembershipRules.Join(pol, Faction.同盟, roster, 22, 1, "").reason, "他勢力の人物");
            StringAssert.Contains("名簿に居ない", PartyMembershipRules.Join(pol, Faction.同盟, roster, 99, 1, "").reason);
            StringAssert.Contains("見つからない", PartyMembershipRules.Join(pol, Faction.同盟, roster, 20, 9, "").reason);

            PartyMembershipChange ok = PartyMembershipRules.Join(pol, Faction.同盟, roster, 20, 1, "試験");
            Assert.IsTrue(ok.ok);
            Assert.AreEqual(1, ok.toPartyId);
            PartyMembershipChange twice = PartyMembershipRules.Join(pol, Faction.同盟, roster, 20, 2, "");
            Assert.IsFalse(twice.ok, "二つ目の党へは入党できない");
            StringAssert.Contains("移籍", twice.reason);
            Assert.AreEqual(1, PartyMembershipRules.MembershipCount(pol.parties, 20));
            Assert.IsFalse(PartyMembershipRules.Join(null, Faction.同盟, roster, 20, 1, "").ok, "null で例外を出さない");
        }

        // ===== 移籍・離党 =====

        [Test]
        public void Transfer_ClearsOldPostsFactionAndSeat_KeepsSeatTotalsWinsAndGovernment()
        {
            FactionState s = ElectedTwoParties(out List<Person> roster);
            PoliticsState pol = s.politics;
            Party p1 = pol.parties[0], p2 = pol.parties[1];
            // 前提：12 は上院区分A（党1）、首相は党1党首10（1/2議席＝少数政権）
            Assert.IsTrue(LegislatorRosterRules.IsSeated(pol, 12));
            Assert.AreEqual(1, LegislatorRosterRules.Find(pol, 12).seatPartyId);
            Assert.AreEqual(10, pol.government.premierPersonId);
            PartyOrganizationRules.AppointPost(p1, PartyPost.幹事長, 12);
            var faction = new PartyFaction(1, "甲派", 12);
            faction.memberIds.AddRange(new[] { 12, 14 });
            p1.factions.Add(faction);
            int upperSeats1 = ElectionCycleRules.SeatsOf(pol.upperSeats, 1);
            int upperSeats2 = ElectionCycleRules.SeatsOf(pol.upperSeats, 2);

            PartyMembershipChange c = PartyMembershipRules.Transfer(pol, Faction.同盟, roster, 12, 2, "路線対立");

            Assert.IsTrue(c.ok);
            Assert.AreEqual(1, c.fromPartyId);
            Assert.AreEqual(2, c.toPartyId);
            Assert.AreEqual(1, c.postsVacated);
            Assert.IsFalse(c.leaderVacated);
            Assert.IsTrue(c.seatVacated, "議席は当選時の党に帰属");
            Assert.IsFalse(p1.memberIds.Contains(12));
            Assert.IsTrue(p2.memberIds.Contains(12));
            Assert.AreEqual(1, PartyMembershipRules.MembershipCount(pol.parties, 12));
            Assert.AreEqual(-1, PartyOrganizationRules.HolderOf(p1, PartyPost.幹事長), "旧党の役職が残らない");
            Assert.AreEqual(0, p2.posts.Count, "移籍先で役職を与えない");
            Assert.AreEqual(-1, faction.bossId, "旧党の派閥の領袖が残らない");
            CollectionAssert.AreEqual(new[] { 14 }, faction.memberIds);
            Assert.AreEqual(11, p2.leaderId, "移籍先の党首は変わらない");

            LegislatorRecord r = LegislatorRosterRules.Find(pol, 12);
            Assert.IsFalse(r.seated);
            Assert.AreEqual(-1, r.seatPartyId);
            Assert.AreEqual(1, r.upperWins, "当選回数は増えも減りもしない");
            StringAssert.Contains("民政党", r.statusReason);
            Assert.AreEqual(upperSeats1, ElectionCycleRules.SeatsOf(pol.upperSeats, 1), "確定議席は変えない");
            Assert.AreEqual(upperSeats2, ElectionCycleRules.SeatsOf(pol.upperSeats, 2));
            Assert.AreEqual(1, LegislatorRosterRules.AggregateSeats(pol, LegislativeChamber.上院, 1), "外れた議席は集計へ");
            Assert.AreEqual(1, LegislatorRosterRules.NamedSeats(pol, LegislativeChamber.上院, 2, 0), "移籍先の実在議員は増えない（13のみ）");

            Assert.AreEqual(10, pol.government.premierPersonId, "所属変化で首相は動かない");
            Assert.AreEqual(1, pol.government.partyId);
            Assert.AreEqual(0, LegislatorRosterRules.Reconcile(pol, Faction.同盟, roster, 801).Count, "重ねて外さない");
        }

        [Test]
        public void Transfer_Rejections_AndUnaffiliatedMustJoin()
        {
            FactionState s = ElectedTwoParties(out List<Person> roster);
            PoliticsState pol = s.politics;
            pol.parties.Add(new Party(3, "帝政党", Faction.帝国));
            roster.Add(Pol(30));

            StringAssert.Contains("他勢力", PartyMembershipRules.Transfer(pol, Faction.同盟, roster, 13, 3, "").reason);
            StringAssert.Contains("既に", PartyMembershipRules.Transfer(pol, Faction.同盟, roster, 13, 2, "").reason);
            StringAssert.Contains("入党", PartyMembershipRules.Transfer(pol, Faction.同盟, roster, 30, 1, "").reason);
            roster.Find(p => p.id == 13).captiveStatus = CaptiveStatus.捕虜;
            PartyMembershipChange captive = PartyMembershipRules.Transfer(pol, Faction.同盟, roster, 13, 1, "");
            Assert.IsFalse(captive.ok, "拘束中は移籍できない（党籍と議席は保つ）");
            Assert.IsTrue(pol.parties[1].memberIds.Contains(13));
            Assert.IsTrue(LegislatorRosterRules.IsSeated(pol, 13));
        }

        [Test]
        public void LeaderLeaves_NoException_PremierUnchanged_NotAutoRejoined_LeaderRefilledFromMembers()
        {
            FactionState s = ElectedTwoParties(out List<Person> roster);
            PoliticsState pol = s.politics;
            Party p1 = pol.parties[0];
            pol.parties.Add(new Party(3, "空党", Faction.同盟)); // 党員も党首もいない党

            PartyMembershipChange c = PartyMembershipRules.Leave(pol, 10, "離党届");
            Assert.IsTrue(c.ok);
            Assert.IsTrue(c.leaderVacated);
            Assert.IsTrue(c.seatVacated);
            Assert.AreEqual(-1, p1.leaderId);
            Assert.AreEqual(10, pol.government.premierPersonId, "離党だけで首相・内閣を動かさない");
            Assert.AreEqual(1, LegislatorRosterRules.Find(pol, 10).lowerWins);
            Assert.IsFalse(PartyMembershipRules.Leave(pol, 10, "").ok, "無所属は離党できない");

            Assert.DoesNotThrow(() => ElectionCycleRules.OrganizeParties(pol, roster, Faction.同盟));
            Assert.IsNull(PartyMembershipRules.PartyOf(pol, 10), "自ら無所属を選んだ人を自動で再入党させない");
            Assert.IsTrue(p1.leaderId == 12 || p1.leaderId == 14, "党首は残る党員から");
            Assert.AreEqual(-1, pol.parties[2].leaderId, "空党の党首は捏造しない");
            Assert.AreEqual(0, pol.parties[2].memberIds.Count, "空党へ既存党員を移さない");

            // 本人が入党し直せば無所属表明は外れる
            Assert.IsTrue(PartyMembershipRules.Join(pol, Faction.同盟, roster, 10, 3, "新党へ").ok);
            Assert.IsFalse(pol.independentPersonIds.Contains(10));
            Assert.AreEqual(1, LegislatorRosterRules.Find(pol, 10).lowerWins, "入党で当選回数は増えない");
            Assert.IsFalse(LegislatorRosterRules.IsSeated(pol, 10), "入党で議席は戻らない");
        }

        // ===== 整理 =====

        [Test]
        public void Normalize_DuplicatesInvalidAndOrphanedOffices()
        {
            var pol = new PoliticsState();
            var p1 = new Party(1, "民政党", Faction.同盟) { leaderId = 22 };
            p1.memberIds.AddRange(new[] { 20, 20, 21, -5, 22 });
            p1.posts.Add(new PartyAppointment(PartyPost.幹事長, 30));   // 党外
            p1.posts.Add(new PartyAppointment(PartyPost.政調会長, 20));
            p1.posts.Add(new PartyAppointment(PartyPost.政調会長, 21)); // 重複した役職
            p1.posts.Add(null);
            var pf = new PartyFaction(1, "甲派", 21);
            pf.memberIds.AddRange(new[] { 20, 21, 21 });
            p1.factions.Add(pf);
            var p2 = new Party(2, "進歩党", Faction.同盟) { leaderId = 21 };
            p2.memberIds.AddRange(new[] { 21, 22, 23 });
            pol.parties.Add(p1);
            pol.parties.Add(p2);
            pol.legislators.Add(new LegislatorRecord(22) { seated = true, seatChamber = LegislativeChamber.下院, seatPartyId = 2 });
            Person dead = Pol(23); dead.deathYear = 799;
            var roster = new List<Person> { Pol(20), Pol(21), Pol(22), dead };

            int changes = PartyMembershipRules.Normalize(pol.parties, pol, Faction.同盟, roster);

            Assert.Greater(changes, 0);
            CollectionAssert.AreEqual(new[] { 20 }, p1.memberIds, "22 は議席の党2へ・21 は党首を務める党2へ");
            CollectionAssert.AreEqual(new[] { 21, 22 }, p2.memberIds, "死亡した23は離党");
            Assert.AreEqual(-1, p1.leaderId);
            Assert.AreEqual(21, p2.leaderId);
            Assert.AreEqual(1, p1.posts.Count);
            Assert.AreEqual(20, PartyOrganizationRules.HolderOf(p1, PartyPost.政調会長));
            Assert.AreEqual(-1, pf.bossId);
            CollectionAssert.AreEqual(new[] { 20 }, pf.memberIds);
            for (int id = 20; id <= 22; id++) Assert.AreEqual(1, PartyMembershipRules.MembershipCount(pol.parties, id));
            Assert.AreEqual(0, PartyMembershipRules.Normalize(pol.parties, pol, Faction.同盟, roster), "再実行で変わらない");
        }

        [Test]
        public void Normalize_DuplicateWithoutSeatOrLead_KeepsSmallerPartyId()
        {
            var p1 = new Party(1, "民政党", Faction.同盟);
            var p2 = new Party(2, "進歩党", Faction.同盟);
            p1.memberIds.Add(40);
            p2.memberIds.Add(40);
            var parties = new List<Party> { p2, p1 };
            PartyMembershipRules.Normalize(parties, null, Faction.同盟, null);
            Assert.IsTrue(p1.memberIds.Contains(40));
            Assert.IsFalse(p2.memberIds.Contains(40));
        }

        // ===== 自動補充 =====

        [Test]
        public void AssignUnaffiliated_KeepsExisting_UsesPlatformAndClassBase_WithReasons()
        {
            var pol = new PoliticsState();
            var p1 = new Party(1, "中道党", Faction.同盟);
            p1.memberIds.Add(30);
            var p2 = new Party(2, "共和党", Faction.同盟) { platform = "共和主義" };
            var p3 = new Party(3, "貴族党", Faction.同盟) { classBase = "門閥貴族" };
            var p4 = new Party(4, "帝国共和党", Faction.帝国) { platform = "共和主義" };
            pol.parties.AddRange(new[] { p1, p2, p3, p4 });

            Person existing = Pol(30); existing.creed = Creed.共和主義;
            Person creed = Pol(40); creed.creed = Creed.共和主義;
            Person origin = Pol(41); origin.socialOrigin = SocialOrigin.門閥貴族;
            Person plain = Pol(42);
            Person both = Pol(43); both.creed = Creed.共和主義; both.socialOrigin = SocialOrigin.門閥貴族;
            Person independent = Pol(44);
            pol.independentPersonIds.Add(44);
            var roster = new List<Person> { plain, existing, both, origin, creed, independent };

            List<PartyMembershipChange> list = PartyMembershipRules.AssignUnaffiliated(pol.parties, Faction.同盟, roster, pol);

            Assert.AreEqual(4, list.Count);
            Assert.IsTrue(p1.memberIds.Contains(30), "既に所属している人は綱領が合わなくても移籍しない");
            Assert.AreEqual(40, list[0].personId); Assert.AreEqual(2, list[0].toPartyId); StringAssert.Contains("綱領", list[0].reason);
            Assert.AreEqual(41, list[1].personId); Assert.AreEqual(3, list[1].toPartyId); StringAssert.Contains("支持基盤", list[1].reason);
            Assert.AreEqual(42, list[2].personId); Assert.AreEqual(1, list[2].toPartyId, "党員数同数は党ID小");
            StringAssert.Contains("対応なし", list[2].reason);
            Assert.AreEqual(43, list[3].personId); Assert.AreEqual(2, list[3].toPartyId, "信条の一致を出自より重く見る");
            Assert.IsNull(PartyMembershipRules.PartyOf(pol, 44));
            Assert.AreEqual(0, p4.memberIds.Count, "他勢力の党へ配属しない");
        }

        [Test]
        public void RunNationalYear_DoesNotRebalanceExistingMembers()
        {
            var s = new FactionState(Faction.同盟) { governmentForm = GovernmentForm.共和制, politics = new PoliticsState() };
            var p1 = new Party(1, "民政党", Faction.同盟) { support = 0.5f };
            p1.memberIds.AddRange(new[] { 10, 11, 12 });
            var p2 = new Party(2, "進歩党", Faction.同盟) { support = 0.5f };
            s.politics.parties.Add(p1);
            s.politics.parties.Add(p2);
            var roster = new List<Person> { Pol(10), Pol(11), Pol(12), Pol(13) };

            NationalYearOutcome o = ElectionCycleRules.RunNationalYear(s, 800, NoElection(), null, roster, Small);
            CollectionAssert.AreEqual(new[] { 10, 11, 12 }, p1.memberIds);
            CollectionAssert.AreEqual(new[] { 13 }, p2.memberIds, "無所属の13だけが党員の少ない党へ");
            Assert.AreEqual(2, ElectionCycleRules.SeatsOf(s.politics.lowerSeats, 1) + ElectionCycleRules.SeatsOf(s.politics.lowerSeats, 2));
            Assert.IsNotNull(o.lowerRecord);
        }

        // ===== 与党・野党 =====

        [Test]
        public void Role_FromGovernmentAndSeats_NotSupport()
        {
            var pol = new PoliticsState();
            pol.parties.Add(new Party(1, "民政党", Faction.同盟) { support = 0.2f });
            pol.parties.Add(new Party(2, "進歩党", Faction.同盟) { support = 0.8f });
            pol.parties.Add(new Party(3, "小党", Faction.同盟) { support = 0f });
            Assert.AreEqual(PartyGovernmentRole.未確定, PartyMembershipRules.RoleOf(pol, 2), "組閣が無ければ支持率最大でも与党にしない");

            ElectionCycleRules.EnsureSeats(pol, Small);
            pol.lowerSeats.seated = true;
            pol.lowerSeats.parties.Add(new PartySeatCount(1) { classA = 2 });
            pol.lowerSeats.parties.Add(new PartySeatCount(2) { classA = 0 });
            pol.upperSeats.seated = true;
            pol.upperSeats.parties.Add(new PartySeatCount(2) { classA = 1, classB = 1 });
            pol.government = new GovernmentFormation { status = CabinetStatus.単独過半, partyId = 1, premierPersonId = 10 };

            Assert.AreEqual(PartyGovernmentRole.与党, PartyMembershipRules.RoleOf(pol, 1));
            Assert.AreEqual(PartyGovernmentRole.野党, PartyMembershipRules.RoleOf(pol, 2));
            Assert.AreEqual(PartyGovernmentRole.議席なし, PartyMembershipRules.RoleOf(pol, 3));
            Assert.AreEqual(0.8f, pol.parties[1].support, 1e-6f, "判定で支持率を変えない");
            Assert.AreEqual(2, ElectionCycleRules.SeatsOf(pol.lowerSeats, 1), "判定で議席を変えない");

            pol.government.status = CabinetStatus.組閣未成立;
            Assert.AreEqual(PartyGovernmentRole.未確定, PartyMembershipRules.RoleOf(pol, 1));
            pol.government.status = CabinetStatus.対象外;
            Assert.AreEqual(-1, PartyMembershipRules.GovernmentPartyId(pol));
            pol.government = new GovernmentFormation { status = CabinetStatus.少数政権, partyId = 9 };
            Assert.AreEqual(PartyGovernmentRole.未確定, PartyMembershipRules.RoleOf(pol, 1), "存在しない党の政府は与党を持たない");
            Assert.AreEqual(PartyGovernmentRole.未確定, PartyMembershipRules.RoleOf(null, 1));
        }

        // ===== 一般党員の集計・内訳 =====

        [Test]
        public void GeneralMembership_UnknownByDefault_NotDerivedFromNamedIds()
        {
            var p = new Party(1, "民政党", Faction.同盟);
            p.memberIds.AddRange(new[] { 1, 2, 3, 3 });

            PartyStatusSummary before = PartyMembershipRules.Summarize(null, p);
            Assert.IsFalse(before.nationalMembershipKnown);
            Assert.AreEqual(0L, before.nationalMembership);
            Assert.AreEqual(3, before.namedPoliticians, "重複IDは数えない");
            Assert.AreEqual(0, before.regionalTalliesKnown);
            Assert.IsFalse(PartyMembershipRules.TryGetGeneralMembership(p, PartyMembershipTally.NationalScope, out _));

            Assert.IsFalse(PartyMembershipRules.SetGeneralMembership(p, PartyMembershipTally.NationalScope, -1, "x", 800));
            Assert.IsFalse(PartyMembershipRules.SetGeneralMembership(p, -2, 10, "x", 800));
            Assert.IsTrue(PartyMembershipRules.SetGeneralMembership(p, PartyMembershipTally.NationalScope, 1200000L, "党公表", 800));
            Assert.IsTrue(PartyMembershipRules.SetGeneralMembership(p, 7, 3000L, "星系支部", 800));
            Assert.IsTrue(PartyMembershipRules.SetGeneralMembership(p, 5, 0L, "星系支部", 800));

            PartyStatusSummary after = PartyMembershipRules.Summarize(null, p);
            Assert.IsTrue(after.nationalMembershipKnown);
            Assert.AreEqual(1200000L, after.nationalMembership);
            Assert.AreEqual(2, after.regionalTalliesKnown);
            Assert.AreEqual(3, after.namedPoliticians, "集計を入れてもネームド数は変わらない");
            Assert.AreEqual(5, p.regionalMemberships[0].systemId, "星系ID順");
            Assert.AreEqual("党公表", p.nationalMembership.source);
            Assert.IsTrue(PartyMembershipRules.TryGetGeneralMembership(p, 7, out long seven));
            Assert.AreEqual(3000L, seven);
            Assert.IsFalse(PartyMembershipRules.TryGetGeneralMembership(p, 9, out _), "載っていない星系は不明");

            Assert.IsTrue(PartyMembershipRules.ClearGeneralMembership(p, PartyMembershipTally.NationalScope));
            Assert.IsFalse(PartyMembershipRules.Summarize(null, p).nationalMembershipKnown);
            Assert.IsFalse(PartyMembershipRules.ClearGeneralMembership(p, PartyMembershipTally.NationalScope));
        }

        [Test]
        public void Summarize_SeparatesSupportSeatsNamedAndLegislators()
        {
            FactionState s = ElectedTwoParties(out _);
            PoliticsState pol = s.politics;
            PartyStatusSummary a = PartyMembershipRules.Summarize(pol, pol.parties[0]);
            Assert.AreEqual(PartyGovernmentRole.与党, a.role);
            Assert.AreEqual(0.5f, a.support, 1e-6f);
            Assert.AreEqual(1, a.lowerSeats);
            Assert.AreEqual(2, a.upperSeats);
            Assert.AreEqual(3, a.namedPoliticians);
            Assert.AreEqual(1, a.namedLowerLegislators);
            Assert.AreEqual(2, a.namedUpperLegislators);
            Assert.IsFalse(a.nationalMembershipKnown);
            Assert.AreEqual(PartyGovernmentRole.野党, PartyMembershipRules.Summarize(pol, pol.parties[1]).role);
        }

        // ===== 保存・旧データ =====

        [Test]
        public void SaveRoundTrip_KeepsTalliesUnknownAndIndependents_LoadAddsNoMembersAndKeepsHistory()
        {
            FactionState s = ElectedTwoParties(out List<Person> roster);
            PoliticsState pol = s.politics;
            PartyMembershipRules.SetGeneralMembership(pol.parties[0], PartyMembershipTally.NationalScope, 500000L, "党公表", 800);
            PartyMembershipRules.SetGeneralMembership(pol.parties[1], 0, 1200L, "支部集計", 800);
            PartyMembershipRules.Leave(pol, 15, "無所属へ");
            roster.Add(Pol(16)); // 名簿にいる無所属の適格者（読込では入党させない）

            var map = new GalaxyMap();
            map.AddSystem(new StarSystem(0, "ハイネセン", new Vector2(0f, 0f), Faction.同盟));
            var c = new CampaignState(map);
            c.states.Add(s);
            PoliticsState a = CampaignSerializer.FromJson(CampaignSerializer.ToJson(c)).states[0].politics;

            Assert.IsTrue(a.parties[0].nationalMembership.known);
            Assert.AreEqual(500000L, a.parties[0].nationalMembership.members);
            Assert.AreEqual("党公表", a.parties[0].nationalMembership.source);
            Assert.AreEqual(800, a.parties[0].nationalMembership.asOfYear);
            Assert.IsFalse(a.parties[1].nationalMembership.known, "未設定の全国集計は不明のまま");
            Assert.AreEqual(1, a.parties[1].regionalMemberships.Count);
            Assert.AreEqual(1200L, a.parties[1].regionalMemberships[0].members);
            CollectionAssert.AreEqual(pol.parties[0].memberIds, a.parties[0].memberIds);
            CollectionAssert.AreEqual(pol.parties[1].memberIds, a.parties[1].memberIds);
            CollectionAssert.AreEqual(new[] { 15 }, a.independentPersonIds);
            Assert.IsNull(PartyMembershipRules.PartyOf(a, 16), "読込で入党を起こさない");
            Assert.AreEqual(ElectionCycleRules.SeatsOf(pol.lowerSeats, 1), ElectionCycleRules.SeatsOf(a.lowerSeats, 1));
            Assert.AreEqual(ElectionCycleRules.SeatsOf(pol.upperSeats, 2), ElectionCycleRules.SeatsOf(a.upperSeats, 2));
            Assert.AreEqual(pol.recentResults.Count, a.recentResults.Count, "読込で選挙を起こさない");
            Assert.AreEqual(1, LegislatorRosterRules.Find(a, 15).upperWins, "離党しても当選履歴は残る");
            Assert.IsFalse(LegislatorRosterRules.IsSeated(a, 15));
            Assert.AreEqual(1, LegislatorRosterRules.Find(a, 10).lowerWins);
            Assert.AreEqual(pol.government.premierPersonId, a.government.premierPersonId);
        }

        [Test]
        public void OldData_NullTalliesAndDuplicates_NormalizeWithoutJoining()
        {
            var save = new CampaignSaveData();
            var pol = new PoliticsState { independentPersonIds = null };
            var p1 = new Party(1, "民政党", Faction.同盟) { nationalMembership = null, regionalMemberships = null, leaderId = 10 };
            p1.memberIds.AddRange(new[] { 10, 11 });
            var p2 = new Party(2, "進歩党", Faction.同盟)
            {
                nationalMembership = new PartyMembershipTally { known = true, members = -3, source = null },
            };
            p2.regionalMemberships.Add(null);
            p2.regionalMemberships.Add(new PartyMembershipTally(4) { known = true, members = 10 });
            p2.regionalMemberships.Add(new PartyMembershipTally(4) { known = true, members = 99 });
            p2.memberIds.AddRange(new[] { 11, 12 });
            pol.parties.Add(p1);
            pol.parties.Add(p2);
            save.states.Add(new FactionStateSave { faction = (int)Faction.同盟, hasPolitics = true, politics = pol });

            PoliticsState loaded = null;
            Assert.DoesNotThrow(() => loaded = CampaignSerializer.FromSaveData(save).states[0].politics);
            Assert.IsNotNull(loaded.independentPersonIds);
            Assert.IsNotNull(loaded.parties[0].nationalMembership);
            Assert.IsFalse(loaded.parties[0].nationalMembership.known, "旧データは不明");
            Assert.IsNotNull(loaded.parties[0].regionalMemberships);
            Assert.IsFalse(loaded.parties[1].nationalMembership.known, "負の人数は不明に戻す");
            Assert.AreEqual("", loaded.parties[1].nationalMembership.source);
            Assert.AreEqual(1, loaded.parties[1].regionalMemberships.Count, "null と重複した星系を除く");
            Assert.AreEqual(10L, loaded.parties[1].regionalMemberships[0].members);
            CollectionAssert.AreEqual(new[] { 10, 11 }, loaded.parties[0].memberIds, "重複所属は党ID小へ（議席も党首もない場合）");
            CollectionAssert.AreEqual(new[] { 12 }, loaded.parties[1].memberIds);
            Assert.AreEqual(3, PartyMembershipRules.MembershipCount(loaded.parties, 10) + PartyMembershipRules.MembershipCount(loaded.parties, 11)
                + PartyMembershipRules.MembershipCount(loaded.parties, 12), "人を増やさない");
        }
    }
}
