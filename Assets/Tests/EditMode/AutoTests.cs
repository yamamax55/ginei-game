using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>自動車メーカー（#2024・<see cref="AutoRules"/>）：量産(AUTO-1)・モデルチェンジ(AUTO-2)・リコール(AUTO-3)・サプライチェーン(AUTO-4)・利益(AUTO-5)。</summary>
    public class AutoTests
    {
        [Test]
        public void MassProduction_AndModelFreshness()
        {
            Assert.AreEqual(85f, AutoRules.MassProductionUnitCost(100f, 200f, 100f, 0.3f), 1e-3f); // 2倍台数で単価↓
            Assert.AreEqual(700f, AutoRules.ModelFreshnessSales(1000f, 2f, 0.15f, 0.4f), 1e-3f);   // 2年落ちで販売減
        }

        [Test]
        public void Recall_SupplyAndProfit()
        {
            Assert.AreEqual(500000f, AutoRules.RecallCost(10000f, 50f), 1e-1f);
            Assert.AreEqual(800f, AutoRules.SupplyConstrainedOutput(1000f, 800f), 1e-3f); // JIT部品不足で減産
            Assert.AreEqual(6500f, AutoRules.AutoProfit(100f, 200f, 85f, 5000f), 1e-1f); // 100×115−固定5000
        }
    }
}
