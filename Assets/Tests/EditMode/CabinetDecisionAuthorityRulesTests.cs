using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 閣僚の決裁権限を共通の権限判定へ接続する（CabinetDecisionAuthorityRules・#2768 #141 #67）を固定する：
    /// 所管大臣は所管案件を裁可・他省の大臣/政務官/党三役は所管大臣へ上申、副大臣は有効な 所管決裁 の委任と期限があるときだけ、
    /// 所管省の解決（一意の所掌・複数は推測しない・明示の省ID）、決裁時点の再判定（死亡・解任・首相交代・職務執行と期限・知事就任・委任の撤回/期限切れ）、
    /// 既存の役職（元首・宰相）の権限を狭めない・文民統制は覆さない、作戦指揮は閣僚の権限に含めない、保存往復後も同じ判定、判定は状態を変えない。
    /// </summary>
    public class CabinetDecisionAuthorityRulesTests
    {
        const int Year = 800;
        const Faction F = Faction.同盟;
        const int Top = 1000, Shikibu = 1001, Minbu = 1002, Okura = 1003, Hyobu = 1004;
        const int Premier = 1, OkuraMinister = 2, OkuraVice = 3, OkuraSecretary = 4, HyobuMinister = 5, ShikibuMinister = 6;
        const int PartySecretary = 14, Backbencher = 15, Soldier = 9;
        static readonly CabinetParams Prm = CabinetParams.Default;

        static Person Pol(int id)
            => new Person(id, "政治家" + id, F, PersonRole.文民) { isPolitician = true, birthYear = 760, operation = 50, intelligence = 50 };

        class World
        {
            public PoliticsState pol;
            public Party ruling, opposition;
            public List<Ministry> tree;
            public List<Person> roster;
            public Person P(int id) => roster.Find(x => x.id == id);
            public CabinetDecisionContext Ctx(int year = Year) => new CabinetDecisionContext(pol, F, tree, Top, roster, year);
        }

        /// <summary>
        /// 与党 民政党（党首=首相1・党員2..6,14,15）と野党 進歩党（党首7・党員8）。太政官の下に 式部省/民部省（どちらも内政）・大蔵省（財政）・兵部省（軍事）。
        /// 大蔵大臣2・大蔵副大臣3・大蔵大臣政務官4・兵部大臣5・式部大臣6、幹事長14（閣僚でない）、15は無役、9は軍人の政治家。
        /// </summary>
        static World NewWorld()
        {
            var w = new World { pol = new PoliticsState() };
            w.ruling = new Party(1, "民政党", F) { leaderId = Premier };
            w.ruling.memberIds.AddRange(new[] { 1, 2, 3, 4, 5, 6, 14, 15 });
            w.opposition = new Party(2, "進歩党", F) { leaderId = 7 };
            w.opposition.memberIds.AddRange(new[] { 7, 8 });
            w.pol.parties.Add(w.ruling);
            w.pol.parties.Add(w.opposition);
            w.pol.government = new GovernmentFormation
            {
                status = CabinetStatus.単独過半, premierPersonId = Premier, partyId = 1, formedYear = Year, sourceElectionId = "e1",
            };
            w.tree = new List<Ministry>
            {
                new Ministry(Top, "太政官", OfficeDomain.内政),
                new Ministry(Shikibu, "式部省", OfficeDomain.内政) { parentId = Top },
                new Ministry(Minbu, "民部省", OfficeDomain.内政) { parentId = Top },
                new Ministry(Okura, "大蔵省", OfficeDomain.財政) { parentId = Top },
                new Ministry(Hyobu, "兵部省", OfficeDomain.軍事) { parentId = Top },
            };
            w.roster = new List<Person>();
            for (int i = 1; i <= 8; i++) w.roster.Add(Pol(i));
            w.roster.Add(new Person(Soldier, "軍人9", F, PersonRole.軍人) { isPolitician = true });
            w.roster.Add(Pol(PartySecretary));
            w.roster.Add(Pol(Backbencher));
            CabinetAppointmentRules.Reconcile(w.pol, F, w.tree, Top, w.roster, Year, Prm);
            Appoint(w, Okura, CabinetPostKind.大臣, OkuraMinister);
            Appoint(w, Okura, CabinetPostKind.副大臣, OkuraVice);
            Appoint(w, Okura, CabinetPostKind.政務官, OkuraSecretary);
            Appoint(w, Hyobu, CabinetPostKind.大臣, HyobuMinister);
            Appoint(w, Shikibu, CabinetPostKind.大臣, ShikibuMinister);
            Assert.IsTrue(PartyExecutiveRules.TryAppoint(w.pol, F, w.ruling, Premier, PartyPost.幹事長, PartySecretary, w.roster, Year, "試験", Prm).ok);
            return w;
        }

        static void Appoint(World w, int ministry, CabinetPostKind kind, int person)
        {
            AppointmentResult r = CabinetAppointmentRules.TryAppoint(w.pol, F, Premier, w.tree, Top, ministry, kind, person, w.roster, Year, "試験", Prm);
            Assert.IsTrue(r.ok, r.reason);
        }

        /// <summary>役職なし・文民統制で、閣僚の権限を合成して判定する。</summary>
        static DecisionAuthorityResult Eval(World w, int actorId, string key, int year = Year, List<Office> offices = null,
            System.Func<OfficeDomain, ICharacter> findAddressee = null)
            => CabinetDecisionAuthorityRules.Evaluate(w.P(actorId), key, offices ?? new List<Office>(), CivilianControlType.文民統制,
                findAddressee, w.Ctx(year), id => w.P(id));

        static bool Delegate(World w, CabinetDelegation scope, int until)
            => CabinetAppointmentRules.Delegate(w.pol, F, OkuraMinister, Okura, scope, until, w.roster, Year, Prm).ok;

        // ===== 1. 所管省の解決 =====

        [Test]
        public void ResolveMinistry_UniqueDomain_AmbiguousAndMissingAreNotGuessed_ExplicitMustMatch()
        {
            World w = NewWorld();
            Assert.AreEqual(Okura, CabinetDecisionAuthorityRules.ResolveMinistry(w.tree, Top, "tax.hike", -1, out string p));
            Assert.IsNull(p);
            Assert.AreEqual(Hyobu, CabinetDecisionAuthorityRules.ResolveMinistry(w.tree, Top, "fleet.establish:12000", -1, out _));

            Assert.AreEqual(-1, CabinetDecisionAuthorityRules.ResolveMinistry(w.tree, Top, "welfare.up", -1, out p));
            StringAssert.Contains("複数", p);
            StringAssert.Contains("式部省", p);
            StringAssert.Contains("民部省", p);

            Assert.AreEqual(-1, CabinetDecisionAuthorityRules.ResolveMinistry(w.tree, Top, "diplo.ceasefire", -1, out p));
            StringAssert.Contains("所管する省が内閣に無い", p);

            Assert.AreEqual(Minbu, CabinetDecisionAuthorityRules.ResolveMinistry(w.tree, Top, "welfare.up", Minbu, out _), "明示の省IDなら一意");
            Assert.AreEqual(-1, CabinetDecisionAuthorityRules.ResolveMinistry(w.tree, Top, "tax.hike", Hyobu, out p));
            StringAssert.Contains("所管しない", p);
            Assert.AreEqual(-1, CabinetDecisionAuthorityRules.ResolveMinistry(w.tree, Top, "tax.hike", Top, out p), "最上位は大臣を置く省でない");
            Assert.AreEqual(-1, CabinetDecisionAuthorityRules.ResolveMinistry(null, Top, "tax.hike", -1, out p));
            StringAssert.Contains("大臣を置く省が無い", p);
        }

        // ===== 2. 職ごとの権限差 =====

        [Test]
        public void Minister_DecidesOwnMinistryCase()
        {
            World w = NewWorld();
            DecisionAuthorityResult r = Eval(w, OkuraMinister, "tax.hike");
            Assert.IsTrue(r.CanDecide, r.basis);
            StringAssert.Contains("大蔵大臣", r.basis);
            Assert.IsTrue(Eval(w, HyobuMinister, "fleet.establish:12000").CanDecide, "兵部大臣は軍事政策（艦隊の設立）を決裁できる");
        }

        [Test]
        public void OtherMinistryMinister_SecretaryAndPartyExecutive_PetitionToProperMinister()
        {
            World w = NewWorld();
            foreach (int actor in new[] { HyobuMinister, OkuraSecretary, PartySecretary, Backbencher })
            {
                DecisionAuthorityResult r = Eval(w, actor, "tax.hike");
                Assert.AreEqual(DecisionAuthority.上申, r.authority, "人物#" + actor + "：" + r.basis);
                Assert.AreEqual(OkuraMinister, r.addresseeId, "上申先は大蔵大臣（人物#" + actor + "）");
                Assert.AreEqual("政治家2", r.addresseeName);
            }
            StringAssert.Contains("最終決裁権を持たない", Eval(w, OkuraSecretary, "tax.hike").basis);
            StringAssert.Contains("閣僚職に就いていない", Eval(w, HyobuMinister, "tax.hike").basis);
        }

        [Test]
        public void Vice_OnlyWithValidDecisionDelegationAndTerm()
        {
            World w = NewWorld();
            DecisionAuthorityResult r = Eval(w, OkuraVice, "tax.hike");
            Assert.AreEqual(DecisionAuthority.上申, r.authority, "委任なしでは決裁できない");
            Assert.AreEqual(OkuraMinister, r.addresseeId);
            StringAssert.Contains("委任なし", r.basis);

            Assert.IsTrue(Delegate(w, CabinetDelegation.所管政策, Year + 1));
            r = Eval(w, OkuraVice, "tax.hike");
            Assert.IsFalse(r.CanDecide, "所管政策だけの委任では決裁できない");
            StringAssert.Contains("所管決裁 は委任されていない", r.basis);

            Assert.IsTrue(CabinetAppointmentRules.RevokeDelegation(w.pol, F, OkuraMinister, Okura, Year, "", Prm).ok);
            Assert.IsTrue(Delegate(w, CabinetDelegation.所管決裁, Year + 1));
            r = Eval(w, OkuraVice, "tax.hike");
            Assert.IsTrue(r.CanDecide, r.basis);
            StringAssert.Contains("委任", r.basis);
            Assert.IsTrue(Eval(w, OkuraVice, "tax.hike", Year + 1).CanDecide, "期限の年までは有効");

            r = Eval(w, OkuraVice, "tax.hike", Year + 2);
            Assert.IsFalse(r.CanDecide, "期限切れ（整理前でも決裁時点で失効）");
            StringAssert.Contains("期限", r.basis);
            Assert.IsFalse(Eval(w, OkuraVice, "fleet.establish:100").CanDecide, "委任は所管の省だけ（他省の案件は不可）");

            Assert.IsTrue(CabinetAppointmentRules.RevokeDelegation(w.pol, F, OkuraMinister, Okura, Year, "撤回", Prm).ok);
            r = Eval(w, OkuraVice, "tax.hike");
            Assert.IsFalse(r.CanDecide, "撤回後は決裁できない");
            Assert.AreEqual(OkuraMinister, r.addresseeId);
        }

        // ===== 3. 決裁時点の再判定 =====

        [Test]
        public void DeadOrDismissedMinister_LosesAuthority_AndDelegationDies()
        {
            World w = NewWorld();
            Assert.IsTrue(Delegate(w, CabinetDelegation.所管決裁, Year + 1));
            w.P(OkuraMinister).deathYear = Year - 1; // 整理（Reconcile）を待たない

            DecisionAuthorityResult r = Eval(w, OkuraMinister, "tax.hike");
            Assert.IsFalse(r.CanDecide, "死亡した大臣は決裁できない");
            r = Eval(w, OkuraVice, "tax.hike");
            Assert.IsFalse(r.CanDecide, "委任した大臣の死亡で委任は効かない");
            Assert.AreNotEqual(OkuraMinister, r.addresseeId, "死亡した大臣へ上申しない");
            Assert.AreEqual(-1, CabinetDecisionAuthorityRules.ValidMinisterOf(w.Ctx(), Okura));

            World d = NewWorld();
            Assert.IsTrue(CabinetAppointmentRules.Dismiss(d.pol, F, Premier, Okura, CabinetPostKind.大臣, d.roster, Year, "", Prm).ok);
            r = Eval(d, OkuraMinister, "tax.hike");
            Assert.IsFalse(r.CanDecide, "解任された元大臣は決裁できない");
            Assert.AreEqual(DecisionAuthority.権限外, r.authority, "所管大臣が空席で既存の上申先も無い");
            Assert.AreEqual(DecisionAuthority.上申,
                Eval(d, OkuraMinister, "tax.hike", Year, null, _ => d.P(Premier)).authority, "所管大臣が居なければ既存の上申先へ");
        }

        [Test]
        public void PremierChangeOrGovernorOrLapse_BeforeReconcile_RevokesAuthority()
        {
            World w = NewWorld();
            w.pol.government.premierPersonId = Backbencher; // 首相交代（内閣の整理前）
            DecisionAuthorityResult r = Eval(w, OkuraMinister, "tax.hike");
            Assert.IsFalse(r.CanDecide);
            StringAssert.Contains("首相交代", r.basis);

            w = NewWorld();
            w.pol.government.sourceElectionId = "e2"; // 改選後の首班指名
            Assert.IsFalse(Eval(w, OkuraMinister, "tax.hike").CanDecide);

            w = NewWorld();
            w.pol.locals.Add(new LocalElectionState(50) { governorPersonId = OkuraMinister, status = LocalElectionStatus.当選 });
            r = Eval(w, OkuraMinister, "tax.hike");
            Assert.IsFalse(r.CanDecide);
            StringAssert.Contains("知事", r.basis);

            w = NewWorld();
            w.ruling.memberIds.Remove(OkuraMinister);
            w.opposition.memberIds.Add(OkuraMinister);
            Assert.IsFalse(Eval(w, OkuraMinister, "tax.hike").CanDecide, "野党へ移った大臣は決裁できない");

            w = NewWorld();
            w.P(OkuraMinister).faction = Faction.帝国;
            Assert.IsFalse(Eval(w, OkuraMinister, "tax.hike").CanDecide, "他勢力の人物は決裁できない");
        }

        [Test]
        public void Caretaker_MinisterDecidesUntilDeadline_ThenNot()
        {
            World w = NewWorld();
            Assert.IsTrue(Delegate(w, CabinetDelegation.所管決裁, Year + 1));
            w.pol.government.premierPersonId = -1;
            w.pol.government.status = CabinetStatus.組閣未成立;
            CabinetAppointmentRules.Reconcile(w.pol, F, w.tree, Top, w.roster, Year, Prm);
            Assert.IsTrue(w.pol.cabinet.caretaker);

            Assert.IsTrue(Eval(w, OkuraMinister, "tax.hike").CanDecide, "職務執行内閣の大臣は所管の決裁を続ける");
            Assert.IsFalse(Eval(w, OkuraVice, "tax.hike").CanDecide, "職務執行内閣では委任が効かない");
            DecisionAuthorityResult r = Eval(w, OkuraMinister, "tax.hike", Year + 2);
            Assert.IsFalse(r.CanDecide);
            StringAssert.Contains("職務執行の期限", r.basis);

            w.pol.government.premierPersonId = Backbencher;
            w.pol.government.status = CabinetStatus.単独過半;
            r = Eval(w, OkuraMinister, "tax.hike");
            Assert.IsFalse(r.CanDecide, "新しい首相が決まったら職務執行内閣は退く");
            StringAssert.Contains("組閣待ち", r.basis);
        }

        // ===== 4. 既存権限・文民統制・作戦指揮 =====

        [Test]
        public void ExistingOfficeAuthority_IsNotNarrowed_CabinetNullIsIdentical()
        {
            World w = NewWorld();
            var head = new List<Office> { new Office(1, "元首", OfficeScope.国家, OfficeDomain.元首) };
            Assert.IsTrue(Eval(w, Backbencher, "tax.hike", Year, head).CanDecide, "元首の権限は閣僚の有無で狭まらない");
            Assert.IsTrue(Eval(w, Backbencher, "mil.offensive", Year, head).CanDecide);

            var premierOffice = new List<Office> { new Office(2, "宰相", OfficeScope.国家, OfficeDomain.内政) { civilianOnly = true } };
            Assert.IsTrue(Eval(w, Premier, "welfare.up", Year, premierOffice).CanDecide, "首相（宰相職）の内政の権限は維持");
            DecisionAuthorityResult r = Eval(w, Premier, "tax.hike", Year, premierOffice);
            Assert.AreEqual(DecisionAuthority.上申, r.authority, "首相も所管外の財政は所管大臣へ上申（元から役職で決裁できない）");
            Assert.AreEqual(OkuraMinister, r.addresseeId);

            // 内閣なし＝既存判定と同じ
            DecisionAuthorityResult a = CabinetDecisionAuthorityRules.Evaluate(w.P(OkuraMinister), "tax.hike", new List<Office>(),
                CivilianControlType.文民統制, null, null, id => w.P(id));
            DecisionAuthorityResult b = DecisionAuthorityRules.Evaluate(w.P(OkuraMinister), "tax.hike", OfficeScope.国家, new List<Office>(),
                CivilianControlType.文民統制);
            Assert.AreEqual(b.authority, a.authority);
            Assert.AreEqual(b.basis, a.basis);
        }

        [Test]
        public void CivilianControl_SoldierIsRoutedToCivilianMinister()
        {
            World w = NewWorld();
            DecisionAuthorityResult r = Eval(w, Soldier, "tax.hike");
            Assert.AreEqual(DecisionAuthority.上申, r.authority);
            Assert.AreEqual(OkuraMinister, r.addresseeId);
            StringAssert.Contains("文民統制", r.basis);
        }

        [Test]
        public void OperationalCommand_NotGrantedToCabinet()
        {
            World w = NewWorld();
            Assert.IsTrue(CabinetDecisionAuthorityRules.IsOperationalCommand("mil.offensive"));
            Assert.IsFalse(CabinetDecisionAuthorityRules.IsOperationalCommand("mil.mobilize"));
            Assert.IsFalse(CabinetDecisionAuthorityRules.IsOperationalCommand("fleet.establish:12000"));
            Assert.AreEqual(CabinetAction.艦隊作戦指揮, CabinetDecisionAuthorityRules.ActionOf("mil.offensive"));
            Assert.AreEqual(CabinetAction.所管決裁, CabinetDecisionAuthorityRules.ActionOf("tax.hike"));

            DecisionAuthorityResult r = Eval(w, HyobuMinister, "mil.offensive");
            Assert.IsFalse(r.CanDecide, "兵部大臣でも艦隊の作戦指揮は決裁できない");
            StringAssert.Contains("作戦指揮", r.basis);
            Assert.AreNotEqual(HyobuMinister, Eval(w, Backbencher, "mil.offensive").addresseeId, "作戦指揮を大臣へ上申しない");
            Assert.IsFalse(CabinetDecisionAuthorityRules.CabinetAuthority(w.Ctx(), HyobuMinister, "mil.offensive").ok);
        }

        [Test]
        public void AmbiguousDomain_ExplainsReason_KeepsExistingAddressee_ExplicitMinistryWorks()
        {
            World w = NewWorld();
            DecisionAuthorityResult r = Eval(w, ShikibuMinister, "welfare.up");
            Assert.IsFalse(r.CanDecide, "内政の省が複数＝式部大臣に全内政の権限を広げない");
            Assert.AreEqual(DecisionAuthority.権限外, r.authority);
            StringAssert.Contains("複数", r.basis);

            r = Eval(w, ShikibuMinister, "welfare.up", Year, null, _ => w.P(Premier));
            Assert.AreEqual(DecisionAuthority.上申, r.authority);
            Assert.AreEqual(Premier, r.addresseeId, "既存の上申先を維持");

            Assert.IsTrue(CabinetDecisionAuthorityRules.CabinetAuthority(w.Ctx(), ShikibuMinister, "welfare.up", Shikibu).ok, "省を明示すれば所管大臣");
            Assert.IsFalse(CabinetDecisionAuthorityRules.CabinetAuthority(w.Ctx(), ShikibuMinister, "welfare.up", Minbu).ok, "他省（民部省）は不可");
        }

        // ===== 5. 状態を変えない・保存往復 =====

        [Test]
        public void Evaluate_DoesNotChangeState()
        {
            World w = NewWorld();
            Assert.IsTrue(Delegate(w, CabinetDelegation.所管決裁, Year + 1));
            int history = w.pol.cabinet.history.Count;
            int reg = GovernmentRegistry.Appointments.Count;
            w.P(OkuraMinister).deathYear = Year - 1;
            Eval(w, OkuraMinister, "tax.hike");
            Eval(w, OkuraVice, "tax.hike", Year + 5);
            Eval(w, Soldier, "tax.hike");
            Assert.AreEqual(history, w.pol.cabinet.history.Count, "判定で履歴を足さない");
            Assert.AreEqual(OkuraMinister, CabinetAppointmentRules.FindPost(w.pol.cabinet, Okura, CabinetPostKind.大臣).holderId, "判定で失職させない");
            Assert.AreEqual(CabinetDelegation.所管決裁, CabinetAppointmentRules.FindPost(w.pol.cabinet, Okura, CabinetPostKind.副大臣).delegation);
            Assert.AreEqual(reg, GovernmentRegistry.Appointments.Count, "閣僚職を GovernmentRegistry へ登録しない");
        }

        [Test]
        public void SaveRoundTrip_SameJudgement_AndRecheckedAtDecisionYear()
        {
            World w = NewWorld();
            Assert.IsTrue(Delegate(w, CabinetDelegation.所管決裁, Year + 1));

            var map = new GalaxyMap();
            map.AddSystem(new StarSystem(0, "試験星", Vector2.zero, F));
            var campaign = new CampaignState(map);
            campaign.states.Add(new FactionState(F) { governmentForm = GovernmentForm.共和制, politics = w.pol });
            CampaignState loaded = CampaignSerializer.FromJson(CampaignSerializer.ToJson(campaign));
            CabinetAppointmentRules.NormalizeLoaded(loaded.states[0].politics);
            var lw = new World { pol = loaded.states[0].politics, tree = w.tree, roster = w.roster };
            lw.ruling = lw.pol.parties[0];
            lw.opposition = lw.pol.parties[1];

            Assert.IsTrue(Eval(lw, OkuraMinister, "tax.hike").CanDecide, "復元した大臣");
            Assert.IsTrue(Eval(lw, OkuraVice, "tax.hike").CanDecide, "復元した委任");
            DecisionAuthorityResult sec = Eval(lw, OkuraSecretary, "tax.hike");
            Assert.AreEqual(DecisionAuthority.上申, sec.authority);
            Assert.AreEqual(OkuraMinister, sec.addresseeId);
            Assert.IsFalse(Eval(lw, OkuraVice, "tax.hike", Year + 2).CanDecide, "復元後も決裁時点の年で期限を見る");

            lw.P(OkuraMinister).deathYear = Year; // 復元後の死亡
            Assert.IsFalse(Eval(lw, OkuraMinister, "tax.hike", Year + 1).CanDecide);
        }
    }
}
