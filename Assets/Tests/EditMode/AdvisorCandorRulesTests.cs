using NUnit.Framework;
using UnityEngine;

namespace Ginei.Tests
{
    /// <summary>
    /// 直言参謀と佞臣（#1141・マキャヴェッリ）の純ロジック EditMode テスト。
    /// 情報品質・佞臣圧力・政策の現実乖離・真実の到達・直言の涵養・イエスマンの連鎖・意思決定の質・
    /// エコーチェンバー判定を既定 Params の具体値で固定する。
    /// </summary>
    public class AdvisorCandorRulesTests
    {
        const float Eps = 0.0001f;

        /// <summary>情報品質＝直言が高く追従が低いほど真実が届く（裸の王様の逆）。</summary>
        [Test]
        public void InformationQuality_DirectAdviceRaisesAndFlatteryLowers()
        {
            // c=0.8,f=0.2 → 0.4 + 0.5*0.90 = 0.85
            Assert.AreEqual(0.85f, AdvisorCandorRules.InformationQuality(0.8f, 0.2f), Eps);
            // c=0.2,f=0.8 → 0.1 + 0.5*0.30 = 0.25
            Assert.AreEqual(0.25f, AdvisorCandorRules.InformationQuality(0.2f, 0.8f), Eps);
            // 直言が高く追従が低い方が品質は高い。
            Assert.Greater(AdvisorCandorRules.InformationQuality(0.8f, 0.2f),
                           AdvisorCandorRules.InformationQuality(0.2f, 0.8f));
        }

        /// <summary>佞臣圧力＝君主の虚栄が高く異論への寛容が低いほど媚びが得をする。</summary>
        [Test]
        public void FlatteryPressure_VanityAndIntoleranceBreedFlatterers()
        {
            // vanity=0.9, intolerance=1-0.2=0.8 → 0.72
            Assert.AreEqual(0.72f, AdvisorCandorRules.FlatteryPressure(0.9f, 0.2f), Eps);
            // 寛容が満点なら佞臣圧力は消える。
            Assert.AreEqual(0f, AdvisorCandorRules.FlatteryPressure(0.9f, 1f), Eps);
        }

        /// <summary>政策の現実乖離＝情報品質が低いほど政策が現実から外れる（品質1で乖離0）。</summary>
        [Test]
        public void PolicyRealityGap_PoorInformationWidensTheGap()
        {
            // (1-0.25)*0.8 = 0.6
            Assert.AreEqual(0.6f, AdvisorCandorRules.PolicyRealityGap(0.25f), Eps);
            // 完全な情報なら乖離なし。
            Assert.AreEqual(0f, AdvisorCandorRules.PolicyRealityGap(1f), Eps);
        }

        /// <summary>真実の到達＝宮廷フィルターを越えて直言が届く（フィルター1で到達0）。</summary>
        [Test]
        public void TruthReachingRuler_CourtFilterBlocksTheTruth()
        {
            // 0.8*(1-0.5) = 0.4
            Assert.AreEqual(0.4f, AdvisorCandorRules.TruthReachingRuler(0.8f, 0.5f), Eps);
            // 取り巻きが全てを漉せば真実は届かない。
            Assert.AreEqual(0f, AdvisorCandorRules.TruthReachingRuler(0.8f, 1f), Eps);
        }

        /// <summary>直言の涵養＝安全で寛容な環境が諫言を育てる（直言を許す君主）。</summary>
        [Test]
        public void CandorCultivation_SafeEnvironmentGrowsCandor()
        {
            // target=0.8*0.9=0.72, MoveTowards(0.5,0.72,0.1)=0.6
            Assert.AreEqual(0.6f, AdvisorCandorRules.CandorCultivation(0.5f, 0.8f, 0.9f, 1f), Eps);
            // 安全がなければ直言は黙る（目標0へ向かう）。
            Assert.Less(AdvisorCandorRules.CandorCultivation(0.5f, 0.8f, 0f, 1f), 0.5f);
        }

        /// <summary>イエスマンの連鎖＝佞臣と同調が皆を追従へ巻き込む。</summary>
        [Test]
        public void YesManCascade_FlatteryAndConformitySpread()
        {
            // target=0.6*0.8=0.48, MoveTowards(0.6,0.48,0.1)=0.5
            Assert.AreEqual(0.5f, AdvisorCandorRules.YesManCascade(0.6f, 0.8f, 1f), Eps);
        }

        /// <summary>意思決定の質＝良い情報×君主の判断力（どちらか欠ければ誤る）。</summary>
        [Test]
        public void DecisionQuality_NeedsBothInformationAndJudgment()
        {
            // 0.85*0.6 = 0.51
            Assert.AreEqual(0.51f, AdvisorCandorRules.DecisionQuality(0.85f, 0.6f), Eps);
            // 真実が届いても暗君なら良い決定にならない。
            Assert.AreEqual(0f, AdvisorCandorRules.DecisionQuality(1f, 0f), Eps);
        }

        /// <summary>エコーチェンバー判定＝情報品質が閾値未満なら佞臣に囲まれた反響室。</summary>
        [Test]
        public void IsEchoChamber_LowQualityIsTrappedInTheBubble()
        {
            // 既定閾0.3。品質0.25<0.3 → true、0.85 → false。
            Assert.IsTrue(AdvisorCandorRules.IsEchoChamber(0.25f));
            Assert.IsFalse(AdvisorCandorRules.IsEchoChamber(0.85f));
        }
    }
}
