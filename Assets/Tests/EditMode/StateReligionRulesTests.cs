using NUnit.Framework;
using UnityEngine;

namespace Ginei.Tests
{
    /// <summary>
    /// 国教（国家宗教）の純ロジック <see cref="StateReligionRules"/> の EditMode テスト。
    /// 既定 Params での期待値を手計算で固定（Sqrt 箇所は 1e-3、他は 1e-4）。
    /// </summary>
    public class StateReligionRulesTests
    {
        private const float Eps = 1e-4f;
        private const float SqrtEps = 1e-3f;

        [Test]
        public void LegitimationPower_GeometricMeanOfAuthorityAndFaith()
        {
            // sqrt(0.8*0.5)=sqrt(0.4)=0.6324555 × 1.0
            Assert.AreEqual(0.6324555f, StateReligionRules.LegitimationPower(0.8f, 0.5f), SqrtEps);
        }

        [Test]
        public void LegitimationPower_ZeroFaith_GivesZero()
        {
            // 信仰ゼロなら権威があっても正統化は立たない（幾何平均）
            Assert.AreEqual(0f, StateReligionRules.LegitimationPower(0.9f, 0f), Eps);
        }

        [Test]
        public void NationalUnification_LegitimationTimesHomogeneity()
        {
            // 0.6 * 0.5 * 1.0 = 0.3
            Assert.AreEqual(0.3f, StateReligionRules.NationalUnification(0.6f, 0.5f), Eps);
        }

        [Test]
        public void ChurchStateBalance_SignedByDominantPower()
        {
            // 世俗優位＝正
            Assert.AreEqual(0.4f, StateReligionRules.ChurchStateBalance(0.7f, 0.3f), Eps);
            // 聖職者優位＝負
            Assert.AreEqual(-0.6f, StateReligionRules.ChurchStateBalance(0.2f, 0.8f), Eps);
        }

        [Test]
        public void TheocraticTension_ClericalPowerTimesReform()
        {
            // 0.8 * 0.5 * 0.8 = 0.32
            Assert.AreEqual(0.32f, StateReligionRules.TheocraticTension(0.8f, 0.5f), Eps);
        }

        [Test]
        public void ReligiousMobilization_FaithTimesDoctrinalCall()
        {
            // 0.9 * 0.7 * 0.9 = 0.567
            Assert.AreEqual(0.567f, StateReligionRules.ReligiousMobilization(0.9f, 0.7f), Eps);
        }

        [Test]
        public void Secularization_GeometricMeanOfModernizationAndEducation()
        {
            // sqrt(0.8*0.5)=0.6324555 × 0.7 = 0.4427189
            Assert.AreEqual(0.4427189f, StateReligionRules.Secularization(0.8f, 0.5f), SqrtEps);
        }

        [Test]
        public void PoliticalExploitation_LegitimationTimesCynicism()
        {
            // 0.5 * 0.8 * 0.6 = 0.24
            Assert.AreEqual(0.24f, StateReligionRules.PoliticalExploitation(0.5f, 0.8f), Eps);
        }

        [Test]
        public void StateReligionStability_UnificationMinusTensionAndSecularization()
        {
            // 0.5 - 0.3*0.6 - 0.4*0.4 = 0.5 - 0.18 - 0.16 = 0.16
            Assert.AreEqual(0.16f, StateReligionRules.StateReligionStability(0.5f, 0.3f, 0.4f), Eps);
        }

        [Test]
        public void Inputs_AreClamped()
        {
            // 範囲外入力でも内部 Clamp で破綻しない
            Assert.AreEqual(StateReligionRules.LegitimationPower(1f, 1f),
                StateReligionRules.LegitimationPower(5f, 9f), Eps);
            Assert.AreEqual(0f, StateReligionRules.TheocraticTension(-3f, 0.5f), Eps);
            float bal = StateReligionRules.ChurchStateBalance(2f, -2f);
            Assert.GreaterOrEqual(bal, -1f);
            Assert.LessOrEqual(bal, 1f);
        }

        [Test]
        public void Narrative_DevoutHomogeneousState_IsStable()
        {
            // 厚い信仰・高い権威・同質・低緊張・低世俗化＝聖俗が調和した国教国家は安定する。
            float legit = StateReligionRules.LegitimationPower(0.9f, 0.8f); // sqrt(0.72)=0.8485281
            float unif = StateReligionRules.NationalUnification(legit, 0.9f); // 0.8485281*0.9=0.7636753
            float tension = StateReligionRules.TheocraticTension(0.3f, 0.2f); // 0.3*0.2*0.8=0.048
            float secular = StateReligionRules.Secularization(0.2f, 0.3f); // sqrt(0.06)=0.2449490 ×0.7=0.1714643
            float stability = StateReligionRules.StateReligionStability(unif, tension, secular);
            // 0.7636753 - 0.048*0.6 - 0.1714643*0.4 = 0.7636753 - 0.0288 - 0.0685857 = 0.6662896
            Assert.AreEqual(0.6662896f, stability, SqrtEps);
            Assert.Greater(stability, 0.5f, "調和した国教国家は安定（正）であるべき");

            // 対照：分極・高緊張・高世俗化＝政教対立で揺らぐ国は安定が低い。
            float legitLow = StateReligionRules.LegitimationPower(0.4f, 0.3f);
            float unifLow = StateReligionRules.NationalUnification(legitLow, 0.3f);
            float tensionHigh = StateReligionRules.TheocraticTension(0.9f, 0.9f);
            float secularHigh = StateReligionRules.Secularization(0.9f, 0.9f);
            float stabilityLow = StateReligionRules.StateReligionStability(unifLow, tensionHigh, secularHigh);
            Assert.Less(stabilityLow, stability, "対立し世俗化した国は調和国家より不安定であるべき");
        }
    }
}
