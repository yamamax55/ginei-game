using NUnit.Framework;
using UnityEngine;

namespace Ginei.Tests
{
    /// <summary>無権利者の創出（無国籍＝法外人口・TOTL-5 #1526）の純ロジックを担保する。</summary>
    public class StatelessnessRulesTests
    {
        private const float Eps = 1e-4f;

        /// <summary>無権利状態＝国籍剥奪が進み法的保護が薄いほど高い（剥奪×非保護×規模）。</summary>
        [Test]
        public void RightlessnessLevel_StrippedAndUnprotected_IsHigh()
        {
            // 剥奪0.8×(1−保護0.2)=0.8×0.8×1.0=0.64
            float r = StatelessnessRules.RightlessnessLevel(0.8f, 0.2f);
            Assert.AreEqual(0.64f, r, Eps);

            // 保護が完全（1.0）なら剥奪されても無権利にはならない
            Assert.AreEqual(0f, StatelessnessRules.RightlessnessLevel(1f, 1f), Eps);
        }

        /// <summary>法外に置かれた人口の規模＝剥奪人口比×総人口。</summary>
        [Test]
        public void StatelessPopulation_IsSharedTimesTotal()
        {
            // 0.5×0.4=0.2
            Assert.AreEqual(0.2f, StatelessnessRules.StatelessPopulation(0.5f, 0.4f), Eps);
            Assert.AreEqual(0f, StatelessnessRules.StatelessPopulation(0f, 1f), Eps);
        }

        /// <summary>法の保護の空白＝無権利状態に比例（誰も守らない領域の広さ）。</summary>
        [Test]
        public void ProtectionVoid_ScalesWithRightlessness()
        {
            // 0.6×1.0=0.6
            Assert.AreEqual(0.6f, StatelessnessRules.ProtectionVoid(0.6f), Eps);
        }

        /// <summary>国籍剥奪の進行＝迫害が無国籍者を増やす（全体主義の前段）。</summary>
        [Test]
        public void DenationalizationTick_PersecutionGrowsStateless()
        {
            // 0.3 + 迫害0.8×0.1×2=0.3+0.16=0.46
            float s = StatelessnessRules.DenationalizationTick(0.3f, 0.8f, 2f);
            Assert.AreEqual(0.46f, s, Eps);

            // 迫害0なら増えない
            Assert.AreEqual(0.3f, StatelessnessRules.DenationalizationTick(0.3f, 0f, 5f), Eps);
        }

        /// <summary>虐待への脆弱性＝無権利×加害者の不処罰（保護がないゆえ虐待され放題）。</summary>
        [Test]
        public void VulnerabilityToAbuse_RightlessAndImpunity_IsHigh()
        {
            // 0.7×0.9×0.9=0.567
            float v = StatelessnessRules.VulnerabilityToAbuse(0.7f, 0.9f);
            Assert.AreEqual(0.567f, v, Eps);

            // 不処罰0（必ず裁かれる）なら虐待は起きにくい
            Assert.AreEqual(0f, StatelessnessRules.VulnerabilityToAbuse(0.7f, 0f), Eps);
        }

        /// <summary>過激化の温床＝行き場のない無国籍者×絶望（運動の温床）。</summary>
        [Test]
        public void RadicalizationBreedingGround_StatelessAndDespair_IsHigh()
        {
            // 0.6×0.5×0.7=0.21
            float g = StatelessnessRules.RadicalizationBreedingGround(0.6f, 0.5f);
            Assert.AreEqual(0.21f, g, Eps);
        }

        /// <summary>再統合コスト＝受け入れ能力が薄いほど嵩む（剥がすは一瞬・戻すは高い）。</summary>
        [Test]
        public void ReintegrationCost_LowCapacity_CostsMore()
        {
            // 能力0.5：0.4×1.2/0.5=0.96
            float cHi = StatelessnessRules.ReintegrationCost(0.4f, 0.5f);
            Assert.AreEqual(0.96f, cHi, Eps);

            // 能力1.0：0.4×1.2/1.0=0.48＝能力が高いほど安い
            float cLo = StatelessnessRules.ReintegrationCost(0.4f, 1.0f);
            Assert.AreEqual(0.48f, cLo, Eps);
            Assert.Greater(cHi, cLo);
        }

        /// <summary>権利を持つ権利の崩壊判定＝無権利状態が閾値以上で成立。</summary>
        [Test]
        public void IsRightsCollapse_AboveThreshold()
        {
            Assert.IsTrue(StatelessnessRules.IsRightsCollapse(0.8f, 0.7f));
            Assert.IsFalse(StatelessnessRules.IsRightsCollapse(0.5f, 0.7f));
        }
    }
}
