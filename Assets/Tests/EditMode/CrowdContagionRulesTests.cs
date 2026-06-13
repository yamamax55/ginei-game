using NUnit.Framework;
using UnityEngine;

namespace Ginei.Tests
{
    /// <summary>
    /// 群衆心理＝群衆化の相転移と集団精神（CRWD-1 #1820）の EditMode テスト。
    /// 既定 Params で期待値を固定（許容 1e-4f、Pow/Sqrt 箇所のみ緩める）。
    /// </summary>
    public class CrowdContagionRulesTests
    {
        private const float Eps = 1e-4f;

        [Test]
        public void CrowdIntensity_WeightedAverageOfThreeFactors()
        {
            // (0.9+0.8+0.7)/3 = 0.8
            float c = CrowdContagionRules.CrowdIntensity(0.9f, 0.8f, 0.7f);
            Assert.AreEqual(0.8f, c, Eps);
        }

        [Test]
        public void PhaseTransition_TrueOnlyAboveThreshold()
        {
            Assert.IsTrue(CrowdContagionRules.PhaseTransition(0.8f, 0.6f));
            Assert.IsFalse(CrowdContagionRules.PhaseTransition(0.5f, 0.6f));
        }

        [Test]
        public void Suggestibility_RisesSuperlinearlyWithIntensity()
        {
            // 0.64 * sqrt(0.64) = 0.64 * 0.8 = 0.512
            float s = CrowdContagionRules.Suggestibility(0.64f);
            Assert.AreEqual(0.512f, s, 1e-3f);
        }

        [Test]
        public void RationalityDrop_LinearUpToMax()
        {
            // 0.5 * 0.8 = 0.4
            float d = CrowdContagionRules.RationalityDrop(0.5f);
            Assert.AreEqual(0.4f, d, Eps);
            // 強度1で最大幅 0.8
            Assert.AreEqual(0.8f, CrowdContagionRules.RationalityDrop(1f), Eps);
        }

        [Test]
        public void CollectiveMind_RequiresSharedFocus()
        {
            // 0.8 * 0.5 = 0.4
            Assert.AreEqual(0.4f, CrowdContagionRules.CollectiveMind(0.8f, 0.5f), Eps);
            // 焦点ゼロは集団精神も立たない
            Assert.AreEqual(0f, CrowdContagionRules.CollectiveMind(0.9f, 0f), Eps);
        }

        [Test]
        public void IndividualSubmersion_StrongIdentityResistsDissolving()
        {
            // 0.8 * (1-0.25) = 0.6
            Assert.AreEqual(0.6f, CrowdContagionRules.IndividualSubmersion(0.8f, 0.25f), Eps);
            // 完全な自我は溶けない
            Assert.AreEqual(0f, CrowdContagionRules.IndividualSubmersion(0.9f, 1f), Eps);
        }

        [Test]
        public void LevelShiftHysteresis_CrowdStateIsStickyToCollapse()
        {
            // 既に群衆：戻り閾値 0.6-0.2=0.4。0.45>0.4 なので維持される。
            Assert.IsTrue(CrowdContagionRules.LevelShiftHysteresis(true, 0.45f, 0.6f));
            // 非群衆：素の閾値 0.6 を超えないので群衆化しない。
            Assert.IsFalse(CrowdContagionRules.LevelShiftHysteresis(false, 0.45f, 0.6f));
        }

        [Test]
        public void ContagionSusceptibility_CriticalThinkingResists()
        {
            // 0.6 * (1-0.5) = 0.3
            Assert.AreEqual(0.3f, CrowdContagionRules.ContagionSusceptibility(0.6f, 0.5f), Eps);
        }

        [Test]
        public void Story_DenseExcitedCrowdPhaseShiftsAndDrownsRationalityButStrongSelfResists()
        {
            // 密集・興奮・匿名の群衆＝強度が高い
            var p = CrowdContagionParams.Default;
            float intensity = CrowdContagionRules.CrowdIntensity(0.9f, 0.9f, 0.9f, p); // = 0.9
            Assert.AreEqual(0.9f, intensity, Eps);

            // 閾値 0.5 を超えて相転移＝群衆状態
            Assert.IsTrue(CrowdContagionRules.PhaseTransition(intensity, 0.5f));
            Assert.IsTrue(CrowdContagionRules.IsCrowdState(intensity, 0.5f));

            // 被暗示性が上がり、理性が下がる
            float sugg = CrowdContagionRules.Suggestibility(intensity);
            float drop = CrowdContagionRules.RationalityDrop(intensity, p);
            Assert.Greater(sugg, 0.7f);          // 高強度で被暗示性は跳ねる
            Assert.AreEqual(0.72f, drop, Eps);   // 0.9 * 0.8

            // 共通の焦点があれば集団精神が立ち上がる
            float mind = CrowdContagionRules.CollectiveMind(intensity, 0.8f);
            Assert.Greater(mind, 0.5f);

            // しかし強い自我（0.8）は溶けにくい：弱い自我（0.1）より埋没が浅い
            float weakSelf = CrowdContagionRules.IndividualSubmersion(intensity, 0.1f);
            float strongSelf = CrowdContagionRules.IndividualSubmersion(intensity, 0.8f);
            Assert.Greater(weakSelf, strongSelf);
            Assert.AreEqual(intensity * 0.2f, strongSelf, Eps); // 0.9 * (1-0.8)
        }
    }
}
