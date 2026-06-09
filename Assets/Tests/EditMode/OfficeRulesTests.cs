using System.Collections.Generic;
using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 役職の資格・提案権限（GOV-1 #142／GOV-3 #144）を固定する：軍人/文民/政治任用/必要階級の就任資格、
    /// スコープ包含（国家 ⊇ 方面 ⊇ 星系）、所掌×スコープの提案権限（元首は全所掌）。
    /// </summary>
    public class OfficeRulesTests
    {
        private static Person Soldier(int tier = 0)
        {
            var p = new Person(1, "軍", Faction.帝国, PersonRole.軍人) { rankTier = tier };
            return p;
        }

        private static Person Civilian(bool politician = false)
        {
            var p = new Person(2, "文", Faction.帝国, PersonRole.文民) { isPolitician = politician };
            return p;
        }

        [Test]
        public void CanHold_MilitaryOnly_RejectsCivilian()
        {
            var o = new Office(1, "軍務大臣", OfficeScope.国家, OfficeDomain.軍事) { militaryOnly = true };
            Assert.IsTrue(OfficeRules.CanHold(Soldier(), o));
            Assert.IsFalse(OfficeRules.CanHold(Civilian(), o));
        }

        [Test]
        public void CanHold_CivilianOnly_RejectsSoldier()
        {
            var o = new Office(2, "内務大臣", OfficeScope.国家, OfficeDomain.内政) { civilianOnly = true };
            Assert.IsTrue(OfficeRules.CanHold(Civilian(), o));
            Assert.IsFalse(OfficeRules.CanHold(Soldier(), o));
        }

        [Test]
        public void CanHold_PoliticalAppointmentOnly_RequiresPolitician()
        {
            var o = new Office(3, "首相", OfficeScope.国家, OfficeDomain.元首) { politicalAppointmentOnly = true };
            Assert.IsTrue(OfficeRules.CanHold(Civilian(politician: true), o));
            Assert.IsFalse(OfficeRules.CanHold(Civilian(politician: false), o)); // 職業官僚は就けない
        }

        [Test]
        public void CanHold_RequiredTier_GatesByRank()
        {
            var o = new Office(4, "総督", OfficeScope.星系, OfficeDomain.内政) { requiredTier = 8 };
            Assert.IsFalse(OfficeRules.CanHold(Soldier(tier: 7), o));
            Assert.IsTrue(OfficeRules.CanHold(Soldier(tier: 8), o));
        }

        [Test]
        public void CoversScope_NationContainsRegionAndSystem()
        {
            Assert.IsTrue(OfficeRules.CoversScope(OfficeScope.国家, OfficeScope.星系));
            Assert.IsTrue(OfficeRules.CoversScope(OfficeScope.方面, OfficeScope.星系));
            Assert.IsFalse(OfficeRules.CoversScope(OfficeScope.星系, OfficeScope.国家));
        }

        [Test]
        public void CanPropose_MatchesDomainAndScope()
        {
            var warMinister = new Office(5, "軍務大臣", OfficeScope.国家, OfficeDomain.軍事);
            var held = new List<Office> { warMinister };

            Assert.IsTrue(OfficeRules.CanPropose(held, OfficeDomain.軍事, OfficeScope.星系)); // 国家規模で軍事提案
            Assert.IsFalse(OfficeRules.CanPropose(held, OfficeDomain.内政, OfficeScope.国家)); // 所掌外
        }

        [Test]
        public void CanPropose_HeadOfState_CoversAllDomains()
        {
            var sovereign = new Office(6, "元首", OfficeScope.国家, OfficeDomain.元首);
            var held = new List<Office> { sovereign };

            Assert.IsTrue(OfficeRules.CanPropose(held, OfficeDomain.軍事, OfficeScope.国家));
            Assert.IsTrue(OfficeRules.CanPropose(held, OfficeDomain.内政, OfficeScope.星系));
            Assert.IsTrue(OfficeRules.CanPropose(held, OfficeDomain.外交, OfficeScope.方面));
        }

        [Test]
        public void CanPropose_SystemGovernor_CannotProposeNationwide()
        {
            var governor = new Office(7, "星系総督", OfficeScope.星系, OfficeDomain.内政);
            var held = new List<Office> { governor };

            Assert.IsTrue(OfficeRules.CanPropose(held, OfficeDomain.内政, OfficeScope.星系));
            Assert.IsFalse(OfficeRules.CanPropose(held, OfficeDomain.内政, OfficeScope.国家)); // 星系は国家を包含しない
        }
    }
}
