using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>陣形の相性（じゃんけん）：三すくみで有利/不利、守勢陣形と同形は中立。</summary>
    public class FormationMatchupRulesTests
    {
        [Test]
        public void Triangle_AdvantageCycle()
        {
            // 紡錘＞横陣＞鶴翼＞紡錘
            Assert.AreEqual(FormationMatchupRules.Advantage, FormationMatchupRules.AttackFactor(Formation.紡錘陣, Formation.横陣), 1e-4f);
            Assert.AreEqual(FormationMatchupRules.Advantage, FormationMatchupRules.AttackFactor(Formation.横陣, Formation.鶴翼陣), 1e-4f);
            Assert.AreEqual(FormationMatchupRules.Advantage, FormationMatchupRules.AttackFactor(Formation.鶴翼陣, Formation.紡錘陣), 1e-4f);
        }

        [Test]
        public void Triangle_DisadvantageIsInverse()
        {
            Assert.AreEqual(FormationMatchupRules.Disadvantage, FormationMatchupRules.AttackFactor(Formation.横陣, Formation.紡錘陣), 1e-4f);
            Assert.AreEqual(FormationMatchupRules.Disadvantage, FormationMatchupRules.AttackFactor(Formation.鶴翼陣, Formation.横陣), 1e-4f);
            Assert.AreEqual(FormationMatchupRules.Disadvantage, FormationMatchupRules.AttackFactor(Formation.紡錘陣, Formation.鶴翼陣), 1e-4f);
        }

        [Test]
        public void SameFormation_AndDefensive_AreNeutral()
        {
            Assert.AreEqual(FormationMatchupRules.Neutral, FormationMatchupRules.AttackFactor(Formation.横陣, Formation.横陣), 1e-4f);
            // 円陣・方陣は三すくみの外＝相性なし（攻守どちらでも中立）
            Assert.AreEqual(FormationMatchupRules.Neutral, FormationMatchupRules.AttackFactor(Formation.紡錘陣, Formation.円陣), 1e-4f);
            Assert.AreEqual(FormationMatchupRules.Neutral, FormationMatchupRules.AttackFactor(Formation.円陣, Formation.横陣), 1e-4f);
            Assert.AreEqual(FormationMatchupRules.Neutral, FormationMatchupRules.AttackFactor(Formation.方陣, Formation.鶴翼陣), 1e-4f);
            Assert.AreEqual(FormationMatchupRules.Neutral, FormationMatchupRules.AttackFactor(Formation.鶴翼陣, Formation.方陣), 1e-4f);
        }
    }
}
