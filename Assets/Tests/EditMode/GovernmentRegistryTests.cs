using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 役職の任命台帳（GOV-1 #142）を固定する：資格を満たす任命の成立、定員超過の拒否、解任、
    /// スコープキー（星系総督）の別管理、保持者・保持役職の取得。static のため各テストで Clear する。
    /// </summary>
    public class GovernmentRegistryTests
    {
        [SetUp]
        public void Setup() => GovernmentRegistry.Clear();

        private static Person Mk(int id, PersonRole role, int tier = 0)
            => new Person(id, "p" + id, Faction.帝国, role) { rankTier = tier };

        [Test]
        public void TryAppoint_Succeeds_WhenEligible()
        {
            var office = new Office(1, "内務大臣", OfficeScope.国家, OfficeDomain.内政) { civilianOnly = true };
            var civ = Mk(1, PersonRole.文民);

            Assert.IsTrue(GovernmentRegistry.TryAppoint(Faction.帝国, office, civ));
            Assert.AreSame(civ, GovernmentRegistry.GetHolder(office));
        }

        [Test]
        public void TryAppoint_Rejects_WhenIneligible()
        {
            var office = new Office(1, "内務大臣", OfficeScope.国家, OfficeDomain.内政) { civilianOnly = true };
            var soldier = Mk(2, PersonRole.軍人);

            Assert.IsFalse(GovernmentRegistry.TryAppoint(Faction.帝国, office, soldier));
            Assert.IsNull(GovernmentRegistry.GetHolder(office));
        }

        [Test]
        public void TryAppoint_Rejects_WhenSlotsFull()
        {
            var head = new Office(3, "元首", OfficeScope.国家, OfficeDomain.元首) { slots = 1 };
            Assert.IsTrue(GovernmentRegistry.TryAppoint(Faction.帝国, head, Mk(1, PersonRole.文民)));
            Assert.IsFalse(GovernmentRegistry.TryAppoint(Faction.帝国, head, Mk(2, PersonRole.文民))); // 定員1
        }

        [Test]
        public void MultiSlotOffice_AcceptsManyHolders()
        {
            var bureaucrat = new Office(4, "官僚", OfficeScope.国家, OfficeDomain.内政) { slots = 3 };
            Assert.IsTrue(GovernmentRegistry.TryAppoint(Faction.帝国, bureaucrat, Mk(1, PersonRole.文民)));
            Assert.IsTrue(GovernmentRegistry.TryAppoint(Faction.帝国, bureaucrat, Mk(2, PersonRole.文民)));
            Assert.AreEqual(2, GovernmentRegistry.HolderCount(bureaucrat));
        }

        [Test]
        public void Dismiss_RemovesHolder()
        {
            var office = new Office(5, "外務大臣", OfficeScope.国家, OfficeDomain.外交);
            var civ = Mk(1, PersonRole.文民);
            GovernmentRegistry.TryAppoint(Faction.帝国, office, civ);

            Assert.IsTrue(GovernmentRegistry.Dismiss(office, civ));
            Assert.IsNull(GovernmentRegistry.GetHolder(office));
        }

        [Test]
        public void ScopeKey_KeepsSystemGovernorsSeparate()
        {
            var governor = new Office(6, "星系総督", OfficeScope.星系, OfficeDomain.内政) { slots = 1 };
            var a = Mk(1, PersonRole.文民);
            var b = Mk(2, PersonRole.文民);

            Assert.IsTrue(GovernmentRegistry.TryAppoint(Faction.帝国, governor, a, scopeKey: 100));
            Assert.IsTrue(GovernmentRegistry.TryAppoint(Faction.帝国, governor, b, scopeKey: 200)); // 別星系は別枠
            Assert.AreSame(a, GovernmentRegistry.GetHolder(governor, 100));
            Assert.AreSame(b, GovernmentRegistry.GetHolder(governor, 200));
        }

        [Test]
        public void GetOffices_ReturnsAllHeldByPerson()
        {
            var head = new Office(7, "元首", OfficeScope.国家, OfficeDomain.元首);
            var war = new Office(8, "軍務大臣", OfficeScope.国家, OfficeDomain.軍事);
            var p = Mk(1, PersonRole.文民);
            GovernmentRegistry.TryAppoint(Faction.帝国, head, p);
            GovernmentRegistry.TryAppoint(Faction.帝国, war, p);

            Assert.AreEqual(2, GovernmentRegistry.GetOffices(p).Count);
        }
    }
}
