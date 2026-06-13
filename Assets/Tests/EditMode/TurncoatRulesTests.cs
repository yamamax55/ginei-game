using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>寝返り（小早川秀秋）：布陣後はご法度・名誉大幅減・調略+圧力で靡く・裏切られた側の戦線崩壊。</summary>
    public class TurncoatRulesTests
    {
        [Test]
        public void TabooAndFamePenalty_AfterDeploymentIsForbidden()
        {
            Assert.IsTrue(TurncoatRules.IsTabooDefection(true));
            Assert.IsFalse(TurncoatRules.IsTabooDefection(false));

            Assert.AreEqual(50, TurncoatRules.FamePenalty(true));  // 布陣後＝大幅
            Assert.AreEqual(10, TurncoatRules.FamePenalty(false)); // 布陣前＝軽微

            Assert.AreEqual(10, TurncoatRules.ApplyHonorPenalty(60, true));  // 名誉大幅減
            Assert.AreEqual(50, TurncoatRules.ApplyHonorPenalty(60, false));
            Assert.AreEqual(0, TurncoatRules.ApplyHonorPenalty(30, true));   // 下限0
        }

        [Test]
        public void SwayChance_IntrigueAndPressure()
        {
            Assert.AreEqual(1.0f, TurncoatRules.SwayChance(true, 1f, 1f), 1e-4f);   // 調略+問鉄砲で必至
            Assert.AreEqual(0.6f, TurncoatRules.SwayChance(true, 0.5f, 0.5f), 1e-4f);
            Assert.AreEqual(0.2f, TurncoatRules.SwayChance(true, 0f, 0f), 1e-4f);   // 寝返り型の素地
            Assert.AreEqual(0.4f, TurncoatRules.SwayChance(false, 1f, 1f), 1e-4f);  // 並は靡きにくい

            Assert.IsTrue(TurncoatRules.Betrays(0.6f, 0.5f));
            Assert.IsFalse(TurncoatRules.Betrays(0.6f, 0.7f));
        }

        [Test]
        public void BetrayalMoraleShock_CollapsesBetrayedSide()
        {
            Assert.AreEqual(0.4f, TurncoatRules.BetrayalMoraleShock(true), 1e-4f);  // 布陣後＝戦線崩壊
            Assert.AreEqual(0.15f, TurncoatRules.BetrayalMoraleShock(false), 1e-4f);
        }
    }
}
