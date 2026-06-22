using System.Collections.Generic;
using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 適材適所の最適任命（<see cref="AppointmentPlanRules"/>）の EditMode テスト。
    /// 文官席は文才（運営/情報）の高い者・武官席は軍才（統率）の高い者が就く／資格不足は割り当てない／
    /// 一人一職・一席一人／決定論／null・空安全 を担保する。
    /// </summary>
    public class AppointmentPlanRulesTests
    {
        // --- ヘルパ：人物・役職の生成 ---

        private static Person Civilian(int id, int operation, int intelligence, int rankTier = 0)
        {
            return new Person(id, "文" + id, Faction.同盟, PersonRole.文民)
            {
                rankTier = rankTier,
                operation = operation,
                intelligence = intelligence,
            };
        }

        private static Person Soldier(int id, int leadership, int attack = 50, int rankTier = 8)
        {
            return new Person(id, "武" + id, Faction.同盟, PersonRole.軍人)
            {
                rankTier = rankTier,
                leadership = leadership,
                attack = attack,
                defense = 50,
                mobility = 50,
            };
        }

        private static Office CivilOffice(int id)
            => new Office(id, "内政官", OfficeScope.国家, OfficeDomain.内政) { civilianOnly = true };

        private static Office MilitaryOffice(int id, int requiredTier = 0)
            => new Office(id, "軍司令", OfficeScope.国家, OfficeDomain.軍事)
            { militaryOnly = true, requiredTier = requiredTier };

        // --- 適合度の基礎 ---

        [Test]
        public void FitForOffice_IneligibleReturnsZero()
        {
            // 文民を軍人専用の席へ＝資格無しで0。
            Person civ = Civilian(1, 90, 90);
            Office mil = MilitaryOffice(10);
            Assert.AreEqual(0f, AppointmentPlanRules.FitForOffice(civ, mil), 1e-6f);
        }

        [Test]
        public void FitForOffice_RankTooLowReturnsZero()
        {
            Person lowRank = Soldier(1, 90, rankTier: 4);
            Office mil = MilitaryOffice(10, requiredTier: 8);
            Assert.AreEqual(0f, AppointmentPlanRules.FitForOffice(lowRank, mil), 1e-6f);
        }

        [Test]
        public void FitForOffice_CompetentScoresHigherThanMediocre()
        {
            Person good = Civilian(1, 95, 95);
            Person weak = Civilian(2, 20, 20);
            Office civ = CivilOffice(10);
            Assert.Greater(AppointmentPlanRules.FitForOffice(good, civ),
                           AppointmentPlanRules.FitForOffice(weak, civ));
        }

        [Test]
        public void FitForOffice_NullSafe()
        {
            Assert.AreEqual(0f, AppointmentPlanRules.FitForOffice(null, CivilOffice(1)));
            Assert.AreEqual(0f, AppointmentPlanRules.FitForOffice(Civilian(1, 50, 50), null));
        }

        // --- 全体最適：所掌に合う人が合う席へ ---

        [Test]
        public void Plan_CivilOfficePrefersHighCivilAptitude()
        {
            var people = new List<Person>
            {
                Civilian(1, 90, 90), // 文才高
                Civilian(2, 10, 10), // 文才低
            };
            var seats = new List<OfficeVacancy> { new OfficeVacancy(CivilOffice(100)) };

            AppointmentPlan plan = AppointmentPlanRules.Plan(people, seats);

            Assert.AreEqual(1, plan.FilledCount);
            Assert.AreEqual(1, plan.assignments[0].person.id); // 文才高が就く
        }

        [Test]
        public void Plan_MilitaryOfficePrefersHighLeadership()
        {
            var people = new List<Person>
            {
                Soldier(1, 30), // 統率低
                Soldier(2, 95), // 統率高
            };
            var seats = new List<OfficeVacancy> { new OfficeVacancy(MilitaryOffice(100)) };

            AppointmentPlan plan = AppointmentPlanRules.Plan(people, seats);

            Assert.AreEqual(1, plan.FilledCount);
            Assert.AreEqual(2, plan.assignments[0].person.id); // 統率高が就く
        }

        [Test]
        public void Plan_AssignsBestPersonToEachMatchingPost()
        {
            // 文才の人と軍才の人がそれぞれの所掌へ振り分けられる（適材適所の全体最適）。
            Person scholar = Civilian(1, 95, 95);
            Person general = Soldier(2, 95);
            var people = new List<Person> { scholar, general };
            var seats = new List<OfficeVacancy>
            {
                new OfficeVacancy(CivilOffice(100)),
                new OfficeVacancy(MilitaryOffice(200)),
            };

            AppointmentPlan plan = AppointmentPlanRules.Plan(people, seats);

            Assert.AreEqual(2, plan.FilledCount);
            // 文官席には scholar、軍官席には general。
            foreach (var a in plan.assignments)
            {
                if (a.vacancy.office.id == 100) Assert.AreEqual(1, a.person.id);
                if (a.vacancy.office.id == 200) Assert.AreEqual(2, a.person.id);
            }
        }

        // --- 一人一職・一席一人 ---

        [Test]
        public void Plan_OnePersonOnePost()
        {
            // 1人しかいないが席が2つ＝1席だけ埋まる。
            var people = new List<Person> { Civilian(1, 90, 90) };
            var seats = new List<OfficeVacancy>
            {
                new OfficeVacancy(CivilOffice(100)),
                new OfficeVacancy(CivilOffice(200)),
            };

            AppointmentPlan plan = AppointmentPlanRules.Plan(people, seats);

            Assert.AreEqual(1, plan.FilledCount);
            // 同一人物が複数席に重複していない。
            var used = new HashSet<int>();
            foreach (var a in plan.assignments)
                Assert.IsTrue(used.Add(a.person.id), "一人一職が破れている");
        }

        [Test]
        public void Plan_OneSeatOnePerson()
        {
            // 2人が1席を奪い合う＝良い方だけが就く。
            var people = new List<Person> { Civilian(1, 90, 90), Civilian(2, 80, 80) };
            var seats = new List<OfficeVacancy> { new OfficeVacancy(CivilOffice(100)) };

            AppointmentPlan plan = AppointmentPlanRules.Plan(people, seats);

            Assert.AreEqual(1, plan.FilledCount);
            Assert.AreEqual(1, plan.assignments[0].person.id);
        }

        [Test]
        public void Plan_IneligibleNotAssigned()
        {
            // 文民しかいないのに軍人専用席＝誰も就かない。
            var people = new List<Person> { Civilian(1, 99, 99) };
            var seats = new List<OfficeVacancy> { new OfficeVacancy(MilitaryOffice(100)) };

            AppointmentPlan plan = AppointmentPlanRules.Plan(people, seats);

            Assert.AreEqual(0, plan.FilledCount);
        }

        // --- 決定論 ---

        [Test]
        public void Plan_DeterministicTieBreakByIdAscending()
        {
            // 全く同じ能力の2人＝id 昇順で安定して同じ結果。
            var seats = new List<OfficeVacancy> { new OfficeVacancy(CivilOffice(100)) };
            Person a1 = Civilian(5, 70, 70);
            Person a2 = Civilian(3, 70, 70);

            AppointmentPlan p1 = AppointmentPlanRules.Plan(new List<Person> { a1, a2 }, seats);
            AppointmentPlan p2 = AppointmentPlanRules.Plan(new List<Person> { a2, a1 }, seats);

            Assert.AreEqual(1, p1.FilledCount);
            Assert.AreEqual(p1.assignments[0].person.id, p2.assignments[0].person.id);
            Assert.AreEqual(3, p1.assignments[0].person.id); // 小さい id が勝つ
        }

        // --- null / 空 安全 ---

        [Test]
        public void Plan_NullInputsSafe()
        {
            Assert.AreEqual(0, AppointmentPlanRules.Plan(null, null).FilledCount);
            Assert.AreEqual(0, AppointmentPlanRules.Plan(null, new List<OfficeVacancy>()).FilledCount);
            Assert.AreEqual(0, AppointmentPlanRules.Plan(new List<Person>(), null).FilledCount);
        }

        [Test]
        public void Plan_EmptyInputsSafe()
        {
            Assert.AreEqual(0, AppointmentPlanRules.Plan(new List<Person>(), new List<OfficeVacancy>()).FilledCount);
        }

        [Test]
        public void Plan_SkipsNullEntries()
        {
            var people = new List<Person> { null, Civilian(1, 90, 90), null };
            var seats = new List<OfficeVacancy> { null, new OfficeVacancy(CivilOffice(100)), new OfficeVacancy(null) };

            AppointmentPlan plan = AppointmentPlanRules.Plan(people, seats);

            Assert.AreEqual(1, plan.FilledCount);
            Assert.AreEqual(1, plan.assignments[0].person.id);
        }

        // --- 補助：PostTypeOf / RankFit ---

        [Test]
        public void PostTypeOf_MilitaryDomainIsMilitaryPost()
        {
            Assert.AreEqual(PostType.軍務, AppointmentPlanRules.PostTypeOf(OfficeDomain.軍事));
            Assert.AreEqual(PostType.政務, AppointmentPlanRules.PostTypeOf(OfficeDomain.内政));
            Assert.AreEqual(PostType.政務, AppointmentPlanRules.PostTypeOf(OfficeDomain.外交));
            Assert.AreEqual(PostType.政務, AppointmentPlanRules.PostTypeOf(OfficeDomain.財政));
        }

        [Test]
        public void RankFit_ExactRequirementScoresFull()
        {
            Assert.AreEqual(1f, AppointmentPlanRules.RankFit(6, 6), 1e-6f);
        }

        [Test]
        public void RankFit_BelowRequirementZero()
        {
            Assert.AreEqual(0f, AppointmentPlanRules.RankFit(5, 6), 1e-6f);
        }

        [Test]
        public void RankFit_OverqualifiedSlightlyPenalized()
        {
            float exact = AppointmentPlanRules.RankFit(6, 6);
            float over = AppointmentPlanRules.RankFit(9, 6);
            Assert.Less(over, exact);
            Assert.GreaterOrEqual(over, 0.5f);
        }
    }
}
