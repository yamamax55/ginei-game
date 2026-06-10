using System.Collections.Generic;
using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 政党と最小選挙・官僚の役職上限（GOV-6 #159）を固定する：最大支持の党が与党＝党首が首班、
    /// 民主国家で政治系の高位役職が政治任用専用へ昇格し職業官僚が就けなくなること（軍/非民主は不変）。
    /// </summary>
    public class PartyRulesTests
    {
        [Test]
        public void RulingParty_IsHighestSupport()
        {
            var a = new Party(1, "民政党", Faction.同盟) { support = 0.45f, leaderId = 100 };
            var b = new Party(2, "革新党", Faction.同盟) { support = 0.55f, leaderId = 200 };
            var parties = new List<Party> { a, b };

            Assert.AreSame(b, PartyRules.RulingParty(parties));
            Assert.AreEqual(200, PartyRules.Premier(parties)); // 党勢で首班決定
        }

        [Test]
        public void MarkDemocraticAppointments_PromotesHighOfficesToPolitical()
        {
            var bureaucrat = new Office(1, "事務次官", OfficeScope.国家, OfficeDomain.内政) { requiredTier = 7 };
            var minister = new Office(2, "内務大臣", OfficeScope.国家, OfficeDomain.内政) { requiredTier = 9 };
            var general = new Office(3, "軍務大臣", OfficeScope.国家, OfficeDomain.軍事) { requiredTier = 9 };
            var offices = new List<Office> { bureaucrat, minister, general };

            // 民主国家・上限tier7：tier7超の政治職を政治任用専用へ
            PartyRules.MarkDemocraticAppointments(offices, careerCeilingTier: 7, CivilianControlType.文民統制);

            Assert.IsFalse(bureaucrat.politicalAppointmentOnly); // 事務次官級＝官僚で可
            Assert.IsTrue(minister.politicalAppointmentOnly);    // 大臣＝政治家のみ
            Assert.IsFalse(general.politicalAppointmentOnly);    // 軍は別系統＝対象外

            // 職業官僚（非政治家）は大臣に就けないが事務次官には就ける
            var careerOfficial = new Person(1, "官僚", Faction.同盟, PersonRole.文民) { rankTier = 9, isPolitician = false };
            Assert.IsFalse(OfficeRules.CanHold(careerOfficial, minister));
            Assert.IsTrue(OfficeRules.CanHold(careerOfficial, bureaucrat));

            var politician = new Person(2, "政治家", Faction.同盟, PersonRole.文民) { rankTier = 9, isPolitician = true };
            Assert.IsTrue(OfficeRules.CanHold(politician, minister));
        }

        [Test]
        public void MarkDemocraticAppointments_NoOp_UnderNonDemocratic()
        {
            var minister = new Office(2, "内務大臣", OfficeScope.国家, OfficeDomain.内政) { requiredTier = 9 };
            var offices = new List<Office> { minister };

            // 共産（党軍）では官僚上限を課さない＝従来動作
            PartyRules.MarkDemocraticAppointments(offices, careerCeilingTier: 7, CivilianControlType.党軍);
            Assert.IsFalse(minister.politicalAppointmentOnly);
        }
    }
}
