using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>ADM-6 専門領域・非線形性：得意戦型の状況一致ボーナス・90+の天才の質的飛躍。</summary>
    public class SpecialtyRulesTests
    {
        [Test]
        public void SpecialtyBonus_MatchesSituation()
        {
            Assert.IsTrue(SpecialtyRules.Matches(CombatSpecialty.防衛, BattleSituation.防衛));
            Assert.IsFalse(SpecialtyRules.Matches(CombatSpecialty.防衛, BattleSituation.会戦));
            Assert.IsFalse(SpecialtyRules.Matches(CombatSpecialty.なし, BattleSituation.会戦));

            Assert.AreEqual(1.15f, SpecialtyRules.SpecialtyBonus(CombatSpecialty.防衛, BattleSituation.防衛), 1e-4f);
            Assert.AreEqual(1.0f, SpecialtyRules.SpecialtyBonus(CombatSpecialty.防衛, BattleSituation.会戦), 1e-4f);
            Assert.AreEqual(1.0f, SpecialtyRules.SpecialtyBonus(CombatSpecialty.なし, BattleSituation.機動戦), 1e-4f);
        }

        [Test]
        public void GeniusFactor_BreakpointAbove90()
        {
            Assert.AreEqual(1.0f, SpecialtyRules.GeniusFactor(80f), 1e-4f);
            Assert.AreEqual(1.0f, SpecialtyRules.GeniusFactor(90f), 1e-4f);
            Assert.AreEqual(1.1f, SpecialtyRules.GeniusFactor(95f), 1e-4f);
            Assert.AreEqual(1.2f, SpecialtyRules.GeniusFactor(100f), 1e-4f);
        }
    }
}
