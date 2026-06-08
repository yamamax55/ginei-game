using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 編制ツリー（#147 軍団システム）の純ロジックを固定する：
    /// 司令部固定・中身流動（艦隊/下位梯団の attach/detach・単一所属）／梯団別の司令配属（階級ゲート #14）／
    /// 配下集計（ツリー再帰）／循環防止／勢力独立。
    /// </summary>
    public class OrderOfBattleTests
    {
        private static AdmiralData Admiral(int tier)
        {
            var a = ScriptableObject.CreateInstance<AdmiralData>();
            a.admiralName = "提督"; a.rankTier = tier;
            return a;
        }

        [SetUp]
        public void Reset() => OrderOfBattle.Clear();

        [Test]
        public void Create_AssignsUniqueIds()
        {
            var a = OrderOfBattle.Create(EchelonType.軍団, Faction.帝国);
            var b = OrderOfBattle.Create(EchelonType.軍集団, Faction.帝国);
            Assert.AreNotEqual(a.id, b.id);
            Assert.AreSame(a, OrderOfBattle.Get(a.id));
        }

        [Test]
        public void AttachDetachFleet()
        {
            var corps = OrderOfBattle.Create(EchelonType.軍団, Faction.同盟);
            Assert.IsTrue(OrderOfBattle.AttachFleet(corps.id, 13));
            Assert.Contains(13, corps.fleetNumbers);
            Assert.IsTrue(OrderOfBattle.DetachFleet(corps.id, 13));
            Assert.IsFalse(corps.fleetNumbers.Contains(13));
        }

        [Test]
        public void Fleet_SingleMembership_MovesBetweenCorps()
        {
            var a = OrderOfBattle.Create(EchelonType.軍団, Faction.帝国);
            var b = OrderOfBattle.Create(EchelonType.軍団, Faction.帝国);
            OrderOfBattle.AttachFleet(a.id, 5);
            OrderOfBattle.AttachFleet(b.id, 5); // 中身流動：a から b へ移る（単一所属）
            Assert.IsFalse(a.fleetNumbers.Contains(5));
            Assert.Contains(5, b.fleetNumbers);
        }

        [Test]
        public void Tree_AggregatesFleetsAcrossEchelons()
        {
            var group = OrderOfBattle.Create(EchelonType.軍集団, Faction.帝国);
            var corps1 = OrderOfBattle.Create(EchelonType.軍団, Faction.帝国);
            var corps2 = OrderOfBattle.Create(EchelonType.軍団, Faction.帝国);
            Assert.IsTrue(OrderOfBattle.AttachFormation(group.id, corps1.id));
            Assert.IsTrue(OrderOfBattle.AttachFormation(group.id, corps2.id));
            OrderOfBattle.AttachFleet(corps1.id, 1);
            OrderOfBattle.AttachFleet(corps1.id, 2);
            OrderOfBattle.AttachFleet(corps2.id, 3);

            var all = OrderOfBattle.AllFleetNumbersUnder(group.id);
            Assert.AreEqual(3, all.Count);
            Assert.IsTrue(all.Contains(1) && all.Contains(2) && all.Contains(3));
            Assert.AreEqual(3, OrderOfBattle.CountFleetsUnder(group.id));
            Assert.AreEqual(group.id, corps1.parentId);
        }

        [Test]
        public void AttachFormation_PreventsCycle()
        {
            var g = OrderOfBattle.Create(EchelonType.軍集団, Faction.帝国);
            var c = OrderOfBattle.Create(EchelonType.軍団, Faction.帝国);
            Assert.IsTrue(OrderOfBattle.AttachFormation(g.id, c.id));
            // c の下に g を付けると循環 → 拒否
            Assert.IsFalse(OrderOfBattle.AttachFormation(c.id, g.id));
        }

        [Test]
        public void Commander_RankGate_PerEchelon()
        {
            Assert.AreEqual(7, OrderOfBattle.RequiredTier(EchelonType.艦隊));   // 中将
            Assert.AreEqual(8, OrderOfBattle.RequiredTier(EchelonType.軍団));   // 大将
            Assert.AreEqual(10, OrderOfBattle.RequiredTier(EchelonType.軍集団)); // 元帥

            var corps = OrderOfBattle.Create(EchelonType.軍団, Faction.帝国);
            Assert.IsFalse(OrderOfBattle.AssignCommander(corps.id, Admiral(7))); // 中将では軍団を持てない
            Assert.IsTrue(OrderOfBattle.AssignCommander(corps.id, Admiral(8)));  // 大将ならOK
            Assert.IsTrue(corps.HasCommander);

            var group = OrderOfBattle.Create(EchelonType.軍集団, Faction.帝国);
            Assert.IsFalse(OrderOfBattle.AssignCommander(group.id, Admiral(8)));  // 大将では軍集団を持てない
            Assert.IsTrue(OrderOfBattle.AssignCommander(group.id, Admiral(10)));  // 元帥ならOK

            Assert.IsFalse(OrderOfBattle.AssignCommander(corps.id, null));
        }

        [Test]
        public void DetachFormation_ClearsParent()
        {
            var g = OrderOfBattle.Create(EchelonType.軍集団, Faction.帝国);
            var c = OrderOfBattle.Create(EchelonType.軍団, Faction.帝国);
            OrderOfBattle.AttachFormation(g.id, c.id);
            Assert.IsTrue(OrderOfBattle.DetachFormation(g.id, c.id));
            Assert.AreEqual(0, c.parentId);
            Assert.IsFalse(g.childFormationIds.Contains(c.id));
        }

        [Test]
        public void DisplayName_FallsBackToEchelonAndId()
        {
            var c = OrderOfBattle.Create(EchelonType.軍団, Faction.帝国);
            Assert.AreEqual($"軍団#{c.id}", c.DisplayName);
            var named = OrderOfBattle.Create(EchelonType.軍集団, Faction.帝国, "ローエングラム軍集団");
            Assert.AreEqual("ローエングラム軍集団", named.DisplayName);
        }

        [Test]
        public void Fleet_NumberSpace_IsPerFaction_AtFormationLevel()
        {
            var imp = OrderOfBattle.Create(EchelonType.軍団, Faction.帝国);
            var all = OrderOfBattle.Create(EchelonType.軍団, Faction.同盟);
            OrderOfBattle.AttachFleet(imp.id, 1);
            OrderOfBattle.AttachFleet(all.id, 1); // 同盟の第1艦隊は帝国の第1艦隊と別物（単一所属は同勢力のみ）
            Assert.Contains(1, imp.fleetNumbers);
            Assert.Contains(1, all.fleetNumbers);
        }
    }
}
