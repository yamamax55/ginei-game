using NUnit.Framework;
using System.Collections.Generic;
using UnityEngine;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>配下艦スロット割当（EMOV-1/2）：席替え交差の解消＋艦種を前面/側面へ寄せる。</summary>
    public class EscortSlotAssignmentRulesTests
    {
        [Test]
        public void Assign_AlreadyAtSlots_IsIdentity()
        {
            var slots = new List<Vector2> { new Vector2(0, 1), new Vector2(0, -1), new Vector2(1, 0) };
            var pos = new List<Vector2>(slots);
            int[] a = EscortSlotAssignmentRules.Assign(pos, slots);
            Assert.AreEqual(new[] { 0, 1, 2 }, a); // 既にスロット上＝動かない
        }

        [Test]
        public void Assign_SwappedPositions_PicksNearest_NoCross()
        {
            // member0 はスロット1側に、member1 はスロット0側にいる＝近い方へ割り当て＝交差しない。
            var slots = new List<Vector2> { new Vector2(-1, 0), new Vector2(1, 0) };
            var pos = new List<Vector2> { new Vector2(0.9f, 0), new Vector2(-0.9f, 0) };
            int[] a = EscortSlotAssignmentRules.Assign(pos, slots);
            Assert.AreEqual(1, a[0]);
            Assert.AreEqual(0, a[1]);
        }

        [Test]
        public void Assign_MoreMembersThanSlots_LeavesUnassigned()
        {
            var slots = new List<Vector2> { new Vector2(0, 0), new Vector2(2, 0) };
            var pos = new List<Vector2> { new Vector2(0, 0), new Vector2(2, 0), new Vector2(4, 0) };
            int[] a = EscortSlotAssignmentRules.Assign(pos, slots);
            Assert.AreEqual(0, a[0]);
            Assert.AreEqual(1, a[1]);
            Assert.AreEqual(-1, a[2]); // 余りは未割当
        }

        [Test]
        public void Assign_FewerMembersThanSlots_AllAssignedDistinct()
        {
            var slots = new List<Vector2> { new Vector2(0, 0), new Vector2(2, 0), new Vector2(4, 0) };
            var pos = new List<Vector2> { new Vector2(3.9f, 0), new Vector2(0.1f, 0) };
            int[] a = EscortSlotAssignmentRules.Assign(pos, slots);
            Assert.AreEqual(2, a[0]);
            Assert.AreEqual(0, a[1]);
            Assert.AreNotEqual(a[0], a[1]);
        }

        [Test]
        public void PreferredClassForSlot_FrontOuter_Battleship()
        {
            Assert.AreEqual(ShipClass.戦艦, EscortSlotAssignmentRules.PreferredClassForSlot(new Vector2(0, 3), 3f));
        }

        [Test]
        public void PreferredClassForSlot_FlankOrRear_Destroyer()
        {
            Assert.AreEqual(ShipClass.駆逐艦, EscortSlotAssignmentRules.PreferredClassForSlot(new Vector2(3, 0), 3f));   // 側面
            Assert.AreEqual(ShipClass.駆逐艦, EscortSlotAssignmentRules.PreferredClassForSlot(new Vector2(0, -3), 3f));  // 後方
        }

        [Test]
        public void PreferredClassForSlot_Center_Cruiser()
        {
            Assert.AreEqual(ShipClass.巡航艦, EscortSlotAssignmentRules.PreferredClassForSlot(new Vector2(0, 0), 3f));
        }

        [Test]
        public void AssignWithClass_BiasesClassesToZones()
        {
            // 全員が原点（生成直後）＝距離はスロット位置に等しい。艦種重みで配置を決める。
            var slots = new List<Vector2> { new Vector2(0, 3), new Vector2(0, 0), new Vector2(3, 0) };
            var pos = new List<Vector2> { Vector2.zero, Vector2.zero, Vector2.zero };
            var classes = new List<ShipClass> { ShipClass.戦艦, ShipClass.巡航艦, ShipClass.駆逐艦 };
            int[] a = EscortSlotAssignmentRules.AssignWithClass(pos, slots, classes, 100f);
            Assert.AreEqual(0, a[0]); // 戦艦→前面/外周
            Assert.AreEqual(1, a[1]); // 巡航艦→中核
            Assert.AreEqual(2, a[2]); // 駆逐艦→側面
        }

        [Test]
        public void AssignWithClass_ZeroBias_IsValidPermutation()
        {
            var slots = new List<Vector2> { new Vector2(0, 1), new Vector2(0, -1), new Vector2(1, 0) };
            var pos = new List<Vector2>(slots);
            var classes = new List<ShipClass> { ShipClass.戦艦, ShipClass.巡航艦, ShipClass.駆逐艦 };
            int[] a = EscortSlotAssignmentRules.AssignWithClass(pos, slots, classes, 0f);
            CollectionAssert.AreEquivalent(new[] { 0, 1, 2 }, a); // 全スロットが1回ずつ使われる
        }
    }
}
