using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 省内職位の人事（CivilServicePostRules・#141）の基盤を固定する：配属・異動・昇任・降任・解任の正常系と台帳の中身、
    /// 官位／考課／最低在職年の不足、当該省に在籍しない・他勢力・死亡の拒否、1省1職位の重複と飛び級の拒否、空席定員、
    /// 内閣人事局の承認（事務次官級＝首相／局長級以下＝所管大臣・委任を受けた副大臣だけ・期限切れ／政務官・党三役・本人は不可）、
    /// 省内職位が軍指揮権・政府決裁権を生まないこと、確認が状態を変えず実行と同じ判定であること、保存往復と旧セーブ、履歴上限と打切り件数、
    /// 台帳の配列が欠けていても確認・拒否は何も作らず（成功したときだけ実行直前に用意する）こと、
    /// 既存の配属（Ministry.staffIds）との整合（重複なしの枠で定員を数える・配属を書き込めないなら台帳も確定しない・昇任/降任は配属を動かさない・
    /// 台帳へ未移行の既存配属者は配属で別の省へ移せず異動だけが省を移す／同じ省なら台帳へ初回登録できる）。
    /// </summary>
    public class CivilServicePostRulesTests
    {
        const int Year = 800;
        const Faction F = Faction.同盟;
        const int Top = 1000, Shikibu = 1001, Hyobu = 1004;
        // 政治家：1=首相・2=兵部大臣・3=兵部副大臣・4=兵部政務官・5=式部大臣・6=幹事長（閣外）
        const int Premier = 1, Minister = 2, Vice = 3, Secretary = 4, ShikibuMinister = 5, PartyExec = 6;
        // 官僚：20/21=昇任の要件を満たす・22=官位不足・23=考課不足
        const int Bur20 = 20, Bur21 = 21, LowRank = 22, LowMerit = 23, Foreign = 24, Dead = 25, Soldier = 26, Politician = 27;

        static readonly CivilServicePostParams Prm = CivilServicePostParams.Default;
        static readonly CabinetParams CabPrm = CabinetParams.Default;

        static Person Pol(int id)
            => new Person(id, "政治家" + id, F, PersonRole.文民) { isPolitician = true, birthYear = 760 };

        /// <summary>文官（職業官僚）を作る。考課は平均 <paramref name="meritAvg"/> の記録を4回ぶん持たせる。</summary>
        static Person Bur(int id, CourtRank rank, float meritAvg, Faction f = F)
        {
            var p = new Person(id, "官僚" + id, f, PersonRole.文民) { birthYear = 770, courtRank = rank };
            p.merit = new OfficialMerit(id) { evaluations = 4, cumulativeScore = meritAvg * 4f };
            return p;
        }

        class World
        {
            public PoliticsState pol;
            public Party ruling;
            public List<Ministry> tree;
            public List<Person> roster;
            public CivilServiceState civil;
            public Ministry M(int id) => MinistryRules.Get(tree, id);
        }

        /// <summary>与党のみ（党首=首相1）・太政官の下に式部省/兵部省。兵部省は大臣2/副大臣3/政務官4、式部省は大臣5。台帳は空。</summary>
        static World NewWorld()
        {
            var w = new World { pol = new PoliticsState(), civil = new CivilServiceState() };
            w.ruling = new Party(1, "民政党", F) { leaderId = Premier };
            w.ruling.memberIds.AddRange(new[] { 1, 2, 3, 4, 5, 6 });
            w.pol.parties.Add(w.ruling);
            w.pol.government = new GovernmentFormation
            {
                status = CabinetStatus.単独過半, premierPersonId = Premier, partyId = 1, formedYear = Year, sourceElectionId = "e1",
            };
            w.tree = new List<Ministry>
            {
                new Ministry(Top, "太政官", OfficeDomain.内政) { staffSlots = 2 },
                new Ministry(Shikibu, "式部省", OfficeDomain.内政) { parentId = Top, staffSlots = 6 },
                new Ministry(Hyobu, "兵部省", OfficeDomain.軍事) { parentId = Top, staffSlots = 6 },
            };
            w.roster = new List<Person>();
            for (int i = 1; i <= 6; i++) w.roster.Add(Pol(i));
            w.roster.Add(Bur(Bur20, CourtRank.正七位上, 6f));
            w.roster.Add(Bur(Bur21, CourtRank.正七位上, 6f));
            w.roster.Add(Bur(LowRank, CourtRank.正八位上, 6f));
            w.roster.Add(Bur(LowMerit, CourtRank.正七位上, 4f));
            w.roster.Add(Bur(Foreign, CourtRank.正七位上, 6f, Faction.帝国));
            w.roster.Add(Bur(Dead, CourtRank.正七位上, 6f));
            w.roster.Find(x => x.id == Dead).deathYear = Year - 1;
            w.roster.Add(new Person(Soldier, "軍人" + Soldier, F, PersonRole.軍人) { courtRank = CourtRank.正七位上 });
            w.roster.Add(Pol(Politician));

            CabinetAppointmentRules.Reconcile(w.pol, F, w.tree, Top, w.roster, Year, CabPrm);
            Assert.IsTrue(CabinetAppointmentRules.TryAppoint(w.pol, F, Premier, w.tree, Top, Hyobu, CabinetPostKind.大臣, Minister, w.roster, Year, "", CabPrm).ok);
            Assert.IsTrue(CabinetAppointmentRules.TryAppoint(w.pol, F, Premier, w.tree, Top, Hyobu, CabinetPostKind.副大臣, Vice, w.roster, Year, "", CabPrm).ok);
            Assert.IsTrue(CabinetAppointmentRules.TryAppoint(w.pol, F, Premier, w.tree, Top, Hyobu, CabinetPostKind.政務官, Secretary, w.roster, Year, "", CabPrm).ok);
            Assert.IsTrue(CabinetAppointmentRules.TryAppoint(w.pol, F, Premier, w.tree, Top, Shikibu, CabinetPostKind.大臣, ShikibuMinister, w.roster, Year, "", CabPrm).ok);
            // 6 は閣外の党三役（承認権がないことを確かめるため。就けなくても平党員として同じ結論になる）
            PartyExecutiveRules.TryAppoint(w.pol, F, w.ruling, Premier, PartyPost.幹事長, PartyExec, w.roster, Year, "党三役", CabPrm);
            return w;
        }

        static AppointmentResult Ex(World w, int actor, int ministry, CivilServiceAction a, int person,
            BureaucratGrade g = BureaucratGrade.一般官僚, int year = Year)
            => CivilServicePostRules.Execute(w.pol, F, actor, w.tree, ministry, a, person, g, w.roster, year, "試験", w.civil, Prm);

        static AppointmentResult Ck(World w, int actor, int ministry, CivilServiceAction a, int person,
            BureaucratGrade g = BureaucratGrade.一般官僚, int year = Year)
            => CivilServicePostRules.Check(w.pol, F, actor, w.tree, ministry, a, person, g, w.roster, year, w.civil, Prm);

        static AppointmentResult ExP(World w, int actor, int ministry, CivilServiceAction a, int person,
            BureaucratGrade g, int year, CivilServicePostParams prm)
            => CivilServicePostRules.Execute(w.pol, F, actor, w.tree, ministry, a, person, g, w.roster, year, "試験", w.civil, prm);

        static CivilServiceRecord Serving(World w, int person) => CivilServicePostRules.FindServing(w.civil, person);

        /// <summary>20 を兵部省に配属し、Year+3 で課長級へ昇任させる（以降の試験の土台）。</summary>
        static void AssignAndPromote(World w, int person = Bur20)
        {
            Assert.IsTrue(Ex(w, Minister, Hyobu, CivilServiceAction.配属, person).ok);
            Assert.IsTrue(Ex(w, Minister, Hyobu, CivilServiceAction.昇任, person, BureaucratGrade.課長級, Year + 3).ok);
        }

        // ===== 1. 正常系 =====

        [Test]
        public void Assign_ByMinister_WritesLedger_AndMinistryStaffing()
        {
            World w = NewWorld();
            AppointmentResult check = Ck(w, Minister, Hyobu, CivilServiceAction.配属, Bur20);
            Assert.IsTrue(check.ok, check.reason);
            Assert.AreEqual(0, w.civil.records.Count, "確認では台帳を変えない");
            Assert.IsFalse(w.M(Hyobu).staffIds.Contains(Bur20), "確認では配属もしない");

            AppointmentResult r = Ex(w, Minister, Hyobu, CivilServiceAction.配属, Bur20);
            Assert.IsTrue(r.ok, r.reason);
            Assert.AreEqual(1, w.civil.records.Count);
            CivilServiceRecord rec = w.civil.records[0];
            Assert.AreEqual(Hyobu, rec.ministryId);
            Assert.AreEqual("兵部省", rec.ministryName);
            Assert.AreEqual(Bur20, rec.personId);
            Assert.AreEqual(BureaucratGrade.一般官僚, rec.grade);
            Assert.AreEqual(Year, rec.appointedYear);
            Assert.AreEqual(0, rec.vacatedYear);
            Assert.AreEqual(Minister, rec.appointedById, "承認した所管大臣が任命者");
            Assert.AreEqual(CivilServiceStatus.在任, rec.status);
            Assert.IsTrue(rec.IsServing);
            Assert.IsTrue(w.M(Hyobu).staffIds.Contains(Bur20), "既存の Ministry.staffIds を再利用する");
            Assert.AreEqual(0, w.civil.history.Count);
            Assert.AreEqual("兵部省 職員", CivilServicePostRules.GradeTitle("兵部省", BureaucratGrade.一般官僚));
            Assert.AreEqual("兵部課長", CivilServicePostRules.GradeTitle("兵部省", BureaucratGrade.課長級));
            Assert.AreEqual("兵部事務次官", CivilServicePostRules.GradeTitle("兵部省", BureaucratGrade.事務次官級));
        }

        [Test]
        public void Promote_MovesOneGrade_AndOldRecordGoesToHistory()
        {
            World w = NewWorld();
            AssignAndPromote(w);

            Assert.AreEqual(1, w.civil.records.Count);
            CivilServiceRecord now = Serving(w, Bur20);
            Assert.AreEqual(BureaucratGrade.課長級, now.grade);
            Assert.AreEqual(Year + 3, now.appointedYear);
            Assert.AreEqual(Hyobu, now.ministryId);

            Assert.AreEqual(1, w.civil.history.Count);
            CivilServiceRecord old = w.civil.history[0];
            Assert.AreEqual(BureaucratGrade.一般官僚, old.grade);
            Assert.AreEqual(CivilServiceStatus.昇任, old.status);
            Assert.AreEqual(Year + 3, old.vacatedYear);
            Assert.AreEqual(Year, old.appointedYear);
            Assert.AreEqual(0, w.civil.historyDropped);
            Assert.IsTrue(w.M(Hyobu).staffIds.Contains(Bur20), "昇任では所属省は変わらない");
        }

        [Test]
        public void Demote_MovesOneGradeDown_WithoutRankOrMeritGate()
        {
            World w = NewWorld();
            AssignAndPromote(w);
            // 官位も考課も下げずに降任できる（上げるときだけ資格を問う）
            AppointmentResult r = Ex(w, Minister, Hyobu, CivilServiceAction.降任, Bur20, BureaucratGrade.一般官僚, Year + 4);
            Assert.IsTrue(r.ok, r.reason);
            Assert.AreEqual(BureaucratGrade.一般官僚, Serving(w, Bur20).grade);
            Assert.AreEqual(CivilServiceStatus.降任, w.civil.history[1].status);
        }

        [Test]
        public void Transfer_KeepsGrade_AndMovesStaffing()
        {
            World w = NewWorld();
            AssignAndPromote(w);
            AppointmentResult r = CivilServicePostRules.Execute(w.pol, F, ShikibuMinister, w.tree, Shikibu,
                CivilServiceAction.異動, Bur20, BureaucratGrade.一般官僚, w.roster, Year + 4, "式部省へ", w.civil, Prm);
            Assert.IsTrue(r.ok, r.reason);
            CivilServiceRecord now = Serving(w, Bur20);
            Assert.AreEqual(Shikibu, now.ministryId);
            Assert.AreEqual(BureaucratGrade.課長級, now.grade, "異動は段を変えない（渡した段は無視する）");
            Assert.AreEqual(CivilServiceStatus.異動, w.civil.history[1].status);
            Assert.IsTrue(w.M(Shikibu).staffIds.Contains(Bur20));
            Assert.IsFalse(w.M(Hyobu).staffIds.Contains(Bur20), "単一所属＝元の省からは外れる");
            Assert.AreEqual(1, w.civil.records.Count);
        }

        [Test]
        public void Dismiss_EndsRecord_RemovesStaffing_AndFreesSlot()
        {
            World w = NewWorld();
            AssignAndPromote(w);
            AppointmentResult r = Ex(w, Minister, Hyobu, CivilServiceAction.解任, Bur20, BureaucratGrade.課長級, Year + 5);
            Assert.IsTrue(r.ok, r.reason);
            Assert.AreEqual(0, w.civil.records.Count);
            Assert.IsNull(Serving(w, Bur20));
            Assert.AreEqual(CivilServiceStatus.解任, w.civil.history[1].status);
            Assert.AreEqual(Year + 5, w.civil.history[1].vacatedYear);
            Assert.IsFalse(w.M(Hyobu).staffIds.Contains(Bur20));
            Assert.AreEqual(0, CivilServicePostRules.ServingCount(w.civil, Hyobu, BureaucratGrade.課長級));
            Assert.IsTrue(Ex(w, Minister, Hyobu, CivilServiceAction.配属, Bur20, BureaucratGrade.一般官僚, Year + 6).ok, "解任後は入り直せる");
        }

        // ===== 2. 資格（官位・考課・在職年） =====

        [Test]
        public void Promote_RejectsInsufficientCourtRank()
        {
            World w = NewWorld();
            Assert.IsTrue(Ex(w, Minister, Hyobu, CivilServiceAction.配属, LowRank).ok, "一般官僚は官位を問わない");
            AppointmentResult r = Ex(w, Minister, Hyobu, CivilServiceAction.昇任, LowRank, BureaucratGrade.課長級, Year + 3);
            Assert.IsFalse(r.ok);
            StringAssert.Contains("官位", r.reason);
            StringAssert.Contains("正七位上", r.reason);
            Assert.AreEqual(BureaucratGrade.一般官僚, Serving(w, LowRank).grade, "拒否では何も変えない");
            Assert.AreEqual(CourtRank.正七位上, CivilServicePostRules.RequiredRank(BureaucratGrade.課長級));
            Assert.AreEqual(CourtRank.従五位下, CivilServicePostRules.RequiredRank(BureaucratGrade.局長級));
            Assert.AreEqual(CourtRank.正五位下, CivilServicePostRules.RequiredRank(BureaucratGrade.事務次官級));
            Assert.AreEqual(CourtRank.無位, CivilServicePostRules.RequiredRank(BureaucratGrade.一般官僚));
        }

        [Test]
        public void Promote_RejectsInsufficientMerit_AndUnevaluated()
        {
            World w = NewWorld();
            Assert.IsTrue(Ex(w, Minister, Hyobu, CivilServiceAction.配属, LowMerit).ok);
            AppointmentResult r = Ex(w, Minister, Hyobu, CivilServiceAction.昇任, LowMerit, BureaucratGrade.課長級, Year + 3);
            Assert.IsFalse(r.ok);
            StringAssert.Contains("考第", r.reason);
            Assert.AreEqual(5f, CivilServicePostRules.RequiredMerit(BureaucratGrade.課長級, Prm), 1e-4f);
            Assert.AreEqual(6f, CivilServicePostRules.RequiredMerit(BureaucratGrade.局長級, Prm), 1e-4f);
            Assert.AreEqual(7f, CivilServicePostRules.RequiredMerit(BureaucratGrade.事務次官級, Prm), 1e-4f);
            Assert.AreEqual(0f, CivilServicePostRules.RequiredMerit(BureaucratGrade.一般官僚, Prm), 1e-4f);

            w.roster.Find(x => x.id == LowMerit).merit = null; // 未評定
            AppointmentResult r2 = Ex(w, Minister, Hyobu, CivilServiceAction.昇任, LowMerit, BureaucratGrade.課長級, Year + 3);
            Assert.IsFalse(r2.ok);
            StringAssert.Contains("未評定", r2.reason);
        }

        [Test]
        public void Promote_RejectsInsufficientTenure()
        {
            World w = NewWorld();
            Assert.IsTrue(Ex(w, Minister, Hyobu, CivilServiceAction.配属, Bur20).ok);
            AppointmentResult r = Ex(w, Minister, Hyobu, CivilServiceAction.昇任, Bur20, BureaucratGrade.課長級, Year + 2);
            Assert.IsFalse(r.ok);
            StringAssert.Contains("在職年", r.reason);
            Assert.IsTrue(Ex(w, Minister, Hyobu, CivilServiceAction.昇任, Bur20, BureaucratGrade.課長級, Year + 3).ok, "3年で満たす");
            Assert.AreEqual(0, CivilServicePostRules.RequiredTenureYears(BureaucratGrade.一般官僚, Prm));
            Assert.AreEqual(3, CivilServicePostRules.RequiredTenureYears(BureaucratGrade.課長級, Prm));
            Assert.AreEqual(6, CivilServicePostRules.RequiredTenureYears(BureaucratGrade.局長級, Prm));
            Assert.AreEqual(9, CivilServicePostRules.RequiredTenureYears(BureaucratGrade.事務次官級, Prm));
        }

        // ===== 3. 人物・在籍の拒否 =====

        [Test]
        public void Rejects_ForeignDeceasedCaptiveSoldierPolitician_AndUnknown()
        {
            World w = NewWorld();
            StringAssert.Contains("他勢力", Ex(w, Minister, Hyobu, CivilServiceAction.配属, Foreign).reason);
            StringAssert.Contains("死亡", Ex(w, Minister, Hyobu, CivilServiceAction.配属, Dead).reason);
            StringAssert.Contains("軍人", Ex(w, Minister, Hyobu, CivilServiceAction.配属, Soldier).reason);
            StringAssert.Contains("政治家", Ex(w, Minister, Hyobu, CivilServiceAction.配属, Politician).reason);
            StringAssert.Contains("名簿に存在しない", Ex(w, Minister, Hyobu, CivilServiceAction.配属, 999).reason);
            w.roster.Find(x => x.id == Bur21).captiveStatus = CaptiveStatus.捕虜;
            StringAssert.Contains("拘束", Ex(w, Minister, Hyobu, CivilServiceAction.配属, Bur21).reason);
            w.roster.Find(x => x.id == Bur21).captiveStatus = CaptiveStatus.自由;
            w.roster.Find(x => x.id == Bur21).isFreeAgent = true;
            StringAssert.Contains("在野", Ex(w, Minister, Hyobu, CivilServiceAction.配属, Bur21).reason);
            Assert.AreEqual(0, w.civil.records.Count, "拒否では台帳が動かない");
            StringAssert.Contains("存在しない省", Ex(w, Minister, 7777, CivilServiceAction.配属, Bur20).reason);
        }

        [Test]
        public void Rejects_ActionsOnAnotherMinistry()
        {
            World w = NewWorld();
            AssignAndPromote(w); // 兵部省 課長級
            AppointmentResult r = CivilServicePostRules.Execute(w.pol, F, ShikibuMinister, w.tree, Shikibu,
                CivilServiceAction.昇任, Bur20, BureaucratGrade.局長級, w.roster, Year + 9, "他省からの昇任", w.civil, Prm);
            Assert.IsFalse(r.ok);
            StringAssert.Contains("当該省に在籍していない", r.reason);
            AppointmentResult d = CivilServicePostRules.Execute(w.pol, F, ShikibuMinister, w.tree, Shikibu,
                CivilServiceAction.解任, Bur20, BureaucratGrade.課長級, w.roster, Year + 9, "他省からの解任", w.civil, Prm);
            Assert.IsFalse(d.ok);
            StringAssert.Contains("当該省に在籍していない", d.reason);
            Assert.AreEqual(BureaucratGrade.課長級, Serving(w, Bur20).grade);

            World w2 = NewWorld();
            StringAssert.Contains("在籍していない", Ex(w2, Minister, Hyobu, CivilServiceAction.昇任, Bur20, BureaucratGrade.課長級, Year + 3).reason);
            StringAssert.Contains("在籍していない", Ex(w2, Minister, Hyobu, CivilServiceAction.解任, Bur20).reason);
            StringAssert.Contains("在籍していない", Ex(w2, Minister, Hyobu, CivilServiceAction.異動, Bur20).reason);
        }

        [Test]
        public void Rejects_DuplicatePost_AndTransferToSameMinistry()
        {
            World w = NewWorld();
            Assert.IsTrue(Ex(w, Minister, Hyobu, CivilServiceAction.配属, Bur20).ok);
            AppointmentResult again = Ex(w, Minister, Hyobu, CivilServiceAction.配属, Bur20);
            Assert.IsFalse(again.ok);
            StringAssert.Contains("1省1職位", again.reason);
            AppointmentResult other = CivilServicePostRules.Execute(w.pol, F, ShikibuMinister, w.tree, Shikibu,
                CivilServiceAction.配属, Bur20, BureaucratGrade.一般官僚, w.roster, Year, "掛け持ち", w.civil, Prm);
            Assert.IsFalse(other.ok, "他省への二重配属も拒否");
            AppointmentResult same = Ex(w, Minister, Hyobu, CivilServiceAction.異動, Bur20);
            Assert.IsFalse(same.ok);
            StringAssert.Contains("異動先が同じ", same.reason);
            Assert.AreEqual(1, w.civil.records.Count);
        }

        [Test]
        public void Rejects_GradeSkip_OnAssignAndPromotionAndDemotion()
        {
            World w = NewWorld();
            AppointmentResult entry = Ex(w, Minister, Hyobu, CivilServiceAction.配属, Bur20, BureaucratGrade.課長級);
            Assert.IsFalse(entry.ok);
            StringAssert.Contains("飛び級", entry.reason);
            Assert.AreEqual(0, w.civil.records.Count);

            Assert.IsTrue(Ex(w, Minister, Hyobu, CivilServiceAction.配属, Bur20).ok);
            AppointmentResult skip = Ex(w, Minister, Hyobu, CivilServiceAction.昇任, Bur20, BureaucratGrade.局長級, Year + 9);
            Assert.IsFalse(skip.ok);
            StringAssert.Contains("1つ上の段", skip.reason);
            AppointmentResult stay = Ex(w, Minister, Hyobu, CivilServiceAction.昇任, Bur20, BureaucratGrade.一般官僚, Year + 9);
            Assert.IsFalse(stay.ok, "据置は昇任でない");
            AppointmentResult down = Ex(w, Minister, Hyobu, CivilServiceAction.降任, Bur20, BureaucratGrade.課長級, Year + 9);
            Assert.IsFalse(down.ok, "降任で段を上げられない");
            StringAssert.Contains("1つ下の段", down.reason);
            Assert.AreEqual(BureaucratGrade.一般官僚, Serving(w, Bur20).grade);
        }

        // ===== 4. 空席定員 =====

        [Test]
        public void Rejects_WhenGradeSlotsOrMinistryCapacityFull()
        {
            var tight = new CivilServicePostParams(60, 1, 2, 1, 3, 5f); // 課長級の定員を1にする
            World w = NewWorld();
            Assert.IsTrue(ExP(w, Minister, Hyobu, CivilServiceAction.配属, Bur20, BureaucratGrade.一般官僚, Year, tight).ok);
            Assert.IsTrue(ExP(w, Minister, Hyobu, CivilServiceAction.配属, Bur21, BureaucratGrade.一般官僚, Year, tight).ok);
            Assert.IsTrue(ExP(w, Minister, Hyobu, CivilServiceAction.昇任, Bur20, BureaucratGrade.課長級, Year + 3, tight).ok);
            AppointmentResult full = ExP(w, Minister, Hyobu, CivilServiceAction.昇任, Bur21, BureaucratGrade.課長級, Year + 3, tight);
            Assert.IsFalse(full.ok);
            StringAssert.Contains("空席がない", full.reason);
            StringAssert.Contains("定員 1名", full.reason);
            Assert.AreEqual(BureaucratGrade.一般官僚, Serving(w, Bur21).grade);
            Assert.AreEqual(1, CivilServicePostRules.SlotsFor(w.M(Hyobu), BureaucratGrade.事務次官級, Prm));
            Assert.AreEqual(2, CivilServicePostRules.SlotsFor(w.M(Hyobu), BureaucratGrade.局長級, Prm));
            Assert.AreEqual(6, CivilServicePostRules.SlotsFor(w.M(Hyobu), BureaucratGrade.一般官僚, Prm), "一般官僚は省の配属定員");

            World w2 = NewWorld();
            w2.M(Shikibu).staffSlots = 1;
            Assert.IsTrue(CivilServicePostRules.Execute(w2.pol, F, ShikibuMinister, w2.tree, Shikibu,
                CivilServiceAction.配属, Bur20, BureaucratGrade.一般官僚, w2.roster, Year, "", w2.civil, Prm).ok);
            AppointmentResult over = CivilServicePostRules.Execute(w2.pol, F, ShikibuMinister, w2.tree, Shikibu,
                CivilServiceAction.配属, Bur21, BureaucratGrade.一般官僚, w2.roster, Year, "", w2.civil, Prm);
            Assert.IsFalse(over.ok);
            StringAssert.Contains("配属定員がいっぱい", over.reason);
        }

        // ===== 4b. 既存の配属（Ministry.staffIds）との整合 =====

        [Test]
        public void MinistryCapacity_CountsExistingStaffingAndLedgerWithoutDoubleCounting()
        {
            // ① 台帳へ移していない既存の配属だけで満員＝台帳が空でも定員を超えさせない
            World w = NewWorld();
            w.M(Hyobu).staffSlots = 1;
            MinistryRules.AssignOfficial(w.tree, Hyobu, Bur21);
            Assert.AreEqual(1, CivilServicePostRules.OccupiedStaffCount(w.M(Hyobu), w.civil));
            AppointmentResult full = Ex(w, Minister, Hyobu, CivilServiceAction.配属, Bur20);
            Assert.IsFalse(full.ok);
            StringAssert.Contains("配属定員がいっぱい", full.reason);
            Assert.AreEqual(0, w.civil.records.Count, "拒否では台帳が動かない");
            Assert.IsFalse(w.M(Hyobu).staffIds.Contains(Bur20));
            Assert.IsFalse(Ck(w, Minister, Hyobu, CivilServiceAction.配属, Bur20).ok, "確認も同じ結論");

            // ② 台帳にしかいない在任者（読込直後で staffIds へ未反映）だけで満員
            World w2 = NewWorld();
            w2.M(Hyobu).staffSlots = 1;
            w2.civil.records.Add(new CivilServiceRecord
            {
                ministryId = Hyobu, ministryName = "兵部省", personId = Bur21,
                grade = BureaucratGrade.一般官僚, appointedYear = Year, status = CivilServiceStatus.在任
            });
            Assert.AreEqual(0, w2.M(Hyobu).staffIds.Count, "配属へはまだ写していない");
            Assert.AreEqual(1, CivilServicePostRules.OccupiedStaffCount(w2.M(Hyobu), w2.civil));
            AppointmentResult full2 = Ex(w2, Minister, Hyobu, CivilServiceAction.配属, Bur20);
            Assert.IsFalse(full2.ok);
            StringAssert.Contains("配属定員がいっぱい", full2.reason);
            Assert.AreEqual(1, w2.civil.records.Count);

            // ③ 両方に同じ人物がいても二重に数えない＝残りの空きは使える
            World w3 = NewWorld();
            w3.M(Hyobu).staffSlots = 2;
            Assert.IsTrue(Ex(w3, Minister, Hyobu, CivilServiceAction.配属, Bur21).ok);
            Assert.AreEqual(1, CivilServicePostRules.OccupiedStaffCount(w3.M(Hyobu), w3.civil), "staffIds と台帳の同じ人物で1枠");
            Assert.AreEqual(0, CivilServicePostRules.OccupiedStaffCount(w3.M(Hyobu), w3.civil, Bur21), "本人ぶんは数えない");
            Assert.IsTrue(Ex(w3, Minister, Hyobu, CivilServiceAction.配属, Bur20).ok, "残り1枠へは入れる");
            Assert.AreEqual(0, CivilServicePostRules.OccupiedStaffCount(null, w3.civil), "null 安全");
            Assert.AreEqual(0, CivilServicePostRules.OccupiedStaffCount(w3.M(Top), null));
        }

        [Test]
        public void Transfer_RejectedWhenTargetIsFullWithExistingStaffingOnly()
        {
            World w = NewWorld();
            w.M(Shikibu).staffSlots = 1;
            MinistryRules.AssignOfficial(w.tree, Shikibu, LowRank); // 台帳に無い既存の配属で満員
            Assert.IsTrue(Ex(w, Minister, Hyobu, CivilServiceAction.配属, Bur20).ok);

            AppointmentResult r = CivilServicePostRules.Execute(w.pol, F, ShikibuMinister, w.tree, Shikibu,
                CivilServiceAction.異動, Bur20, BureaucratGrade.一般官僚, w.roster, Year + 1, "式部省へ", w.civil, Prm);
            Assert.IsFalse(r.ok);
            StringAssert.Contains("配属定員がいっぱい", r.reason);
            Assert.AreEqual(Hyobu, Serving(w, Bur20).ministryId, "拒否では台帳も配属も動かない");
            Assert.AreEqual(1, w.civil.records.Count);
            Assert.AreEqual(0, w.civil.history.Count);
            Assert.IsTrue(w.M(Hyobu).staffIds.Contains(Bur20));
            Assert.IsFalse(w.M(Shikibu).staffIds.Contains(Bur20));
            Assert.IsTrue(w.M(Shikibu).staffIds.Contains(LowRank), "既存の配属者を押し出さない");
        }

        [Test]
        public void Assign_IsAtomic_WhenMinistryStaffingCannotAcceptThePerson()
        {
            World w = NewWorld();
            w.civil.records = null;
            w.civil.history = null;
            Ministry m = w.M(Hyobu);
            m.staffSlots = 2;
            m.staffIds.Add(LowRank);
            m.staffIds.Add(LowRank); // 壊れた配属（重複）＝重複なしの枠は1つだが staffIds は書き込めない
            Assert.AreEqual(1, CivilServicePostRules.OccupiedStaffCount(m, w.civil), "重複は1枠として数える");

            AppointmentResult r = Ex(w, Minister, Hyobu, CivilServiceAction.配属, Bur20);
            Assert.IsFalse(r.ok, "配属を書き込めないなら台帳も確定しない");
            StringAssert.Contains("空きがない", r.reason);
            Assert.IsNull(w.civil.records, "台帳と配属のどちらも動かない");
            Assert.IsNull(w.civil.history);
            Assert.AreEqual(2, m.staffIds.Count);
            Assert.IsFalse(m.staffIds.Contains(Bur20));
            Assert.IsFalse(Ck(w, Minister, Hyobu, CivilServiceAction.配属, Bur20).ok, "確認も同じ結論");
        }

        [Test]
        public void Assign_RejectedWhenPersonIsAlreadyStaffedInAnotherMinistry()
        {
            // 台帳へ移していない既存の配属者を「配属」で別の省へ攫わない（省を移すのは台帳上の在任者の異動だけ）
            World w = NewWorld();
            w.civil.records = null;
            w.civil.history = null;
            MinistryRules.AssignOfficial(w.tree, Shikibu, Bur20);
            Assert.AreEqual(Shikibu, CivilServicePostRules.FindStaffedMinistryId(w.tree, Bur20));

            AppointmentResult r = Ex(w, Minister, Hyobu, CivilServiceAction.配属, Bur20);
            Assert.IsFalse(r.ok, "配属では省を移せない");
            StringAssert.Contains("式部省", r.reason);
            StringAssert.Contains("異動", r.reason);
            Assert.IsNull(w.civil.records, "拒否では台帳に触れない（欠けた配列も作らない）");
            Assert.IsNull(w.civil.history);
            Assert.IsTrue(w.M(Shikibu).staffIds.Contains(Bur20), "既存の配属は動かない");
            Assert.IsFalse(w.M(Hyobu).staffIds.Contains(Bur20), "配属先へも入れない");
            Assert.IsFalse(Ck(w, Minister, Hyobu, CivilServiceAction.配属, Bur20).ok, "確認も同じ結論");
        }

        [Test]
        public void Assign_AllowedWhenPersonIsAlreadyStaffedInTheSameMinistry()
        {
            // 同じ省の既存配属者は移動ではない＝その省の台帳へ初めて載せられる
            World w = NewWorld();
            MinistryRules.AssignOfficial(w.tree, Hyobu, Bur20);
            Assert.IsNull(Serving(w, Bur20), "台帳にはまだ載っていない");

            AppointmentResult r = Ex(w, Minister, Hyobu, CivilServiceAction.配属, Bur20);
            Assert.IsTrue(r.ok, r.reason);
            CivilServiceRecord rec = Serving(w, Bur20);
            Assert.AreEqual(Hyobu, rec.ministryId);
            Assert.AreEqual(BureaucratGrade.一般官僚, rec.grade);
            Assert.AreEqual(Year, rec.appointedYear);
            Assert.AreEqual(1, w.M(Hyobu).staffIds.Count, "既存の配属を重複させない");
            Assert.IsTrue(w.M(Hyobu).staffIds.Contains(Bur20));

            // 台帳へ載せた後は、省を移すのは異動＝そちらは従来どおり通る
            AppointmentResult t = CivilServicePostRules.Execute(w.pol, F, ShikibuMinister, w.tree, Shikibu,
                CivilServiceAction.異動, Bur20, BureaucratGrade.一般官僚, w.roster, Year + 1, "式部省へ", w.civil, Prm);
            Assert.IsTrue(t.ok, t.reason);
            Assert.AreEqual(Shikibu, Serving(w, Bur20).ministryId);
            Assert.IsFalse(w.M(Hyobu).staffIds.Contains(Bur20));
            Assert.IsTrue(w.M(Shikibu).staffIds.Contains(Bur20));
        }

        [Test]
        public void PromotionAndDemotion_DoNotMoveExistingStaffing()
        {
            World w = NewWorld();
            Ministry m = w.M(Hyobu);
            m.staffSlots = 1;
            m.staffIds.Add(LowRank); // 台帳に無い既存の配属で満員
            w.civil.records.Add(new CivilServiceRecord
            {
                ministryId = Hyobu, ministryName = "兵部省", personId = Bur20,
                grade = BureaucratGrade.一般官僚, appointedYear = Year, status = CivilServiceStatus.在任
            });

            AppointmentResult up = Ex(w, Minister, Hyobu, CivilServiceAction.昇任, Bur20, BureaucratGrade.課長級, Year + 3);
            Assert.IsTrue(up.ok, up.reason);
            Assert.AreEqual(BureaucratGrade.課長級, Serving(w, Bur20).grade);
            Assert.AreEqual(1, m.staffIds.Count, "同じ省の中＝既存の配属は動かさない");
            Assert.IsTrue(m.staffIds.Contains(LowRank), "他人を押し出さない");
            Assert.IsFalse(m.staffIds.Contains(Bur20), "欠けた配属の復元は SyncStaffing の責務");

            AppointmentResult down = Ex(w, Minister, Hyobu, CivilServiceAction.降任, Bur20, BureaucratGrade.一般官僚, Year + 4);
            Assert.IsTrue(down.ok, down.reason);
            Assert.AreEqual(1, m.staffIds.Count, "降任でも配属は動かない");

            CivilServicePostRules.SyncStaffing(w.tree, w.civil);
            Assert.IsFalse(m.staffIds.Contains(Bur20), "空きが無ければ写らない（定員は守る）");
            m.staffSlots = 2;
            CivilServicePostRules.SyncStaffing(w.tree, w.civil);
            Assert.IsTrue(m.staffIds.Contains(Bur20), "空きができれば読込後の復元で揃う");
        }

        [Test]
        public void FindStaffedMinistryId_ReportsExistingStaffing()
        {
            World w = NewWorld();
            Assert.AreEqual(-1, CivilServicePostRules.FindStaffedMinistryId(w.tree, Bur20));
            Assert.IsTrue(Ex(w, Minister, Hyobu, CivilServiceAction.配属, Bur20).ok);
            Assert.AreEqual(Hyobu, CivilServicePostRules.FindStaffedMinistryId(w.tree, Bur20));
            MinistryRules.AssignOfficial(w.tree, Shikibu, LowRank); // 台帳に無い配属も見える
            Assert.AreEqual(Shikibu, CivilServicePostRules.FindStaffedMinistryId(w.tree, LowRank));
            Assert.AreEqual(-1, CivilServicePostRules.FindStaffedMinistryId(null, Bur20), "null 安全");
            Assert.AreEqual(-1, CivilServicePostRules.FindStaffedMinistryId(w.tree, -1));
        }

        // ===== 5. 内閣人事局の承認 =====

        [Test]
        public void Approval_PremierForViceMinisterGrade_MinisterForBelow()
        {
            World w = NewWorld();
            AppointmentResult byPremier = CivilServicePostRules.ApprovalAuthority(w.pol, F, Premier, Hyobu, BureaucratGrade.事務次官級, w.roster, Year);
            Assert.IsTrue(byPremier.ok, byPremier.reason);
            StringAssert.Contains("内閣人事局", byPremier.reason);

            AppointmentResult premierOnBureau = CivilServicePostRules.ApprovalAuthority(w.pol, F, Premier, Hyobu, BureaucratGrade.局長級, w.roster, Year);
            Assert.IsFalse(premierOnBureau.ok, "局長級以下は所管大臣の承認");
            Assert.AreEqual(Minister, premierOnBureau.petitionToId);

            AppointmentResult ministerOnVice = CivilServicePostRules.ApprovalAuthority(w.pol, F, Minister, Hyobu, BureaucratGrade.事務次官級, w.roster, Year);
            Assert.IsFalse(ministerOnVice.ok, "事務次官級は首相の承認");
            Assert.AreEqual(Premier, ministerOnVice.petitionToId);

            foreach (BureaucratGrade g in new[] { BureaucratGrade.一般官僚, BureaucratGrade.課長級, BureaucratGrade.局長級 })
                Assert.IsTrue(CivilServicePostRules.ApprovalAuthority(w.pol, F, Minister, Hyobu, g, w.roster, Year).ok, g.ToString());
        }

        [Test]
        public void Approval_RejectsSecretaryPartyExecutiveOtherMinisterAndSelf()
        {
            World w = NewWorld();
            AppointmentResult sec = Ex(w, Secretary, Hyobu, CivilServiceAction.配属, Bur20);
            Assert.IsFalse(sec.ok, "政務官は承認できない");
            Assert.AreEqual(Minister, sec.petitionToId);
            Assert.IsFalse(Ex(w, PartyExec, Hyobu, CivilServiceAction.配属, Bur20).ok, "党三役は承認できない");
            Assert.IsFalse(Ex(w, ShikibuMinister, Hyobu, CivilServiceAction.配属, Bur20).ok, "他省の大臣は承認できない");
            AppointmentResult self = Ex(w, Bur20, Hyobu, CivilServiceAction.配属, Bur20);
            Assert.IsFalse(self.ok);
            StringAssert.Contains("本人", self.reason);
            Assert.AreEqual(0, w.civil.records.Count);

            // 所管大臣が空席なら局長級以下は承認できない（権限を新台帳へ写さない）
            Assert.IsTrue(CabinetAppointmentRules.Dismiss(w.pol, F, Premier, Hyobu, CabinetPostKind.大臣, w.roster, Year, "", CabPrm).ok);
            AppointmentResult vacant = Ex(w, Premier, Hyobu, CivilServiceAction.配属, Bur20);
            Assert.IsFalse(vacant.ok);
            StringAssert.Contains("所管大臣", vacant.reason);
        }

        [Test]
        public void Approval_ViceMinisterOnlyWithValidDelegation_AndExpires()
        {
            World w = NewWorld();
            AppointmentResult noDeleg = Ex(w, Vice, Hyobu, CivilServiceAction.配属, Bur20);
            Assert.IsFalse(noDeleg.ok, "委任のない副大臣は承認できない");
            Assert.AreEqual(Minister, noDeleg.petitionToId);

            Assert.IsTrue(CabinetAppointmentRules.Delegate(w.pol, F, Minister, Hyobu, CabinetDelegation.所管決裁, Year + 1, w.roster, Year, CabPrm).ok);
            Assert.IsTrue(Ex(w, Vice, Hyobu, CivilServiceAction.配属, Bur20).ok, "委任があれば承認できる");
            Assert.AreEqual(Vice, Serving(w, Bur20).appointedById);

            AppointmentResult expired = Ex(w, Vice, Hyobu, CivilServiceAction.配属, Bur21, BureaucratGrade.一般官僚, Year + 2);
            Assert.IsFalse(expired.ok, "委任の期限を過ぎたら承認できない");
            Assert.AreEqual(Minister, expired.petitionToId);

            AppointmentResult viceOnTop = CivilServicePostRules.ApprovalAuthority(w.pol, F, Vice, Hyobu, BureaucratGrade.事務次官級, w.roster, Year);
            Assert.IsFalse(viceOnTop.ok, "委任があっても事務次官級は首相の承認");
            Assert.AreEqual(Premier, viceOnTop.petitionToId);
        }

        [Test]
        public void GradeAuthority_GrantsNoFleetCommandNorGovernmentDecision()
        {
            foreach (BureaucratGrade g in new[] { BureaucratGrade.一般官僚, BureaucratGrade.課長級, BureaucratGrade.局長級, BureaucratGrade.事務次官級 })
            {
                Assert.IsFalse(CivilServicePostRules.GradeAuthority(g, CabinetAction.艦隊作戦指揮).ok, g.ToString());
                Assert.IsFalse(CivilServicePostRules.GradeAuthority(g, CabinetAction.国庫支出).ok, g.ToString());
                Assert.IsFalse(CivilServicePostRules.GradeAuthority(g, CabinetAction.閣僚任免).ok, g.ToString());
                Assert.IsFalse(CivilServicePostRules.GradeAuthority(g, CabinetAction.所管決裁).ok, g.ToString());
                Assert.IsFalse(CivilServicePostRules.GradeAuthority(g, CabinetAction.所管政策決定).ok, g.ToString());
                Assert.IsTrue(CivilServicePostRules.GradeAuthority(g, CabinetAction.政策提案).ok, g.ToString());
                Assert.IsTrue(CivilServicePostRules.GradeAuthority(g, CabinetAction.政策調整).ok, g.ToString());
            }
        }

        [Test]
        public void CivilServiceAppointment_DoesNotTouchCabinetOrGovernmentRegistry()
        {
            World w = NewWorld();
            int cabHistory = w.pol.cabinet.history.Count;
            int posts = w.pol.cabinet.posts.Count;
            AssignAndPromote(w);
            Assert.AreEqual(cabHistory, w.pol.cabinet.history.Count, "内閣の履歴は動かない");
            Assert.AreEqual(posts, w.pol.cabinet.posts.Count);
            Assert.AreEqual(Minister, CabinetAppointmentRules.FindPost(w.pol.cabinet, Hyobu, CabinetPostKind.大臣).holderId);
            Assert.IsNull(CabinetAppointmentRules.PostHeldBy(w.pol.cabinet, Bur20), "官僚は内閣の職に載らない");
        }

        // ===== 6. 確認＝実行 =====

        [Test]
        public void Check_MatchesExecution_AndChangesNothing()
        {
            World w = NewWorld();
            // 拒否（権限・飛び級・人物・在籍なし）を先に通し、最後に成功させる＝どの分岐でも確認と実行が一致する
            var cases = new[]
            {
                new object[] { Secretary, Hyobu, CivilServiceAction.配属, Bur20, BureaucratGrade.一般官僚 },
                new object[] { Premier, Hyobu, CivilServiceAction.配属, Bur20, BureaucratGrade.一般官僚 },
                new object[] { Minister, Hyobu, CivilServiceAction.配属, Bur20, BureaucratGrade.課長級 },
                new object[] { Minister, Hyobu, CivilServiceAction.配属, Foreign, BureaucratGrade.一般官僚 },
                new object[] { Minister, Hyobu, CivilServiceAction.昇任, Bur20, BureaucratGrade.課長級 },
                new object[] { Minister, Hyobu, CivilServiceAction.配属, Bur20, BureaucratGrade.一般官僚 },
            };
            foreach (object[] c in cases)
            {
                AppointmentResult ck = Ck(w, (int)c[0], (int)c[1], (CivilServiceAction)c[2], (int)c[3], (BureaucratGrade)c[4]);
                AppointmentResult ex = Ex(w, (int)c[0], (int)c[1], (CivilServiceAction)c[2], (int)c[3], (BureaucratGrade)c[4]);
                Assert.AreEqual(ex.ok, ck.ok, ck.reason + " / " + ex.reason);
                if (!ex.ok)
                {
                    Assert.AreEqual(ex.reason, ck.reason, "拒否の理由も一致する");
                    Assert.AreEqual(ex.petitionToId, ck.petitionToId);
                }
            }
            World w2 = NewWorld();
            Assert.IsTrue(Ck(w2, Minister, Hyobu, CivilServiceAction.配属, Bur20).ok);
            Assert.IsTrue(Ck(w2, Minister, Hyobu, CivilServiceAction.配属, Bur20).ok, "確認は何度呼んでも同じ");
            Assert.AreEqual(0, w2.civil.records.Count);
            Assert.AreEqual(0, w2.civil.history.Count);
            Assert.AreEqual(0, w2.M(Hyobu).staffIds.Count);
        }

        // ===== 7. 履歴上限・保存 =====

        [Test]
        public void History_IsCapped_AndDroppedCountsAreKept()
        {
            var small = new CivilServicePostParams(2, 4, 2, 1, 3, 5f); // 履歴2件まで
            World w = NewWorld();
            for (int i = 0; i < 4; i++)
            {
                Assert.IsTrue(ExP(w, Minister, Hyobu, CivilServiceAction.配属, Bur20, BureaucratGrade.一般官僚, Year + i, small).ok);
                Assert.IsTrue(ExP(w, Minister, Hyobu, CivilServiceAction.解任, Bur20, BureaucratGrade.一般官僚, Year + i, small).ok);
            }
            Assert.AreEqual(2, w.civil.history.Count, "上限で頭打ち");
            Assert.AreEqual(2, w.civil.historyDropped, "あふれた件数は黙って捨てず数える");
            Assert.AreEqual(Year + 2, w.civil.history[0].vacatedYear, "古い方から捨てる");
            Assert.AreEqual(Year + 3, w.civil.history[1].vacatedYear);
            Assert.AreEqual(0, w.civil.records.Count);
        }

        [Test]
        public void SaveRoundTrip_KeepsLedger_AndLegacySaveStaysNull()
        {
            World w = NewWorld();
            AssignAndPromote(w);
            Assert.IsTrue(Ex(w, Minister, Hyobu, CivilServiceAction.配属, Bur21, BureaucratGrade.一般官僚, Year + 4).ok);

            var map = new GalaxyMap();
            map.AddSystem(new StarSystem(0, "試験星", Vector2.zero, F));
            var campaign = new CampaignState(map);
            campaign.states.Add(new FactionState(F) { governmentForm = GovernmentForm.共和制, politics = w.pol, civilService = w.civil });
            CampaignState loaded = CampaignSerializer.FromJson(CampaignSerializer.ToJson(campaign));

            CivilServiceState st = loaded.states[0].civilService;
            Assert.IsNotNull(st);
            Assert.AreEqual(2, st.records.Count);
            CivilServiceRecord chief = CivilServicePostRules.FindServing(st, Bur20);
            Assert.AreEqual(BureaucratGrade.課長級, chief.grade);
            Assert.AreEqual(Hyobu, chief.ministryId);
            Assert.AreEqual("兵部省", chief.ministryName);
            Assert.AreEqual(Year + 3, chief.appointedYear);
            Assert.AreEqual(Minister, chief.appointedById);
            Assert.AreEqual(1, st.history.Count);
            Assert.AreEqual(CivilServiceStatus.昇任, st.history[0].status);

            // 復元した台帳から省庁の配属を組み直せる（保存されない省庁ツリーへ写す）
            var tree = new List<Ministry> { new Ministry(Hyobu, "兵部省", OfficeDomain.軍事) { staffSlots = 6 } };
            CivilServicePostRules.SyncStaffing(tree, st);
            Assert.IsTrue(tree[0].staffIds.Contains(Bur20));
            Assert.IsTrue(tree[0].staffIds.Contains(Bur21));

            // 旧セーブ（旗が立っていない）は読まない＝null のまま
            CampaignSaveData raw = CampaignSerializer.ToSaveData(campaign);
            raw.states[0].hasCivilService = false;
            Assert.IsNull(CampaignSerializer.FromSaveData(raw).states[0].civilService);
        }

        [Test]
        public void NormalizeLoaded_FixesNulls_DropsBrokenAndDuplicateRecords()
        {
            CivilServicePostRules.NormalizeLoaded(null); // null 安全
            var st = new CivilServiceState();
            st.records.Add(new CivilServiceRecord { ministryId = Hyobu, personId = Bur20, grade = BureaucratGrade.課長級, appointedYear = Year, ministryName = null, reason = null });
            st.records.Add(new CivilServiceRecord { ministryId = Hyobu, personId = Bur20, grade = BureaucratGrade.一般官僚, appointedYear = Year });
            st.records.Add(new CivilServiceRecord { ministryId = -1, personId = Bur21, appointedYear = Year });
            st.records.Add(new CivilServiceRecord { ministryId = Hyobu, personId = Bur21, appointedYear = Year, vacatedYear = Year + 1, status = CivilServiceStatus.解任 });
            st.records.Add(null);
            st.history.Add(new CivilServiceRecord { ministryId = Hyobu, personId = Bur20, ministryName = null, reason = null, status = CivilServiceStatus.在任 });
            st.historyDropped = -5;

            CivilServicePostRules.NormalizeLoaded(st);
            Assert.AreEqual(1, st.records.Count, "壊れた記録・退任済み・重複在任を落とす");
            Assert.AreEqual(Bur20, st.records[0].personId);
            Assert.AreEqual(BureaucratGrade.課長級, st.records[0].grade, "同一人物は段の高い方を残す");
            Assert.AreEqual("", st.records[0].ministryName);
            Assert.AreEqual("", st.records[0].reason);
            Assert.AreEqual(0, st.historyDropped);
            Assert.AreEqual(CivilServiceStatus.退職, st.history[0].status, "履歴に在任は残らない");
            Assert.AreEqual("", st.history[0].reason);
        }

        [Test]
        public void Execute_WithoutLedger_IsRejected()
        {
            World w = NewWorld();
            AppointmentResult r = CivilServicePostRules.Execute(w.pol, F, Minister, w.tree, Hyobu,
                CivilServiceAction.配属, Bur20, BureaucratGrade.一般官僚, w.roster, Year, "", null, Prm);
            Assert.IsFalse(r.ok);
            StringAssert.Contains("人事台帳がない", r.reason);
        }

        // ===== 8. 台帳の配列が欠けていても確認・拒否は何も変えない =====

        [Test]
        public void Check_WithMissingLedgerLists_CreatesNothing()
        {
            World w = NewWorld();
            w.civil.records = null;
            w.civil.history = null;

            AppointmentResult ok = Ck(w, Minister, Hyobu, CivilServiceAction.配属, Bur20);
            Assert.IsTrue(ok.ok, ok.reason);
            AppointmentResult ng = Ck(w, Secretary, Hyobu, CivilServiceAction.配属, Bur20);
            Assert.IsFalse(ng.ok, "政務官は承認できない");

            Assert.IsNull(w.civil.records, "確認は欠けた在任記録の配列すら作らない");
            Assert.IsNull(w.civil.history, "確認は欠けた履歴の配列すら作らない");
            Assert.AreEqual(0, w.civil.historyDropped);
            Assert.AreEqual(0, w.M(Hyobu).staffIds.Count, "確認では配属もしない");

            // 参照 API も欠けた台帳で落ちない（同じ台帳を読んで同じ結論になる）
            Assert.IsNull(CivilServicePostRules.FindServing(w.civil, Bur20));
            Assert.AreEqual(0, CivilServicePostRules.ServingCount(w.civil, Hyobu));
            Assert.AreEqual(0, CivilServicePostRules.ServingCount(w.civil, Hyobu, BureaucratGrade.課長級));
        }

        [Test]
        public void RejectedExecute_WithMissingLedgerLists_CreatesNothing()
        {
            // 権限外・飛び級・人物不可・存在しない省・在籍なし・本人承認＝どの拒否でも台帳に触れない
            var cases = new[]
            {
                new object[] { Secretary, Hyobu, CivilServiceAction.配属, Bur20, BureaucratGrade.一般官僚 },
                new object[] { Minister, Hyobu, CivilServiceAction.配属, Bur20, BureaucratGrade.課長級 },
                new object[] { Minister, Hyobu, CivilServiceAction.配属, Foreign, BureaucratGrade.一般官僚 },
                new object[] { Minister, 7777, CivilServiceAction.配属, Bur20, BureaucratGrade.一般官僚 },
                new object[] { Minister, Hyobu, CivilServiceAction.昇任, Bur20, BureaucratGrade.課長級 },
                new object[] { Bur20, Hyobu, CivilServiceAction.配属, Bur20, BureaucratGrade.一般官僚 },
            };
            foreach (object[] c in cases)
            {
                World w = NewWorld();
                w.civil.records = null;
                w.civil.history = null;
                AppointmentResult r = Ex(w, (int)c[0], (int)c[1], (CivilServiceAction)c[2], (int)c[3], (BureaucratGrade)c[4]);
                Assert.IsFalse(r.ok, "拒否のはず：" + r.reason);
                Assert.IsNull(w.civil.records, r.reason);
                Assert.IsNull(w.civil.history, r.reason);
                Assert.AreEqual(0, w.civil.historyDropped, r.reason);
                Assert.AreEqual(0, w.M(Hyobu).staffIds.Count, r.reason);
            }
        }

        [Test]
        public void Execute_WithMissingLedgerLists_InitializesOnlyWhenItSucceeds()
        {
            World w = NewWorld();
            w.civil.records = null;
            w.civil.history = null;

            AppointmentResult r = Ex(w, Minister, Hyobu, CivilServiceAction.配属, Bur20);
            Assert.IsTrue(r.ok, r.reason);
            Assert.AreEqual(1, w.civil.records.Count, "成功したときだけ台帳を用意して従来どおり書く");
            Assert.AreEqual(Bur20, w.civil.records[0].personId);
            Assert.AreEqual(BureaucratGrade.一般官僚, w.civil.records[0].grade);
            Assert.AreEqual(Year, w.civil.records[0].appointedYear);
            Assert.IsNotNull(w.civil.history);
            Assert.AreEqual(0, w.civil.history.Count);
            Assert.IsTrue(w.M(Hyobu).staffIds.Contains(Bur20));

            // 履歴だけ欠けた台帳でも退任記録を積める（欠けを実行直前に補う）
            w.civil.history = null;
            AppointmentResult d = Ex(w, Minister, Hyobu, CivilServiceAction.解任, Bur20, BureaucratGrade.一般官僚, Year + 1);
            Assert.IsTrue(d.ok, d.reason);
            Assert.AreEqual(0, w.civil.records.Count);
            Assert.AreEqual(1, w.civil.history.Count);
            Assert.AreEqual(CivilServiceStatus.解任, w.civil.history[0].status);
            Assert.AreEqual(Year + 1, w.civil.history[0].vacatedYear);
            Assert.AreEqual(0, w.civil.historyDropped);
            Assert.IsFalse(w.M(Hyobu).staffIds.Contains(Bur20));
        }
    }
}
