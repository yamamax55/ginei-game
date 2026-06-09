using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Ginei;
using CP = Ginei.CommandStaffRules.CommandParams;

namespace Ginei.Tests
{
    /// <summary>
    /// 艦隊指揮班（提督・副提督・参謀の3ネームド・#885）を固定する：配置の階級ゲート（CMD-2）、副提督の補佐・参謀の
    /// 底上げ（CMD-3・実効値パターン・基準値非破壊）、提督喪失で副提督が昇格し新副提督を補充する継承（CMD-4）。
    /// </summary>
    public class CommandStaffRulesTests
    {
        private static AdmiralData Admiral(string name, int tier, int leadership = 80, int defense = 80, int operation = 80, int intelligence = 80)
        {
            var a = ScriptableObject.CreateInstance<AdmiralData>();
            a.admiralName = name; a.rankTier = tier;
            a.leadership = leadership; a.defense = defense; a.operation = operation; a.intelligence = intelligence;
            a.staffOfficers = new AdmiralData[0]; // 提督自身の幕僚は無し（指揮班とは別系統）
            return a;
        }

        private static FleetUnitData Unit(AdmiralData commander)
        {
            var u = ScriptableObject.CreateInstance<FleetUnitData>();
            u.faction = Faction.帝国; u.fleetNumber = 1;
            u.assignedAdmiral = commander;
            return u;
        }

        // ===== CMD-2 配置・階級ゲート =====

        [Test]
        public void AssignVice_RequiresRankAtOrBelowCommander()
        {
            var unit = Unit(Admiral("提督", tier: 8));
            Assert.IsTrue(CommandStaffRules.AssignVice(unit, Admiral("次席", tier: 7)));  // 提督以下＝可
            Assert.AreEqual(7, unit.viceCommander.rankTier);

            var unit2 = Unit(Admiral("提督", tier: 7));
            Assert.IsFalse(CommandStaffRules.AssignVice(unit2, Admiral("上位", tier: 9))); // 提督より上＝不可
            Assert.IsNull(unit2.viceCommander);
        }

        [Test]
        public void AssignVice_RejectsSameAsCommander()
        {
            var cmd = Admiral("提督", 8);
            var unit = Unit(cmd);
            Assert.IsFalse(CommandStaffRules.AssignVice(unit, cmd)); // 兼任不可
        }

        [Test]
        public void AssignChief_RejectsDuplicateOfCommanderOrVice()
        {
            var cmd = Admiral("提督", 8);
            var vice = Admiral("次席", 7);
            var unit = Unit(cmd);
            CommandStaffRules.AssignVice(unit, vice);

            Assert.IsFalse(CommandStaffRules.AssignChief(unit, cmd));
            Assert.IsFalse(CommandStaffRules.AssignChief(unit, vice));
            Assert.IsTrue(CommandStaffRules.AssignChief(unit, Admiral("参謀", 6)));
            Assert.IsTrue(unit.HasFullCommandStaff);
        }

        // ===== CMD-3 能力反映（実効値パターン） =====

        [Test]
        public void EffectiveLeadership_AddsViceAssist_BaseNonDestructive()
        {
            var cmd = Admiral("提督", 8, leadership: 80);
            var vice = Admiral("次席", 7, leadership: 60);
            var unit = Unit(cmd);
            CommandStaffRules.AssignVice(unit, vice);

            // 80 + round(60 * 0.25) = 80 + 15 = 95
            Assert.AreEqual(95, CommandStaffRules.EffectiveLeadership(unit, CP.Default));
            Assert.AreEqual(80, cmd.leadership); // 基準値は非破壊
        }

        [Test]
        public void EffectiveLeadership_CapsAtMaxStatValue()
        {
            var cmd = Admiral("提督", 8, leadership: 95);
            var vice = Admiral("次席", 8, leadership: 100);
            var unit = Unit(cmd);
            CommandStaffRules.AssignVice(unit, vice);
            // 95 + 25 = 120 → 100 でクランプ
            Assert.AreEqual(AdmiralData.MaxStatValue, CommandStaffRules.EffectiveLeadership(unit, CP.Default));
        }

        [Test]
        public void EffectiveOperation_AddsChiefAssist()
        {
            var cmd = Admiral("提督", 8, operation: 50);
            var unit = Unit(cmd);
            CommandStaffRules.AssignChief(unit, Admiral("参謀", 6, operation: 90));
            // 50 + round(90 * 0.20) = 50 + 18 = 68
            Assert.AreEqual(68, CommandStaffRules.EffectiveOperation(unit, CP.Default));
        }

        [Test]
        public void EffectiveStats_CommanderOnly_NoBonus()
        {
            var cmd = Admiral("提督", 8, leadership: 70, operation: 70);
            var unit = Unit(cmd);
            Assert.AreEqual(70, CommandStaffRules.EffectiveLeadership(unit, CP.Default)); // 副提督なし
            Assert.AreEqual(70, CommandStaffRules.EffectiveOperation(unit, CP.Default));  // 参謀なし
        }

        // ===== CMD-4 継承 =====

        [Test]
        public void PromoteVice_FillsEmptyCommanderSeat()
        {
            var unit = Unit(Admiral("提督", 8));
            var vice = Admiral("次席", 7);
            CommandStaffRules.AssignVice(unit, vice);

            unit.assignedAdmiral = null; // 提督喪失（戦死/捕虜）
            Assert.IsTrue(CommandStaffRules.NeedsSuccession(unit));
            Assert.IsTrue(CommandStaffRules.PromoteVice(unit));
            Assert.AreSame(vice, unit.assignedAdmiral); // 副提督が提督へ昇格
            Assert.IsNull(unit.viceCommander);           // 副提督席は空く
        }

        [Test]
        public void Succeed_PromotesAndRefillsVice()
        {
            var cmd = Admiral("提督", 8);
            var vice = Admiral("次席", 7);
            var chief = Admiral("参謀", 6);
            var unit = Unit(cmd);
            CommandStaffRules.AssignVice(unit, vice);
            CommandStaffRules.AssignChief(unit, chief);

            unit.assignedAdmiral = null; // 提督喪失
            var pool = new List<AdmiralData> { Admiral("候補A", 5), Admiral("候補B", 7), Admiral("上位", 9) };

            Assert.IsTrue(CommandStaffRules.Succeed(unit, pool));
            Assert.AreSame(vice, unit.assignedAdmiral);   // 副提督→提督
            Assert.IsNotNull(unit.viceCommander);          // 新副提督を補充
            Assert.AreEqual(7, unit.viceCommander.rankTier); // 新提督(tier7)以下の最高位＝候補B(7)
        }

        [Test]
        public void Succeed_NoVice_LeavesCommanderVacant()
        {
            var unit = Unit(Admiral("提督", 8));
            unit.assignedAdmiral = null; // 提督喪失・副提督なし
            Assert.IsFalse(CommandStaffRules.Succeed(unit, new List<AdmiralData> { Admiral("外部", 8) }));
            Assert.IsNull(unit.assignedAdmiral); // 空席のまま（無痛補充にしない）
        }
    }
}
