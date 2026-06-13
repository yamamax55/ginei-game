using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>王家の教育（帝王学）：出生でネームド化・子供/大人で別能力・家庭教師の質・元勲ボーナス。</summary>
    public class RoyalEducationRulesTests
    {
        private static Person Tutor(int stat)
        {
            var p = new Person(1, "師", Faction.帝国, PersonRole.文民);
            p.leadership = stat; p.operation = stat; p.intelligence = stat;
            return p;
        }

        [Test]
        public void TutorQuality_And_GenroBonus()
        {
            Assert.AreEqual(0.9f, RoyalEducationRules.TutorQuality(Tutor(90)), 1e-4f);
            Assert.AreEqual(0f, RoyalEducationRules.TutorQuality(null), 1e-4f);
            // 元勲（質80）→ 0.8×0.3=0.24。
            Assert.AreEqual(0.24f, RoyalEducationRules.GenroBonus(Tutor(80)), 1e-4f);
            // 上限0.25でクランプ（質100→0.3）。
            Assert.AreEqual(0.25f, RoyalEducationRules.GenroBonus(Tutor(100)), 1e-4f);
            Assert.AreEqual(0f, RoyalEducationRules.GenroBonus(null), 1e-4f);
        }

        [Test]
        public void EducationCap_And_Accumulate_CapsAtTutorQuality()
        {
            Assert.AreEqual(1.0f, RoyalEducationRules.EducationCap(0.9f, 0.24f), 1e-4f); // 1.14→クランプ
            Assert.AreEqual(0.5f, RoyalEducationRules.EducationCap(0.5f, 0f), 1e-4f);

            // 名師＋元勲を子供時代いっぱい → 満教育。
            Assert.AreEqual(1.0f, RoyalEducationRules.AccumulateEducation(0f, 0.9f, 0.24f, 12f), 1e-4f);
            // 凡庸な師は長年でも上限止まり（24年でも0.5）。
            Assert.AreEqual(0.5f, RoyalEducationRules.AccumulateEducation(0f, 0.5f, 0f, 24f), 1e-4f);
            // 師なし → 教育されない。
            Assert.AreEqual(0f, RoyalEducationRules.AccumulateEducation(0f, 0f, 0f, 12f), 1e-4f);
        }

        [Test]
        public void ChildStat_And_AdultStat_AreDistinct()
        {
            Assert.IsTrue(RoyalEducationRules.IsChild(15));
            Assert.IsFalse(RoyalEducationRules.IsChild(16));

            // 子供時代＝素養の未成熟分（90→36）。
            Assert.AreEqual(36, RoyalEducationRules.ChildStat(90));
            // 大人時代＝帝王学で素養が実現（無教育45・満教育90・中間は素養80で60）。
            Assert.AreEqual(45, RoyalEducationRules.AdultStat(90, 0f));
            Assert.AreEqual(90, RoyalEducationRules.AdultStat(90, 1f));
            Assert.AreEqual(60, RoyalEducationRules.AdultStat(80, 0.5f));
        }

        [Test]
        public void BornRoyal_NamedAtBirth_WithChildStats()
        {
            var person = new Person(10, "皇子", Faction.帝国, PersonRole.文民);
            var up = new RoyalUpbringing(796, 90, 70, 60, 60, 95, 90); // 素養（大人時代の天井）
            RoyalEducationRules.BornRoyal(person, up);

            Assert.IsTrue(person.isRoyal);          // 生まれた瞬間にネームド化（王家フラグ）
            Assert.AreEqual(796, person.birthYear);
            Assert.IsFalse(up.matured);
            Assert.AreEqual(0f, up.education, 1e-4f);
            // 子供時代の能力＝素養の未成熟分（90→36, 95→38）。
            Assert.AreEqual(36, person.leadership);
            Assert.AreEqual(38, person.operation);
        }

        [Test]
        public void Mature_WellTutoredVsNeglected_DivergeIntoDifferentKings()
        {
            var up = new RoyalUpbringing(796, 90, 70, 60, 60, 95, 90);

            // 名師＋元勲に育てられた賢君：素養を満たす。
            var wise = new Person(11, "賢君", Faction.帝国, PersonRole.文民);
            RoyalEducationRules.BornRoyal(wise, up);
            RoyalEducationRules.TickEducation(up, Tutor(90), Tutor(80), 12f); // 満教育
            RoyalEducationRules.Mature(wise, up);
            Assert.IsTrue(up.matured);
            Assert.AreEqual(90, wise.leadership);   // 素養どおり
            Assert.AreEqual(95, wise.operation);
            // 二度目の成人は無効（一度きり）。
            RoyalEducationRules.Mature(wise, up);
            Assert.AreEqual(90, wise.leadership);

            // 放埒に育った暗君（師なし＝無教育）：素養があっても下限止まり。
            var up2 = new RoyalUpbringing(796, 90, 70, 60, 60, 95, 90);
            var fool = new Person(12, "暗君", Faction.帝国, PersonRole.文民);
            RoyalEducationRules.BornRoyal(fool, up2);
            RoyalEducationRules.TickEducation(up2, null, null, 12f); // 師なし
            RoyalEducationRules.Mature(fool, up2);
            Assert.AreEqual(45, fool.leadership);   // 素養90でも教育されねば半分
            Assert.AreEqual(48, fool.operation);    // 95→round(47.5)=48
            // 同じ素養でも、教育で別人の器になる。
            Assert.Greater(wise.leadership, fool.leadership);
        }
    }
}
