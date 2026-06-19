using NUnit.Framework;
using UnityEngine;

namespace Ginei.Tests
{
    /// <summary>異端弾圧の純ロジックの EditMode テスト。既定 Params で期待値を固定。</summary>
    public class HereticSuppressionRulesTests
    {
        const float Eps = 1e-4f;

        [Test]
        public void HeresyDetection_WeightsDeviationAndVigilance()
        {
            // 0.8*0.6 + 0.6*0.4 = 0.48 + 0.24 = 0.72
            float d = HereticSuppressionRules.HeresyDetection(0.8f, 0.6f);
            Assert.AreEqual(0.72f, d, Eps);
        }

        [Test]
        public void HeresyDetection_NoDeviationOrVigilance_IsZero()
        {
            Assert.AreEqual(0f, HereticSuppressionRules.HeresyDetection(0f, 0f), Eps);
        }

        [Test]
        public void InquisitionSeverity_ZealAmplifies_AndClampsToOne()
        {
            // 0.5*(1+0.4*0.5) = 0.5*1.2 = 0.6
            Assert.AreEqual(0.6f, HereticSuppressionRules.InquisitionSeverity(0.5f, 0.4f), Eps);
            // 0.72*(1+0.8*0.5) = 0.72*1.4 = 1.008 -> clamp 1.0
            Assert.AreEqual(1f, HereticSuppressionRules.InquisitionSeverity(0.72f, 0.8f), Eps);
        }

        [Test]
        public void Intimidation_PublicExecutionAmplifies()
        {
            // 0.6*(1+0.8*0.5) = 0.6*1.4 = 0.84
            Assert.AreEqual(0.84f, HereticSuppressionRules.Intimidation(0.6f, 0.8f), Eps);
        }

        [Test]
        public void MartyrdomEffect_RisesWithConviction()
        {
            // 0.6*0.9*0.6 = 0.324
            Assert.AreEqual(0.324f, HereticSuppressionRules.MartyrdomEffect(0.6f, 0.9f), Eps);
            // 信念0なら殉教は生じない
            Assert.AreEqual(0f, HereticSuppressionRules.MartyrdomEffect(0.6f, 0f), Eps);
        }

        [Test]
        public void UndergroundFaith_SumsIntimidationAndMartyrdom()
        {
            // 0.84*0.5 + 0.324*0.5 = 0.42 + 0.162 = 0.582
            Assert.AreEqual(0.582f, HereticSuppressionRules.UndergroundFaith(0.84f, 0.324f), Eps);
        }

        [Test]
        public void SuppressionLegitimacy_WeightsConsensusAndThreat()
        {
            // 0.7*0.6 + 0.5*0.4 = 0.42 + 0.2 = 0.62
            Assert.AreEqual(0.62f, HereticSuppressionRules.SuppressionLegitimacy(0.7f, 0.5f), Eps);
        }

        [Test]
        public void ToleranceBenefit_WeightsHarmonyAndFreedom()
        {
            // 0.6*0.5 + 0.8*0.5 = 0.3 + 0.4 = 0.7
            Assert.AreEqual(0.7f, HereticSuppressionRules.ToleranceBenefit(0.6f, 0.8f), Eps);
        }

        [Test]
        public void NetSuppressionValue_SubtractsMartyrdomAndUnderground()
        {
            // 0.84 - 0.324 - 0.582 = -0.066
            Assert.AreEqual(-0.066f, HereticSuppressionRules.NetSuppressionValue(0.84f, 0.324f, 0.582f), Eps);
            // 純粋な威圧のみなら正
            Assert.AreEqual(0.8f, HereticSuppressionRules.NetSuppressionValue(0.8f, 0f, 0f), Eps);
        }

        [Test]
        public void Inputs_AreClamped_NoOutOfRange()
        {
            // 範囲外入力でも破綻しない
            float d = HereticSuppressionRules.HeresyDetection(5f, -3f);
            Assert.AreEqual(0.6f, d, Eps); // 1*0.6 + 0*0.4
            float net = HereticSuppressionRules.NetSuppressionValue(-9f, 9f, 9f);
            Assert.AreEqual(-1f, net, Eps); // 0 - 1 - 1 -> clamp -1
        }

        [Test]
        public void Narrative_HarshSuppressionBackfires()
        {
            // 物語：苛烈な弾圧の連鎖を流す。
            // 教義から大きく逸脱(0.9)した一派を、鋭い審問(0.9)が異端と認定する。
            float detect = HereticSuppressionRules.HeresyDetection(0.9f, 0.9f);
            // 0.9*0.6 + 0.9*0.4 = 0.54 + 0.36 = 0.9
            Assert.AreEqual(0.9f, detect, Eps);

            // 正統派が熱狂(0.9)し、宗教裁判は極めて苛烈になる。
            float severity = HereticSuppressionRules.InquisitionSeverity(detect, 0.9f);
            // 0.9*(1+0.9*0.5) = 0.9*1.45 = 1.305 -> clamp 1.0
            Assert.AreEqual(1f, severity, Eps);

            // 公開処刑(1.0)で見せしめの威圧は最大化する。
            float intim = HereticSuppressionRules.Intimidation(severity, 1f);
            Assert.AreEqual(1f, intim, Eps); // 1*(1+0.5) -> clamp 1.0

            // だが異端者の信念は固く(1.0)、その死は殉教となって信仰を固める。
            float martyr = HereticSuppressionRules.MartyrdomEffect(severity, 1f);
            // 1*1*0.6 = 0.6
            Assert.AreEqual(0.6f, martyr, Eps);

            // 威圧と殉教が信仰を地下へ深く潜らせる。
            float under = HereticSuppressionRules.UndergroundFaith(intim, martyr);
            // 1*0.5 + 0.6*0.5 = 0.5 + 0.3 = 0.8
            Assert.AreEqual(0.8f, under, Eps);

            // 弾圧の正味効果は逆効果＝苛烈さが信仰を強めてしまった。
            float net = HereticSuppressionRules.NetSuppressionValue(intim, martyr, under);
            // 1 - 0.6 - 0.8 = -0.4
            Assert.AreEqual(-0.4f, net, Eps);
            Assert.Less(net, 0f, "苛烈すぎる弾圧は逆効果（信仰を固め地下へ潜らせる）であるべき");

            // 対して、寛容の選択は社会の調和(0.7)と思想の自由(0.7)から利益を得る。
            float tolerance = HereticSuppressionRules.ToleranceBenefit(0.7f, 0.7f);
            // 0.7*0.5 + 0.7*0.5 = 0.7
            Assert.AreEqual(0.7f, tolerance, Eps);
            Assert.Greater(tolerance, net, "この局面では寛容の利益が弾圧の正味効果を上回るべき");
        }
    }
}
