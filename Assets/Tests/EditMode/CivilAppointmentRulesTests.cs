using System.Collections.Generic;
using NUnit.Framework;
using Ginei;
using AP = Ginei.CivilServiceRules.AppointmentParams;

namespace Ginei.Tests
{
    /// <summary>
    /// 文官の銓衡配属（官位相当で資格判定し、考課＋位階で最適者を GovernmentRegistry へ任命）を固定する。
    /// </summary>
    public class CivilAppointmentRulesTests
    {
        [SetUp]
        public void Clear() => GovernmentRegistry.Clear();

        private static Office Saisho()
            => new Office(1, "宰相", OfficeScope.国家, OfficeDomain.内政) { civilianOnly = true, requiredTier = 0 };

        private static Person Civil(int id, CourtRank rank, params MeritRating[] ratings)
        {
            var p = new Person(id, "文官" + id, Faction.帝国, PersonRole.文民)
            { courtRank = rank, merit = new OfficialMerit(id) };
            for (int i = 0; i < ratings.Length; i++) MeritEvaluationRules.Record(p.merit, ratings[i]);
            return p;
        }

        [Test]
        public void IsQualified_RequiresCivilian_AndOfficeRank()
        {
            var office = Saisho();
            var noble = Civil(1, CourtRank.従五位下);
            var below = Civil(2, CourtRank.正六位上); // 官位相当に達しない
            var soldier = new Person(3, "提督", Faction.帝国, PersonRole.軍人) { courtRank = CourtRank.正三位 };
            var dead = Civil(4, CourtRank.正三位); dead.deathYear = 799;

            Assert.IsTrue(CivilAppointmentRules.IsQualified(noble, office, CourtRank.従五位下));
            Assert.IsFalse(CivilAppointmentRules.IsQualified(below, office, CourtRank.従五位下)); // 位階不足
            Assert.IsFalse(CivilAppointmentRules.IsQualified(soldier, office, CourtRank.従五位下)); // 軍人は文民専用職に就けない
            Assert.IsFalse(CivilAppointmentRules.IsQualified(dead, office, CourtRank.従五位下));    // 故人
        }

        [Test]
        public void SelectFor_PrefersMeritAmongQualified()
        {
            var office = Saisho();
            var good = Civil(1, CourtRank.従五位下, MeritRating.上上, MeritRating.上上, MeritRating.上上);
            var poor = Civil(2, CourtRank.従五位下, MeritRating.下下, MeritRating.下下, MeritRating.下下);
            var roster = new List<Person> { poor, good };
            var pick = CivilAppointmentRules.SelectFor(office, CourtRank.従五位下, roster, AP.Default);
            Assert.AreSame(good, pick);
        }

        [Test]
        public void FillVacancy_AppointsBest_AndReappointsIncumbent()
        {
            var office = Saisho();
            var p = Civil(1, CourtRank.従五位下, MeritRating.上中);
            var roster = new List<Person> { p };

            var appointed = CivilAppointmentRules.FillVacancy(Faction.帝国, office, CourtRank.従五位下, roster, AP.Default);
            Assert.AreSame(p, appointed);
            Assert.AreSame(p, GovernmentRegistry.GetHolder(office) as Person);

            // 在任の文民は再任（重複任命しない）
            var again = CivilAppointmentRules.FillVacancy(Faction.帝国, office, CourtRank.従五位下, roster, AP.Default);
            Assert.AreSame(p, again);
            Assert.AreEqual(1, GovernmentRegistry.HolderCount(office));
        }

        [Test]
        public void FillVacancy_ScopedAppointments_AreIndependentPerSystem()
        {
            // 総督＝星系スコープ。scopeKey（星系id）ごとに独立して任命できる（同一 Office を使い回す）。
            var gov = new Office(2, "総督", OfficeScope.星系, OfficeDomain.内政) { civilianOnly = true, requiredTier = 0 };
            var a = Civil(1, CourtRank.正六位上, MeritRating.上中);
            var b = Civil(2, CourtRank.正六位上, MeritRating.上中);
            var ra = CivilAppointmentRules.FillVacancy(Faction.帝国, gov, CourtRank.正六位上,
                new List<Person> { a }, AP.Default, scopeKey: 100);
            var rb = CivilAppointmentRules.FillVacancy(Faction.帝国, gov, CourtRank.正六位上,
                new List<Person> { b }, AP.Default, scopeKey: 200);
            Assert.AreSame(a, ra);
            Assert.AreSame(b, rb);
            Assert.AreSame(a, GovernmentRegistry.GetHolder(gov, 100) as Person);
            Assert.AreSame(b, GovernmentRegistry.GetHolder(gov, 200) as Person);
        }

        [Test]
        public void FillVacancy_NoQualified_LeavesVacant()
        {
            var office = Saisho();
            var roster = new List<Person> { Civil(1, CourtRank.正六位上) }; // 位階不足
            var appointed = CivilAppointmentRules.FillVacancy(Faction.帝国, office, CourtRank.従五位下, roster, AP.Default);
            Assert.IsNull(appointed);
            Assert.IsNull(GovernmentRegistry.GetHolder(office));
        }
    }
}
