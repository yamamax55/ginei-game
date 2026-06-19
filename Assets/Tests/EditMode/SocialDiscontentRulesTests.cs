using NUnit.Framework;
using UnityEngine;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 社会的不満を固定する：相対的剥奪は期待−現実の落差だけ（過充足は不満を生まない）、
    /// 格差は公正規範ほど刺さる、世代間不公平は若者の閉塞×上の既得権、機会の閉塞は
    /// (1−流動性)、蓄積は三源の加重平均、福祉と改革応答のガス抜き、J字カーブは改善の後の
    /// 急反転で爆発、不満水準は蓄積−ガス抜き＋J字（-1..1）。クランプを担保。
    /// </summary>
    public class SocialDiscontentRulesTests
    {
        private static readonly SocialDiscontentParams P = SocialDiscontentParams.Default;
        // 剥奪1/不公正1/世代1/閉塞1・蓄積重み(0.4/0.3/0.3)・ガス抜き(福祉0.5/改革0.5×効き0.8)・J字1.2

        [Test]
        public void RelativeDeprivation_OnlyTheGap_OverfulfilmentIsZero()
        {
            // 期待0.8−現実0.3=0.5 の落差が剥奪に
            Assert.AreEqual(0.5f, SocialDiscontentRules.RelativeDeprivation(0.8f, 0.3f, P), 1e-4f);
            // 現実が期待を上回れば剥奪0（過充足は不満を負にしない）
            Assert.AreEqual(0f, SocialDiscontentRules.RelativeDeprivation(0.3f, 0.8f, P), 1e-4f);
            // 入力クランプ（期待2・現実−1でも 1−0=1）
            Assert.AreEqual(1f, SocialDiscontentRules.RelativeDeprivation(2f, -1f, P), 1e-4f);
        }

        [Test]
        public void InequityPerception_FairnessNormAmplifies()
        {
            // 格差0.6×公正規範0.5＝0.3
            Assert.AreEqual(0.3f, SocialDiscontentRules.InequityPerception(0.6f, 0.5f, P), 1e-4f);
            // 公正規範0なら格差は不満にならない
            Assert.AreEqual(0f, SocialDiscontentRules.InequityPerception(0.9f, 0f, P), 1e-4f);
            // 規範が高いほど同じ格差が刺さる
            Assert.Greater(SocialDiscontentRules.InequityPerception(0.6f, 1f, P),
                           SocialDiscontentRules.InequityPerception(0.6f, 0.5f, P));
        }

        [Test]
        public void GenerationalGrievance_BleakYouthTimesElderPrivilege()
        {
            // 若者の見通し0.2（暗さ0.8）×上の既得権0.7＝0.56
            Assert.AreEqual(0.56f, SocialDiscontentRules.GenerationalGrievance(0.2f, 0.7f, P), 1e-4f);
            // 若者の見通しが明るい（1）なら不公平0
            Assert.AreEqual(0f, SocialDiscontentRules.GenerationalGrievance(1f, 1f, P), 1e-4f);
            // 上の世代に既得権が無ければ不公平0
            Assert.AreEqual(0f, SocialDiscontentRules.GenerationalGrievance(0.1f, 0f, P), 1e-4f);
        }

        [Test]
        public void MobilityBlockage_LadderPulledUp()
        {
            // 流動性0.2＝閉塞0.8（梯子が外された感）
            Assert.AreEqual(0.8f, SocialDiscontentRules.MobilityBlockage(0.2f, P), 1e-4f);
            // 流動性1（誰でも上れる）＝閉塞0
            Assert.AreEqual(0f, SocialDiscontentRules.MobilityBlockage(1f, P), 1e-4f);
            // 入力クランプ（負の流動性でも閉塞は1止まり）
            Assert.AreEqual(1f, SocialDiscontentRules.MobilityBlockage(-1f, P), 1e-4f);
        }

        [Test]
        public void DiscontentAccumulation_WeightedBlendOfThreeSources()
        {
            // 剥奪0.5×0.4+不公正0.3×0.3+閉塞0.8×0.3=0.2+0.09+0.24=0.53
            Assert.AreEqual(0.53f, SocialDiscontentRules.DiscontentAccumulation(0.5f, 0.3f, 0.8f, P), 1e-4f);
            // 三源すべて最大なら蓄積も最大
            Assert.AreEqual(1f, SocialDiscontentRules.DiscontentAccumulation(1f, 1f, 1f, P), 1e-4f);
            // 三源すべてゼロなら蓄積ゼロ
            Assert.AreEqual(0f, SocialDiscontentRules.DiscontentAccumulation(0f, 0f, 0f, P), 1e-4f);
        }

        [Test]
        public void SafetyValve_WelfareAndReformReleasePressure()
        {
            // (福祉0.8×0.5+改革0.4×0.5)/1.0×効き0.8=0.6×0.8=0.48
            Assert.AreEqual(0.48f, SocialDiscontentRules.SafetyValve(0.8f, 0.4f, P), 1e-4f);
            // 何も与えなければガス抜き0
            Assert.AreEqual(0f, SocialDiscontentRules.SafetyValve(0f, 0f, P), 1e-4f);
            // 福祉も改革も最大でも効き倍率0.8で頭打ち
            Assert.AreEqual(0.8f, SocialDiscontentRules.SafetyValve(1f, 1f, P), 1e-4f);
        }

        [Test]
        public void JCurveRisk_ExplodesWhenImprovementSuddenlyReverses()
        {
            // 改善0.7×急反転0.5×1.2=0.42
            Assert.AreEqual(0.42f, SocialDiscontentRules.JCurveRisk(0.7f, 0.5f, P), 1e-4f);
            // 改善が無ければ（上り調子でない）反転しても臨界0
            Assert.AreEqual(0f, SocialDiscontentRules.JCurveRisk(0f, 1f, P), 1e-4f);
            // 改善があっても反転が無ければ臨界0
            Assert.AreEqual(0f, SocialDiscontentRules.JCurveRisk(1f, 0f, P), 1e-4f);
            // 強い改善×急反転で臨界が跳ね上がる
            Assert.Greater(SocialDiscontentRules.JCurveRisk(1f, 1f, P),
                           SocialDiscontentRules.JCurveRisk(0.7f, 0.5f, P));
        }

        [Test]
        public void DiscontentLevel_AccumulationMinusValvePlusJCurve_Clamped()
        {
            // 蓄積0.53−ガス抜き0.48+J字0.42=0.47
            Assert.AreEqual(0.47f, SocialDiscontentRules.DiscontentLevel(0.53f, 0.48f, 0.42f, P), 1e-4f);
            // ガス抜きが蓄積を上回れば負（充足）
            Assert.AreEqual(-0.3f, SocialDiscontentRules.DiscontentLevel(0.2f, 0.5f, 0f, P), 1e-4f);
            // 蓄積最大＋J字最大は1にクランプ
            Assert.AreEqual(1f, SocialDiscontentRules.DiscontentLevel(1f, 0f, 1f, P), 1e-4f);
            // ガス抜きだけ最大は−1にクランプ
            Assert.AreEqual(-1f, SocialDiscontentRules.DiscontentLevel(0f, 1f, 0f, P), 1e-4f);
        }

        [Test]
        public void Narrative_RisingHopesThenCrashIgnitesRevolution()
        {
            // 改革を進めた繁栄期：流動性も改革応答も高く、剥奪・不公正は低い＝不満は負（充足）
            float depCalm = SocialDiscontentRules.RelativeDeprivation(0.5f, 0.5f, P);   // 落差0
            float ineqCalm = SocialDiscontentRules.InequityPerception(0.3f, 0.5f, P);   // 0.15
            float blockCalm = SocialDiscontentRules.MobilityBlockage(0.8f, P);          // 0.2
            float accCalm = SocialDiscontentRules.DiscontentAccumulation(depCalm, ineqCalm, blockCalm, P);
            float valveCalm = SocialDiscontentRules.SafetyValve(0.8f, 0.8f, P);
            float levelCalm = SocialDiscontentRules.DiscontentLevel(accCalm, valveCalm, 0f, P);
            Assert.Less(levelCalm, 0f); // 上り調子の社会は静穏（充足）

            // 改善が続いた後に急反転：期待は高止まり・現実が落ち・改革も止まる＝J字が点火
            float depCrash = SocialDiscontentRules.RelativeDeprivation(0.9f, 0.3f, P);  // 0.6
            float ineqCrash = SocialDiscontentRules.InequityPerception(0.7f, 0.7f, P);  // 0.49
            float blockCrash = SocialDiscontentRules.MobilityBlockage(0.3f, P);         // 0.7
            float accCrash = SocialDiscontentRules.DiscontentAccumulation(depCrash, ineqCrash, blockCrash, P);
            float valveCrash = SocialDiscontentRules.SafetyValve(0.2f, 0.1f, P);        // ガス抜き失効
            float jCurve = SocialDiscontentRules.JCurveRisk(0.9f, 0.9f, P);             // 上り調子が折れた
            float levelCrash = SocialDiscontentRules.DiscontentLevel(accCrash, valveCrash, jCurve, P);

            // 革命は最悪期でなく「良くなった後に折れた瞬間」に来る＝静穏→臨界へ跳ねる
            Assert.Greater(levelCrash, 0.5f);
            Assert.Greater(levelCrash, levelCalm + 1f);
        }
    }
}
