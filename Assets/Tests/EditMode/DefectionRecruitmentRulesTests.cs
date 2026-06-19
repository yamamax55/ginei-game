using NUnit.Framework;
using UnityEngine;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 寝返り工作 <see cref="DefectionRecruitmentRules"/> の純ロジック検証。
    /// 乗りやすさ・見返りの効き・忠誠の抵抗・寝返り確率・二重スパイのリスク・露見リスク・要人の価値・寝返り判定。
    /// 既定 DefectionRecruitmentParams（不満0.5・野心0.3・疑念0.2・見返り0.6・恐れ0.7・露見0.5・閾値0.5）で期待値固定。
    /// </summary>
    public class DefectionRecruitmentRulesTests
    {
        const float Eps = 1e-4f;

        /// <summary>乗りやすさ＝不満・野心・疑念の加重平均。</summary>
        [Test]
        public void TargetSusceptibility_kajuuheikin()
        {
            // (0.8*0.5 + 0.4*0.3 + 0.6*0.2)/1.0 = 0.4 + 0.12 + 0.12 = 0.64
            Assert.AreEqual(0.64f, DefectionRecruitmentRules.TargetSusceptibility(0.8f, 0.4f, 0.6f), Eps);
            // 不満も野心も疑念も無ければ乗らない
            Assert.AreEqual(0f, DefectionRecruitmentRules.TargetSusceptibility(0f, 0f, 0f), Eps);
            // 不満が大きいほど乗りやすい
            Assert.Greater(
                DefectionRecruitmentRules.TargetSusceptibility(0.9f, 0.3f, 0.3f),
                DefectionRecruitmentRules.TargetSusceptibility(0.2f, 0.3f, 0.3f));
        }

        /// <summary>見返りの効き＝見返り×rewardScale×乗りやすさ。乗りにくい相手には効きにくい。</summary>
        [Test]
        public void Inducement_norikiniyorukouka()
        {
            // 1.0 * 0.6 * 0.5 = 0.3
            Assert.AreEqual(0.3f, DefectionRecruitmentRules.Inducement(1.0f, 0.5f), Eps);
            // 乗り気でない相手には同じ見返りも効きが薄い
            Assert.Greater(
                DefectionRecruitmentRules.Inducement(1.0f, 0.8f),
                DefectionRecruitmentRules.Inducement(1.0f, 0.2f));
        }

        /// <summary>忠誠の抵抗＝忠誠と報復の恐れの合成。固いほど寝返らない。</summary>
        [Test]
        public void LoyaltyResistance_chuuseitokyoufu()
        {
            // fear = 0.5*0.7 = 0.35; 1-(1-0.6)(1-0.35) = 1-0.4*0.65 = 1-0.26 = 0.74
            Assert.AreEqual(0.74f, DefectionRecruitmentRules.LoyaltyResistance(0.6f, 0.5f), Eps);
            // 忠誠も恐れも無ければ抵抗ゼロ
            Assert.AreEqual(0f, DefectionRecruitmentRules.LoyaltyResistance(0f, 0f), Eps);
            // 報復の恐れが大きいほど抵抗が固い
            Assert.Greater(
                DefectionRecruitmentRules.LoyaltyResistance(0.3f, 0.9f),
                DefectionRecruitmentRules.LoyaltyResistance(0.3f, 0.1f));
        }

        /// <summary>寝返る確率＝見返りの効き×(1−抵抗)。抵抗が完全なら寝返らない。</summary>
        [Test]
        public void DefectionProbability_kouicarmu()
        {
            // 0.6 * (1-0.25) = 0.45
            Assert.AreEqual(0.45f, DefectionRecruitmentRules.DefectionProbability(0.6f, 0.25f), Eps);
            // 抵抗が完全なら見返りに関わらず寝返らない
            Assert.AreEqual(0f, DefectionRecruitmentRules.DefectionProbability(1.0f, 1.0f), Eps);
        }

        /// <summary>二重スパイのリスク＝乗りやすさ×不自然な乗り気。簡単に前のめりな相手は罠。</summary>
        [Test]
        public void DoubleAgentRisk_norikisugiwawana()
        {
            // 0.8 * 0.5 = 0.4
            Assert.AreEqual(0.4f, DefectionRecruitmentRules.DoubleAgentRisk(0.8f, 0.5f), Eps);
            // 前のめりでなければ罠のリスクは小さい
            Assert.Greater(
                DefectionRecruitmentRules.DoubleAgentRisk(0.8f, 0.9f),
                DefectionRecruitmentRules.DoubleAgentRisk(0.8f, 0.2f));
        }

        /// <summary>露見リスク＝接触の大胆さ×敵防諜×exposureScale。大胆なほどバレる。</summary>
        [Test]
        public void ApproachExposure_daitansetoubousai()
        {
            // 0.8 * 0.5 * 0.5 = 0.2
            Assert.AreEqual(0.2f, DefectionRecruitmentRules.ApproachExposure(0.8f, 0.5f), Eps);
            // 敵の防諜が弱ければ露見しにくい
            Assert.Greater(
                DefectionRecruitmentRules.ApproachExposure(0.8f, 0.9f),
                DefectionRecruitmentRules.ApproachExposure(0.8f, 0.1f));
        }

        /// <summary>要人の価値＝地位と機密アクセスの幾何平均。片方が乏しいと価値が落ちる。</summary>
        [Test]
        public void DefectorValue_chiitokimitsunokikaheikin()
        {
            // sqrt(0.81 * 0.64) = sqrt(0.5184) = 0.72
            Assert.AreEqual(0.72f, DefectionRecruitmentRules.DefectorValue(0.81f, 0.64f), 1e-3f);
            // 機密に触れない相手は地位が高くても価値が出ない
            Assert.AreEqual(0f, DefectionRecruitmentRules.DefectorValue(1.0f, 0f), Eps);
        }

        /// <summary>寝返り判定＝確率が閾値以上で成立。</summary>
        [Test]
        public void DoesDefect_iikinetaikairyou()
        {
            Assert.IsTrue(DefectionRecruitmentRules.DoesDefect(0.5f));   // 既定閾値0.5ちょうど
            Assert.IsTrue(DefectionRecruitmentRules.DoesDefect(0.7f));
            Assert.IsFalse(DefectionRecruitmentRules.DoesDefect(0.49f));
        }

        /// <summary>
        /// 物語＝不満を抱え忠誠の薄い敵の参謀に、地位と金を約束して引き抜く。
        /// 乗りやすさが高く抵抗が薄いので寝返り確率が立ち、低めの裁可閾値で寝返りが成立する。
        /// 一方、同じ見返りを忠義に篤い将に持ちかけても抵抗が固く寝返らない。
        /// </summary>
        [Test]
        public void Story_fumannosanboumonegaeru()
        {
            // 不満を抱えた野心家の参謀（大義にも疑い）
            float sus = DefectionRecruitmentRules.TargetSusceptibility(0.9f, 0.7f, 0.5f);
            // (0.45 + 0.21 + 0.10) = 0.76
            Assert.AreEqual(0.76f, sus, Eps);

            // 地位と金を満額提示
            float ind = DefectionRecruitmentRules.Inducement(1.0f, sus); // 0.6*0.76 = 0.456
            Assert.AreEqual(0.456f, ind, Eps);

            // 忠誠は薄く報復も恐れていない
            float res = DefectionRecruitmentRules.LoyaltyResistance(0.2f, 0.1f);
            // fear=0.07; 1-(0.8)(0.93)=0.256
            Assert.AreEqual(0.256f, res, Eps);

            float prob = DefectionRecruitmentRules.DefectionProbability(ind, res); // 0.456*0.744 = 0.339264
            Assert.AreEqual(0.339264f, prob, Eps);
            // 工作員の積極的な後押しで裁可閾値を下げれば寝返る
            Assert.IsTrue(DefectionRecruitmentRules.DoesDefect(prob, 0.3f));

            // 同じ見返りでも忠義に篤い将は固く抵抗して寝返らない
            float loyalRes = DefectionRecruitmentRules.LoyaltyResistance(0.95f, 0.9f);
            float loyalProb = DefectionRecruitmentRules.DefectionProbability(ind, loyalRes);
            Assert.IsFalse(DefectionRecruitmentRules.DoesDefect(loyalProb, 0.3f));
            Assert.Less(loyalProb, prob);
        }
    }
}
