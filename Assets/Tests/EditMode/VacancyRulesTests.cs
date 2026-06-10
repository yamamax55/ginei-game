using System.Collections.Generic;
using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 死亡・捕虜で空いた席の後任補充（LIFE-2 #152・継承）を固定する：故人保持者の解任、有資格の最高tier後任の補充、
    /// 適任不在なら空席のまま、台帳から任に就けない保持者の一掃。static のため Clear する。
    /// </summary>
    public class VacancyRulesTests
    {
        [SetUp]
        public void Setup() => GovernmentRegistry.Clear();

        private static Person Mk(int id, int tier, PersonRole role = PersonRole.文民)
            => new Person(id, "p" + id, Faction.帝国, role) { rankTier = tier };

        [Test]
        public void SelectSuccessor_PicksHighestTierAvailable()
        {
            var a = Mk(1, 7);
            var b = Mk(2, 9);
            var dead = Mk(3, 10); dead.deathYear = 800; // 故人は除外
            var pool = new List<ICharacter> { a, b, dead };

            var chosen = VacancyRules.SelectSuccessor(pool, c => true);
            Assert.AreSame(b, chosen); // 最高tier=9（故人10は不可）
        }

        [Test]
        public void SelectSuccessor_RespectsEligibility()
        {
            var soldier = Mk(1, 10, PersonRole.軍人);
            var civilian = Mk(2, 7, PersonRole.文民);
            var pool = new List<ICharacter> { soldier, civilian };

            // 文民専用＝軍人は不可。tier低くても文民が選ばれる
            var chosen = VacancyRules.SelectSuccessor(pool, c => !c.IsMilitary);
            Assert.AreSame(civilian, chosen);
        }

        [Test]
        public void FillVacancy_ReplacesDeceasedHolder()
        {
            var office = new Office(1, "総督", OfficeScope.星系, OfficeDomain.内政) { civilianOnly = true };
            var incumbent = Mk(1, 8);
            GovernmentRegistry.TryAppoint(Faction.帝国, office, incumbent);

            incumbent.deathYear = 800; // 在職中に死去
            var pool = new List<ICharacter> { incumbent, Mk(2, 7) };

            Assert.IsTrue(VacancyRules.FillVacancy(Faction.帝国, office, pool));
            var holder = GovernmentRegistry.GetHolder(office);
            Assert.AreEqual(2, holder.Id); // 後任に交代
            Assert.IsTrue(holder.IsAvailable);
        }

        [Test]
        public void FillVacancy_LeavesVacant_WhenNoEligible()
        {
            var office = new Office(1, "軍務大臣", OfficeScope.国家, OfficeDomain.軍事) { militaryOnly = true };
            var pool = new List<ICharacter> { Mk(1, 9, PersonRole.文民) }; // 文民しか居ない

            Assert.IsFalse(VacancyRules.FillVacancy(Faction.帝国, office, pool));
            Assert.IsNull(GovernmentRegistry.GetHolder(office)); // 適任不在＝空席
        }

        [Test]
        public void ClearDeparted_RemovesUnavailableHolders()
        {
            var o1 = new Office(1, "A", OfficeScope.国家, OfficeDomain.内政);
            var o2 = new Office(2, "B", OfficeScope.国家, OfficeDomain.外交);
            var alive = Mk(1, 7);
            var captured = Mk(2, 7); captured.captiveStatus = CaptiveStatus.捕虜;
            GovernmentRegistry.TryAppoint(Faction.帝国, o1, alive);
            GovernmentRegistry.TryAppoint(Faction.帝国, o2, captured);

            var vacated = VacancyRules.ClearDeparted();
            Assert.AreEqual(1, vacated.Count);     // 捕虜の席だけ空く
            Assert.AreSame(o2, vacated[0].office);
            Assert.IsNotNull(GovernmentRegistry.GetHolder(o1)); // 存命は残る
            Assert.IsNull(GovernmentRegistry.GetHolder(o2));
        }
    }
}
