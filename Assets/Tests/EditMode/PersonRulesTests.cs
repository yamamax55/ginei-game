using System.Collections.Generic;
using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 人物システム（軍人/文民・適材適所＝正名 #866）を固定する：軍才/文才の算出、役割と役職の一致判定、
    /// ミスマッチによる実効力の減衰、勢力内での最適配属。
    /// </summary>
    public class PersonRulesTests
    {
        private static Person Soldier(int leadership, int attack, int defense, int mobility, Faction f = Faction.帝国)
        {
            var p = new Person(1, "軍", f, PersonRole.軍人);
            p.leadership = leadership; p.attack = attack; p.defense = defense; p.mobility = mobility;
            return p;
        }

        private static Person Civilian(int operation, int intelligence, Faction f = Faction.帝国)
        {
            var p = new Person(2, "文", f, PersonRole.文民);
            p.operation = operation; p.intelligence = intelligence;
            return p;
        }

        [Test]
        public void MilitaryAptitude_AveragesFourMartialStats()
        {
            var p = Soldier(80, 60, 40, 20);
            Assert.AreEqual(50f, p.MilitaryAptitude, 1e-4f); // (80+60+40+20)/4
        }

        [Test]
        public void CivilAptitude_AveragesTwoCivilStats()
        {
            var p = Civilian(70, 30);
            Assert.AreEqual(50f, p.CivilAptitude, 1e-4f); // (70+30)/2
        }

        [Test]
        public void RoleMatches_SoldierToMilitary_CivilianToCivil()
        {
            var soldier = Soldier(50, 50, 50, 50);
            var civilian = Civilian(50, 50);

            Assert.IsTrue(PersonRules.RoleMatches(soldier, PostType.軍務));
            Assert.IsFalse(PersonRules.RoleMatches(soldier, PostType.政務));
            Assert.IsTrue(PersonRules.RoleMatches(civilian, PostType.政務));
            Assert.IsFalse(PersonRules.RoleMatches(civilian, PostType.軍務));
        }

        [Test]
        public void Effectiveness_FullWhenMatched()
        {
            var soldier = Soldier(80, 80, 80, 80); // 軍才80
            Assert.AreEqual(80f, PersonRules.Effectiveness(soldier, PostType.軍務), 1e-4f);
        }

        [Test]
        public void Effectiveness_PenalizedWhenMismatched()
        {
            // 軍人を政務に就ける＝文才80でも既定ペナルティ0.5で半減
            var soldier = Soldier(50, 50, 50, 50);
            soldier.operation = 80; soldier.intelligence = 80; // 文才80（だが軍人）
            Assert.AreEqual(40f, PersonRules.Effectiveness(soldier, PostType.政務), 1e-4f);
        }

        [Test]
        public void BestFor_PicksHighestEffectiveAptitude_RespectingRole()
        {
            // 政務(文民↔政務)。文民Bは文才低めだが一致＝満額／軍人Aは文才高いがミスマッチ＝半減
            var soldierA = Soldier(0, 0, 0, 0);
            soldierA.operation = 100; soldierA.intelligence = 100; // 文才100×0.5=50
            var civilianB = Civilian(70, 70);                      // 文才70×1.0=70

            var pool = new List<Person> { soldierA, civilianB };
            var best = PersonRules.BestFor(pool, Faction.帝国, PostType.政務);
            Assert.AreSame(civilianB, best); // 適材適所＝役割の合う文民が勝つ
        }

        [Test]
        public void BestFor_FiltersByFaction()
        {
            var imp = Soldier(90, 90, 90, 90, Faction.帝国);
            var ally = Soldier(50, 50, 50, 50, Faction.同盟);
            var pool = new List<Person> { imp, ally };

            Assert.AreSame(ally, PersonRules.BestFor(pool, Faction.同盟, PostType.軍務));
        }

        [Test]
        public void BestFor_NullWhenNoCandidate()
        {
            var pool = new List<Person> { Soldier(50, 50, 50, 50, Faction.帝国) };
            Assert.IsNull(PersonRules.BestFor(pool, Faction.同盟, PostType.軍務));
        }
    }
}
