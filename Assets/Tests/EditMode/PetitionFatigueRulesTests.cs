using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>具申の乱発ペナルティ：無罰枠・超過件数・食傷倍率・実効採用率。</summary>
    public class PetitionFatigueRulesTests
    {
        private static readonly PetitionFatigueParams P = PetitionFatigueParams.Default; // 無罰2件／超過×0.8

        [Test]
        public void ExcessPending_BeyondFreeQuota()
        {
            Assert.AreEqual(0, PetitionFatigueRules.ExcessPending(2, P));
            Assert.AreEqual(0, PetitionFatigueRules.ExcessPending(1, P));
            Assert.AreEqual(3, PetitionFatigueRules.ExcessPending(5, P));
        }

        [Test]
        public void FatigueFactor_DecaysPerExtra()
        {
            Assert.AreEqual(1f, PetitionFatigueRules.FatigueFactor(2, P), 1e-4f);    // 無罰枠内
            Assert.AreEqual(0.8f, PetitionFatigueRules.FatigueFactor(3, P), 1e-4f);  // 超過1
            Assert.AreEqual(0.64f, PetitionFatigueRules.FatigueFactor(4, P), 1e-4f); // 超過2＝0.8²
        }

        [Test]
        public void EffectiveAdoptChance_AppliesFatigueAndClamps()
        {
            Assert.AreEqual(0.5f, PetitionFatigueRules.EffectiveAdoptChance(0.5f, 2, P), 1e-4f);   // 無罰
            Assert.AreEqual(0.4f, PetitionFatigueRules.EffectiveAdoptChance(0.5f, 3, P), 1e-4f);   // 0.5×0.8
            Assert.AreEqual(0.32f, PetitionFatigueRules.EffectiveAdoptChance(0.5f, 4, P), 1e-4f);  // 0.5×0.64
            Assert.AreEqual(1f, PetitionFatigueRules.EffectiveAdoptChance(2f, 0, P), 1e-4f);       // 上クランプ
        }
    }
}
