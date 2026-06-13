using System;
using System.Collections.Generic;
using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 出産（結婚と出産システム基盤）を固定する：父（男性）母（女性）から子の Person が生まれ、能力は両親と相関しつつばらつく。
    /// <b>倫理ガード：優生学NG</b>＝出産可否は能力を参照せず、子を能力で間引く API も無い。子の能力は平均回帰＋乱数で
    /// 決まり、有能な親同士でも上限に届かず（ラチェット無し）、きょうだいで違う。
    /// </summary>
    public class ChildbirthRulesTests
    {
        const int BirthYear = 805;

        static Person Father(int id, int stat)
            => new Person(id, "父", Faction.帝国, PersonRole.軍人)
            { sex = Sex.男性, birthYear = BirthYear - 30,
              leadership = stat, attack = stat, defense = stat, mobility = stat, operation = stat, intelligence = stat };

        static Person Mother(int id, int stat)
            => new Person(id, "母", Faction.同盟, PersonRole.軍人)
            { sex = Sex.女性, birthYear = BirthYear - 25,
              leadership = stat, attack = stat, defense = stat, mobility = stat, operation = stat, intelligence = stat };

        static Func<float> Const(float v) => () => v;

        [Test]
        public void Conceive_ChildInheritsLineageAndPaternalFaction()
        {
            var f = Father(1, 60);
            var m = Mother(2, 60);
            var child = ChildbirthRules.Conceive(f, m, childId: 100, birthYear: BirthYear, sexRoll: 0.2f, roll: Const(0.5f));
            Assert.IsNotNull(child);
            Assert.AreEqual(1, child.fatherId);
            Assert.AreEqual(2, child.motherId);
            Assert.AreEqual(Faction.帝国, child.faction); // 父系
            Assert.AreEqual(Sex.男性, child.sex);          // sexRoll<0.5
            Assert.AreEqual(BirthYear, child.birthYear);
        }

        [Test]
        public void Ability_CorrelatesButRegresses_NoEugenicRatchet()
        {
            // 親が二人とも全能力100でも、子は平均回帰で75（無ノイズ roll=0.5）＝100には届かない
            var child = ChildbirthRules.Conceive(Father(1, 100), Mother(2, 100), 100, BirthYear, 0.6f, Const(0.5f));
            Assert.IsNotNull(child);
            Assert.AreEqual(75, child.leadership);
            Assert.AreEqual(75, child.intelligence);
            Assert.Less(child.attack, 100); // 上限に届かない＝品種改良のラチェットが効かない
        }

        [Test]
        public void Siblings_VaryByRoll()
        {
            // 能力ごとに独立な乱数を引く＝同じ親でもきょうだいで能力が散る（leadership..production の順に roll を消費）
            var seq = new Queue<float>(new[] { 0f, 1f, 0f, 1f, 0f, 1f, 0f, 1f, 0f, 1f });
            Func<float> roll = () => seq.Dequeue();
            var child = ChildbirthRules.Conceive(Father(1, 100), Mother(2, 100), 100, BirthYear, 0.6f, roll);
            Assert.IsNotNull(child);
            Assert.AreEqual(63, child.leadership); // roll=0 → 75-12
            Assert.AreEqual(87, child.attack);     // roll=1 → 75+12
            Assert.AreEqual(63, child.defense);
            Assert.AreNotEqual(child.leadership, child.attack); // きょうだい/能力でばらつく
        }

        [Test]
        public void NoAbilityGate_LowStatParentsStillBear()
        {
            // 能力で出産を選別しない＝全能力0の両親でも必ず子が生まれる（優生学NG）
            var child = ChildbirthRules.Conceive(Father(1, 0), Mother(2, 0), 100, BirthYear, 0.2f, Const(1f));
            Assert.IsNotNull(child);
            Assert.Greater(child.leadership, 0); // 上振れで親(0)を超える子もありうる
        }

        [Test]
        public void CanConceive_RequiresOppositeSex_AndChildbearingAge()
        {
            Assert.IsTrue(ChildbirthRules.CanConceive(Father(1, 50), Mother(2, 50), BirthYear));
            // 同性は出産不可（生物学的）
            Assert.IsFalse(ChildbirthRules.CanConceive(Father(1, 50), Father(2, 50), BirthYear));
            // 母が出産可能年齢を外れる（65歳）
            var oldMother = new Person(3, "母", Faction.同盟, PersonRole.軍人) { sex = Sex.女性, birthYear = BirthYear - 65 };
            Assert.IsFalse(ChildbirthRules.CanConceive(Father(1, 50), oldMother, BirthYear));
            Assert.IsNull(ChildbirthRules.Conceive(Father(1, 50), oldMother, 100, BirthYear, 0.5f, Const(0.5f)));
        }

        [Test]
        public void CloseKinParents_CannotConceive()
        {
            var f = Father(1, 50);
            var sister = new Person(2, "妹", Faction.帝国, PersonRole.軍人)
            { sex = Sex.女性, birthYear = BirthYear - 24 };
            // 同じ父(9)を持つきょうだい＝近親
            f.fatherId = 9;
            sister.fatherId = 9;
            Assert.IsFalse(ChildbirthRules.CanConceive(f, sister, BirthYear));
            Assert.IsNull(ChildbirthRules.Conceive(f, sister, 100, BirthYear, 0.5f, Const(0.5f)));
        }

        [Test]
        public void NullSafe()
        {
            Assert.IsFalse(ChildbirthRules.CanConceive(null, Mother(2, 50), BirthYear));
            Assert.IsNull(ChildbirthRules.Conceive(null, Mother(2, 50), 1, BirthYear, 0.5f, null));
        }
    }
}
