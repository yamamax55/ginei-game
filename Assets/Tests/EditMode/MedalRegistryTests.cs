using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>勲章台帳：叙勲・保有・恩給/名誉の導出・クリア。</summary>
    public class MedalRegistryTests
    {
        [TearDown]
        public void Cleanup() => MedalRegistry.Clear();

        [Test]
        public void Award_AndQuery()
        {
            MedalRegistry.Award(7, new Decoration(MedalKind.武功章, MedalGrade.二級, 800));
            MedalRegistry.Award(7, MedalKind.戦功章, 95f, 801); // 戦功95→一級
            Assert.AreEqual(2, MedalRegistry.Count(7));
            Assert.AreEqual(MedalGrade.一級, MedalRegistry.Decorations(7)[1].grade);
            // 未叙勲の人物は空・従来動作
            Assert.AreEqual(0, MedalRegistry.Count(99));
            Assert.AreEqual(1.0f, MedalRegistry.PensionFactor(99), 1e-4f);
            Assert.AreEqual(0f, MedalRegistry.Prestige(99), 1e-4f);
        }

        [Test]
        public void PensionAndPrestige_FromHeldMedals()
        {
            MedalRegistry.Award(1, new Decoration(MedalKind.勲功章, MedalGrade.一級)); // 価値1.0
            Assert.AreEqual(1.1f, MedalRegistry.PensionFactor(1), 1e-4f);  // 恩給+10%
            Assert.AreEqual(10f, MedalRegistry.Prestige(1), 1e-4f);        // 名誉10
        }

        [Test]
        public void Clear_EmptiesLedger()
        {
            MedalRegistry.Award(1, new Decoration(MedalKind.勲功章, MedalGrade.一級));
            MedalRegistry.Clear();
            Assert.AreEqual(0, MedalRegistry.Count(1));
            Assert.AreEqual(1.0f, MedalRegistry.PensionFactor(1), 1e-4f);
        }
    }
}
