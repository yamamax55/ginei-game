using System.Collections.Generic;
using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 省庁ツリーと官僚配属（GOV-5 #158）を固定する：親子付け替え（単一親・循環防止）、配属の定員順守・単一所属、
    /// 異動、再帰集計、臨時省庁の新設・廃止（配属の再配置）、縦割りの横断政策フリクション。
    /// </summary>
    public class MinistryRulesTests
    {
        private static List<Ministry> Gov()
        {
            return new List<Ministry>
            {
                new Ministry(1, "内務省", OfficeDomain.内政) { staffSlots = 4 },
                new Ministry(2, "警察庁", OfficeDomain.内政) { staffSlots = 4 },
                new Ministry(3, "財務省", OfficeDomain.財政) { staffSlots = 4 },
            };
        }

        [Test]
        public void AttachChild_BuildsTree_SingleParent()
        {
            var g = Gov();
            Assert.IsTrue(MinistryRules.AttachChild(g, 1, 2)); // 警察庁 ⊂ 内務省
            Assert.AreEqual(1, MinistryRules.Get(g, 2).parentId);
            Assert.Contains(2, MinistryRules.Get(g, 1).childIds);

            // 別の親へ付け替え＝旧親から外れる
            Assert.IsTrue(MinistryRules.AttachChild(g, 3, 2));
            Assert.AreEqual(3, MinistryRules.Get(g, 2).parentId);
            Assert.IsFalse(MinistryRules.Get(g, 1).childIds.Contains(2));
        }

        [Test]
        public void AttachChild_RejectsCycle()
        {
            var g = Gov();
            MinistryRules.AttachChild(g, 1, 2); // 2 ⊂ 1
            Assert.IsFalse(MinistryRules.AttachChild(g, 2, 1)); // 1 を 2 の子に＝循環
        }

        [Test]
        public void AssignOfficial_RespectsSlots_AndSingleMembership()
        {
            var g = Gov();
            var small = MinistryRules.Get(g, 1);
            small.staffSlots = 1;

            Assert.IsTrue(MinistryRules.AssignOfficial(g, 1, 10));
            Assert.IsFalse(MinistryRules.AssignOfficial(g, 1, 11)); // 定員1で満杯

            // 単一所属：別省庁へ配属すると元から外れる
            Assert.IsTrue(MinistryRules.AssignOfficial(g, 3, 10));
            Assert.IsFalse(MinistryRules.Get(g, 1).staffIds.Contains(10));
            Assert.Contains(10, MinistryRules.Get(g, 3).staffIds);
        }

        [Test]
        public void Transfer_MovesOfficialBetweenMinistries()
        {
            var g = Gov();
            MinistryRules.AssignOfficial(g, 1, 10);
            Assert.IsTrue(MinistryRules.Transfer(g, 1, 3, 10));
            Assert.IsFalse(MinistryRules.Get(g, 1).staffIds.Contains(10));
            Assert.Contains(10, MinistryRules.Get(g, 3).staffIds);
        }

        [Test]
        public void AllOfficialsUnder_RecursesTree()
        {
            var g = Gov();
            MinistryRules.AttachChild(g, 1, 2); // 警察庁 ⊂ 内務省
            MinistryRules.AssignOfficial(g, 1, 10);
            MinistryRules.AssignOfficial(g, 2, 20);

            var under = MinistryRules.AllOfficialsUnder(g, 1);
            Assert.AreEqual(2, under.Count);
            Assert.Contains(10, under);
            Assert.Contains(20, under);
        }

        [Test]
        public void CreateTemporary_ThenDissolve_ReassignsStaff()
        {
            var g = Gov();
            var temp = MinistryRules.CreateTemporary(g, 99, "軍需省", OfficeDomain.財政);
            Assert.IsNotNull(temp);
            Assert.IsTrue(temp.isTemporary);
            MinistryRules.AssignOfficial(g, 99, 10);

            // 廃止＝官僚を財務省へ再配置
            Assert.IsTrue(MinistryRules.Dissolve(g, 99, reassignToId: 3));
            Assert.IsNull(MinistryRules.Get(g, 99));
            Assert.Contains(10, MinistryRules.Get(g, 3).staffIds);
        }

        [Test]
        public void SectionalismFriction_AveragesInstitutionalInterest()
        {
            var g = Gov();
            MinistryRules.Get(g, 1).institutionalInterest = 0.8f;
            MinistryRules.Get(g, 3).institutionalInterest = 0.4f;
            float friction = MinistryRules.SectionalismFriction(g, new List<int> { 1, 3 });
            Assert.AreEqual(0.6f, friction, 1e-4f); // 横断政策への平均抵抗
        }
    }
}
