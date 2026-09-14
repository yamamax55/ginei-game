using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 内閣の政治任用（CabinetAppointmentRules）と党三役（PartyExecutiveRules）の任免基盤を固定する：
    /// 任免権者（首相・党首）だけが任免でき権限外は上申、資格のない人物（名簿外/死亡/拘束/他勢力/軍人/非政治家/野党）と存在しない省の拒否と理由、
    /// 兼任（閣僚職の重複・知事・党三役・首相でない党首）、大臣/副大臣/政務官の権限差と委任の範囲・期限・失効、艦隊作戦指揮と国庫支出の不許可、
    /// 首相交代の総辞職・首相不在の職務執行（期限つき）・政体移行、死亡/離党の失職と冪等、AI 組閣の選定（能力・年功の逓減・経験の目安・候補不足は空席）、
    /// 党三役の役割差・党首交代の改任と続投・党首不在の暫定、職業官僚と GovernmentRegistry を変えない、保存往復と旧セーブ。
    /// </summary>
    public class CabinetAppointmentRulesTests
    {
        const int Year = 800;
        const Faction F = Faction.同盟;
        const int Top = 1000, Shikibu = 1001, Okura = 1003, Hyobu = 1004;
        static readonly CabinetParams Prm = CabinetParams.Default;

        static Person Pol(int id, int op = 50, int intel = 50)
            => new Person(id, "政治家" + id, F, PersonRole.文民) { isPolitician = true, birthYear = 760, operation = op, intelligence = intel };

        class World
        {
            public PoliticsState pol;
            public Party ruling, opposition;
            public List<Ministry> tree;
            public List<Person> roster;
            public Person P(int id) => roster.Find(x => x.id == id);
        }

        /// <summary>
        /// 与党 民政党（党首=首相1・党員2..6）と野党 進歩党（党首7・党員8）、太政官の下に式部省/大蔵省/兵部省（兵部省に職業官僚90,91）。
        /// 名簿外の人物：9=軍人の政治家・10=政治家でない文民・11=他勢力・12=死亡・13=捕虜（いずれも党籍なし）。
        /// </summary>
        static World NewWorld()
        {
            var w = new World { pol = new PoliticsState() };
            w.ruling = new Party(1, "民政党", F) { leaderId = 1 };
            w.ruling.memberIds.AddRange(new[] { 1, 2, 3, 4, 5, 6 });
            w.opposition = new Party(2, "進歩党", F) { leaderId = 7 };
            w.opposition.memberIds.AddRange(new[] { 7, 8 });
            w.pol.parties.Add(w.ruling);
            w.pol.parties.Add(w.opposition);
            w.pol.government = new GovernmentFormation
            {
                status = CabinetStatus.単独過半, premierPersonId = 1, partyId = 1, formedYear = Year, sourceElectionId = "e1",
            };
            w.tree = new List<Ministry>
            {
                new Ministry(Top, "太政官", OfficeDomain.内政) { staffSlots = 2 },
                new Ministry(Shikibu, "式部省", OfficeDomain.内政) { parentId = Top },
                new Ministry(Okura, "大蔵省", OfficeDomain.財政) { parentId = Top },
                new Ministry(Hyobu, "兵部省", OfficeDomain.軍事) { parentId = Top, staffIds = new List<int> { 90, 91 } },
            };
            w.roster = new List<Person>();
            for (int i = 1; i <= 8; i++) w.roster.Add(Pol(i));
            w.roster.Add(new Person(9, "軍人9", F, PersonRole.軍人) { isPolitician = true });
            w.roster.Add(new Person(10, "官僚10", F, PersonRole.文民));
            w.roster.Add(new Person(11, "帝国人11", Faction.帝国, PersonRole.文民) { isPolitician = true });
            w.roster.Add(new Person(12, "故人12", F, PersonRole.文民) { isPolitician = true, deathYear = Year - 1 });
            w.roster.Add(new Person(13, "捕虜13", F, PersonRole.文民) { isPolitician = true, captiveStatus = CaptiveStatus.捕虜 });
            CabinetAppointmentRules.Reconcile(w.pol, F, w.tree, Top, w.roster, Year, Prm);
            return w;
        }

        static AppointmentResult Appoint(World w, int actor, int ministry, CabinetPostKind kind, int person, int year = Year)
            => CabinetAppointmentRules.TryAppoint(w.pol, F, actor, w.tree, Top, ministry, kind, person, w.roster, year, "試験", Prm);

        static AppointmentResult Auth(World w, int person, int ministry, CabinetAction a, int year = Year)
            => CabinetAppointmentRules.Authority(w.pol, F, person, ministry, a, w.roster, year);

        static int Holder(World w, int ministry, CabinetPostKind kind)
            => CabinetAppointmentRules.FindPost(w.pol.cabinet, ministry, kind).holderId;

        static int CountAction(List<AppointmentHistoryEntry> h, string action)
        {
            int n = 0;
            for (int i = 0; i < h.Count; i++) if (h[i].action == action) n++;
            return n;
        }

        static int FilledTotal(CabinetState cab)
            => CabinetAppointmentRules.FilledCount(cab, CabinetPostKind.大臣) + CabinetAppointmentRules.FilledCount(cab, CabinetPostKind.副大臣)
             + CabinetAppointmentRules.FilledCount(cab, CabinetPostKind.政務官);

        // ===== 1. 組閣の器 =====

        [Test]
        public void Reconcile_BindsPremier_CreatesThreePostsPerMinistry_WithoutAppointing()
        {
            World w = NewWorld();
            CabinetState cab = w.pol.cabinet;
            Assert.AreEqual(1, cab.premierPersonId);
            Assert.AreEqual("e1", cab.sourceElectionId);
            Assert.AreEqual(9, cab.posts.Count, "太政官を除く3省×3職");
            Assert.IsNull(CabinetAppointmentRules.FindPost(cab, Top, CabinetPostKind.大臣), "最上位（太政官）には大臣を置かない");
            foreach (CabinetPost p in cab.posts)
            {
                Assert.AreEqual(-1, p.holderId);
                Assert.AreEqual("未任命", p.vacancyReason);
            }
            Assert.AreEqual(0, cab.history.Count);
            Assert.AreEqual("兵部大臣", CabinetAppointmentRules.PostTitle("兵部省", CabinetPostKind.大臣));
            Assert.AreEqual("兵部副大臣", CabinetAppointmentRules.PostTitle("兵部省", CabinetPostKind.副大臣));
            Assert.AreEqual("兵部大臣政務官", CabinetAppointmentRules.PostTitle("兵部省", CabinetPostKind.政務官));
            Assert.AreEqual(0, CabinetAppointmentRules.Reconcile(w.pol, F, w.tree, Top, w.roster, Year, Prm).Count, "同じ状態で繰り返しても何も起きない");
        }

        // ===== 2. 任免権者と資格 =====

        [Test]
        public void TryAppoint_OnlyFormalPremier_OthersArePetitions()
        {
            World w = NewWorld();
            int regBefore = GovernmentRegistry.Appointments.Count;

            AppointmentResult r = Appoint(w, 2, Hyobu, CabinetPostKind.大臣, 3);
            Assert.IsFalse(r.ok);
            Assert.IsTrue(r.canPetition, "権限外は上申");
            Assert.AreEqual(1, r.petitionToId, "上申先は首相");
            StringAssert.Contains("権限外", r.reason);
            Assert.AreEqual(-1, Holder(w, Hyobu, CabinetPostKind.大臣));

            r = Appoint(w, 1, Hyobu, CabinetPostKind.大臣, 2);
            Assert.IsTrue(r.ok, r.reason);
            CabinetPost post = CabinetAppointmentRules.FindPost(w.pol.cabinet, Hyobu, CabinetPostKind.大臣);
            Assert.AreEqual(2, post.holderId);
            Assert.AreEqual(1, post.appointedById);
            Assert.AreEqual(1, post.partyId);
            Assert.AreEqual(Year, post.appointedYear);
            Assert.AreEqual("", post.vacancyReason);
            Assert.AreEqual(1, w.pol.cabinet.history.Count);
            Assert.AreEqual("就任", w.pol.cabinet.history[0].action);
            Assert.AreEqual("兵部大臣", w.pol.cabinet.history[0].postLabel);

            Assert.AreEqual(regBefore, GovernmentRegistry.Appointments.Count, "閣僚職を GovernmentRegistry へ登録しない");
            CollectionAssert.AreEqual(new[] { 90, 91 }, w.tree[3].staffIds, "職業官僚の配属を変えない");

            // 首相がいない（組閣未成立）なら誰も任命できない＝理由つき
            w.pol.government.status = CabinetStatus.組閣未成立;
            w.pol.government.premierPersonId = -1;
            r = Appoint(w, 1, Okura, CabinetPostKind.大臣, 3);
            Assert.IsFalse(r.ok);
            Assert.IsFalse(r.canPetition);
            StringAssert.Contains("任免権者（首相）が不在", r.reason);
        }

        [Test]
        public void TryAppoint_RejectsIneligibleAndUnknownMinistry_WithReasons_NoChange()
        {
            World w = NewWorld();
            var cases = new (int person, string expect)[]
            {
                (999, "名簿に存在しない"), (12, "死亡"), (13, "拘束"), (11, "他勢力"), (9, "軍人"), (10, "政治家でない"),
                (7, "首相でない党首"), (8, "与党でない党"), (1, "首相は閣僚職を兼任しない"),
            };
            foreach (var c in cases)
            {
                AppointmentResult r = Appoint(w, 1, Okura, CabinetPostKind.政務官, c.person);
                Assert.IsFalse(r.ok, "人物#" + c.person + " を任命できてしまった");
                StringAssert.Contains(c.expect, r.reason, "人物#" + c.person);
            }
            AppointmentResult bad = Appoint(w, 1, 5555, CabinetPostKind.大臣, 2);
            Assert.IsFalse(bad.ok);
            StringAssert.Contains("存在しない省", bad.reason);
            AppointmentResult top = Appoint(w, 1, Top, CabinetPostKind.大臣, 2);
            Assert.IsFalse(top.ok, "最上位の太政官は大臣を置く省でない");
            Assert.AreEqual(0, FilledTotal(w.pol.cabinet));
            Assert.AreEqual(0, w.pol.cabinet.history.Count);
        }

        [Test]
        public void Concurrency_OnePostPerPerson_GovernorPartyExecutiveAndOccupiedRejected()
        {
            World w = NewWorld();
            Assert.IsTrue(Appoint(w, 1, Hyobu, CabinetPostKind.大臣, 2).ok);

            AppointmentResult r = Appoint(w, 1, Okura, CabinetPostKind.副大臣, 2);
            Assert.IsFalse(r.ok);
            StringAssert.Contains("兼任不可", r.reason);

            r = Appoint(w, 1, Hyobu, CabinetPostKind.大臣, 5);
            Assert.IsFalse(r.ok);
            StringAssert.Contains("在任者", r.reason);

            w.pol.locals.Add(new LocalElectionState(50) { governorPersonId = 3, status = LocalElectionStatus.当選 });
            r = Appoint(w, 1, Okura, CabinetPostKind.大臣, 3);
            Assert.IsFalse(r.ok);
            StringAssert.Contains("知事と兼任できない", r.reason);

            Assert.IsTrue(PartyExecutiveRules.TryAppoint(w.pol, F, w.ruling, 1, PartyPost.幹事長, 4, w.roster, Year, "試験", Prm).ok);
            r = Appoint(w, 1, Okura, CabinetPostKind.大臣, 4);
            Assert.IsFalse(r.ok);
            StringAssert.Contains("党三役", r.reason);

            AppointmentResult pr = PartyExecutiveRules.TryAppoint(w.pol, F, w.ruling, 1, PartyPost.政調会長, 2, w.roster, Year, "試験", Prm);
            Assert.IsFalse(pr.ok);
            StringAssert.Contains("閣僚（兵部大臣）と党三役は兼任しない", pr.reason);
        }

        [Test]
        public void PartyExecutives_GovernorRejected_GovernorOrPremierInOfficeLosesPartyPost_WithReason()
        {
            World w = NewWorld();
            Party p = w.ruling;

            // 現職知事は共通入口で拒否（理由つき・何も変えない）
            w.pol.locals.Add(new LocalElectionState(50) { governorPersonId = 3, status = LocalElectionStatus.当選 });
            AppointmentResult r = PartyExecutiveRules.TryAppoint(w.pol, F, p, 1, PartyPost.幹事長, 3, w.roster, Year, "試験", Prm);
            Assert.IsFalse(r.ok);
            StringAssert.Contains("星系#50 の知事と党三役は兼任しない", r.reason);
            Assert.AreEqual(-1, PartyOrganizationRules.HolderOf(p, PartyPost.幹事長));
            Assert.AreEqual(0, p.postHistory.Count);

            // AI 補充も同じ入口：候補 2..6 から首相/党首1・知事3を除く 2,4,5（同点は ID 小）
            PartyExecutiveRules.AutoFill(w.pol, F, w.roster, Year, Prm);
            Assert.AreEqual(2, PartyOrganizationRules.HolderOf(p, PartyPost.幹事長));
            Assert.AreEqual(4, PartyOrganizationRules.HolderOf(p, PartyPost.政調会長));
            Assert.AreEqual(5, PartyOrganizationRules.HolderOf(p, PartyPost.総務会長));

            // 在任の政調会長4が知事に就いた → 三役だけ失職（理由・権限なし）、知事の在任は消さない、繰り返しても二重に記録しない
            w.pol.locals.Add(new LocalElectionState(51) { governorPersonId = 4, status = LocalElectionStatus.当選 });
            List<AppointmentHistoryEntry> changes = PartyExecutiveRules.Reconcile(w.pol, F, w.roster, Year, Prm);
            Assert.AreEqual(1, changes.Count);
            Assert.AreEqual("失職", changes[0].action);
            Assert.AreEqual(4, changes[0].personId);
            StringAssert.Contains("星系#51 の知事に就いた", changes[0].reason);
            Assert.AreEqual(-1, PartyOrganizationRules.HolderOf(p, PartyPost.政調会長));
            StringAssert.Contains("知事に就いた", PartyExecutiveRules.VacancyReason(p, PartyPost.政調会長));
            Assert.IsFalse(PartyExecutiveRules.Authority(p, F, 4, PartyExecutiveAction.政策調整, w.roster, Year).ok);
            Assert.AreEqual(51, LocalElectionRules.GovernedSystemOf(w.pol, 4, -1), "三役の整理で知事を外さない");
            Assert.AreEqual(0, PartyExecutiveRules.Reconcile(w.pol, F, w.roster, Year, Prm).Count, "二重に記録しない");
            PartyExecutiveRules.AutoFill(w.pol, F, w.roster, Year, Prm);
            Assert.AreEqual(6, PartyOrganizationRules.HolderOf(p, PartyPost.政調会長), "補充でも知事3,4を選ばない");

            // 党首でない人物が首相に就いた（幹事長2）→ 三役を失職。党首1はそのまま（首相と党首の分離はあり得る）
            w.pol.government.premierPersonId = 2;
            changes = PartyExecutiveRules.Reconcile(w.pol, F, w.roster, Year, Prm);
            Assert.AreEqual(1, changes.Count);
            Assert.AreEqual(2, changes[0].personId);
            StringAssert.Contains("首相に就いた", changes[0].reason);
            Assert.AreEqual(-1, PartyOrganizationRules.HolderOf(p, PartyPost.幹事長));
            Assert.AreEqual(1, p.leaderId);
            Assert.AreEqual(2, w.pol.government.premierPersonId, "三役の整理で首相を動かさない");
        }

        [Test]
        public void Cabinet_MinisterBecomingNonPremierLeader_LosesPost_PremierLosingLeadershipKeepsOffice()
        {
            World w = NewWorld();
            Assert.IsTrue(Appoint(w, 1, Hyobu, CabinetPostKind.大臣, 2).ok);
            Assert.IsTrue(Appoint(w, 1, Okura, CabinetPostKind.大臣, 3).ok);

            // 総裁選で大臣2が与党の党首に（首相1は党首でなくなる）
            w.ruling.leaderId = 2;
            List<AppointmentHistoryEntry> changes = CabinetAppointmentRules.Reconcile(w.pol, F, w.tree, Top, w.roster, Year, Prm);
            Assert.AreEqual(1, CountAction(changes, "失職"));
            Assert.AreEqual(0, CountAction(changes, "総辞職"), "首相が党首でなくなっても総辞職にしない");
            Assert.AreEqual(0, CountAction(changes, "職務執行"));
            Assert.AreEqual(-1, Holder(w, Hyobu, CabinetPostKind.大臣));
            StringAssert.Contains("党首（民政党）に就いたため失職",
                CabinetAppointmentRules.FindPost(w.pol.cabinet, Hyobu, CabinetPostKind.大臣).vacancyReason);
            Assert.IsFalse(Auth(w, 2, Hyobu, CabinetAction.所管決裁).ok, "党首に就いた元大臣の権限が残った");
            Assert.AreEqual(3, Holder(w, Okura, CabinetPostKind.大臣), "党首でない大臣は続投");
            Assert.AreEqual(1, w.pol.cabinet.premierPersonId);
            Assert.IsTrue(Auth(w, 1, Okura, CabinetAction.閣僚任免).ok, "党首でなくなった正式な首相の任免権は残る");

            AppointmentResult again = Appoint(w, 1, Hyobu, CabinetPostKind.大臣, 2);
            Assert.IsFalse(again.ok);
            StringAssert.Contains("首相でない党首", again.reason);
            Assert.AreEqual(0, CabinetAppointmentRules.Reconcile(w.pol, F, w.tree, Top, w.roster, Year, Prm).Count, "二重に記録しない");

            // 党三役が党首に就いた場合も三役を失職（新党首2の任命した幹事長4が党首へ）
            Assert.IsTrue(PartyExecutiveRules.TryAppoint(w.pol, F, w.ruling, 2, PartyPost.幹事長, 4, w.roster, Year, "試験", Prm).ok);
            w.ruling.leaderId = 4;
            List<AppointmentHistoryEntry> party = PartyExecutiveRules.Reconcile(w.pol, F, w.roster, Year, Prm);
            Assert.AreEqual(1, CountAction(party, "失職"));
            StringAssert.Contains("党首に就いた", party[0].reason);
            Assert.AreEqual(-1, PartyOrganizationRules.HolderOf(w.ruling, PartyPost.幹事長));
        }

        // ===== 3. 権限差・委任 =====

        [Test]
        public void Authority_MinisterViceSecretaryPremier_Differ_NoFleetCommandOrTreasury()
        {
            World w = NewWorld();
            Assert.IsTrue(Appoint(w, 1, Hyobu, CabinetPostKind.大臣, 2).ok);
            Assert.IsTrue(Appoint(w, 1, Hyobu, CabinetPostKind.副大臣, 3).ok);
            Assert.IsTrue(Appoint(w, 1, Hyobu, CabinetPostKind.政務官, 5).ok);

            Assert.IsTrue(Auth(w, 2, Hyobu, CabinetAction.所管決裁).ok, "大臣は所管の決裁");
            Assert.IsTrue(Auth(w, 2, Hyobu, CabinetAction.所管政策決定).ok);
            Assert.IsFalse(Auth(w, 2, Okura, CabinetAction.所管決裁).ok, "他省の決裁はできない");
            Assert.IsFalse(Auth(w, 2, Hyobu, CabinetAction.閣僚任免).ok, "大臣は閣僚を任免しない");

            AppointmentResult vice = Auth(w, 3, Hyobu, CabinetAction.所管決裁);
            Assert.IsFalse(vice.ok, "委任なしの副大臣は決裁できない");
            Assert.AreEqual(2, vice.petitionToId);
            Assert.IsTrue(Auth(w, 3, Hyobu, CabinetAction.政策提案).ok);
            Assert.IsTrue(Auth(w, 3, Hyobu, CabinetAction.政策調整).ok);

            Assert.IsFalse(Auth(w, 5, Hyobu, CabinetAction.所管決裁).ok, "政務官は最終決裁権なし");
            Assert.IsFalse(Auth(w, 5, Hyobu, CabinetAction.所管政策決定).ok);
            Assert.IsTrue(Auth(w, 5, Hyobu, CabinetAction.政策提案).ok);

            Assert.IsTrue(Auth(w, 1, Hyobu, CabinetAction.閣僚任免).ok, "首相は任免権者");
            Assert.IsFalse(Auth(w, 1, Hyobu, CabinetAction.所管決裁).ok, "首相でも所管の決裁は大臣");

            foreach (int person in new[] { 1, 2, 3, 5 })
            {
                AppointmentResult fleet = Auth(w, person, Hyobu, CabinetAction.艦隊作戦指揮);
                Assert.IsFalse(fleet.ok, "人物#" + person + " が艦隊の作戦指揮権を得た");
                StringAssert.Contains("作戦指揮権を含まない", fleet.reason);
                Assert.IsFalse(Auth(w, person, Okura, CabinetAction.国庫支出).ok);
            }
            Assert.IsFalse(Auth(w, 12, Hyobu, CabinetAction.政策提案).ok, "死亡した人物は何もできない");
        }

        [Test]
        public void Delegation_ExplicitScopeAndTerm_OnlyMinister_VoidedByMinisterChangeAndExpiry()
        {
            World w = NewWorld();
            Assert.IsTrue(Appoint(w, 1, Hyobu, CabinetPostKind.大臣, 2).ok);
            Assert.IsTrue(Appoint(w, 1, Hyobu, CabinetPostKind.副大臣, 3).ok);

            AppointmentResult r = CabinetAppointmentRules.Delegate(w.pol, F, 3, Hyobu, CabinetDelegation.所管決裁, Year + 1, w.roster, Year, Prm);
            Assert.IsFalse(r.ok, "副大臣が自分へ委任できた");
            Assert.AreEqual(2, r.petitionToId);
            Assert.IsFalse(CabinetAppointmentRules.Delegate(w.pol, F, 2, Hyobu, CabinetDelegation.なし, Year + 1, w.roster, Year, Prm).ok);
            r = CabinetAppointmentRules.Delegate(w.pol, F, 2, Hyobu, CabinetDelegation.所管決裁, Year + 3, w.roster, Year, Prm);
            Assert.IsFalse(r.ok);
            StringAssert.Contains("無期限の代行はしない", r.reason);
            Assert.IsFalse(CabinetAppointmentRules.Delegate(w.pol, F, 2, Hyobu, CabinetDelegation.所管決裁, Year - 1, w.roster, Year, Prm).ok);

            Assert.IsTrue(CabinetAppointmentRules.Delegate(w.pol, F, 2, Hyobu, CabinetDelegation.所管決裁, Year + 1, w.roster, Year, Prm).ok);
            Assert.IsTrue(Auth(w, 3, Hyobu, CabinetAction.所管決裁).ok, "委任の範囲なら副大臣が決裁");
            AppointmentResult policy = Auth(w, 3, Hyobu, CabinetAction.所管政策決定);
            Assert.IsFalse(policy.ok, "委任されていない範囲は代行しない");
            StringAssert.Contains("委任されていない", policy.reason);
            Assert.IsFalse(Auth(w, 3, Okura, CabinetAction.所管決裁).ok, "他省は代行しない");
            Assert.IsFalse(Auth(w, 3, Hyobu, CabinetAction.所管決裁, Year + 2).ok, "期限後は代行しない");

            // 大臣の解任＝委任は失効
            Assert.IsTrue(CabinetAppointmentRules.Dismiss(w.pol, F, 1, Hyobu, CabinetPostKind.大臣, w.roster, Year, "", Prm).ok);
            CabinetPost vice = CabinetAppointmentRules.FindPost(w.pol.cabinet, Hyobu, CabinetPostKind.副大臣);
            Assert.AreEqual(CabinetDelegation.なし, vice.delegation);
            Assert.IsFalse(Auth(w, 3, Hyobu, CabinetAction.所管決裁).ok);
            Assert.AreEqual(1, CountAction(w.pol.cabinet.history, "委任失効"));
            Assert.AreEqual(1, CountAction(w.pol.cabinet.history, "解任"));

            // 期限切れは整理で失効
            Assert.IsTrue(Appoint(w, 1, Hyobu, CabinetPostKind.大臣, 4).ok);
            Assert.IsTrue(CabinetAppointmentRules.Delegate(w.pol, F, 4, Hyobu, CabinetDelegation.所管政策 | CabinetDelegation.所管決裁, Year + 1, w.roster, Year, Prm).ok);
            Assert.AreEqual(0, CabinetAppointmentRules.Reconcile(w.pol, F, w.tree, Top, w.roster, Year + 1, Prm).Count, "期限内は失効しない");
            List<AppointmentHistoryEntry> expired = CabinetAppointmentRules.Reconcile(w.pol, F, w.tree, Top, w.roster, Year + 2, Prm);
            Assert.AreEqual(1, expired.Count);
            Assert.AreEqual("委任失効", expired[0].action);
            StringAssert.Contains("期限", expired[0].reason);
            Assert.AreEqual(CabinetDelegation.なし, vice.delegation);
        }

        // ===== 4. AI 組閣と首相交代 =====

        [Test]
        public void AutoFill_SelectsByAbilityAndDiminishingSeniority_ShortageLeavesVacancy_PremierChangeResigns()
        {
            World w = NewWorld();
            w.roster.Find(x => x.id == 6).operation = 90;   // 若手の実力者（当選履歴なし）
            w.roster.Find(x => x.id == 6).intelligence = 90;
            w.pol.legislators.Add(new LegislatorRecord(5) { lowerWins = 7, seated = true }); // ベテラン

            List<AppointmentHistoryEntry> formed = CabinetAppointmentRules.AutoFill(w.pol, F, w.tree, Top, w.roster, Year, Prm);
            CabinetState cab = w.pol.cabinet;
            // 大臣：5=0.6*0.35+0.25*0.875+0.1+0.1=0.629 → 式部／6=0.6*0.63+0.1=0.478 → 大蔵／2,3,4=0.31 同点は ID 小 → 兵部
            Assert.AreEqual(5, Holder(w, Shikibu, CabinetPostKind.大臣));
            Assert.AreEqual(6, Holder(w, Okura, CabinetPostKind.大臣), "実績のある若手の抜擢");
            Assert.AreEqual(2, Holder(w, Hyobu, CabinetPostKind.大臣));
            Assert.AreEqual(3, Holder(w, Shikibu, CabinetPostKind.副大臣));
            Assert.AreEqual(4, Holder(w, Okura, CabinetPostKind.副大臣));
            Assert.AreEqual(-1, Holder(w, Hyobu, CabinetPostKind.副大臣));
            Assert.AreEqual(0, CabinetAppointmentRules.FilledCount(cab, CabinetPostKind.政務官));
            Assert.AreEqual(5, formed.Count);
            StringAssert.Contains("適格な候補なし", CabinetAppointmentRules.FindPost(cab, Hyobu, CabinetPostKind.政務官).vacancyReason);
            CabinetPost m5 = CabinetAppointmentRules.FindPost(cab, Shikibu, CabinetPostKind.大臣);
            StringAssert.Contains("選定理由", m5.appointmentReason);
            StringAssert.Contains("国政当選7回（ベテラン）", m5.appointmentReason);
            Assert.AreEqual(0, CabinetAppointmentRules.AutoFill(w.pol, F, w.tree, Top, w.roster, Year, Prm).Count, "候補不足で人物を作らない・繰り返しても増えない");
            Assert.AreEqual(-1, CabinetAppointmentRules.FindPost(cab, Hyobu, CabinetPostKind.政務官).holderId);

            // 政権交代：進歩党の首相7 → 前内閣は総辞職、旧閣僚の権限なし
            w.pol.government = new GovernmentFormation { status = CabinetStatus.少数政権, premierPersonId = 7, partyId = 2, formedYear = Year + 1, sourceElectionId = "e2" };
            List<AppointmentHistoryEntry> resigned = CabinetAppointmentRules.Reconcile(w.pol, F, w.tree, Top, w.roster, Year + 1, Prm);
            Assert.AreEqual(1, CountAction(resigned, "総辞職"));
            Assert.AreEqual(5, CountAction(resigned, "退任"));
            StringAssert.Contains("首相交代", resigned[0].reason);
            Assert.AreEqual(0, FilledTotal(cab));
            Assert.AreEqual(7, cab.premierPersonId);
            Assert.AreEqual(Year + 1, cab.formedYear);
            Assert.IsFalse(Auth(w, 5, Shikibu, CabinetAction.所管決裁, Year + 1).ok, "旧大臣の権限が残った");
            AppointmentResult oldPremier = Auth(w, 1, Shikibu, CabinetAction.閣僚任免, Year + 1);
            Assert.IsFalse(oldPremier.ok, "旧首相の任免権が残った");
            Assert.AreEqual(7, oldPremier.petitionToId);

            CabinetAppointmentRules.AutoFill(w.pol, F, w.tree, Top, w.roster, Year + 1, Prm);
            Assert.AreEqual(8, Holder(w, Shikibu, CabinetPostKind.大臣), "新与党の党員だけが入閣");
            Assert.AreEqual(1, FilledTotal(cab), "旧与党（野党になった）の党員は入閣しない");
            Assert.AreEqual(7, CabinetAppointmentRules.FindPost(cab, Shikibu, CabinetPostKind.大臣).appointedById);

            // 同じ首相でも下院の改選後の首班指名で組み直す＝同じ職への再任は続投
            w.pol.government.sourceElectionId = "e3";
            List<AppointmentHistoryEntry> again = CabinetAppointmentRules.Reconcile(w.pol, F, w.tree, Top, w.roster, Year + 2, Prm);
            Assert.AreEqual(1, CountAction(again, "総辞職"));
            List<AppointmentHistoryEntry> refilled = CabinetAppointmentRules.AutoFill(w.pol, F, w.tree, Top, w.roster, Year + 2, Prm);
            Assert.AreEqual(1, refilled.Count);
            Assert.AreEqual("続投", refilled[0].action);
        }

        [Test]
        public void PremierAbsent_CaretakerWithLimitedScope_ThenExpiresAndResigns()
        {
            World w = NewWorld();
            Assert.IsTrue(Appoint(w, 1, Hyobu, CabinetPostKind.大臣, 2).ok);
            Assert.IsTrue(Appoint(w, 1, Hyobu, CabinetPostKind.副大臣, 3).ok);
            Assert.IsTrue(CabinetAppointmentRules.Delegate(w.pol, F, 2, Hyobu, CabinetDelegation.所管決裁, Year + 1, w.roster, Year, Prm).ok);

            w.P(1).deathYear = Year; // 首相の死亡（政府の記録はまだ首相1のまま）
            List<AppointmentHistoryEntry> changes = CabinetAppointmentRules.Reconcile(w.pol, F, w.tree, Top, w.roster, Year, Prm);
            CabinetState cab = w.pol.cabinet;
            Assert.IsTrue(cab.caretaker);
            Assert.AreEqual(Year + 1, cab.caretakerUntilYear);
            Assert.AreEqual(1, cab.caretakerOfPremierId);
            Assert.AreEqual(-1, cab.premierPersonId);
            Assert.AreEqual(1, CountAction(changes, "職務執行"));
            Assert.AreEqual(1, CountAction(changes, "委任失効"));
            StringAssert.Contains("死亡", cab.caretakerReason);

            AppointmentResult decide = Auth(w, 2, Hyobu, CabinetAction.所管決裁);
            Assert.IsTrue(decide.ok, "職務執行でも所管の決裁は続く");
            StringAssert.Contains("職務執行", decide.reason);
            Assert.IsFalse(Auth(w, 2, Hyobu, CabinetAction.所管政策決定).ok, "職務執行で政策決定はしない");
            Assert.IsFalse(Auth(w, 3, Hyobu, CabinetAction.所管決裁).ok, "委任は失効");
            AppointmentResult appoint = Appoint(w, 2, Okura, CabinetPostKind.大臣, 4);
            Assert.IsFalse(appoint.ok);
            StringAssert.Contains("任免権者（首相）が不在", appoint.reason);
            Assert.AreEqual(0, CabinetAppointmentRules.AutoFill(w.pol, F, w.tree, Top, w.roster, Year, Prm).Count);
            Assert.IsFalse(CabinetAppointmentRules.Delegate(w.pol, F, 2, Hyobu, CabinetDelegation.所管決裁, Year + 1, w.roster, Year, Prm).ok);

            Assert.AreEqual(0, CabinetAppointmentRules.Reconcile(w.pol, F, w.tree, Top, w.roster, Year + 1, Prm).Count, "期限内は続く");
            Assert.AreEqual(2, Holder(w, Hyobu, CabinetPostKind.大臣));
            List<AppointmentHistoryEntry> ended = CabinetAppointmentRules.Reconcile(w.pol, F, w.tree, Top, w.roster, Year + 2, Prm);
            Assert.AreEqual(1, CountAction(ended, "総辞職"));
            StringAssert.Contains("期限", ended[0].reason);
            Assert.IsFalse(cab.caretaker);
            Assert.AreEqual(0, FilledTotal(cab));
            Assert.IsFalse(Auth(w, 2, Hyobu, CabinetAction.所管決裁, Year + 2).ok, "期限後に権限が残った");
        }

        [Test]
        public void HolderDeathCaptivityDefection_VacateWithReason_Idempotent_AndRegimeChangeResigns()
        {
            World w = NewWorld();
            Assert.IsTrue(Appoint(w, 1, Hyobu, CabinetPostKind.大臣, 2).ok);
            Assert.IsTrue(Appoint(w, 1, Okura, CabinetPostKind.大臣, 3).ok);
            Assert.IsTrue(Appoint(w, 1, Shikibu, CabinetPostKind.大臣, 4).ok);

            w.P(2).deathYear = Year;
            w.P(4).captiveStatus = CaptiveStatus.捕虜;
            w.ruling.memberIds.Remove(3);
            w.opposition.memberIds.Add(3);
            List<AppointmentHistoryEntry> changes = CabinetAppointmentRules.Reconcile(w.pol, F, w.tree, Top, w.roster, Year, Prm);
            Assert.AreEqual(3, CountAction(changes, "失職"));
            StringAssert.Contains("死亡", CabinetAppointmentRules.FindPost(w.pol.cabinet, Hyobu, CabinetPostKind.大臣).vacancyReason);
            StringAssert.Contains("拘束", CabinetAppointmentRules.FindPost(w.pol.cabinet, Shikibu, CabinetPostKind.大臣).vacancyReason);
            StringAssert.Contains("与党でない党", CabinetAppointmentRules.FindPost(w.pol.cabinet, Okura, CabinetPostKind.大臣).vacancyReason);
            Assert.AreEqual(0, CabinetAppointmentRules.Reconcile(w.pol, F, w.tree, Top, w.roster, Year, Prm).Count, "二重に失職を記録しない");

            Assert.IsTrue(Appoint(w, 1, Hyobu, CabinetPostKind.大臣, 5).ok);
            w.pol.government = new GovernmentFormation { status = CabinetStatus.対象外, partyId = -1, formedYear = Year };
            List<AppointmentHistoryEntry> suspended = CabinetAppointmentRules.Reconcile(w.pol, F, w.tree, Top, w.roster, Year, Prm);
            Assert.AreEqual(1, CountAction(suspended, "総辞職"));
            StringAssert.Contains("政体", suspended[0].reason);
            Assert.AreEqual(0, FilledTotal(w.pol.cabinet));
            Assert.AreEqual(-1, w.pol.cabinet.premierPersonId);
        }

        // ===== 5. 党三役 =====

        [Test]
        public void PartyExecutives_LeaderOnly_RoleSeparation_NoGovernmentAuthority()
        {
            World w = NewWorld();
            Party p = w.ruling;
            AppointmentResult r = PartyExecutiveRules.TryAppoint(w.pol, F, p, 2, PartyPost.幹事長, 3, w.roster, Year, "試験", Prm);
            Assert.IsFalse(r.ok);
            Assert.IsTrue(r.canPetition);
            Assert.AreEqual(1, r.petitionToId, "提案先は党首");
            StringAssert.Contains("総裁選", PartyExecutiveRules.TryAppoint(w.pol, F, p, 1, PartyPost.党首, 3, w.roster, Year, "", Prm).reason);
            Assert.IsFalse(PartyExecutiveRules.TryAppoint(w.pol, F, p, 1, PartyPost.国対委員長, 3, w.roster, Year, "", Prm).ok);
            StringAssert.Contains("党員でない", PartyExecutiveRules.TryAppoint(w.pol, F, p, 1, PartyPost.幹事長, 8, w.roster, Year, "", Prm).reason);
            StringAssert.Contains("党首は三役を兼ねない", PartyExecutiveRules.TryAppoint(w.pol, F, p, 1, PartyPost.幹事長, 1, w.roster, Year, "", Prm).reason);

            Assert.IsTrue(PartyExecutiveRules.TryAppoint(w.pol, F, p, 1, PartyPost.幹事長, 2, w.roster, Year, "試験", Prm).ok);
            StringAssert.Contains("兼任不可", PartyExecutiveRules.TryAppoint(w.pol, F, p, 1, PartyPost.政調会長, 2, w.roster, Year, "", Prm).reason);
            Assert.IsTrue(PartyExecutiveRules.TryAppoint(w.pol, F, p, 1, PartyPost.政調会長, 3, w.roster, Year, "試験", Prm).ok);
            Assert.IsTrue(PartyExecutiveRules.TryAppoint(w.pol, F, p, 1, PartyPost.総務会長, 4, w.roster, Year, "試験", Prm).ok);
            Assert.AreEqual(2, PartyOrganizationRules.HolderOf(p, PartyPost.幹事長));
            Assert.AreEqual(1, p.leaderId, "党首は leaderId が単一の出所");
            Assert.AreEqual(3, p.postHistory.Count);

            Assert.IsTrue(PartyExecutiveRules.Authority(p, F, 2, PartyExecutiveAction.党運営, w.roster, Year).ok);
            Assert.IsTrue(PartyExecutiveRules.Authority(p, F, 2, PartyExecutiveAction.選挙候補調整, w.roster, Year).ok);
            Assert.IsFalse(PartyExecutiveRules.Authority(p, F, 2, PartyExecutiveAction.政策調整, w.roster, Year).ok);
            Assert.IsTrue(PartyExecutiveRules.Authority(p, F, 3, PartyExecutiveAction.政策調整, w.roster, Year).ok);
            Assert.IsFalse(PartyExecutiveRules.Authority(p, F, 3, PartyExecutiveAction.党内合意, w.roster, Year).ok);
            Assert.IsTrue(PartyExecutiveRules.Authority(p, F, 4, PartyExecutiveAction.党内合意, w.roster, Year).ok);
            Assert.IsFalse(PartyExecutiveRules.Authority(p, F, 2, PartyExecutiveAction.党役職任免, w.roster, Year).ok);
            Assert.IsTrue(PartyExecutiveRules.Authority(p, F, 1, PartyExecutiveAction.党役職任免, w.roster, Year).ok);
            foreach (int person in new[] { 1, 2, 3, 4 })
                foreach (PartyExecutiveAction a in new[] { PartyExecutiveAction.政府決裁, PartyExecutiveAction.閣僚任免, PartyExecutiveAction.艦隊作戦指揮, PartyExecutiveAction.国庫支出 })
                    Assert.IsFalse(PartyExecutiveRules.Authority(p, F, person, a, w.roster, Year).ok, "人物#" + person + " が党役職で " + a + " を得た");
            Assert.IsFalse(Auth(w, 2, Hyobu, CabinetAction.所管決裁).ok, "幹事長は政府の決裁権を持たない");
            Assert.IsFalse(Auth(w, 2, Hyobu, CabinetAction.閣僚任免).ok);
        }

        [Test]
        public void PartyExecutives_DepartureLeaderChangeAndAbsence_ClearOldAuthority()
        {
            World w = NewWorld();
            Party p = w.ruling;
            Assert.IsTrue(PartyExecutiveRules.TryAppoint(w.pol, F, p, 1, PartyPost.幹事長, 2, w.roster, Year, "試験", Prm).ok);
            Assert.IsTrue(PartyExecutiveRules.TryAppoint(w.pol, F, p, 1, PartyPost.政調会長, 3, w.roster, Year, "試験", Prm).ok);
            Assert.IsTrue(PartyExecutiveRules.TryAppoint(w.pol, F, p, 1, PartyPost.総務会長, 4, w.roster, Year, "試験", Prm).ok);

            // 離党（PartyOrganizationRules.Leave が在任を消す）→ 整理で失職を記録
            Assert.IsTrue(PartyOrganizationRules.Leave(p, 3));
            List<AppointmentHistoryEntry> left = PartyExecutiveRules.Reconcile(w.pol, F, w.roster, Year, Prm);
            Assert.AreEqual(1, left.Count);
            Assert.AreEqual("失職", left[0].action);
            StringAssert.Contains("党籍を失った", PartyExecutiveRules.VacancyReason(p, PartyPost.政調会長));
            Assert.IsFalse(PartyExecutiveRules.Authority(p, F, 3, PartyExecutiveAction.政策調整, w.roster, Year).ok);
            Assert.AreEqual(0, PartyExecutiveRules.Reconcile(w.pol, F, w.roster, Year, Prm).Count, "二重に記録しない");

            // 党首交代 → 三役は退任、新党首の再任は続投
            p.leaderId = 5;
            List<AppointmentHistoryEntry> change = PartyExecutiveRules.Reconcile(w.pol, F, w.roster, Year, Prm);
            Assert.AreEqual(2, CountAction(change, "党首交代"));
            Assert.AreEqual(-1, PartyOrganizationRules.HolderOf(p, PartyPost.幹事長));
            Assert.IsFalse(PartyExecutiveRules.Authority(p, F, 2, PartyExecutiveAction.党運営, w.roster, Year).ok, "旧党首の任命した幹事長の権限が残った");
            Assert.IsFalse(PartyExecutiveRules.TryAppoint(w.pol, F, p, 1, PartyPost.幹事長, 2, w.roster, Year, "", Prm).ok, "前党首が任命できた");
            Assert.IsTrue(PartyExecutiveRules.TryAppoint(w.pol, F, p, 5, PartyPost.幹事長, 2, w.roster, Year, "再任", Prm).ok);
            Assert.AreEqual("続投", p.postHistory[p.postHistory.Count - 1].action);

            // AI の補充も同じ入口（党首5の任命）。候補：1=首相（兼ねない）・2=幹事長・5=党首を除く 4,6（同点は ID 小）。野党は 8 が幹事長、残りは候補なし
            List<AppointmentHistoryEntry> filled = PartyExecutiveRules.AutoFill(w.pol, F, w.roster, Year, Prm);
            Assert.AreEqual(3, filled.Count);
            Assert.AreEqual(4, PartyOrganizationRules.HolderOf(p, PartyPost.政調会長));
            Assert.AreEqual(6, PartyOrganizationRules.HolderOf(p, PartyPost.総務会長));
            Assert.AreEqual(8, PartyOrganizationRules.HolderOf(w.opposition, PartyPost.幹事長));
            StringAssert.Contains("適格な候補なし", PartyExecutiveRules.VacancyReason(w.opposition, PartyPost.政調会長));
            StringAssert.Contains("首相は党三役を兼ねない", PartyExecutiveRules.CandidateProblem(w.pol, F, p, 1, w.roster, Prm));
            Assert.AreEqual(5, PartyExecutiveRules.AppointmentOf(p, PartyPost.政調会長).appointedById);
            StringAssert.Contains("選定理由", PartyExecutiveRules.AppointmentOf(p, PartyPost.政調会長).reason);

            // 党首不在 → 暫定（期限まで所掌だけ）→ 期限切れで退任
            p.leaderId = -1;
            List<AppointmentHistoryEntry> absent = PartyExecutiveRules.Reconcile(w.pol, F, w.roster, Year, Prm);
            Assert.AreEqual(3, CountAction(absent, "暫定"));
            AppointmentResult caretaker = PartyExecutiveRules.Authority(p, F, 2, PartyExecutiveAction.党運営, w.roster, Year + 1);
            Assert.IsTrue(caretaker.ok);
            StringAssert.Contains("暫定", caretaker.reason);
            Assert.IsFalse(PartyExecutiveRules.Authority(p, F, 2, PartyExecutiveAction.党運営, w.roster, Year + 2).ok);
            Assert.AreEqual(0, PartyExecutiveRules.AutoFill(w.pol, F, w.roster, Year, Prm).Count, "党首不在では任命しない");
            List<AppointmentHistoryEntry> expired = PartyExecutiveRules.Reconcile(w.pol, F, w.roster, Year + 2, Prm);
            Assert.AreEqual(3, CountAction(expired, "失職"));
            Assert.AreEqual(-1, PartyOrganizationRules.HolderOf(p, PartyPost.幹事長));
            StringAssert.Contains("党首", PartyExecutiveRules.VacancyReason(p, PartyPost.幹事長));
        }

        // ===== 6. 保存 =====

        [Test]
        public void SaveRoundTrip_KeepsCabinetDelegationPartyPostsHistory_LoadDoesNotReappoint()
        {
            World w = NewWorld();
            CabinetAppointmentRules.AutoFill(w.pol, F, w.tree, Top, w.roster, Year, Prm);
            int minister = Holder(w, Hyobu, CabinetPostKind.大臣);
            int vice = Holder(w, Shikibu, CabinetPostKind.副大臣);
            int shikibuMinister = Holder(w, Shikibu, CabinetPostKind.大臣);
            Assert.IsTrue(CabinetAppointmentRules.Delegate(w.pol, F, shikibuMinister, Shikibu, CabinetDelegation.所管決裁, Year + 2, w.roster, Year, Prm).ok);
            // 三役に回す人を残すため、ここは手で党首の任命をする（閣僚でない党員はいない＝兼任拒否を確かめる）
            Assert.IsFalse(PartyExecutiveRules.TryAppoint(w.pol, F, w.ruling, 1, PartyPost.幹事長, minister, w.roster, Year, "", Prm).ok);
            Assert.IsTrue(PartyExecutiveRules.TryAppoint(w.pol, F, w.opposition, 7, PartyPost.幹事長, 8, w.roster, Year, "野党の幹事長", Prm).ok);
            int historyCount = w.pol.cabinet.history.Count;

            var map = new GalaxyMap();
            map.AddSystem(new StarSystem(0, "試験星", Vector2.zero, F));
            var campaign = new CampaignState(map);
            campaign.states.Add(new FactionState(F) { governmentForm = GovernmentForm.共和制, politics = w.pol });
            CampaignState loaded = CampaignSerializer.FromJson(CampaignSerializer.ToJson(campaign));
            PoliticsState a = loaded.states[0].politics;
            var lw = new World { pol = a, tree = w.tree, roster = w.roster, ruling = a.parties[0], opposition = a.parties[1] };

            Assert.AreEqual(1, a.cabinet.premierPersonId);
            Assert.AreEqual(minister, Holder(lw, Hyobu, CabinetPostKind.大臣));
            Assert.AreEqual(vice, Holder(lw, Shikibu, CabinetPostKind.副大臣));
            CabinetPost v = CabinetAppointmentRules.FindPost(a.cabinet, Shikibu, CabinetPostKind.副大臣);
            Assert.AreEqual(CabinetDelegation.所管決裁, v.delegation);
            Assert.AreEqual(shikibuMinister, v.delegatedById);
            Assert.AreEqual(Year + 2, v.delegationEndYear);
            Assert.AreEqual(historyCount, a.cabinet.history.Count);
            StringAssert.Contains("適格な候補なし", CabinetAppointmentRules.FindPost(a.cabinet, Hyobu, CabinetPostKind.政務官).vacancyReason);
            PartyAppointment sec = PartyExecutiveRules.AppointmentOf(lw.opposition, PartyPost.幹事長);
            Assert.AreEqual(8, sec.holderId);
            Assert.AreEqual(7, sec.appointedById);
            Assert.AreEqual("野党の幹事長", sec.reason);
            Assert.AreEqual(1, lw.opposition.postHistory.Count);

            // 読込後の同じ年：整理・補充とも何も起こさない（任命の二重反映なし）
            Assert.AreEqual(0, CabinetAppointmentRules.Reconcile(a, F, w.tree, Top, w.roster, Year, Prm).Count);
            Assert.AreEqual(0, CabinetAppointmentRules.AutoFill(a, F, w.tree, Top, w.roster, Year, Prm).Count);
            Assert.AreEqual(0, PartyExecutiveRules.Reconcile(a, F, w.roster, Year, Prm).Count);
            Assert.AreEqual(historyCount, a.cabinet.history.Count);
            Assert.IsTrue(Auth(lw, vice, Shikibu, CabinetAction.所管決裁).ok, "復元した委任が効く");
        }

        [Test]
        public void LegacySave_PartyPostsWithoutAppointer_AreKeptWithoutReappointment()
        {
            World w = NewWorld();
            w.pol.cabinet = null;
            w.ruling.posts.Add(new PartyAppointment(PartyPost.幹事長, 2)); // 旧セーブ：任命者・就任年なし
            ElectionCycleRules.NormalizeLoaded(w.pol);

            List<AppointmentHistoryEntry> changes = PartyExecutiveRules.Reconcile(w.pol, F, w.roster, Year, Prm);
            Assert.AreEqual(0, changes.Count, "旧セーブの任命で就任・失職の出来事を起こさない");
            PartyAppointment a = PartyExecutiveRules.AppointmentOf(w.ruling, PartyPost.幹事長);
            Assert.AreEqual(2, a.holderId);
            Assert.AreEqual(1, a.appointedById, "現党首の任命として続行");
            StringAssert.Contains("旧セーブ", a.reason);
            Assert.AreEqual(0, w.ruling.postHistory.Count);

            List<AppointmentHistoryEntry> cab = CabinetAppointmentRules.Reconcile(w.pol, F, w.tree, Top, w.roster, Year, Prm);
            Assert.AreEqual(0, cab.Count);
            Assert.IsNotNull(w.pol.cabinet);
            Assert.AreEqual(1, w.pol.cabinet.premierPersonId);
        }
    }
}
