using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 賃金(POPLAB-4)・戦時動員(POPLAB-6)・リスキリング再配置速度(SKILL-5)の配線。
    /// <see cref="LaborWageTickRules"/>／<see cref="LaborMarketTickRules"/>（動員・技能フロー）。
    /// </summary>
    public class LaborWageMobilizationTests
    {
        // --- POPLAB-6 戦時動員：生産労働→軍属（総力戦・総量保存） ---
        [Test]
        public void Mobilization_ShiftsProductionToMilitary()
        {
            var p = new Province(1, "", 100f) { systemType = SystemType.工業 }; // 安定（循環失業なし）
            var demand = LaborMarketTickRules.JobDemandShares(p, 0.25f);
            // 生産労働(農0.10/工0.50/鉱0.10=0.70)の25%=0.175 を軍属へ → 軍属0.10+0.175=0.275
            Assert.AreEqual(0.275f, demand.Share(Occupation.軍属), 1e-4f);
            Assert.AreEqual(0.375f, demand.Share(Occupation.工員), 1e-4f); // 0.50×0.75
            Assert.AreEqual(0.075f, demand.Share(Occupation.農民), 1e-4f); // 0.10×0.75
            Assert.AreEqual(1f, demand.Total, 1e-4f); // 総量保存
        }

        [Test]
        public void Mobilization_WithInstability_TotalPreserved()
        {
            var p = new Province(2, "", 100f) { systemType = SystemType.工業 };
            p.stability = GovernanceRules.BaseStability - 20f; // 不安定＋戦時
            var demand = LaborMarketTickRules.JobDemandShares(p, 0.25f);
            Assert.AreEqual(1f, demand.Total, 1e-4f);              // 動員＋循環失業でも総量保存
            Assert.Greater(demand.Share(Occupation.無職), 0.05f);  // 不安定で失業増
            Assert.Greater(demand.Share(Occupation.軍属), 0.10f);  // 戦時で軍属増
        }

        // --- SKILL-5 技能による再配置速度（リスキリング） ---
        [Test]
        public void ReskillingFlowRate_SkilledAdaptFaster()
        {
            Assert.AreEqual(0.25f, LaborMarketTickRules.ReskillingFlowRate(0.25f, 0.5f), 1e-4f);   // 標準
            Assert.AreEqual(0.375f, LaborMarketTickRules.ReskillingFlowRate(0.25f, 1.0f), 1e-4f);  // 高技能＝1.5倍速
            Assert.AreEqual(0.125f, LaborMarketTickRules.ReskillingFlowRate(0.25f, 0.0f), 1e-4f);  // 低技能＝0.5倍速
        }

        // --- POPLAB-4 賃金：逼迫×技能 ---
        [Test]
        public void Wage_TargetAndConvergence()
        {
            Assert.AreEqual(1.2f, LaborWageTickRules.TargetWageIndex(1.0f, 1.0f), 1e-4f);   // 完全雇用×高技能
            Assert.AreEqual(0.8f, LaborWageTickRules.TargetWageIndex(1.0f, 0.0f), 1e-4f);   // 完全雇用×無技能
            Assert.AreEqual(0.56f, LaborWageTickRules.TargetWageIndex(0.0f, 0.0f), 1e-4f);  // 失業×無技能＝低賃金 (0.7×0.8)

            var p = new Province(3, "", 100f) { systemType = SystemType.工業 };
            p.workforce = new Workforce(new[] { 0.2f, 0.5f, 0.1f, 0.1f, 0.1f, 0f }); // 無職0＝就業率1.0
            p.skills = new SkillStock(new[] { 1f, 1f, 1f, 1f, 1f, 1f });             // 技能1.0
            Assert.AreEqual(1f, p.wageIndex, 1e-4f);
            LaborWageTickRules.TickYear(p, 0.5f); // target=1.2 → 1.0+(0.2)×0.5=1.1
            Assert.AreEqual(1.1f, p.wageIndex, 1e-4f);
        }
    }
}
