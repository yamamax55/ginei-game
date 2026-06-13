using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>CDR-6 一騎討ち・宿敵：宿敵判定・発生率（性格/功名心/宿敵）・武勇＋武名で勝敗。</summary>
    public class DuelRulesTests
    {
        [Test]
        public void Nemesis_LowAffinity()
        {
            Assert.IsTrue(DuelRules.IsNemesis(0.2f));
            Assert.IsTrue(DuelRules.IsNemesis(0.1f));
            Assert.IsFalse(DuelRules.IsNemesis(0.3f));
        }

        [Test]
        public void DuelChance_BoldAmbitiousNemesis()
        {
            Assert.AreEqual(0.3f, DuelRules.DuelChance(CommanderPersonality.果敢, 50, false), 1e-4f);
            Assert.AreEqual(0.1f, DuelRules.DuelChance(CommanderPersonality.慎重, 50, false), 1e-4f);
            Assert.AreEqual(0.6f, DuelRules.DuelChance(CommanderPersonality.果敢, 50, true), 1e-4f);
            Assert.AreEqual(0.85f, DuelRules.DuelChance(CommanderPersonality.激情, 100, true), 1e-4f);
        }

        [Test]
        public void WinProbability_AndResolve()
        {
            Assert.AreEqual(0.5f, DuelRules.WinProbability(80, 90, 80, 90), 1e-4f);
            Assert.AreEqual(0.7222f, DuelRules.WinProbability(100, 100, 50, 0), 1e-3f);
            Assert.IsTrue(DuelRules.ResolveDuel(100, 100, 50, 0, 0.5f));
            Assert.IsFalse(DuelRules.ResolveDuel(100, 100, 50, 0, 0.8f));
        }
    }
}
