using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>食品メーカー（#2024・<see cref="FoodRules"/>）：ディフェンシブ需要(FOOD-1)・コスト転嫁(FOOD-2)・利益(FOOD-3)。</summary>
    public class FoodTests
    {
        [Test]
        public void DefensiveDemand_LowSensitivity()
        {
            // 不況−50%でも感応度0.2なら需要は−10%だけ（生活必需＝ディフェンシブ）
            Assert.AreEqual(900f, FoodRules.DefensiveDemand(1000f, -0.5f, 0.2f), 1e-3f);
        }

        [Test]
        public void CostPassThrough_AndMarginSqueeze()
        {
            Assert.AreEqual(110f, FoodRules.CostPassThrough(100f, 20f, 0.5f), 1e-3f); // 原料高の半分だけ転嫁
            Assert.AreEqual(10f, FoodRules.MarginSqueeze(20f, 0.5f), 1e-3f);          // 転嫁できず利益を削る分
            Assert.AreEqual(2000f, FoodRules.FoodProfit(1000f, 12f, 10f), 1e-1f);
        }
    }
}
