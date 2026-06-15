using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 惑星のインフラ普及率（#2021・<see cref="InfrastructureRules"/>）を固定する：接続需要・供給余力・普及率の目標/収束・
    /// 生活水準/生産性/安定度への効果。
    /// </summary>
    public class InfrastructureRulesTests
    {
        [Test]
        public void ConnectedDemand_ScalesWithPopulationAndPenetration()
        {
            Assert.AreEqual(50f, InfrastructureRules.ConnectedDemand(100f, 1f, 0.5f), 1e-3f); // 100×1×0.5
            Assert.AreEqual(0f, InfrastructureRules.ConnectedDemand(100f, 1f, 0f), 1e-3f);    // 未普及は需要0
        }

        [Test]
        public void CapacityAdequacy_SpareVsShortage()
        {
            Assert.AreEqual(2f, InfrastructureRules.CapacityAdequacy(100f, 50f), 1e-3f);  // 余裕
            Assert.AreEqual(0.5f, InfrastructureRules.CapacityAdequacy(50f, 100f), 1e-3f); // 不足
            Assert.AreEqual(2f, InfrastructureRules.CapacityAdequacy(100f, 0f), 1e-3f);    // 需要0は十分
        }

        [Test]
        public void PenetrationTarget_HighWhenSpareAndStable()
        {
            Assert.AreEqual(1f, InfrastructureRules.PenetrationTarget(2f, 1f), 1e-3f);   // 余裕＋安定＝満遍なく
            // 供給不足（余力0.5）は目標を頭打ち：0.5×(0.5+0.5×1)=0.5
            Assert.AreEqual(0.5f, InfrastructureRules.PenetrationTarget(0.5f, 1f), 1e-3f);
            // 不安定は普及が伸びにくい：余力1×(0.5+0.5×0)=0.5
            Assert.AreEqual(0.5f, InfrastructureRules.PenetrationTarget(1f, 0f), 1e-3f);
        }

        [Test]
        public void TickPenetration_ConvergesToTarget()
        {
            float v = 0.2f;
            for (int i = 0; i < 100; i++) v = InfrastructureRules.TickPenetration(v, 0.9f, 0.08f);
            Assert.AreEqual(0.9f, v, 1e-3f);
            // 下振れも追従
            float w = 0.9f;
            w = InfrastructureRules.TickPenetration(w, 0.5f, 0.08f);
            Assert.Less(w, 0.9f);
        }

        [Test]
        public void Effects_RiseWithPenetration()
        {
            Assert.Less(InfrastructureRules.LivingStandardFactor(0f, 0.6f), InfrastructureRules.LivingStandardFactor(1f, 0.6f));
            Assert.AreEqual(1f, InfrastructureRules.LivingStandardFactor(1f, 0.6f), 1e-3f);
            Assert.AreEqual(0.6f, InfrastructureRules.LivingStandardFactor(0f, 0.6f), 1e-3f);
            Assert.Greater(InfrastructureRules.ProductivityFactor(1f, 0.8f, 0.2f), InfrastructureRules.ProductivityFactor(0f, 0.8f, 0.2f));
            Assert.Greater(InfrastructureRules.StabilityBonus(1f, 10f), 0f);   // 高普及で+
            Assert.Less(InfrastructureRules.StabilityBonus(0f, 10f), 0f);      // 低普及で−
        }
    }
}
