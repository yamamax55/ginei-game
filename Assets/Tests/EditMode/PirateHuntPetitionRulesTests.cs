using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>海賊狩り具申：effectKey 判別と他キーとの区別。</summary>
    public class PirateHuntPetitionRulesTests
    {
        [Test]
        public void IsPiratePetition_MatchesOnlyOwnKey()
        {
            Assert.AreEqual("mission.pirate", PirateHuntPetitionRules.EffectKey);
            Assert.IsTrue(PirateHuntPetitionRules.IsPiratePetition("mission.pirate"));
            Assert.IsFalse(PirateHuntPetitionRules.IsPiratePetition("career.petition"));
            Assert.IsFalse(PirateHuntPetitionRules.IsPiratePetition("fleet.budget:0:5000"));
            Assert.IsFalse(PirateHuntPetitionRules.IsPiratePetition(null));
        }

        [Test]
        public void FameReward_IsPositive()
        {
            Assert.Greater(PirateHuntPetitionRules.FameReward, 0);
        }

        [Test]
        public void NotClassifiedAsBudgetOrBase()
        {
            // 海賊狩りキーは予算/根拠地として decode されない（採用効果は武名のみ）。
            Assert.AreEqual(PetitionEffect.なし, PetitionEffectRules.Classify(PirateHuntPetitionRules.EffectKey));
        }
    }
}
