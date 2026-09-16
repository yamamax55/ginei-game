using System.Collections.Generic;
using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 官僚人事の年次処理（CivilServiceAnnualRules・#141）を固定する：失職整理が無効な在任者だけを退職させること（正常な在任者は
    /// この入口では免じられない）、上位の段から空席を埋めて1人が同じ年に2段昇任しないこと、候補の並びが決定論であること
    /// （昇任＝考課→在職年→官位→ID／配属＝考課→官位→ID）、承認権者の不在・資格不足を迂回せず見送ること、
    /// 一般官僚の空席補充が適格な未配属の文民だけを入れること（既存の Ministry.staffIds に居る人物は候補に入れず別省へ移さない・
    /// 既存の配属だけで満員の省は空と見ない）、省ごとの年の上限、見送りの明細上限と打切り件数、
    /// null 入力でも例外にならず台帳を壊さないこと、内閣に触れないこと。
    /// </summary>
    public class CivilServiceAnnualRulesTests
    {
        const int Year = 800;
        const Faction F = Faction.同盟;
        const int Top = 1000, Shikibu = 1001, Hyobu = 1004;
        const int Premier = 1, Minister = 2, ShikibuMinister = 5;

        static readonly CivilServicePostParams Prm = CivilServicePostParams.Default;
        static readonly CabinetParams CabPrm = CabinetParams.Default;
        /// <summary>昇任だけを見るための調整値（1省3件まで昇任・配属は止める）。</summary>
        static readonly CivilServiceAnnualParams PromoteOnly = new CivilServiceAnnualParams(3, 0, 60);
        /// <summary>整理だけを見るための調整値（昇任も配属も止める）。</summary>
        static readonly CivilServiceAnnualParams RetireOnly = new CivilServiceAnnualParams(0, 0, 60);

        static Person Pol(int id)
            => new Person(id, "政治家" + id, F, PersonRole.文民) { isPolitician = true, birthYear = 760 };

        /// <summary>文官（職業官僚）を作る。考課は平均 <paramref name="meritAvg"/> の記録を4回ぶん持たせる。</summary>
        static Person Bur(int id, CourtRank rank, float meritAvg)
        {
            var p = new Person(id, "官僚" + id, F, PersonRole.文民) { birthYear = 770, courtRank = rank };
            p.merit = new OfficialMerit(id) { evaluations = 4, cumulativeScore = meritAvg * 4f };
            return p;
        }

        class World
        {
            public PoliticsState pol;
            public List<Ministry> tree;
            public List<Person> roster;
            public CivilServiceState civil;
            public Ministry M(int id) => MinistryRules.Get(tree, id);
        }

        /// <summary>与党のみ（党首=首相1）・式部省/兵部省。兵部大臣=2・式部大臣=5。台帳は空。</summary>
        static World NewWorld()
        {
            var w = new World { pol = new PoliticsState(), civil = new CivilServiceState() };
            var ruling = new Party(1, "民政党", F) { leaderId = Premier };
            ruling.memberIds.AddRange(new[] { Premier, Minister, ShikibuMinister });
            w.pol.parties.Add(ruling);
            w.pol.government = new GovernmentFormation
            {
                status = CabinetStatus.単独過半, premierPersonId = Premier, partyId = 1, formedYear = Year, sourceElectionId = "e1",
            };
            w.tree = new List<Ministry>
            {
                new Ministry(Shikibu, "式部省", OfficeDomain.内政) { parentId = Top, staffSlots = 6 },
                new Ministry(Hyobu, "兵部省", OfficeDomain.軍事) { parentId = Top, staffSlots = 6 },
            };
            w.roster = new List<Person> { Pol(Premier), Pol(Minister), Pol(ShikibuMinister) };

            CabinetAppointmentRules.Reconcile(w.pol, F, w.tree, Top, w.roster, Year, CabPrm);
            Assert.IsTrue(CabinetAppointmentRules.TryAppoint(w.pol, F, Premier, w.tree, Top, Hyobu, CabinetPostKind.大臣, Minister, w.roster, Year, "", CabPrm).ok);
            Assert.IsTrue(CabinetAppointmentRules.TryAppoint(w.pol, F, Premier, w.tree, Top, Shikibu, CabinetPostKind.大臣, ShikibuMinister, w.roster, Year, "", CabPrm).ok);
            return w;
        }

        /// <summary>台帳へ直に在任者を置く（試験の土台。人事そのものは Execute／年次処理で動かす）。</summary>
        static void Seat(World w, int ministryId, int personId, BureaucratGrade grade, int appointedYear)
        {
            Ministry m = w.M(ministryId);
            w.civil.records.Add(new CivilServiceRecord
            {
                ministryId = ministryId,
                ministryName = m.ministryName,
                personId = personId,
                grade = grade,
                appointedYear = appointedYear,
                appointedById = Minister,
                reason = "土台",
                status = CivilServiceStatus.在任
            });
            MinistryRules.AssignOfficial(w.tree, ministryId, personId);
        }

        static CivilServiceAnnualReport Tick(World w, CivilServiceAnnualParams ap, int year = Year,
            CivilServicePostParams prm = default)
        {
            CivilServicePostParams use = prm.maxHistory > 0 ? prm : Prm;
            return CivilServiceAnnualRules.TickYear(w.pol, F, w.tree, w.roster, year, w.civil, use, ap);
        }

        static List<CivilServiceAnnualEntry> Of(CivilServiceAnnualReport rep, CivilServiceAnnualKind kind)
        {
            var list = new List<CivilServiceAnnualEntry>();
            for (int i = 0; i < rep.entries.Count; i++)
                if (rep.entries[i].kind == kind) list.Add(rep.entries[i]);
            return list;
        }

        static BureaucratGrade GradeOf(World w, int personId)
        {
            CivilServiceRecord r = CivilServicePostRules.FindServing(w.civil, personId);
            Assert.IsNotNull(r, "人物#" + personId + " は在任していない");
            return r.grade;
        }

        // ===== 1. 失職整理 =====

        [Test]
        public void Retire_RemovesOnlyIneligibleServingOfficials()
        {
            World w = NewWorld();
            w.M(Hyobu).staffSlots = 8;
            const int Healthy = 40, Dead = 41, Gone = 42, Enlisted = 43, Elected = 44, Held = 45, Free = 46, Ghost = 999;
            foreach (int id in new[] { Healthy, Dead, Gone, Enlisted, Elected, Held, Free })
                w.roster.Add(Bur(id, CourtRank.正七位上, 6f));
            w.roster.Find(x => x.id == Dead).deathYear = Year - 1;
            w.roster.Find(x => x.id == Gone).faction = Faction.帝国;
            w.roster.Find(x => x.id == Enlisted).role = PersonRole.軍人;
            w.roster.Find(x => x.id == Elected).isPolitician = true;
            w.roster.Find(x => x.id == Held).captiveStatus = CaptiveStatus.捕虜;
            w.roster.Find(x => x.id == Free).isFreeAgent = true;
            foreach (int id in new[] { Healthy, Dead, Gone, Enlisted, Elected, Held, Free, Ghost })
                Seat(w, Hyobu, id, BureaucratGrade.一般官僚, Year - 2);

            CivilServiceAnnualReport rep = Tick(w, RetireOnly);

            Assert.AreEqual(7, rep.retiredCount, "死亡・他勢力・軍人・政治家・拘束・在野・名簿消失の7名だけ");
            Assert.AreEqual(0, rep.promotedCount);
            Assert.AreEqual(0, rep.assignedCount);
            Assert.AreEqual(7, Of(rep, CivilServiceAnnualKind.退職).Count);
            Assert.AreEqual(1, w.civil.records.Count, "正常な在任者は残る");
            Assert.AreEqual(Healthy, w.civil.records[0].personId);
            Assert.IsTrue(w.M(Hyobu).staffIds.Contains(Healthy));
            foreach (int id in new[] { Dead, Gone, Enlisted, Elected, Held, Free, Ghost })
            {
                Assert.IsNull(CivilServicePostRules.FindServing(w.civil, id), "人物#" + id);
                Assert.IsFalse(w.M(Hyobu).staffIds.Contains(id), "人物#" + id + " は配属からも外れる");
            }
            Assert.AreEqual(7, w.civil.history.Count);
            for (int i = 0; i < w.civil.history.Count; i++)
            {
                Assert.AreEqual(CivilServiceStatus.退職, w.civil.history[i].status);
                Assert.AreEqual(Year, w.civil.history[i].vacatedYear);
            }
            CivilServiceAnnualEntry dead = Of(rep, CivilServiceAnnualKind.退職).Find(e => e.personId == Dead);
            Assert.AreEqual(Hyobu, dead.ministryId);
            Assert.AreEqual("兵部省", dead.ministryName);
            Assert.AreEqual(BureaucratGrade.一般官僚, dead.grade);
            StringAssert.Contains("死亡", dead.reason);
            StringAssert.Contains("名簿に存在しない", Of(rep, CivilServiceAnnualKind.退職).Find(e => e.personId == Ghost).reason);
        }

        [Test]
        public void RetireIfIneligible_KeepsHealthyOfficials_AndIsNullSafe()
        {
            World w = NewWorld();
            const int Bur40 = 40;
            w.roster.Add(Bur(Bur40, CourtRank.正七位上, 6f));
            Seat(w, Hyobu, Bur40, BureaucratGrade.課長級, Year - 4);

            Assert.IsFalse(CivilServicePostRules.RetireIfIneligible(w.tree, F, Bur40, w.roster, Year, "年次整理", w.civil, Prm, out string ok),
                "正常な在任者は退職させられない（解任は承認つきの Execute を通す）");
            Assert.IsNull(ok);
            Assert.AreEqual(1, w.civil.records.Count);
            Assert.AreEqual(0, w.civil.history.Count);
            Assert.IsTrue(w.M(Hyobu).staffIds.Contains(Bur40));

            Assert.IsFalse(CivilServicePostRules.RetireIfIneligible(w.tree, F, 777, w.roster, Year, "", w.civil, Prm, out string none),
                "どこにも就いていない人物は整理するものがない");
            Assert.IsNull(none);
            Assert.IsFalse(CivilServicePostRules.RetireIfIneligible(w.tree, F, Bur40, w.roster, Year, "", null, Prm, out _), "台帳 null で落ちない");
            w.civil.records = null;
            Assert.IsFalse(CivilServicePostRules.RetireIfIneligible(w.tree, F, Bur40, w.roster, Year, "", w.civil, Prm, out _));
            Assert.IsNull(w.civil.records, "欠けた台帳を作らない");

            World w2 = NewWorld();
            w2.roster.Add(Bur(Bur40, CourtRank.正七位上, 6f));
            Seat(w2, Hyobu, Bur40, BureaucratGrade.課長級, Year - 4);
            w2.roster.Find(x => x.id == Bur40).deathYear = Year;
            Assert.IsTrue(CivilServicePostRules.RetireIfIneligible(w2.tree, F, Bur40, w2.roster, Year, "年次整理", w2.civil, Prm, out string why));
            StringAssert.Contains("死亡", why);
            Assert.AreEqual(0, w2.civil.records.Count);
            Assert.AreEqual(1, w2.civil.history.Count);
            Assert.AreEqual(CivilServiceStatus.退職, w2.civil.history[0].status);
            StringAssert.Contains("年次整理", w2.civil.history[0].reason);
            Assert.IsFalse(w2.M(Hyobu).staffIds.Contains(Bur40));
        }

        // ===== 2. 昇任（上位の段から） =====

        [Test]
        public void Promote_FillsFromTopDown_AndNoOneRisesTwiceInAYear()
        {
            World w = NewWorld();
            const int Bureau = 40, Section = 41, Staff = 42;
            w.roster.Add(Bur(Bureau, CourtRank.正五位下, 7.5f));
            w.roster.Add(Bur(Section, CourtRank.従五位下, 6.5f));
            w.roster.Add(Bur(Staff, CourtRank.正七位上, 5.5f));
            Seat(w, Hyobu, Bureau, BureaucratGrade.局長級, Year - 10);
            Seat(w, Hyobu, Section, BureaucratGrade.課長級, Year - 7);
            Seat(w, Hyobu, Staff, BureaucratGrade.一般官僚, Year - 4);

            CivilServiceAnnualReport rep = Tick(w, PromoteOnly);

            List<CivilServiceAnnualEntry> ups = Of(rep, CivilServiceAnnualKind.昇任);
            Assert.AreEqual(3, rep.promotedCount);
            Assert.AreEqual(Bureau, ups[0].personId, "空席は上位の段から埋める");
            Assert.AreEqual(BureaucratGrade.事務次官級, ups[0].grade);
            Assert.AreEqual(Section, ups[1].personId, "上が空いてから直下が繰り上がる");
            Assert.AreEqual(BureaucratGrade.局長級, ups[1].grade);
            Assert.AreEqual(Staff, ups[2].personId);
            Assert.AreEqual(BureaucratGrade.課長級, ups[2].grade);
            Assert.AreEqual(Hyobu, ups[0].ministryId);
            Assert.AreEqual("兵部省", ups[0].ministryName);

            Assert.AreEqual(BureaucratGrade.事務次官級, GradeOf(w, Bureau));
            Assert.AreEqual(BureaucratGrade.局長級, GradeOf(w, Section));
            Assert.AreEqual(BureaucratGrade.課長級, GradeOf(w, Staff));
            Assert.AreEqual(3, w.civil.records.Count, "人物は1省1職位のまま");
            Assert.AreEqual(3, w.civil.history.Count, "1人につき1件の昇任＝同じ年に2段は上がらない");
            for (int i = 0; i < w.civil.history.Count; i++)
                Assert.AreEqual(CivilServiceStatus.昇任, w.civil.history[i].status);
        }

        [Test]
        public void Promote_StopsAtPerMinistryCap()
        {
            World w = NewWorld();
            const int Bureau = 40, Section = 41, Staff = 42;
            w.roster.Add(Bur(Bureau, CourtRank.正五位下, 7.5f));
            w.roster.Add(Bur(Section, CourtRank.従五位下, 6.5f));
            w.roster.Add(Bur(Staff, CourtRank.正七位上, 5.5f));
            Seat(w, Hyobu, Bureau, BureaucratGrade.局長級, Year - 10);
            Seat(w, Hyobu, Section, BureaucratGrade.課長級, Year - 7);
            Seat(w, Hyobu, Staff, BureaucratGrade.一般官僚, Year - 4);

            CivilServiceAnnualReport rep = Tick(w, new CivilServiceAnnualParams(2, 0, 60)); // 1省2件まで
            Assert.AreEqual(2, rep.promotedCount);
            Assert.AreEqual(BureaucratGrade.事務次官級, GradeOf(w, Bureau));
            Assert.AreEqual(BureaucratGrade.局長級, GradeOf(w, Section));
            Assert.AreEqual(BureaucratGrade.一般官僚, GradeOf(w, Staff), "上限を超えて昇任しない");
        }

        [Test]
        public void Promote_OrdersByMeritThenTenureThenRankThenId()
        {
            var wide = new CivilServicePostParams(60, 5, 2, 1, 3, 5f); // 課長級の定員を5にして順序を全部見る
            World w = NewWorld();
            w.roster.Add(Bur(31, CourtRank.正七位上, 7f));
            w.roster.Add(Bur(32, CourtRank.正七位上, 7f));
            w.roster.Add(Bur(30, CourtRank.正六位上, 7f));
            w.roster.Add(Bur(33, CourtRank.正七位上, 7f));
            w.roster.Add(Bur(29, CourtRank.正七位上, 5.5f));
            Seat(w, Hyobu, 31, BureaucratGrade.一般官僚, Year - 5); // 考課7・在職5・正七位上
            Seat(w, Hyobu, 32, BureaucratGrade.一般官僚, Year - 5); // 同上（ID で決まる）
            Seat(w, Hyobu, 30, BureaucratGrade.一般官僚, Year - 3); // 官位は上だが在職が短い
            Seat(w, Hyobu, 33, BureaucratGrade.一般官僚, Year - 3); // 在職は同じで官位が下
            Seat(w, Hyobu, 29, BureaucratGrade.一般官僚, Year - 9); // 在職は最長だが考課が低い＝最後

            CivilServiceAnnualReport rep = Tick(w, new CivilServiceAnnualParams(5, 0, 60), Year, wide);

            List<CivilServiceAnnualEntry> ups = Of(rep, CivilServiceAnnualKind.昇任);
            Assert.AreEqual(5, ups.Count);
            Assert.AreEqual(31, ups[0].personId, "考課が同じなら在職年→官位→ID");
            Assert.AreEqual(32, ups[1].personId, "すべて同じなら人物ID の小さい順");
            Assert.AreEqual(30, ups[2].personId, "在職年は官位より優先（官位が上でも在職が短ければ後）");
            Assert.AreEqual(33, ups[3].personId, "在職年が同じなら官位の高い方が先");
            Assert.AreEqual(29, ups[4].personId, "在職が最長でも考課の低い方は最後");
            for (int i = 0; i < ups.Count; i++) Assert.AreEqual(BureaucratGrade.課長級, ups[i].grade);
        }

        [Test]
        public void Promote_SkipsUnqualifiedCandidate_AndTakesTheNextOne()
        {
            var tight = new CivilServicePostParams(60, 1, 2, 1, 3, 5f); // 課長級は1名
            World w = NewWorld();
            const int Fresh = 40, Seasoned = 41;
            w.roster.Add(Bur(Fresh, CourtRank.正七位上, 8f));    // 考課は上だが在職1年
            w.roster.Add(Bur(Seasoned, CourtRank.正七位上, 6f)); // 在職4年
            Seat(w, Hyobu, Fresh, BureaucratGrade.一般官僚, Year - 1);
            Seat(w, Hyobu, Seasoned, BureaucratGrade.一般官僚, Year - 4);

            CivilServiceAnnualReport rep = Tick(w, PromoteOnly, Year, tight);

            Assert.AreEqual(1, rep.promotedCount);
            Assert.AreEqual(BureaucratGrade.一般官僚, GradeOf(w, Fresh), "資格不足は飛ばす（権限も資格も迂回しない）");
            Assert.AreEqual(BureaucratGrade.課長級, GradeOf(w, Seasoned));
            CivilServiceAnnualEntry skip = Of(rep, CivilServiceAnnualKind.見送り).Find(e => e.personId == Fresh);
            Assert.IsNotNull(skip);
            StringAssert.Contains("在職年", skip.reason);
            Assert.AreEqual(BureaucratGrade.課長級, skip.grade);
        }

        [Test]
        public void Promote_SkipsWhenApproverIsAbsent()
        {
            World w = NewWorld();
            const int Bureau = 40, Section = 41;
            w.roster.Add(Bur(Bureau, CourtRank.正五位下, 7.5f));
            w.roster.Add(Bur(Section, CourtRank.従五位下, 6.5f));
            Seat(w, Hyobu, Bureau, BureaucratGrade.局長級, Year - 10);
            Seat(w, Hyobu, Section, BureaucratGrade.課長級, Year - 7);
            Assert.IsTrue(CabinetAppointmentRules.Dismiss(w.pol, F, Premier, Hyobu, CabinetPostKind.大臣, w.roster, Year, "", CabPrm).ok);
            w.pol.government = null; // 正式首班がいない

            CivilServiceAnnualReport rep = Tick(w, PromoteOnly);

            Assert.AreEqual(0, rep.promotedCount, "承認権者がいなければ埋めない");
            Assert.AreEqual(0, rep.assignedCount);
            Assert.AreEqual(BureaucratGrade.局長級, GradeOf(w, Bureau));
            Assert.AreEqual(BureaucratGrade.課長級, GradeOf(w, Section));
            Assert.AreEqual(0, w.civil.history.Count, "見送りは台帳を動かさない");
            List<CivilServiceAnnualEntry> skips = Of(rep, CivilServiceAnnualKind.見送り);
            Assert.IsNotNull(skips.Find(e => e.grade == BureaucratGrade.事務次官級 && e.reason.Contains("首相")));
            Assert.IsNotNull(skips.Find(e => e.grade == BureaucratGrade.局長級 && e.reason.Contains("所管大臣")));
        }

        // ===== 3. 一般官僚の空席補充 =====

        [Test]
        public void Assign_FillsVacantSlots_WithEligibleCiviliansInOrder()
        {
            World w = NewWorld();
            w.M(Hyobu).staffSlots = 3;
            w.M(Shikibu).staffSlots = 0; // 兵部省だけを見る
            const int Best = 50, MidHigh = 51, MidLow = 52, Soldier = 53, Foreigner = 54, FreeAgent = 55;
            w.roster.Add(Bur(Best, CourtRank.正七位下, 8f));
            w.roster.Add(Bur(MidLow, CourtRank.正七位下, 6f));   // ID は小さいが官位が下
            w.roster.Add(Bur(MidHigh, CourtRank.正七位上, 6f));
            w.roster.Add(new Person(Soldier, "軍人", F, PersonRole.軍人));
            var foreigner = Bur(Foreigner, CourtRank.正五位下, 9f); foreigner.faction = Faction.帝国; w.roster.Add(foreigner);
            var free = Bur(FreeAgent, CourtRank.正五位下, 9f); free.isFreeAgent = true; w.roster.Add(free);

            CivilServiceAnnualReport rep = Tick(w, new CivilServiceAnnualParams(0, 3, 60));

            List<CivilServiceAnnualEntry> ins = Of(rep, CivilServiceAnnualKind.配属);
            Assert.AreEqual(3, rep.assignedCount);
            Assert.AreEqual(Best, ins[0].personId, "考課の高い順");
            Assert.AreEqual(MidHigh, ins[1].personId, "考課が同じなら官位の高い順");
            Assert.AreEqual(MidLow, ins[2].personId);
            for (int i = 0; i < ins.Count; i++)
            {
                Assert.AreEqual(BureaucratGrade.一般官僚, ins[i].grade, "入省は一般官僚から（飛び級しない）");
                Assert.AreEqual(Hyobu, ins[i].ministryId);
                Assert.IsTrue(w.M(Hyobu).staffIds.Contains(ins[i].personId));
            }
            Assert.IsNull(CivilServicePostRules.FindServing(w.civil, Soldier), "軍人は入省しない");
            Assert.IsNull(CivilServicePostRules.FindServing(w.civil, Foreigner), "他勢力は入省しない");
            Assert.IsNull(CivilServicePostRules.FindServing(w.civil, FreeAgent), "在野は入省しない");
            Assert.IsNull(CivilServicePostRules.FindServing(w.civil, Premier), "政治家は入省しない");
            Assert.AreEqual(3, w.civil.records.Count);
            Assert.AreEqual(3, CivilServicePostRules.ServingCount(w.civil, Hyobu));
        }

        [Test]
        public void Assign_RespectsCapApproverAndCapacity()
        {
            World w = NewWorld();
            w.M(Hyobu).staffSlots = 3;
            w.M(Shikibu).staffSlots = 3;
            for (int id = 50; id < 56; id++) w.roster.Add(Bur(id, CourtRank.正七位上, 6f));
            Assert.IsTrue(CabinetAppointmentRules.Dismiss(w.pol, F, Premier, Shikibu, CabinetPostKind.大臣, w.roster, Year, "", CabPrm).ok);

            CivilServiceAnnualReport rep = Tick(w, new CivilServiceAnnualParams(0, 1, 60)); // 1省1名まで

            Assert.AreEqual(1, rep.assignedCount, "省ごとの年の上限を守る");
            Assert.AreEqual(50, Of(rep, CivilServiceAnnualKind.配属)[0].personId);
            Assert.AreEqual(0, CivilServicePostRules.ServingCount(w.civil, Shikibu), "所管大臣が空席の省は埋めない");
            CivilServiceAnnualEntry skip = Of(rep, CivilServiceAnnualKind.見送り).Find(e => e.ministryId == Shikibu);
            Assert.IsNotNull(skip);
            StringAssert.Contains("所管大臣", skip.reason);

            // 配属定員が埋まっている省は空席がないので候補を探しにいかない
            World w2 = NewWorld();
            w2.M(Hyobu).staffSlots = 1;
            w2.M(Shikibu).staffSlots = 0;
            w2.roster.Add(Bur(50, CourtRank.正七位上, 6f));
            w2.roster.Add(Bur(51, CourtRank.正七位上, 6f));
            Seat(w2, Hyobu, 50, BureaucratGrade.一般官僚, Year - 1);
            CivilServiceAnnualReport rep2 = Tick(w2, new CivilServiceAnnualParams(0, 3, 60));
            Assert.AreEqual(0, rep2.assignedCount);
            Assert.AreEqual(1, w2.civil.records.Count);
        }

        [Test]
        public void Assign_DoesNotTouchAlreadyServingOfficials()
        {
            World w = NewWorld();
            w.M(Hyobu).staffSlots = 4;
            w.M(Shikibu).staffSlots = 4;
            const int Serving = 50, Idle = 51;
            w.roster.Add(Bur(Serving, CourtRank.正七位上, 9f));
            w.roster.Add(Bur(Idle, CourtRank.正七位上, 5f));
            Seat(w, Hyobu, Serving, BureaucratGrade.課長級, Year - 1);

            CivilServiceAnnualReport rep = Tick(w, new CivilServiceAnnualParams(0, 3, 60));

            Assert.AreEqual(1, rep.assignedCount, "在任者は入省の候補に入らない（1省1職位）");
            Assert.AreEqual(Idle, Of(rep, CivilServiceAnnualKind.配属)[0].personId);
            Assert.AreEqual(BureaucratGrade.課長級, GradeOf(w, Serving), "既に就いている職は動かない");
            Assert.AreEqual(Shikibu, CivilServicePostRules.FindServing(w.civil, Idle).ministryId, "省ID の小さい方から埋める");
        }

        [Test]
        public void Assign_ExcludesPeopleAlreadyInMinistryStaffing()
        {
            World w = NewWorld();
            w.M(Hyobu).staffSlots = 3;
            w.M(Shikibu).staffSlots = 3;
            const int Legacy = 50, Idle = 51;
            w.roster.Add(Bur(Legacy, CourtRank.正七位上, 9f)); // 考課は最高だが既に式部省へ配属済み（台帳には無い）
            w.roster.Add(Bur(Idle, CourtRank.正七位上, 5f));
            MinistryRules.AssignOfficial(w.tree, Shikibu, Legacy);
            Assert.AreEqual(Shikibu, CivilServicePostRules.FindStaffedMinistryId(w.tree, Legacy));
            Assert.AreEqual(-1, CivilServicePostRules.FindStaffedMinistryId(w.tree, Idle));

            CivilServiceAnnualReport rep = Tick(w, new CivilServiceAnnualParams(0, 3, 60));

            Assert.AreEqual(1, rep.assignedCount, "既存の配属者は入省の候補に入らない");
            Assert.AreEqual(Idle, Of(rep, CivilServiceAnnualKind.配属)[0].personId);
            Assert.IsNull(CivilServicePostRules.FindServing(w.civil, Legacy), "台帳にも載せない");
            Assert.IsTrue(w.M(Shikibu).staffIds.Contains(Legacy), "既存の配属者を黙って別省へ移さない");
            Assert.IsFalse(w.M(Hyobu).staffIds.Contains(Legacy));
            Assert.AreEqual(1, w.civil.records.Count);
        }

        [Test]
        public void Assign_SkipsMinistryFilledByExistingStaffingAlone()
        {
            World w = NewWorld();
            w.M(Hyobu).staffSlots = 1;
            w.M(Shikibu).staffSlots = 0;
            const int Legacy = 50, Idle = 51;
            w.roster.Add(Bur(Legacy, CourtRank.正七位上, 6f));
            w.roster.Add(Bur(Idle, CourtRank.正七位上, 6f));
            MinistryRules.AssignOfficial(w.tree, Hyobu, Legacy); // 台帳に無い既存の配属だけで満員
            Assert.AreEqual(1, CivilServicePostRules.OccupiedStaffCount(w.M(Hyobu), w.civil));
            Assert.AreEqual(0, CivilServicePostRules.ServingCount(w.civil, Hyobu), "台帳には在任者がいない");

            CivilServiceAnnualReport rep = Tick(w, new CivilServiceAnnualParams(0, 3, 60));

            Assert.AreEqual(0, rep.assignedCount, "既存の配属だけで満員の省を空と見ない");
            Assert.AreEqual(0, w.civil.records.Count);
            Assert.IsFalse(w.M(Hyobu).staffIds.Contains(Idle));
            Assert.AreEqual(1, w.M(Hyobu).staffIds.Count);
        }

        // ===== 4. 通し（順序・決定論・上限・非干渉） =====

        [Test]
        public void TickYear_RunsRetireThenPromoteThenAssign_Deterministically()
        {
            CivilServiceAnnualReport a = RunFullYear();
            CivilServiceAnnualReport b = RunFullYear();

            Assert.AreEqual(a.entries.Count, b.entries.Count);
            for (int i = 0; i < a.entries.Count; i++)
            {
                Assert.AreEqual(a.entries[i].kind, b.entries[i].kind, "同じ入力なら同じ順序（決定論）");
                Assert.AreEqual(a.entries[i].personId, b.entries[i].personId);
                Assert.AreEqual(a.entries[i].ministryId, b.entries[i].ministryId);
                Assert.AreEqual(a.entries[i].grade, b.entries[i].grade);
            }
            Assert.AreEqual(1, a.retiredCount);
            Assert.AreEqual(1, a.promotedCount);
            Assert.AreEqual(1, a.assignedCount);
            Assert.AreEqual(3, a.TotalChanges);

            int retire = a.entries.FindIndex(e => e.kind == CivilServiceAnnualKind.退職);
            int promote = a.entries.FindIndex(e => e.kind == CivilServiceAnnualKind.昇任);
            int assign = a.entries.FindIndex(e => e.kind == CivilServiceAnnualKind.配属);
            Assert.Less(retire, promote, "失職整理→昇任→配属の順で回る");
            Assert.Less(promote, assign);
        }

        /// <summary>死亡者1名の整理・昇任1件・入省1件がちょうど起きる1年ぶん。</summary>
        static CivilServiceAnnualReport RunFullYear()
        {
            World w = NewWorld();
            w.M(Hyobu).staffSlots = 3;
            w.M(Shikibu).staffSlots = 0;
            const int Dead = 40, Rising = 41, Waiting = 42;
            w.roster.Add(Bur(Dead, CourtRank.正七位上, 6f));
            w.roster.Add(Bur(Rising, CourtRank.正七位上, 6f));
            w.roster.Add(Bur(Waiting, CourtRank.正七位上, 6f));
            w.roster.Find(x => x.id == Dead).deathYear = Year - 1;
            Seat(w, Hyobu, Dead, BureaucratGrade.一般官僚, Year - 5);
            Seat(w, Hyobu, Rising, BureaucratGrade.一般官僚, Year - 5);
            return CivilServiceAnnualRules.TickYear(w.pol, F, w.tree, w.roster, Year, w.civil,
                new CivilServicePostParams(60, 1, 2, 1, 3, 5f), new CivilServiceAnnualParams(1, 1, 60));
        }

        [Test]
        public void Notices_AreCapped_AndDroppedCountsAreKept()
        {
            World w = NewWorld();
            const int Bureau = 40;
            w.roster.Add(Bur(Bureau, CourtRank.正五位下, 7.5f));
            Seat(w, Hyobu, Bureau, BureaucratGrade.局長級, Year - 10);
            Assert.IsTrue(CabinetAppointmentRules.Dismiss(w.pol, F, Premier, Hyobu, CabinetPostKind.大臣, w.roster, Year, "", CabPrm).ok);
            Assert.IsTrue(CabinetAppointmentRules.Dismiss(w.pol, F, Premier, Shikibu, CabinetPostKind.大臣, w.roster, Year, "", CabPrm).ok);
            w.pol.government = null;

            CivilServiceAnnualReport rep = Tick(w, new CivilServiceAnnualParams(3, 3, 1)); // 明細は1件だけ

            Assert.AreEqual(1, Of(rep, CivilServiceAnnualKind.見送り).Count, "明細は上限で頭打ち");
            Assert.Greater(rep.skippedCount, 1);
            Assert.AreEqual(rep.skippedCount - 1, rep.noticesDropped, "あふれた件数は黙って捨てず数える");
            Assert.AreEqual(0, rep.TotalChanges);
        }

        [Test]
        public void TickYear_IsNullSafe_AndChangesNothingWithoutInputs()
        {
            World w = NewWorld();
            w.roster.Add(Bur(40, CourtRank.正七位上, 6f));
            Seat(w, Hyobu, 40, BureaucratGrade.一般官僚, Year - 5);

            CivilServiceAnnualReport noLedger = CivilServiceAnnualRules.TickYear(w.pol, F, w.tree, w.roster, Year, null, Prm, PromoteOnly);
            StringAssert.Contains("人事台帳がない", noLedger.entries[0].reason);
            Assert.AreEqual(0, noLedger.TotalChanges);

            CivilServiceAnnualReport noTree = CivilServiceAnnualRules.TickYear(w.pol, F, null, w.roster, Year, w.civil, Prm, PromoteOnly);
            StringAssert.Contains("省庁がない", noTree.entries[0].reason);

            CivilServiceAnnualReport noRoster = CivilServiceAnnualRules.TickYear(w.pol, F, w.tree, null, Year, w.civil, Prm, PromoteOnly);
            StringAssert.Contains("名簿がない", noRoster.entries[0].reason);
            Assert.AreEqual(1, noRoster.skippedCount);

            Assert.AreEqual(1, w.civil.records.Count, "どの入口でも台帳は動かない");
            Assert.AreEqual(0, w.civil.history.Count);
            Assert.AreEqual(BureaucratGrade.一般官僚, GradeOf(w, 40));

            // 政体（内閣）が無ければ承認権者がいない＝整理だけ進み、昇任も配属も起きない
            World w2 = NewWorld();
            w2.roster.Add(Bur(41, CourtRank.正七位上, 6f));
            Seat(w2, Hyobu, 41, BureaucratGrade.一般官僚, Year - 5);
            w2.roster.Find(x => x.id == 41).deathYear = Year - 1;
            CivilServiceAnnualReport noPol = CivilServiceAnnualRules.TickYear(null, F, w2.tree, w2.roster, Year, w2.civil, Prm, PromoteOnly);
            Assert.AreEqual(1, noPol.retiredCount, "失職整理は承認を要さない");
            Assert.AreEqual(0, noPol.promotedCount);
            Assert.AreEqual(0, noPol.assignedCount);
        }

        [Test]
        public void TickYear_DoesNotTouchCabinetOrGovernmentRegistry()
        {
            World w = NewWorld();
            const int Bureau = 40;
            w.roster.Add(Bur(Bureau, CourtRank.正五位下, 7.5f));
            Seat(w, Hyobu, Bureau, BureaucratGrade.局長級, Year - 10);
            int posts = w.pol.cabinet.posts.Count;
            int history = w.pol.cabinet.history.Count;

            CivilServiceAnnualReport rep = Tick(w, PromoteOnly);

            Assert.AreEqual(1, rep.promotedCount);
            Assert.AreEqual(posts, w.pol.cabinet.posts.Count, "内閣の職は増えも減りもしない");
            Assert.AreEqual(history, w.pol.cabinet.history.Count, "内閣の履歴は動かない");
            Assert.AreEqual(Minister, CabinetAppointmentRules.FindPost(w.pol.cabinet, Hyobu, CabinetPostKind.大臣).holderId);
            Assert.IsNull(CabinetAppointmentRules.PostHeldBy(w.pol.cabinet, Bureau), "官僚は内閣の職に載らない");
        }
    }
}
