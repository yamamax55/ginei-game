using NUnit.Framework;
using UnityEngine;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>技術水準→盤面効果の橋渡し <see cref="TechEffectRules"/> の純ロジック検証（#1065 隣）。</summary>
    public class TechEffectRulesTests
    {
        const float Eps = 1e-4f;

        [Test]
        public void TechLevelZero_AllFactorsAreOne()
        {
            // techLevel=0 で全倍率1.0（後方互換＝技術未進展なら従来挙動）。
            Assert.AreEqual(1f, TechEffectRules.CombatStrengthFactor(0f), Eps);
            Assert.AreEqual(1f, TechEffectRules.BuildSpeedFactor(0f), Eps);
            Assert.AreEqual(1f, TechEffectRules.ProductivityFactor(0f), Eps);
        }

        [Test]
        public void HigherTechLevel_IncreasesFactorsMonotonically()
        {
            // 技術が進むほど倍率は単調増（飽和前の領域で）。
            Assert.Greater(TechEffectRules.CombatStrengthFactor(2f), TechEffectRules.CombatStrengthFactor(1f));
            Assert.Greater(TechEffectRules.CombatStrengthFactor(5f), TechEffectRules.CombatStrengthFactor(2f));
            Assert.Greater(TechEffectRules.BuildSpeedFactor(3f), TechEffectRules.BuildSpeedFactor(1f));
            Assert.Greater(TechEffectRules.ProductivityFactor(4f), TechEffectRules.ProductivityFactor(2f));
        }

        [Test]
        public void LinearFormula_MatchesParams()
        {
            // 1 + techLevel×係数（飽和前）を厳密一致で固定。
            var p = TechEffectParams.Default;
            Assert.AreEqual(1f + 3f * p.combatPerLevel, TechEffectRules.CombatStrengthFactor(3f, p), Eps);
            Assert.AreEqual(1f + 3f * p.buildPerLevel, TechEffectRules.BuildSpeedFactor(3f, p), Eps);
            Assert.AreEqual(1f + 3f * p.productivityPerLevel, TechEffectRules.ProductivityFactor(3f, p), Eps);
        }

        [Test]
        public void VeryHighTechLevel_ClampsToCap()
        {
            // 巨大な techLevel でも上限（飽和）で頭打ち＝技術だけで無限に伸びない。
            var p = TechEffectParams.Default;
            Assert.AreEqual(p.combatCap, TechEffectRules.CombatStrengthFactor(10000f, p), Eps);
            Assert.AreEqual(p.buildCap, TechEffectRules.BuildSpeedFactor(10000f, p), Eps);
            Assert.AreEqual(p.productivityCap, TechEffectRules.ProductivityFactor(10000f, p), Eps);
        }

        [Test]
        public void CapIsNotExceeded_AcrossRange()
        {
            // 任意の段で上限を超えない。
            var p = TechEffectParams.Default;
            for (float lv = 0f; lv <= 100f; lv += 1f)
            {
                Assert.LessOrEqual(TechEffectRules.CombatStrengthFactor(lv, p), p.combatCap + Eps);
                Assert.LessOrEqual(TechEffectRules.BuildSpeedFactor(lv, p), p.buildCap + Eps);
                Assert.LessOrEqual(TechEffectRules.ProductivityFactor(lv, p), p.productivityCap + Eps);
            }
        }

        [Test]
        public void NegativeTechLevel_IsTreatedAsZero_NeverBelowOne()
        {
            // 負の techLevel は0扱い＝倍率は1.0以上を保証（後退しない）。
            Assert.AreEqual(1f, TechEffectRules.CombatStrengthFactor(-5f), Eps);
            Assert.AreEqual(1f, TechEffectRules.BuildSpeedFactor(-100f), Eps);
            Assert.AreEqual(1f, TechEffectRules.ProductivityFactor(-1f), Eps);
            Assert.GreaterOrEqual(TechEffectRules.CombatStrengthFactor(-5f), 1f);
        }
    }
}
