using NUnit.Framework;
using UnityEngine;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 戦闘・移動の係数公式（#106）を固定する：提督能力→倍率（50で1.0/100で1.5/0で0.5）、防御→被ダメージ軽減
    /// （200で90%カット）、側背面倍率の線形補間と閾値、ModifierStack の積み上げ合成と下限クランプ。
    /// これらは FleetMovement/ShipCombat/FleetStrength の従来インライン計算と同一＝移行の挙動不変を担保する。
    /// </summary>
    public class CombatModifiersTests
    {
        // --- AbilityFactor（提督攻撃/機動。FleetMovement・ShipCombat の重複公式） ---

        [Test]
        public void AbilityFactor_Baseline50Is1_100Is1p5_0Is0p5()
        {
            Assert.AreEqual(1.0f, CombatModifiers.AbilityFactor(50f), 1e-5f);
            Assert.AreEqual(1.5f, CombatModifiers.AbilityFactor(100f), 1e-5f);
            Assert.AreEqual(0.5f, CombatModifiers.AbilityFactor(0f), 1e-5f);
        }

        [Test]
        public void AbilityFactor_MatchesLegacyInlineFormula()
        {
            // 従来式：1.0 + (stat - 50) / 100
            for (float stat = 0f; stat <= 100f; stat += 7f)
            {
                float legacy = 1.0f + (stat - 50f) / 100f;
                Assert.AreEqual(legacy, CombatModifiers.AbilityFactor(stat), 1e-5f);
            }
        }

        // --- DefenseDamageFactor（FleetStrength.TakeDamage の被ダメージ軽減公式） ---

        [Test]
        public void DefenseDamageFactor_0NoReduction_100Half_200CapsAt90pct()
        {
            Assert.AreEqual(1.0f, CombatModifiers.DefenseDamageFactor(0f), 1e-5f);   // 軽減なし
            Assert.AreEqual(0.5f, CombatModifiers.DefenseDamageFactor(100f), 1e-5f); // 50%カット
            Assert.AreEqual(0.1f, CombatModifiers.DefenseDamageFactor(200f), 1e-5f); // 上限90%カット
            Assert.AreEqual(0.1f, CombatModifiers.DefenseDamageFactor(400f), 1e-5f); // 上限でクランプ
        }

        [Test]
        public void DefenseDamageFactor_MatchesLegacyInlineFormula()
        {
            // 従来式：1.0 - Clamp(def/200, 0, 0.9)
            for (float def = 0f; def <= 400f; def += 17f)
            {
                float legacy = 1.0f - Mathf.Clamp(def / 200f, 0f, 0.9f);
                Assert.AreEqual(legacy, CombatModifiers.DefenseDamageFactor(def), 1e-5f);
            }
        }

        // --- FlankFactor（ShipCombat の側背面倍率） ---

        [Test]
        public void FlankFactor_FrontIsFull_RearIsFlankMin()
        {
            float min = 2.0f;
            // 正面(dot=1)＝1.0倍、側背面ヒットでない
            float front = CombatModifiers.FlankFactor(1f, min, out bool frontFlank);
            Assert.AreEqual(1.0f, front, 1e-5f);
            Assert.IsFalse(frontFlank);
            // 真後ろ(dot=-1)＝flankMin、側背面ヒット
            float rear = CombatModifiers.FlankFactor(-1f, min, out bool rearFlank);
            Assert.AreEqual(min, rear, 1e-5f);
            Assert.IsTrue(rearFlank);
        }

        [Test]
        public void FlankFactor_MatchesLegacyInlineFormula_AndThreshold()
        {
            float min = 2.0f;
            for (float dot = -1f; dot <= 1f; dot += 0.13f)
            {
                float legacy = Mathf.Lerp(min, 1.0f, (dot + 1.0f) / 2.0f);
                float actual = CombatModifiers.FlankFactor(dot, min, out bool isFlank);
                Assert.AreEqual(legacy, actual, 1e-5f);
                Assert.AreEqual(legacy >= 1.3f, isFlank); // 従来の isFlank = multiplier >= 1.3
            }
        }

        // --- ModifierStack（合成スタック） ---

        [Test]
        public void ModifierStack_StartsAtOne_AndMultiplies()
        {
            var m = ModifierStack.Start();
            Assert.AreEqual(1f, m.Value, 1e-5f);
            m.Mul(1.5f);
            m.Mul(0.5f);
            m.Mul(2f);
            Assert.AreEqual(1.5f, m.Value, 1e-5f); // 1*1.5*0.5*2
        }

        [Test]
        public void ModifierStack_ClampMin_PreventsCollapse()
        {
            var m = ModifierStack.Start();
            m.Mul(0.01f); // 極小へ
            Assert.AreEqual(0.2f, m.ClampMin(0.2f), 1e-5f); // 下限で底打ち
            Assert.AreEqual(0.01f, m.Value, 1e-5f);          // 生値は保持
        }

        [Test]
        public void ModifierStack_ComposesAbilityAndDefenseFactors()
        {
            // 機動100(1.5倍)×士気0.8×ZOC0.6 の合成が従来の factor *= ... と一致
            var m = ModifierStack.Start();
            m.Mul(CombatModifiers.AbilityFactor(100f));
            m.Mul(0.8f);
            m.Mul(0.6f);
            Assert.AreEqual(1.5f * 0.8f * 0.6f, m.Value, 1e-5f);
        }
    }
}
