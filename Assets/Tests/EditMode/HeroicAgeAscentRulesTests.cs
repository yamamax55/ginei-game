using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>英雄時代に至るまで（キングダム）：乱世→輩出圧力→英雄の出現→至る目標→局面（泰平/胎動/群雄/英雄時代）。</summary>
    public class HeroicAgeAscentRulesTests
    {
        [Test]
        public void Turmoil_FromWarAndInstability()
        {
            Assert.AreEqual(1.0f, HeroicAgeAscentRules.Turmoil(1f, 1f), 1e-4f);
            Assert.AreEqual(0f, HeroicAgeAscentRules.Turmoil(0f, 0f), 1e-4f);
            Assert.AreEqual(0.5f, HeroicAgeAscentRules.Turmoil(0.5f, 0.5f), 1e-4f);
            Assert.AreEqual(0.6f, HeroicAgeAscentRules.Turmoil(1f, 0f), 1e-4f); // 戦乱の重み0.6
        }

        [Test]
        public void EmergencePressure_TurmoilBreedsHeroes_PeaceDoesNot()
        {
            // 乱世1・英雄度0（EmergenceMultiplier0.5）＝0.5。
            Assert.AreEqual(0.5f, HeroicAgeAscentRules.EmergencePressure(1f, 0f), 1e-4f);
            // 乱世1・英雄度1（EmergenceMultiplier1.5）＝1.5（英雄が次代を呼ぶ）。
            Assert.AreEqual(1.5f, HeroicAgeAscentRules.EmergencePressure(1f, 1f), 1e-4f);
            // 泰平（乱世0）は英雄を生まない。
            Assert.AreEqual(0f, HeroicAgeAscentRules.EmergencePressure(0f, 0.5f), 1e-4f);
        }

        [Test]
        public void HeroEmergence_DeterministicByRoll()
        {
            Assert.AreEqual(0.1f, HeroicAgeAscentRules.HeroEmergenceChance(1f, 0f, 0.2f), 1e-4f); // 0.2×0.5
            Assert.AreEqual(0.3f, HeroicAgeAscentRules.HeroEmergenceChance(1f, 1f, 0.2f), 1e-4f); // 0.2×1.5
            Assert.AreEqual(0f, HeroicAgeAscentRules.HeroEmergenceChance(0f, 1f, 0.2f), 1e-4f);   // 泰平

            Assert.IsTrue(HeroicAgeAscentRules.Emerges(1f, 1f, 0.2f, 0.2f));   // roll<0.3
            Assert.IsFalse(HeroicAgeAscentRules.Emerges(1f, 1f, 0.2f, 0.35f));
            Assert.IsFalse(HeroicAgeAscentRules.Emerges(0f, 1f, 0.2f, 0.0f));  // 泰平は興らない
        }

        [Test]
        public void AscentTarget_TurmoilLeads_HeroesRealizeTheAge()
        {
            // 乱世満ちるが英雄未輩出＝胎動どまり（移行期 0.4）。
            Assert.AreEqual(0.4f, HeroicAgeAscentRules.AscentTarget(1.0f, 0.0f), 1e-4f);
            // 乱世＋英雄が揃う＝英雄時代が満ちる（1.0）。
            Assert.AreEqual(1.0f, HeroicAgeAscentRules.AscentTarget(1.0f, 0.15f), 1e-4f);
            // 泰平でも英雄がいれば英雄度は持ち上がる（0.6）。
            Assert.AreEqual(0.6f, HeroicAgeAscentRules.AscentTarget(0.0f, 0.15f), 1e-4f);
            Assert.AreEqual(0f, HeroicAgeAscentRules.AscentTarget(0f, 0f), 1e-4f);
        }

        [Test]
        public void StageFor_PathToHeroicAge()
        {
            Assert.AreEqual(AscentStage.英雄時代, HeroicAgeAscentRules.StageFor(0.7f, 0.9f));
            Assert.AreEqual(AscentStage.群雄, HeroicAgeAscentRules.StageFor(0.3f, 0.7f));
            Assert.AreEqual(AscentStage.胎動, HeroicAgeAscentRules.StageFor(0.2f, 0.5f));
            Assert.AreEqual(AscentStage.泰平, HeroicAgeAscentRules.StageFor(0.1f, 0.2f));

            Assert.IsTrue(HeroicAgeAscentRules.IsStirring(0.2f, 0.5f));
            Assert.IsFalse(HeroicAgeAscentRules.IsStirring(0.1f, 0.2f));  // 泰平
            Assert.IsFalse(HeroicAgeAscentRules.IsStirring(0.7f, 0.9f));  // 既に英雄時代
        }
    }
}
