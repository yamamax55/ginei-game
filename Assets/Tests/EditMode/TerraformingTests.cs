using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>テラフォーミング（#2025・<see cref="TerraformingRules"/>）：進捗(TERA-1)・居住適性(TERA-2)・請負額(TERA-3)・利益(TERA-4)。</summary>
    public class TerraformingTests
    {
        [Test]
        public void Progress_AndHabitability()
        {
            Assert.AreEqual(500f, TerraformingRules.TerraformProgress(1000f, 0.5f, 1f), 1e-1f); // 抵抗0.5で半減
            Assert.AreEqual(0.5f, TerraformingRules.HabitabilityGain(0.3f, 0.2f, 1.0f), 1e-4f); // 0.3+0.2
            Assert.AreEqual(1.0f, TerraformingRules.HabitabilityGain(0.9f, 0.5f, 1.0f), 1e-4f); // 上限でクランプ
        }

        [Test]
        public void Contract_AndProfit()
        {
            Assert.AreEqual(50000000f, TerraformingRules.ProjectContractValue(1000f, 50000f), 1e1f);
            Assert.AreEqual(10000000f, TerraformingRules.TerraformingProfit(50000000f, 20000000f, 15000000f, 5000000f), 1e1f);
        }
    }
}
