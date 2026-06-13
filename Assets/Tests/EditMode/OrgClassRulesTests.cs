using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 戦略/作戦/戦術単位の区分（ORBAT-4 #1720）を固定する：梯団種別→区分の導出、戦術単位の継戦依存
    /// （上級配属が要る）、自己完結（戦略/作戦）、継戦ペナルティ倍率（実効値パターン）。
    /// </summary>
    public class OrgClassRulesTests
    {
        [Test]
        public void ClassOf_DerivesFromEchelon()
        {
            Assert.AreEqual(UnitEchelonClass.戦術, OrgClassRules.ClassOf(EchelonType.戦隊));
            Assert.AreEqual(UnitEchelonClass.戦術, OrgClassRules.ClassOf(EchelonType.分艦隊));
            Assert.AreEqual(UnitEchelonClass.作戦, OrgClassRules.ClassOf(EchelonType.艦隊));
            Assert.AreEqual(UnitEchelonClass.作戦, OrgClassRules.ClassOf(EchelonType.軍団));
            Assert.AreEqual(UnitEchelonClass.戦略, OrgClassRules.ClassOf(EchelonType.軍));
            Assert.AreEqual(UnitEchelonClass.戦略, OrgClassRules.ClassOf(EchelonType.軍集団));
            Assert.AreEqual(UnitEchelonClass.戦略, OrgClassRules.ClassOf(EchelonType.宇宙艦隊));
        }

        [Test]
        public void SelfSufficiency_TacticalNeedsParent()
        {
            // 戦術単位（戦隊/分艦隊）は自己完結しない＝上級配属が要る
            Assert.IsFalse(OrgClassRules.IsSelfSufficient(EchelonType.分艦隊));
            Assert.IsTrue(OrgClassRules.RequiresParentForSustainment(EchelonType.分艦隊));
            // 作戦・戦略単位は自己完結＝親不要
            Assert.IsTrue(OrgClassRules.IsSelfSufficient(EchelonType.艦隊));
            Assert.IsTrue(OrgClassRules.IsSelfSufficient(EchelonType.軍集団));
            Assert.IsFalse(OrgClassRules.RequiresParentForSustainment(EchelonType.艦隊));
        }

        [Test]
        public void CanSustain_TacticalUnitRequiresParent()
        {
            // 戦術単位：親なしは継戦不可・親ありで継戦可
            Assert.IsFalse(OrgClassRules.CanSustain(EchelonType.分艦隊, hasParentFormation: false));
            Assert.IsTrue(OrgClassRules.CanSustain(EchelonType.分艦隊, hasParentFormation: true));
            // 作戦/戦略単位：親の有無によらず継戦可（自己完結）
            Assert.IsTrue(OrgClassRules.CanSustain(EchelonType.艦隊, hasParentFormation: false));
            Assert.IsTrue(OrgClassRules.CanSustain(EchelonType.軍, hasParentFormation: false));
        }

        [Test]
        public void SustainmentFactor_PenalizesIsolatedTactical()
        {
            // 孤立した戦術単位だけ実効ペナルティ（0.5）。賄えていれば 1.0。
            Assert.AreEqual(OrgClassRules.UnsustainedPenaltyFactor,
                            OrgClassRules.SustainmentFactor(EchelonType.戦隊, hasParentFormation: false), 1e-6f);
            Assert.AreEqual(1f, OrgClassRules.SustainmentFactor(EchelonType.戦隊, hasParentFormation: true), 1e-6f);
            Assert.AreEqual(1f, OrgClassRules.SustainmentFactor(EchelonType.艦隊, hasParentFormation: false), 1e-6f);
        }

        [Test]
        public void Overloads_ReadFormationParentId()
        {
            // parentId==0（親なし）の戦術単位は継戦不可、parentId!=0 で継戦可
            var orphan = new MilitaryFormation { echelon = EchelonType.分艦隊, parentId = 0 };
            var attached = new MilitaryFormation { echelon = EchelonType.分艦隊, parentId = 42 };
            Assert.IsFalse(OrgClassRules.CanSustain(orphan));
            Assert.IsTrue(OrgClassRules.CanSustain(attached));
            Assert.AreEqual(OrgClassRules.UnsustainedPenaltyFactor, OrgClassRules.SustainmentFactor(orphan), 1e-6f);
            Assert.AreEqual(1f, OrgClassRules.SustainmentFactor(attached), 1e-6f);
            // null 安全
            Assert.IsFalse(OrgClassRules.CanSustain((MilitaryFormation)null));
            Assert.AreEqual(1f, OrgClassRules.SustainmentFactor((MilitaryFormation)null), 1e-6f);
        }
    }
}
