using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>表裏比興（真田昌幸）：主家滅亡で覚醒・寡兵で大軍を翻弄・自在な変節・外交の冴え・生存力。</summary>
    public class HyoriHikyoRulesTests
    {
        [Test]
        public void IsActive_OnlyWhenLordHouseFallen()
        {
            Assert.IsTrue(HyoriHikyoRules.IsActive(true, true));    // 表裏比興＋主家滅亡＝覚醒
            Assert.IsFalse(HyoriHikyoRules.IsActive(true, false));  // 主家健在＝潜在
            Assert.IsFalse(HyoriHikyoRules.IsActive(false, true));  // そもそも梟雄でない
        }

        [Test]
        public void UnderdogFactor_RewardsBeingOutnumbered()
        {
            Assert.AreEqual(1.0f, HyoriHikyoRules.UnderdogFactor(1000f, 1000f), 1e-4f); // 互角
            Assert.AreEqual(1.25f, HyoriHikyoRules.UnderdogFactor(1000f, 2000f), 1e-4f); // 2:1
            Assert.AreEqual(1.5f, HyoriHikyoRules.UnderdogFactor(1000f, 3000f), 1e-4f);  // 3:1（上田合戦）
            Assert.AreEqual(1.5f, HyoriHikyoRules.UnderdogFactor(1000f, 5000f), 1e-4f);  // 上限クランプ
            Assert.AreEqual(1.0f, HyoriHikyoRules.UnderdogFactor(2000f, 1000f), 1e-4f);  // 有利なら上乗せ無し

            Assert.AreEqual(1.0f, HyoriHikyoRules.CombatFactor(false, 1000f, 3000f), 1e-4f); // 非覚醒
            Assert.AreEqual(1.5f, HyoriHikyoRules.CombatFactor(true, 1000f, 3000f), 1e-4f);  // 覚醒
        }

        [Test]
        public void Defection_Guile_Survival()
        {
            Assert.IsTrue(HyoriHikyoRules.CanOpportunisticallyDefect(true));
            Assert.IsFalse(HyoriHikyoRules.CanOpportunisticallyDefect(false));

            Assert.AreEqual(1.5f, HyoriHikyoRules.DiplomaticGuileFactor(true, 100), 1e-4f);
            Assert.AreEqual(1.25f, HyoriHikyoRules.DiplomaticGuileFactor(true, 50), 1e-4f);
            Assert.AreEqual(1.0f, HyoriHikyoRules.DiplomaticGuileFactor(false, 100), 1e-4f);

            Assert.AreEqual(0.9f, HyoriHikyoRules.SurvivalChance(true, 80, 80), 1e-4f);
            Assert.AreEqual(0.5f, HyoriHikyoRules.SurvivalChance(true, 0, 0), 1e-4f);
            Assert.AreEqual(0f, HyoriHikyoRules.SurvivalChance(false, 80, 80), 1e-4f); // 非覚醒は特別な生存なし

            Assert.IsTrue(HyoriHikyoRules.EvadesDestruction(true, 80, 80, 0.5f));   // roll<0.9
            Assert.IsFalse(HyoriHikyoRules.EvadesDestruction(true, 80, 80, 0.95f));
            Assert.IsFalse(HyoriHikyoRules.EvadesDestruction(false, 80, 80, 0.0f)); // 非覚醒は免れない
        }
    }
}
