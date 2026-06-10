using System.Collections.Generic;
using NUnit.Framework;
using Ginei;
using CP = Ginei.CareerPipelineRules.CliqueParams;

namespace Ginei.Tests
{
    /// <summary>
    /// 出自パイプライン（LIFE-5/6/7 #155/#156/#157）を固定する：経路ごとの役割・席次の刻み（ハンモック/合格順位）、
    /// 学閥/文官閥の結束（同窓＋同期）、テクノクラートの実力本位の専門才と最適配属。
    /// </summary>
    public class CareerPipelineRulesTests
    {
        [Test]
        public void TrackRole_MilitaryAcademyMakesSoldier_OthersCivilian()
        {
            Assert.AreEqual(PersonRole.軍人, CareerPipelineRules.TrackRole(CareerTrack.士官学校));
            Assert.AreEqual(PersonRole.文民, CareerPipelineRules.TrackRole(CareerTrack.科挙));
            Assert.AreEqual(PersonRole.文民, CareerPipelineRules.TrackRole(CareerTrack.テクノクラート));
            Assert.IsTrue(CareerPipelineRules.TrackIsTechnical(CareerTrack.テクノクラート));
        }

        [Test]
        public void Stamp_MilitaryUsesHammock_ExamUsesRank()
        {
            var officer = new Person(1, "士官", Faction.帝国, PersonRole.文民);
            CareerPipelineRules.Stamp(officer, CareerTrack.士官学校, schoolId: 10, graduationYear: 796, rank: 3);
            Assert.AreEqual(PersonRole.軍人, officer.role);
            Assert.AreEqual(3, officer.hammockNumber);
            Assert.AreEqual(796, officer.graduationYear);
            Assert.AreEqual(0, officer.examRank);

            var bureaucrat = new Person(2, "文官", Faction.帝国, PersonRole.軍人);
            CareerPipelineRules.Stamp(bureaucrat, CareerTrack.科挙, schoolId: 20, graduationYear: 796, rank: 1);
            Assert.AreEqual(PersonRole.文民, bureaucrat.role);
            Assert.AreEqual(1, bureaucrat.examRank);
            Assert.AreEqual(0, bureaucrat.hammockNumber);
        }

        [Test]
        public void CliqueBond_StrongestWhenSameSchoolAndYear()
        {
            var prm = CP.Default; // 同窓0.3＋同期0.4
            var a = new Person(1, "a", Faction.帝国, PersonRole.軍人) { schoolId = 10, graduationYear = 796 };
            var sameBoth = new Person(2, "b", Faction.帝国, PersonRole.軍人) { schoolId = 10, graduationYear = 796 };
            var sameSchool = new Person(3, "c", Faction.帝国, PersonRole.軍人) { schoolId = 10, graduationYear = 800 };
            var stranger = new Person(4, "d", Faction.帝国, PersonRole.軍人) { schoolId = 99, graduationYear = 801 };

            Assert.AreEqual(0.7f, CareerPipelineRules.CliqueBond(a, sameBoth, prm), 1e-4f);
            Assert.AreEqual(0.3f, CareerPipelineRules.CliqueBond(a, sameSchool, prm), 1e-4f);
            Assert.AreEqual(0f, CareerPipelineRules.CliqueBond(a, stranger, prm), 1e-4f);
            Assert.AreEqual(0f, CareerPipelineRules.CliqueBond(a, a, prm), 1e-4f); // 自分とは結束しない
        }

        [Test]
        public void TechnocratEffectiveness_IsTechnicalAptitude()
        {
            var t = new Person(1, "技師", Faction.帝国, PersonRole.文民)
            { research = 80, engineering = 80, planning = 60, production = 80 };
            Assert.AreEqual(75f, CareerPipelineRules.TechnocratEffectiveness(t), 1e-4f); // (80+80+60+80)/4
        }

        [Test]
        public void BestTechnocrat_PicksHighestAvailableInFaction()
        {
            var strong = new Person(1, "強", Faction.帝国, PersonRole.文民)
            { research = 90, engineering = 90, planning = 90, production = 90 };
            var weak = new Person(2, "弱", Faction.帝国, PersonRole.文民)
            { research = 30, engineering = 30, planning = 30, production = 30 };
            var deceased = new Person(3, "故", Faction.帝国, PersonRole.文民)
            { research = 100, engineering = 100, planning = 100, production = 100, deathYear = 800 };
            var enemy = new Person(4, "敵", Faction.同盟, PersonRole.文民)
            { research = 100, engineering = 100, planning = 100, production = 100 };

            var pool = new List<Person> { strong, weak, deceased, enemy };
            Assert.AreSame(strong, CareerPipelineRules.BestTechnocrat(pool, Faction.帝国)); // 故人/敵は除外
        }
    }
}
