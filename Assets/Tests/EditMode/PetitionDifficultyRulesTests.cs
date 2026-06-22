using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>具申の難度：区分別の採用率補正・effectKey→区分の解決。</summary>
    public class PetitionDifficultyRulesTests
    {
        [Test]
        public void AdoptMultiplier_HigherOfficeIsHarder()
        {
            Assert.AreEqual(1.1f, PetitionDifficultyRules.AdoptMultiplier(PetitionCategory.海賊狩り), 1e-4f);
            Assert.AreEqual(1.0f, PetitionDifficultyRules.AdoptMultiplier(PetitionCategory.予算), 1e-4f);
            Assert.AreEqual(0.8f, PetitionDifficultyRules.AdoptMultiplier(PetitionCategory.作戦), 1e-4f);
            Assert.AreEqual(0.7f, PetitionDifficultyRules.AdoptMultiplier(PetitionCategory.人事), 1e-4f);
            Assert.AreEqual(0.6f, PetitionDifficultyRules.AdoptMultiplier(PetitionCategory.政治), 1e-4f);
            // 政治 ＜ 人事 ＜ 作戦 ＜ 予算 ≦ 海賊狩り（高位ほど通りにくい）
            Assert.Less(PetitionDifficultyRules.AdoptMultiplier(PetitionCategory.政治),
                        PetitionDifficultyRules.AdoptMultiplier(PetitionCategory.作戦));
        }

        [Test]
        public void CategoryOf_ResolvesFromEffectKey()
        {
            Assert.AreEqual(PetitionCategory.予算, PetitionDifficultyRules.CategoryOf("fleet.budget:0:5000"));
            Assert.AreEqual(PetitionCategory.作戦, PetitionDifficultyRules.CategoryOf("fleet.base:0:-1"));
            Assert.AreEqual(PetitionCategory.海賊狩り, PetitionDifficultyRules.CategoryOf("mission.pirate"));
            Assert.AreEqual(PetitionCategory.予算, PetitionDifficultyRules.CategoryOf("fleet.reinforce:3000"));
            Assert.AreEqual(PetitionCategory.人事, PetitionDifficultyRules.CategoryOf("hr.honor"));
            Assert.AreEqual(PetitionCategory.政治, PetitionDifficultyRules.CategoryOf("gov.taxcut"));
            Assert.AreEqual(PetitionCategory.建白, PetitionDifficultyRules.CategoryOf("self.clear"));
            Assert.AreEqual(PetitionCategory.建白, PetitionDifficultyRules.CategoryOf("career.petition"));
            Assert.AreEqual(PetitionCategory.建白, PetitionDifficultyRules.CategoryOf(null));
        }
    }
}
