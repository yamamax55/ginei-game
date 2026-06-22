using System.Collections.Generic;
using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 最高の軍自動編成（<see cref="ArmyOrganizationRules"/>）：予備控除・能力適性での司令割付・指揮班・梯団・決定論・null安全。
    /// </summary>
    public class ArmyOrganizationRulesTests
    {
        // 共通ヘルパ：能力均一の将（適性差をなくしてtie-break検証用）。
        private static CommanderProfile Flat(int id, int tier, int stat = 50)
            => new CommanderProfile(id, tier, stat, stat, stat, stat, stat, stat);

        // ===== ReserveFor =====

        [Test]
        public void ReserveForCarvesFraction()
        {
            Assert.AreEqual(12000, ArmyOrganizationRules.ReserveFor(60000, 0.2f)); // 20%
            Assert.AreEqual(0, ArmyOrganizationRules.ReserveFor(0, 0.2f));
            Assert.AreEqual(60000, ArmyOrganizationRules.ReserveFor(60000, 1.5f)); // クランプ上限
            Assert.AreEqual(0, ArmyOrganizationRules.ReserveFor(60000, -0.5f));    // クランプ下限
        }

        [Test]
        public void OrganizeFrontIs48000When60000AndDefault()
        {
            var plan = ArmyOrganizationRules.Organize(60000, null);
            Assert.AreEqual(12000, plan.reserve);
            // 前線 = 60000 - 12000 = 48000、保存される。
            Assert.AreEqual(48000, plan.FrontlineStrength);
            Assert.AreEqual(plan.reserve + plan.FrontlineStrength, 60000);
            // 艦隊数 = RecommendFleetCount(48000, 12000) = 4。
            int expected = FleetAutoOrganizeRules.RecommendFleetCount(48000, 12000);
            Assert.AreEqual(expected, plan.fleets.Count);
            Assert.AreEqual(4, plan.fleets.Count);
        }

        // ===== EchelonForStrength =====

        [Test]
        public void EchelonForStrengthMatchesCapacityRules()
        {
            // 12000 -> 必要tier9(中将) -> 艦隊。
            Assert.AreEqual(EchelonType.艦隊, ArmyOrganizationRules.EchelonForStrength(12000));
            // 3000 -> 必要tier7(准将) -> 分艦隊。
            Assert.AreEqual(EchelonType.分艦隊, ArmyOrganizationRules.EchelonForStrength(3000));
        }

        // ===== AssignCommandersByFitness =====

        [Test]
        public void HigherFitnessCommanderIsChosen()
        {
            var fleets = new List<FleetOrder> { new FleetOrder { strength = 12000, role = ShipRole.戦闘艦 } };
            // どちらも tier9(中将) で 12000 を指揮可能。combat 適性は a が高い。
            var commanders = new List<CommanderProfile>
            {
                new CommanderProfile(1, 9, 30, 30, 30, 30, 30, 30), // 低適性
                new CommanderProfile(2, 9, 90, 90, 90, 30, 30, 30), // 高適性(combat)
            };
            ArmyOrganizationRules.AssignCommandersByFitness(fleets, commanders);
            Assert.AreEqual(2, fleets[0].commanderId);
        }

        [Test]
        public void OverCapacityCommanderIsNotAssigned()
        {
            var fleets = new List<FleetOrder> { new FleetOrder { strength = 12000, role = ShipRole.戦闘艦 } };
            // tier7(准将)=3000 限界なので 12000 を指揮できない。
            var commanders = new List<CommanderProfile> { Flat(1, 7) };
            ArmyOrganizationRules.AssignCommandersByFitness(fleets, commanders);
            Assert.AreEqual(-1, fleets[0].commanderId);
        }

        [Test]
        public void TieBreakPrefersLowerSufficientRank()
        {
            var fleets = new List<FleetOrder> { new FleetOrder { strength = 12000, role = ShipRole.戦闘艦 } };
            // 適性同点。tier9 も tier12 も 12000 を率いられる -> 低い tier9 を選び元帥を温存。
            var commanders = new List<CommanderProfile> { Flat(1, 12), Flat(2, 9) };
            ArmyOrganizationRules.AssignCommandersByFitness(fleets, commanders);
            Assert.AreEqual(2, fleets[0].commanderId);
        }

        [Test]
        public void ReconRolePrefersReconFitness()
        {
            var fleets = new List<FleetOrder> { new FleetOrder { strength = 3000, role = ShipRole.偵察艦 } };
            var commanders = new List<CommanderProfile>
            {
                new CommanderProfile(1, 7, 90, 90, 90, 10, 10, 10), // 戦闘特化だが偵察は低
                new CommanderProfile(2, 7, 10, 10, 10, 90, 10, 90), // 偵察特化(情報+機動高)
            };
            ArmyOrganizationRules.AssignCommandersByFitness(fleets, commanders);
            Assert.AreEqual(2, fleets[0].commanderId);
        }

        // ===== AssignStaff =====

        [Test]
        public void LargeFleetGetsStaffSmallDoesNot()
        {
            var prm = ArmyOrganizationRules.ArmyOrgParams.Default; // staffMin=12000
            var fleets = new List<FleetOrder>
            {
                new FleetOrder { strength = 12000, commanderId = 1 }, // 大：指揮班付く
                new FleetOrder { strength = 3000, commanderId = 2 },  // 小：付かない
            };
            var commanders = new List<CommanderProfile>
            {
                Flat(1, 9), Flat(2, 9),       // 司令（使用済み扱い）
                Flat(3, 8), Flat(4, 8),       // 残り＝幕僚候補
            };
            ArmyOrganizationRules.AssignStaff(fleets, commanders, prm);

            Assert.AreNotEqual(-1, fleets[0].viceId, "大艦隊に副司令");
            Assert.AreNotEqual(-1, fleets[0].chiefId, "大艦隊に参謀");
            Assert.AreEqual(-1, fleets[1].viceId, "小艦隊は副司令なし");
            Assert.AreEqual(-1, fleets[1].chiefId, "小艦隊は参謀なし");

            // 副司令の tier <= 司令の tier（司令=中将9）。
            int viceTier = TierOfId(commanders, fleets[0].viceId);
            Assert.LessOrEqual(viceTier, 9);

            // 二重起用なし（司令/副司令/参謀で重複しない）。
            var ids = new List<int> { fleets[0].commanderId, fleets[0].viceId, fleets[0].chiefId };
            CollectionAssert.AllItemsAreUnique(ids);
        }

        // ===== AssembleCorps =====

        [Test]
        public void CorpsFormsWithQualifiedSpareCommander()
        {
            var prm = ArmyOrganizationRules.ArmyOrgParams.Default; // corpsMaxFleets=3, enableCorps
            var fleets = new List<FleetOrder>
            {
                new FleetOrder { strength = 12000, commanderId = 1 },
                new FleetOrder { strength = 12000, commanderId = 2 },
            };
            var commanders = new List<CommanderProfile>
            {
                Flat(1, 9), Flat(2, 9),   // 司令（使用済み）
                Flat(9, 11),               // tier11(上級大将) の予備＝梯団司令適格(>=10)
            };
            var corps = ArmyOrganizationRules.AssembleCorps(fleets, commanders, prm);
            Assert.AreEqual(1, corps.Count);
            Assert.AreEqual(9, corps[0].commanderId);
            CollectionAssert.AreEquivalent(new[] { 0, 1 }, corps[0].fleetIndices);
            Assert.AreEqual(0, fleets[0].corpsIndex);
            Assert.AreEqual(0, fleets[1].corpsIndex);
        }

        [Test]
        public void NoCorpsWithoutQualifiedCommander()
        {
            var prm = ArmyOrganizationRules.ArmyOrgParams.Default;
            var fleets = new List<FleetOrder>
            {
                new FleetOrder { strength = 12000, commanderId = 1 },
                new FleetOrder { strength = 12000, commanderId = 2 },
            };
            // 予備に tier10 以上がいない（tier9 のみ）。
            var commanders = new List<CommanderProfile> { Flat(1, 9), Flat(2, 9), Flat(3, 9) };
            var corps = ArmyOrganizationRules.AssembleCorps(fleets, commanders, prm);
            Assert.AreEqual(0, corps.Count);
            Assert.AreEqual(-1, fleets[0].corpsIndex);
            Assert.AreEqual(-1, fleets[1].corpsIndex);
        }

        [Test]
        public void CorpsDisabledByParam()
        {
            var prm = new ArmyOrganizationRules.ArmyOrgParams(0.2f, 12000, 12000, 3, false);
            var fleets = new List<FleetOrder>
            {
                new FleetOrder { strength = 12000, commanderId = 1 },
                new FleetOrder { strength = 12000, commanderId = 2 },
            };
            var commanders = new List<CommanderProfile> { Flat(1, 9), Flat(2, 9), Flat(9, 11) };
            var corps = ArmyOrganizationRules.AssembleCorps(fleets, commanders, prm);
            Assert.AreEqual(0, corps.Count);
        }

        // ===== Determinism =====

        [Test]
        public void DeterministicSameInputsSamePlan()
        {
            var commanders = new List<CommanderProfile>
            {
                new CommanderProfile(1, 12, 80, 70, 60, 50, 40, 30),
                new CommanderProfile(2, 10, 60, 60, 60, 60, 60, 60),
                new CommanderProfile(3, 9, 90, 50, 50, 50, 90, 90),
                new CommanderProfile(4, 9, 50, 90, 50, 90, 50, 50),
                new CommanderProfile(5, 8, 40, 40, 40, 40, 40, 40),
            };
            var a = ArmyOrganizationRules.Organize(60000, commanders);
            var b = ArmyOrganizationRules.Organize(60000, commanders);

            Assert.AreEqual(a.reserve, b.reserve);
            Assert.AreEqual(a.fleets.Count, b.fleets.Count);
            for (int i = 0; i < a.fleets.Count; i++)
            {
                Assert.AreEqual(a.fleets[i].strength, b.fleets[i].strength);
                Assert.AreEqual(a.fleets[i].commanderId, b.fleets[i].commanderId);
                Assert.AreEqual(a.fleets[i].viceId, b.fleets[i].viceId);
                Assert.AreEqual(a.fleets[i].chiefId, b.fleets[i].chiefId);
                Assert.AreEqual(a.fleets[i].corpsIndex, b.fleets[i].corpsIndex);
            }
            Assert.AreEqual(a.corps.Count, b.corps.Count);
            for (int i = 0; i < a.corps.Count; i++)
                Assert.AreEqual(a.corps[i].commanderId, b.corps[i].commanderId);
        }

        // ===== Null / empty =====

        [Test]
        public void OrganizeZeroPoolIsEmpty()
        {
            var plan = ArmyOrganizationRules.Organize(0, null);
            Assert.AreEqual(0, plan.reserve);
            Assert.AreEqual(0, plan.fleets.Count);
            Assert.AreEqual(0, plan.corps.Count);
            Assert.AreEqual(0, plan.FrontlineStrength);
        }

        [Test]
        public void NullCommandersLeavesFleetsLeaderless()
        {
            var plan = ArmyOrganizationRules.Organize(48000, null);
            Assert.Greater(plan.fleets.Count, 0);
            foreach (var f in plan.fleets)
            {
                Assert.AreEqual(-1, f.commanderId);
                Assert.AreEqual(-1, f.viceId);
                Assert.AreEqual(-1, f.chiefId);
            }
            Assert.AreEqual(0, plan.corps.Count);
        }

        // ヘルパ：id から tier を引く（テスト内）。
        private static int TierOfId(List<CommanderProfile> commanders, int id)
        {
            foreach (var c in commanders) if (c.id == id) return c.rankTier;
            return -1;
        }
    }
}
