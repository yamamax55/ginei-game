using System.Collections.Generic;
using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 指揮の二系統分離（ゴールドウォーター゠ニコルズ・MILGOV-US §3-A）の純ロジックを固定する：
    /// 役職の系統判定／作戦・管理の頂点抽出／指揮集中度・分断度（クーデターリスク駆動因）。
    /// </summary>
    public class CommandChainRulesTests
    {
        [SetUp]
        public void Reset() => OrderOfBattle.Clear();

        private static Office MilitaryOffice(int id, int tier, CommandChain chain, OfficeScope scope = OfficeScope.国家)
            => new Office(id, "軍事役職", scope, OfficeDomain.軍事) { requiredTier = tier, commandChain = chain };

        // ===== ChainOf =====

        [Test]
        public void ChainOf_MilitaryOffice_ReadsCommandChain()
        {
            Assert.AreEqual(CommandChain.作戦, CommandChainRules.ChainOf(MilitaryOffice(1, 8, CommandChain.作戦)));
            Assert.AreEqual(CommandChain.管理, CommandChainRules.ChainOf(MilitaryOffice(2, 8, CommandChain.管理)));
        }

        [Test]
        public void ChainOf_NonMilitaryOffice_IsAdministrative()
        {
            var civil = new Office(3, "内政大臣", OfficeScope.国家, OfficeDomain.内政) { commandChain = CommandChain.作戦 };
            // 非軍事所掌は作戦指揮を含意しない＝commandChain を無視して管理
            Assert.AreEqual(CommandChain.管理, CommandChainRules.ChainOf(civil));
        }

        [Test]
        public void ChainOf_Null_IsAdministrative()
            => Assert.AreEqual(CommandChain.管理, CommandChainRules.ChainOf(null));

        // ===== OperationalApexFormation =====

        [Test]
        public void OperationalApex_PicksHighestEchelon()
        {
            OrderOfBattle.Create(EchelonType.艦隊, Faction.帝国);
            var group = OrderOfBattle.Create(EchelonType.軍集団, Faction.帝国);
            OrderOfBattle.Create(EchelonType.軍団, Faction.帝国);
            Assert.AreSame(group, CommandChainRules.OperationalApexFormation(Faction.帝国));
        }

        [Test]
        public void OperationalApex_RespectsFaction()
        {
            OrderOfBattle.Create(EchelonType.宇宙艦隊, Faction.同盟);
            var imperialTop = OrderOfBattle.Create(EchelonType.軍団, Faction.帝国);
            Assert.AreSame(imperialTop, CommandChainRules.OperationalApexFormation(Faction.帝国));
        }

        [Test]
        public void OperationalApex_EmptyIsNull()
            => Assert.IsNull(CommandChainRules.OperationalApexFormation(Faction.帝国));

        // ===== AdministrativeApexOffice =====

        [Test]
        public void AdministrativeApex_PicksHighestTierNationalMilitary()
        {
            var offices = new List<Office>
            {
                MilitaryOffice(1, 7, CommandChain.管理),
                MilitaryOffice(2, 10, CommandChain.管理),       // 最高tier＝参謀総長級
                MilitaryOffice(3, 9, CommandChain.管理, OfficeScope.方面), // 国家スコープでない＝除外
                new Office(4, "財務大臣", OfficeScope.国家, OfficeDomain.財政) { requiredTier = 99 }, // 軍事でない＝除外
            };
            var apex = CommandChainRules.AdministrativeApexOffice(offices);
            Assert.AreEqual(2, apex.id);
        }

        [Test]
        public void AdministrativeApex_NoneIsNull()
        {
            var offices = new List<Office> { new Office(1, "外交", OfficeScope.国家, OfficeDomain.外交) };
            Assert.IsNull(CommandChainRules.AdministrativeApexOffice(offices));
        }

        // ===== Concentration =====

        [Test]
        public void Concentration_AllThree_IsOne()
            => Assert.AreEqual(1f, CommandChainRules.Concentration(true, true, true), 1e-5f);

        [Test]
        public void Concentration_None_IsZero()
            => Assert.AreEqual(0f, CommandChainRules.Concentration(false, false, false), 1e-5f);

        [Test]
        public void Concentration_OperationalIsHeaviest()
        {
            float op = CommandChainRules.Concentration(true, false, false);
            float admin = CommandChainRules.Concentration(false, true, false);
            float budget = CommandChainRules.Concentration(false, false, true);
            Assert.Greater(op, admin);
            Assert.Greater(admin, budget);
            Assert.AreEqual(1f, op + admin + budget, 1e-5f); // 重みの総和＝1
        }

        // ===== ConcentratesCommand =====

        [Test]
        public void ConcentratesCommand_RequiresBothApexes()
        {
            Assert.IsTrue(CommandChainRules.ConcentratesCommand(true, true));
            Assert.IsFalse(CommandChainRules.ConcentratesCommand(true, false));
            Assert.IsFalse(CommandChainRules.ConcentratesCommand(false, true));
        }

        // ===== IsUnifiedCommandSeparated =====

        [Test]
        public void Separated_DifferentHolders_True()
            => Assert.IsTrue(CommandChainRules.IsUnifiedCommandSeparated(10, 20));

        [Test]
        public void Separated_SameHolder_False()
            => Assert.IsFalse(CommandChainRules.IsUnifiedCommandSeparated(10, 10));

        [Test]
        public void Separated_VacantApex_False()
        {
            Assert.IsFalse(CommandChainRules.IsUnifiedCommandSeparated(CommandChainRules.Vacant, 20));
            Assert.IsFalse(CommandChainRules.IsUnifiedCommandSeparated(10, CommandChainRules.Vacant));
        }

        // ===== CommandSeparation =====

        [Test]
        public void Separation_FullySeparated_IsOne()
        {
            // 作戦頂点・管理頂点が別人＋予算は第三者（議会/文民）
            float s = CommandChainRules.CommandSeparation(1, 2, 3);
            Assert.AreEqual(1f, s, 1e-5f);
        }

        [Test]
        public void Separation_OnePersonHoldsAll_IsZero()
        {
            float s = CommandChainRules.CommandSeparation(1, 1, 1);
            Assert.AreEqual(0f, s, 1e-5f);
        }

        [Test]
        public void Separation_SplitApexesButApexControlsBudget_IsApexShareOnly()
        {
            // 両頂点は別人だが予算を作戦頂点が握る＝予算独立せず＝ApexSplitShare のみ
            float s = CommandChainRules.CommandSeparation(1, 2, 1);
            Assert.AreEqual(CommandChainRules.ApexSplitShare, s, 1e-5f);
        }

        [Test]
        public void Separation_SameApexButIndependentBudget_IsBudgetShareOnly()
        {
            // 両頂点が同一人物（GN違反）だが予算だけ文民が握る＝BudgetIndependentShare のみ
            float s = CommandChainRules.CommandSeparation(1, 1, 9);
            Assert.AreEqual(CommandChainRules.BudgetIndependentShare, s, 1e-5f);
        }

        [Test]
        public void Separation_VacantBudget_NotCountedIndependent()
        {
            // 予算が空席＝独立に数えない（統制不在）。両頂点別人ぶんのみ
            float s = CommandChainRules.CommandSeparation(1, 2, CommandChainRules.Vacant);
            Assert.AreEqual(CommandChainRules.ApexSplitShare, s, 1e-5f);
        }
    }
}
