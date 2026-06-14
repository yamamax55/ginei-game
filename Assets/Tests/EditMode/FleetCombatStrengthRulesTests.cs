using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 補給×技術×軍の質→実効戦力倍率（FleetCombatStrengthRules）を固定する。
    /// 全中立で1.0／補給0で大幅減／満補給で1.0近辺／技術で増／質で増／bounded／各入力で単調非減少。
    /// 委譲先（MilitaryReadinessRules/TechEffectRules）整合。
    /// </summary>
    public class FleetCombatStrengthRulesTests
    {
        [Test]
        public void EffectiveCombatFactor_AllNeutral_IsOne()
        {
            // 満補給1.0・技術0段・質中立1.0 → 1.0（後方互換）。
            float f = FleetCombatStrengthRules.EffectiveCombatFactor(1f, 0f, 1f);
            Assert.AreEqual(1f, f, 1e-4f);
        }

        [Test]
        public void EffectiveCombatFactor_NoSupply_StronglyReduced()
        {
            // 補給0 → 補給火力係数0.1 → 全体も大幅減（満補給より小さい）。
            float empty = FleetCombatStrengthRules.EffectiveCombatFactor(0f, 0f, 1f);
            float full = FleetCombatStrengthRules.EffectiveCombatFactor(1f, 0f, 1f);
            Assert.Less(empty, full);
            Assert.AreEqual(0.1f, empty, 1e-4f); // FirepowerFactor(0)=0.1×tech1×quality1
        }

        [Test]
        public void EffectiveCombatFactor_HigherTech_Increases()
        {
            float low = FleetCombatStrengthRules.EffectiveCombatFactor(1f, 0f, 1f);
            float high = FleetCombatStrengthRules.EffectiveCombatFactor(1f, 5f, 1f);
            Assert.Greater(high, low);
        }

        [Test]
        public void EffectiveCombatFactor_HigherQuality_Increases()
        {
            float low = FleetCombatStrengthRules.EffectiveCombatFactor(1f, 0f, 0.8f);
            float high = FleetCombatStrengthRules.EffectiveCombatFactor(1f, 0f, 1.5f);
            Assert.Greater(high, low);
        }

        [Test]
        public void EffectiveCombatFactor_Bounded()
        {
            // 極端な入力でも MinFactor〜MaxFactor に収まる。
            float low = FleetCombatStrengthRules.EffectiveCombatFactor(0f, 0f, 0f);
            float high = FleetCombatStrengthRules.EffectiveCombatFactor(1f, 1000f, 1000f);
            Assert.GreaterOrEqual(low, FleetCombatStrengthRules.MinFactor - 1e-4f);
            Assert.AreEqual(FleetCombatStrengthRules.MinFactor, low, 1e-4f);
            Assert.LessOrEqual(high, FleetCombatStrengthRules.MaxFactor + 1e-4f);
            Assert.AreEqual(FleetCombatStrengthRules.MaxFactor, high, 1e-4f);
        }

        [Test]
        public void EffectiveCombatFactor_MonotonicInSupply()
        {
            float prev = -1f;
            for (float s = 0f; s <= 1f; s += 0.2f)
            {
                float f = FleetCombatStrengthRules.EffectiveCombatFactor(s, 1f, 1f);
                Assert.GreaterOrEqual(f, prev - 1e-5f);
                prev = f;
            }
        }

        [Test]
        public void EffectiveStrength_ScalesRawByFactor()
        {
            float factor = FleetCombatStrengthRules.EffectiveCombatFactor(1f, 5f, 1.2f);
            float strength = FleetCombatStrengthRules.EffectiveStrength(100f, 1f, 5f, 1.2f);
            Assert.AreEqual(100f * factor, strength, 1e-3f);
        }

        [Test]
        public void EffectiveStrength_NegativeRaw_IsZero()
        {
            float strength = FleetCombatStrengthRules.EffectiveStrength(-50f, 1f, 0f, 1f);
            Assert.AreEqual(0f, strength, 1e-4f);
        }
    }
}
