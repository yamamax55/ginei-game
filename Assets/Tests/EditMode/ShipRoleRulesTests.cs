using System.Collections.Generic;
using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 役割の混成禁止ルール（#883）を固定する：運用区分の互換判定（戦闘/非戦闘）と、
    /// 梯団(#147)が戦闘艦隊と非戦闘艦隊を混成できないこと（`OrderOfBattle.AttachFleet` のゲート）。
    /// </summary>
    public class ShipRoleRulesTests
    {
        // ===== ShipRoleRules（純ロジック） =====

        [Test]
        public void AreCompatible_SameClassOnly()
        {
            Assert.IsTrue(ShipRoleRules.AreCompatible(ShipRole.戦闘艦, ShipRole.戦闘艦));
            Assert.IsTrue(ShipRoleRules.AreCompatible(ShipRole.偵察艦, ShipRole.輸送艦)); // 非戦闘同士OK
            Assert.IsFalse(ShipRoleRules.AreCompatible(ShipRole.戦闘艦, ShipRole.輸送艦)); // 混成NG
        }

        [Test]
        public void IsHomogeneous_DetectsMix()
        {
            Assert.IsTrue(ShipRoleRules.IsHomogeneous(new List<ShipRole> { ShipRole.戦闘艦, ShipRole.戦闘艦 }));
            Assert.IsTrue(ShipRoleRules.IsHomogeneous(new List<ShipRole> { ShipRole.偵察艦, ShipRole.輸送艦, ShipRole.入植艦 }));
            Assert.IsFalse(ShipRoleRules.IsHomogeneous(new List<ShipRole> { ShipRole.戦闘艦, ShipRole.輸送艦 }));
            Assert.IsTrue(ShipRoleRules.IsHomogeneous(new List<ShipRole>())); // 空は同質
        }

        [Test]
        public void CompatibleWithGroup_EmptyAcceptsAnything()
        {
            Assert.IsTrue(ShipRoleRules.CompatibleWithGroup(ShipRole.輸送艦, new List<ShipRole>()));
            Assert.IsTrue(ShipRoleRules.CompatibleWithGroup(ShipRole.戦闘艦, new List<ShipRole> { ShipRole.戦闘艦 }));
            Assert.IsFalse(ShipRoleRules.CompatibleWithGroup(ShipRole.輸送艦, new List<ShipRole> { ShipRole.戦闘艦 }));
        }

        // ===== OrderOfBattle のゲート（FleetRoster と連携） =====

        [SetUp]
        public void Reset() { OrderOfBattle.Clear(); FleetRoster.Clear(); }

        private static FleetUnitData Fleet(Faction f, int number, ShipRole role)
        {
            var u = FleetRoster.CreateFleet(f, number);
            u.shipRole = role;
            return u;
        }

        [Test]
        public void AttachFleet_AllowsSameClass()
        {
            Fleet(Faction.帝国, 1, ShipRole.戦闘艦);
            Fleet(Faction.帝国, 2, ShipRole.戦闘艦);
            var corps = OrderOfBattle.Create(EchelonType.軍団, Faction.帝国);

            Assert.IsTrue(OrderOfBattle.AttachFleet(corps.id, 1));
            Assert.IsTrue(OrderOfBattle.AttachFleet(corps.id, 2)); // 戦闘同士＝混成にならない
            Assert.AreEqual(2, OrderOfBattle.CountFleetsUnder(corps.id));
        }

        [Test]
        public void AttachFleet_AllowsCombinedArms()
        {
            // #883 混成禁止は撤回（ORBAT-3）＝諸兵科連合：戦闘艦隊＋非戦闘艦隊を同一梯団に混在できる
            Fleet(Faction.帝国, 1, ShipRole.戦闘艦);
            Fleet(Faction.帝国, 2, ShipRole.輸送艦);
            var corps = OrderOfBattle.Create(EchelonType.軍団, Faction.帝国);

            Assert.IsTrue(OrderOfBattle.AttachFleet(corps.id, 1));    // 戦闘艦隊
            Assert.IsTrue(OrderOfBattle.CanAttachFleet(corps.id, 2)); // 輸送も編入可（諸兵科連合）
            Assert.IsTrue(OrderOfBattle.AttachFleet(corps.id, 2));
            Assert.AreEqual(2, OrderOfBattle.CountFleetsUnder(corps.id)); // 両方編入される
        }

        [Test]
        public void AttachFleet_NonCombatGroupsTogether()
        {
            Fleet(Faction.帝国, 3, ShipRole.輸送艦);
            Fleet(Faction.帝国, 4, ShipRole.偵察艦);
            var convoy = OrderOfBattle.Create(EchelonType.軍団, Faction.帝国, "輸送船団");

            Assert.IsTrue(OrderOfBattle.AttachFleet(convoy.id, 3));
            Assert.IsTrue(OrderOfBattle.AttachFleet(convoy.id, 4)); // 非戦闘同士＝編成可
            Assert.AreEqual(2, OrderOfBattle.CountFleetsUnder(convoy.id));
        }

        [Test]
        public void CombinedArmsMove_TransfersFromOriginalFormation()
        {
            Fleet(Faction.帝国, 1, ShipRole.戦闘艦);
            Fleet(Faction.帝国, 2, ShipRole.輸送艦);
            var combatCorps = OrderOfBattle.Create(EchelonType.軍団, Faction.帝国, "戦闘軍団");
            var convoy = OrderOfBattle.Create(EchelonType.軍団, Faction.帝国, "船団");
            OrderOfBattle.AttachFleet(combatCorps.id, 1);
            OrderOfBattle.AttachFleet(convoy.id, 2);

            // 輸送(2)を戦闘軍団へ移せる（諸兵科連合・#883撤回）＝単一所属で元の船団から剥がれる（中身流動）
            Assert.IsTrue(OrderOfBattle.AttachFleet(combatCorps.id, 2));
            Assert.AreEqual(0, OrderOfBattle.CountFleetsUnder(convoy.id));      // 元から剥がれた
            Assert.AreEqual(2, OrderOfBattle.CountFleetsUnder(combatCorps.id)); // 戦闘＋輸送の諸兵科連合
        }

        [Test]
        public void UnregisteredFleet_TreatedAsCombatant_BackwardCompatible()
        {
            // FleetRoster に未登録の番号は戦闘艦扱い＝従来どおり編成できる
            var corps = OrderOfBattle.Create(EchelonType.軍団, Faction.帝国);
            Assert.IsTrue(OrderOfBattle.AttachFleet(corps.id, 99));
            Assert.IsTrue(OrderOfBattle.AttachFleet(corps.id, 98));
        }
    }
}
