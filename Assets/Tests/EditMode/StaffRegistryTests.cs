using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>参謀本部台帳：部隊参謀（艦隊長〜軍団長のみ）・大本営参謀本部（勢力ごと1つ）・配属・クリア。</summary>
    public class StaffRegistryTests
    {
        [TearDown]
        public void Cleanup() => StaffRegistry.Clear();

        [Test]
        public void FieldStaff_OnlyForFleetToCorps()
        {
            var s = StaffRegistry.GetOrCreateFieldStaff(Faction.帝国, EchelonType.艦隊, "帝国/第1艦隊");
            Assert.IsNotNull(s);
            Assert.AreEqual(StaffLevel.部隊参謀, s.level);
            // 同じ鍵は同じ Staff を返す（冪等）
            Assert.AreSame(s, StaffRegistry.GetOrCreateFieldStaff(Faction.帝国, EchelonType.艦隊, "帝国/第1艦隊"));
            // 対象外梯団は作らない
            Assert.IsNull(StaffRegistry.GetOrCreateFieldStaff(Faction.帝国, EchelonType.分艦隊, "帝国/第1分艦隊"));
            Assert.IsNull(StaffRegistry.GetOrCreateFieldStaff(Faction.帝国, EchelonType.軍, "帝国/第1軍"));
            Assert.AreEqual(1, StaffRegistry.FieldStaffCount);
        }

        [Test]
        public void GeneralStaff_OnePerFaction()
        {
            var g1 = StaffRegistry.GetOrCreateGeneralStaff(Faction.帝国);
            Assert.AreEqual(StaffLevel.大本営参謀本部, g1.level);
            Assert.AreSame(g1, StaffRegistry.GetOrCreateGeneralStaff(Faction.帝国));
            var g2 = StaffRegistry.GetOrCreateGeneralStaff(Faction.同盟);
            Assert.AreNotSame(g1, g2);
            Assert.AreEqual(2, StaffRegistry.GeneralStaffCount);
            Assert.IsNotNull(StaffRegistry.GeneralStaff(Faction.帝国)); // 取得できる
        }

        [Test]
        public void Staff_AssignSectionsAndChief()
        {
            var s = StaffRegistry.GetOrCreateFieldStaff(Faction.帝国, EchelonType.軍団, "帝国/第1軍団");
            s.chiefOfStaffId = 100;
            s.Assign(StaffSection.作戦, 201);
            s.Assign(StaffSection.情報, 202);
            Assert.IsTrue(s.HasChief);
            Assert.AreEqual(201, s.Officer(StaffSection.作戦));
            Assert.IsTrue(s.HasSection(StaffSection.情報));
            Assert.IsFalse(s.HasSection(StaffSection.兵站));      // 未配属
            Assert.AreEqual(-1, s.Officer(StaffSection.兵站));
            Assert.AreEqual(2, s.FilledSections);
            // 解任
            s.Assign(StaffSection.作戦, -1);
            Assert.IsFalse(s.HasSection(StaffSection.作戦));
            Assert.AreEqual(1, s.FilledSections);
        }

        [Test]
        public void Clear_EmptiesAll()
        {
            StaffRegistry.GetOrCreateFieldStaff(Faction.帝国, EchelonType.艦隊, "帝国/第1艦隊");
            StaffRegistry.GetOrCreateGeneralStaff(Faction.帝国);
            StaffRegistry.Clear();
            Assert.AreEqual(0, StaffRegistry.FieldStaffCount);
            Assert.AreEqual(0, StaffRegistry.GeneralStaffCount);
            Assert.IsNull(StaffRegistry.GetFieldStaff("帝国/第1艦隊"));
        }
    }
}
