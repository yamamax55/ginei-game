using NUnit.Framework;
using UnityEngine;

namespace Ginei.Tests
{
    /// <summary>
    /// HostageExchangeRules（人質交換＝政治的人質の授受と信義）の EditMode テスト。
    /// 既定 Params で期待値を固定（積和・Lerp のみで Pow/Sqrt は無いため一律 1e-4f）。
    /// </summary>
    public class HostageExchangeRulesTests
    {
        private const float Eps = 1e-4f;

        [Test]
        public void HostageValue_WeightedMixOfRankAndTies()
        {
            // (0.8*0.5 + 0.4*0.5)/1.0 = 0.6
            float v = HostageExchangeRules.HostageValue(0.8f, 0.4f);
            Assert.AreEqual(0.6f, v, Eps);
        }

        [Test]
        public void AllianceGuarantee_ValueTimesReliability()
        {
            // 0.6 * 0.8 = 0.48
            float g = HostageExchangeRules.AllianceGuarantee(0.6f, 0.8f);
            Assert.AreEqual(0.48f, g, Eps);

            // 信義ゼロなら価値が高くても担保にならない。
            Assert.AreEqual(0f, HostageExchangeRules.AllianceGuarantee(1f, 0f), Eps);
        }

        [Test]
        public void HostageTreatment_MagnanimityBaseLiftedByValue()
        {
            // Lerp(0.5,1,0.6)=0.8 ; 0.8*0.5 = 0.4
            float t = HostageExchangeRules.HostageTreatment(0.5f, 0.6f);
            Assert.AreEqual(0.4f, t, Eps);

            // 度量ゼロなら待遇もゼロ。
            Assert.AreEqual(0f, HostageExchangeRules.HostageTreatment(0f, 1f), Eps);
        }

        [Test]
        public void HostageGiverAnxiety_HighWithLowReliability()
        {
            // 0.6 * (1 - 0.8) = 0.12
            float a = HostageExchangeRules.HostageGiverAnxiety(0.6f, 0.8f);
            Assert.AreEqual(0.12f, a, Eps);

            // 信義が満点なら不安は消える。
            Assert.AreEqual(0f, HostageExchangeRules.HostageGiverAnxiety(0.6f, 1f), Eps);
        }

        [Test]
        public void ReleaseCondition_RequiresBothFulfillmentAndTrust()
        {
            // 1.0 * 0.7 = 0.7
            Assert.AreEqual(0.7f, HostageExchangeRules.ReleaseCondition(1f, 0.7f), Eps);
            // 信頼が無ければ義務を果たしても解放は進まない。
            Assert.AreEqual(0f, HostageExchangeRules.ReleaseCondition(1f, 0f), Eps);
        }

        [Test]
        public void ExecutionThreatLeverage_ValueTimesCredibility()
        {
            // 0.8 * 0.9 = 0.72
            Assert.AreEqual(0.72f, HostageExchangeRules.ExecutionThreatLeverage(0.8f, 0.9f), Eps);
        }

        [Test]
        public void ThreatBacklash_RisesWithLeverage()
        {
            // 0.72 * ceiling(1.0) = 0.72
            Assert.AreEqual(0.72f, HostageExchangeRules.ThreatBacklash(0.72f), Eps);
            // てこが強いほど逆効果も大きい（単調）。
            Assert.Greater(HostageExchangeRules.ThreatBacklash(0.9f), HostageExchangeRules.ThreatBacklash(0.2f));
        }

        [Test]
        public void IsExchangeStable_NeedsGuaranteeAndLowAnxiety()
        {
            // 担保 0.48 < しきい 0.5 → 不安定。
            Assert.IsFalse(HostageExchangeRules.IsExchangeStable(0.48f, 0.12f));
            // 担保 0.6 ≥ 0.5・不安 0.12 ≤ 0.6 → 安定。
            Assert.IsTrue(HostageExchangeRules.IsExchangeStable(0.6f, 0.12f));
            // 不安が高すぎれば担保があっても不安定。
            Assert.IsFalse(HostageExchangeRules.IsExchangeStable(0.9f, 0.7f));
        }

        [Test]
        public void Clamp_HandlesOutOfRangeInputs()
        {
            // 範囲外入力でも 0..1 に収まる。
            Assert.AreEqual(1f, HostageExchangeRules.HostageValue(5f, 5f), Eps);
            Assert.AreEqual(0f, HostageExchangeRules.HostageGiverAnxiety(-3f, 2f), Eps);
            Assert.AreEqual(1f, HostageExchangeRules.ThreatBacklash(2f), Eps);
        }

        [Test]
        public void Narrative_FezzanGuaranteesAllianceButThreatBacklashesOnExecution()
        {
            // 物語：高位の王族（地位高・絆厚）を信義ある相手に預け同盟を担保する。
            float value = HostageExchangeRules.HostageValue(0.9f, 0.8f); // (0.45+0.4)=0.85
            Assert.AreEqual(0.85f, value, Eps);

            // 信義ある預け先＝担保力が高く・出す側の不安は小さい・安定。
            float guarantee = HostageExchangeRules.AllianceGuarantee(value, 0.85f); // 0.7225
            float anxiety = HostageExchangeRules.HostageGiverAnxiety(value, 0.85f); // 0.1275
            Assert.AreEqual(0.7225f, guarantee, Eps);
            Assert.AreEqual(0.1275f, anxiety, Eps);
            Assert.IsTrue(HostageExchangeRules.IsExchangeStable(guarantee, anxiety));

            // 殺害威嚇は短期のてこになるが、実行すれば信義喪失・敵結束の逆効果。
            float leverage = HostageExchangeRules.ExecutionThreatLeverage(value, 0.9f); // 0.765
            float backlash = HostageExchangeRules.ThreatBacklash(leverage);            // 0.765
            Assert.AreEqual(0.765f, leverage, Eps);
            Assert.AreEqual(0.765f, backlash, Eps);
            // てこより逆効果は等価以上に大きく響く（威嚇の代償）。
            Assert.GreaterOrEqual(backlash, leverage - Eps);
        }
    }
}
