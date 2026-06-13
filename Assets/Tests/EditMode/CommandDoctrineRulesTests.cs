using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>CDR-1 性格→AI采配：積極性/撤退閾値/特殊指揮選好/陣形選好。</summary>
    public class CommandDoctrineRulesTests
    {
        [Test]
        public void AggressionFactor_PersonalityTimesAmbition()
        {
            Assert.AreEqual(1.3f, CommandDoctrineRules.AggressionFactor(CommanderPersonality.果敢, 50), 1e-4f);
            Assert.AreEqual(1.75f, CommandDoctrineRules.AggressionFactor(CommanderPersonality.激情, 100), 1e-4f);
            Assert.AreEqual(0.7f, CommandDoctrineRules.AggressionFactor(CommanderPersonality.慎重, 50), 1e-4f);
            Assert.AreEqual(0.75f, CommandDoctrineRules.AggressionFactor(CommanderPersonality.冷静, 0), 1e-4f);
        }

        [Test]
        public void RetreatThreshold_CautiousRetreatsEarly()
        {
            Assert.AreEqual(1.3f, CommandDoctrineRules.RetreatThresholdFactor(CommanderPersonality.慎重), 1e-4f);
            Assert.AreEqual(0.6f, CommandDoctrineRules.RetreatThresholdFactor(CommanderPersonality.果敢), 1e-4f);
            Assert.AreEqual(1.0f, CommandDoctrineRules.RetreatThresholdFactor(CommanderPersonality.冷静), 1e-4f);
        }

        [Test]
        public void Preferences_CommandAndFormation()
        {
            Assert.AreEqual(ActiveCommand.突撃, CommandDoctrineRules.PreferredCommand(CommanderPersonality.果敢));
            Assert.AreEqual(ActiveCommand.一斉砲撃, CommandDoctrineRules.PreferredCommand(CommanderPersonality.冷静));
            Assert.AreEqual(ActiveCommand.不退転, CommandDoctrineRules.PreferredCommand(CommanderPersonality.堅実));

            Assert.AreEqual(Formation.紡錘陣, CommandDoctrineRules.FormationBias(CommanderPersonality.果敢));
            Assert.AreEqual(Formation.円陣, CommandDoctrineRules.FormationBias(CommanderPersonality.慎重));
            Assert.AreEqual(Formation.横陣, CommandDoctrineRules.FormationBias(CommanderPersonality.激情));
            Assert.AreEqual(Formation.方陣, CommandDoctrineRules.FormationBias(CommanderPersonality.堅実));
            Assert.AreEqual(Formation.鶴翼陣, CommandDoctrineRules.FormationBias(CommanderPersonality.冷静));
        }
    }
}
