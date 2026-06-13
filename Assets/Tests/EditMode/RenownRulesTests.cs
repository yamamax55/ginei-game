using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>ADM-3 武名・名声（王騎効果）：威圧/鼓舞/寝返り耐性/徴募引力・英雄時代で増幅・戦功で獲得。</summary>
    public class RenownRulesTests
    {
        [Test]
        public void IntimidationFactor_ScalesWithFameAndHeroicAge()
        {
            Assert.AreEqual(0.3f, RenownRules.IntimidationFactor(100, 0f), 1e-4f);  // 平時・武名最大
            Assert.AreEqual(0.45f, RenownRules.IntimidationFactor(100, 1f), 1e-4f); // 英雄時代で増幅
            Assert.AreEqual(0f, RenownRules.IntimidationFactor(0, 1f), 1e-4f);      // 無名は圧が無い
            Assert.AreEqual(0.15f, RenownRules.IntimidationFactor(50, 0f), 1e-4f);
        }

        [Test]
        public void Inspiration_Defection_Recruitment()
        {
            Assert.AreEqual(1.2f, RenownRules.InspirationFactor(100), 1e-4f);
            Assert.AreEqual(1.0f, RenownRules.InspirationFactor(0), 1e-4f);
            Assert.AreEqual(0.5f, RenownRules.DefectionResistance(100), 1e-4f);
            Assert.AreEqual(0.25f, RenownRules.DefectionResistance(50), 1e-4f);
            Assert.AreEqual(1.0f, RenownRules.RecruitmentPull(100), 1e-4f);
            Assert.AreEqual(0.5f, RenownRules.RecruitmentPull(50), 1e-4f);
        }

        [Test]
        public void Gain_AccruesAndClamps()
        {
            Assert.AreEqual(70, RenownRules.Gain(40, 30f));
            Assert.AreEqual(100, RenownRules.Gain(90, 30f)); // クランプ
            Assert.AreEqual(50, RenownRules.Gain(50, -5f));  // 負の戦功は0扱い
        }
    }
}
