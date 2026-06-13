using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>戦局モメンタム：優勢度（拮抗0.5・優勢で偏る）と戦力指標。</summary>
    public class BattleMomentumRulesTests
    {
        [Test]
        public void Advantage_EvenIsHalf()
        {
            Assert.AreEqual(0.5f, BattleMomentumRules.Advantage(100f, 100f), 1e-4f);
            Assert.AreEqual(0.5f, BattleMomentumRules.Advantage(0f, 0f), 1e-4f); // 両軍0＝拮抗
        }

        [Test]
        public void Advantage_SkewsToStronger()
        {
            Assert.AreEqual(0.75f, BattleMomentumRules.Advantage(300f, 100f), 1e-4f); // 3:1
            Assert.AreEqual(0f, BattleMomentumRules.Advantage(0f, 100f), 1e-4f);      // 敵のみ
            Assert.AreEqual(1f, BattleMomentumRules.Advantage(100f, 0f), 1e-4f);      // 自軍のみ
        }

        [Test]
        public void Power_CombinesCountAndStrength()
        {
            // 2隊×1000 + 兵力5000 = 7000
            Assert.AreEqual(7000f, BattleMomentumRules.Power(2, 5000f), 1e-3f);
        }
    }
}
