using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 人物の結婚（結婚と出産システム基盤）を固定する：別人・存命自由・独身・成年・非近親なら結婚でき、相互に配偶者が結ばれる。
    /// <b>倫理ガード：能力/身分で結婚を縛らない（優生学NG）</b>＝低能力どうしでも結婚できる。近親婚・重婚・未成年は禁じる。
    /// </summary>
    public class PersonMarriageRulesTests
    {
        const int Year = 800;

        static Person Adult(int id, Sex sex = Sex.男性)
            => new Person(id, "P" + id, Faction.同盟, PersonRole.文民) { sex = sex, birthYear = Year - 25 };

        [Test]
        public void Marry_LinksSpouses()
        {
            var a = Adult(1, Sex.男性);
            var b = Adult(2, Sex.女性);
            Assert.IsTrue(PersonMarriageRules.CanMarry(a, b, Year));
            Assert.IsTrue(PersonMarriageRules.Marry(a, b, Year));
            Assert.AreEqual(2, a.spouseId);
            Assert.AreEqual(1, b.spouseId);
            Assert.IsTrue(PersonMarriageRules.AreMarried(a, b));
            Assert.IsFalse(PersonMarriageRules.IsSingle(a));
        }

        [Test]
        public void NoAbilityGate_LowStatCouplesMarry()
        {
            // 能力は一切参照しない＝全能力0の二人でも結婚できる（優生学的な結婚制限なし）
            var a = Adult(1, Sex.男性); // 既定で能力0
            var b = Adult(2, Sex.女性);
            Assert.IsTrue(PersonMarriageRules.CanMarry(a, b, Year));
        }

        [Test]
        public void Rejects_Bigamy_Underage_Self()
        {
            var a = Adult(1, Sex.男性);
            var b = Adult(2, Sex.女性);
            var c = Adult(3, Sex.女性);
            PersonMarriageRules.Marry(a, b, Year);
            Assert.IsFalse(PersonMarriageRules.CanMarry(a, c, Year)); // 重婚不可
            Assert.IsFalse(PersonMarriageRules.CanMarry(a, a, Year)); // 自分とは不可

            var child = new Person(4, "child", Faction.同盟, PersonRole.文民) { sex = Sex.女性, birthYear = Year - 10 }; // 10歳
            var d = Adult(5, Sex.男性);
            Assert.IsFalse(PersonMarriageRules.CanMarry(d, child, Year)); // 未成年不可
        }

        [Test]
        public void Rejects_CloseKin()
        {
            var parent = Adult(1, Sex.男性);
            var child = new Person(2, "child", Faction.同盟, PersonRole.文民) { sex = Sex.女性, birthYear = Year - 20, fatherId = 1 };
            Assert.IsTrue(PersonMarriageRules.AreCloseKin(parent, child));
            Assert.IsFalse(PersonMarriageRules.CanMarry(parent, child, Year)); // 親子婚不可

            var sibA = new Person(3, "a", Faction.同盟, PersonRole.文民) { sex = Sex.男性, birthYear = Year - 22, fatherId = 1 };
            var sibB = new Person(4, "b", Faction.同盟, PersonRole.文民) { sex = Sex.女性, birthYear = Year - 21, fatherId = 1 };
            Assert.IsTrue(PersonMarriageRules.AreCloseKin(sibA, sibB)); // 同じ父＝きょうだい
            Assert.IsFalse(PersonMarriageRules.CanMarry(sibA, sibB, Year));
        }

        [Test]
        public void DivorceAndWidow_ClearLinks()
        {
            var a = Adult(1, Sex.男性);
            var b = Adult(2, Sex.女性);
            PersonMarriageRules.Marry(a, b, Year);
            Assert.IsTrue(PersonMarriageRules.Divorce(a, b));
            Assert.AreEqual(-1, a.spouseId);
            Assert.AreEqual(-1, b.spouseId);
            Assert.IsFalse(PersonMarriageRules.Divorce(a, b)); // もう婚姻関係なし

            PersonMarriageRules.Marry(a, b, Year);
            PersonMarriageRules.Widow(a); // b が死亡→生存配偶者 a の婚姻解除
            Assert.AreEqual(-1, a.spouseId);
            Assert.IsTrue(PersonMarriageRules.IsSingle(a));
        }
    }
}
