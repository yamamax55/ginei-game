using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 出自（採用「出自選択」・<see cref="OriginRules"/>）の分類を固定する：平民/貴族/王家＝身分と継承の入口。
    /// 効果は既存（封建#168/王室#188/継承#646）へ接続のみ＝本ルールは分類のみ。
    /// </summary>
    public class OriginRulesTests
    {
        [Test]
        public void Default_IsCommoner()
        {
            Assert.AreEqual(PersonOrigin.平民, OriginRules.Default);
            Assert.IsTrue(OriginRules.IsCommoner(OriginRules.Default));
        }

        [Test]
        public void Noble_Classification()
        {
            Assert.IsFalse(OriginRules.IsNoble(PersonOrigin.平民));
            Assert.IsTrue(OriginRules.IsNoble(PersonOrigin.貴族));
            Assert.IsTrue(OriginRules.IsNoble(PersonOrigin.王家));
        }

        [Test]
        public void Royal_OnlyKingHouse()
        {
            Assert.IsFalse(OriginRules.IsRoyal(PersonOrigin.平民));
            Assert.IsFalse(OriginRules.IsRoyal(PersonOrigin.貴族));
            Assert.IsTrue(OriginRules.IsRoyal(PersonOrigin.王家));
        }

        [Test]
        public void InheritsOnDeath_NobleAndRoyal()
        {
            // #907 の解答：平民は一代記完結→新規、貴族/王家は継承#646 で世継ぎへ
            Assert.IsFalse(OriginRules.InheritsOnDeath(PersonOrigin.平民));
            Assert.IsTrue(OriginRules.InheritsOnDeath(PersonOrigin.貴族));
            Assert.IsTrue(OriginRules.InheritsOnDeath(PersonOrigin.王家));
        }

        [Test]
        public void EnrollPath_Mapping()
        {
            Assert.AreEqual(EnrollPath.士官学校, OriginRules.PathFor(PersonOrigin.平民));
            Assert.AreEqual(EnrollPath.幼年学校, OriginRules.PathFor(PersonOrigin.貴族));
            Assert.AreEqual(EnrollPath.王室教育, OriginRules.PathFor(PersonOrigin.王家));
        }

        [Test]
        public void SchoolName_NonEmpty()
        {
            Assert.IsNotEmpty(OriginRules.SchoolName(EnrollPath.士官学校));
            Assert.IsNotEmpty(OriginRules.SchoolName(EnrollPath.幼年学校));
            Assert.IsNotEmpty(OriginRules.SchoolName(EnrollPath.王室教育));
        }

        [Test]
        public void CommissionFloor_NobleAndRoyalStartHigher()
        {
            Assert.AreEqual(0, OriginRules.CommissionFloor(PersonOrigin.平民));
            Assert.AreEqual(3, OriginRules.CommissionFloor(PersonOrigin.貴族));
            Assert.AreEqual(4, OriginRules.CommissionFloor(PersonOrigin.王家));
        }

        [Test]
        public void Title_NonEmpty()
        {
            Assert.IsNotEmpty(OriginRules.Title(PersonOrigin.平民));
            Assert.IsNotEmpty(OriginRules.Title(PersonOrigin.貴族));
            Assert.IsNotEmpty(OriginRules.Title(PersonOrigin.王家));
        }
    }
}
