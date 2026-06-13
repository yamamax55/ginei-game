using NUnit.Framework;
using UnityEngine;

namespace Ginei.Tests
{
    /// <summary>
    /// 部族連合の亀裂（ガリア戦記型・GAL-1 #1343）の純ロジック検証。
    /// 既定 Params（旧怨0.4・利害0.35・主導権0.25・忠誠侵食0.6・脅威結束0.7・外圧分裂0.8）で期待値を固定。
    /// </summary>
    public class CoalitionFaultlineRulesTests
    {
        const float Eps = 1e-4f;

        /// <summary>亀裂スコア＝三要素の加重平均（重み和=1）。</summary>
        [Test]
        public void FaultlineScore_加重平均を返す()
        {
            // 0.5*0.4 + 0.5*0.35 + 0.5*0.25 = 0.5、重み和1.0で割って0.5。
            float f = CoalitionFaultlineRules.FaultlineScore(0.5f, 0.5f, 0.5f);
            Assert.AreEqual(0.5f, f, Eps);
            // 旧怨だけ1、他0 → 0.4/1.0 = 0.4。
            Assert.AreEqual(0.4f, CoalitionFaultlineRules.FaultlineScore(1f, 0f, 0f), Eps);
        }

        /// <summary>初期忠誠の修正子＝亀裂が出だしの忠誠を負に蝕む。</summary>
        [Test]
        public void InitialLoyaltyModifier_亀裂が忠誠を下げる()
        {
            // -0.5 * 0.6 = -0.3。
            Assert.AreEqual(-0.3f, CoalitionFaultlineRules.InitialLoyaltyModifier(0.5f), Eps);
            // 亀裂ゼロなら修正なし。
            Assert.AreEqual(0f, CoalitionFaultlineRules.InitialLoyaltyModifier(0f), Eps);
        }

        /// <summary>部族の結束＝共通の脅威が亀裂を抑えて結束を保つ（外敵が固める）。</summary>
        [Test]
        public void TribalCohesion_共通の脅威が亀裂を抑える()
        {
            // 強い脅威: base(1-0.6)=0.4 + 1*0.7*0.6=0.42 → 0.82。
            Assert.AreEqual(0.82f, CoalitionFaultlineRules.TribalCohesion(1f, 0.6f), Eps);
            // 脅威が消えると亀裂がそのまま結束を削る → 0.4。
            Assert.AreEqual(0.4f, CoalitionFaultlineRules.TribalCohesion(0f, 0.6f), Eps);
        }

        /// <summary>離反のしやすさ＝亀裂×部族の自立性（従属的な部族は留まる）。</summary>
        [Test]
        public void DefectionSusceptibility_亀裂と自立性の積()
        {
            // 0.8 * 0.5 = 0.4。
            Assert.AreEqual(0.4f, CoalitionFaultlineRules.DefectionSusceptibility(0.8f, 0.5f), Eps);
            // 自立性ゼロ＝中央へ従属する部族は亀裂があっても離反しない。
            Assert.AreEqual(0f, CoalitionFaultlineRules.DefectionSusceptibility(0.9f, 0f), Eps);
        }

        /// <summary>盟主への反感＝支配度の二乗（突出は不釣り合いに反感を生む）。</summary>
        [Test]
        public void HegemonResentment_支配が強すぎると反感()
        {
            // 0.6^2 = 0.36。
            Assert.AreEqual(0.36f, CoalitionFaultlineRules.HegemonResentment(0.6f), Eps);
            // 緩やかな主導は反感が小さい（0.2^2=0.04）。
            Assert.AreEqual(0.04f, CoalitionFaultlineRules.HegemonResentment(0.2f), Eps);
        }

        /// <summary>外圧下の分裂＝亀裂線に沿った直接の圧力が連合を割る。</summary>
        [Test]
        public void FractureUnderPressure_外圧が亀裂を裂く()
        {
            // 0.8 * 0.5 * 0.8 = 0.32。
            Assert.AreEqual(0.32f, CoalitionFaultlineRules.FractureUnderPressure(0.8f, 0.5f), Eps);
            // 外圧ゼロなら亀裂があっても割れない。
            Assert.AreEqual(0f, CoalitionFaultlineRules.FractureUnderPressure(0.8f, 0f), Eps);
        }

        /// <summary>連合の頑健性＝結束を共通の脅威が底上げ（脅威が隙間を埋める）。</summary>
        [Test]
        public void CoalitionResilience_脅威が結束の隙間を埋める()
        {
            // 0.5 + (1-0.5)*0.4 = 0.7。
            Assert.AreEqual(0.7f, CoalitionFaultlineRules.CoalitionResilience(0.5f, 0.4f), Eps);
            // 脅威ゼロなら結束そのまま。
            Assert.AreEqual(0.5f, CoalitionFaultlineRules.CoalitionResilience(0.5f, 0f), Eps);
        }

        /// <summary>割れやすい連合の判定＝亀裂スコアが閾値超。</summary>
        [Test]
        public void IsFracturedCoalition_閾値超で割れやすい()
        {
            Assert.IsTrue(CoalitionFaultlineRules.IsFracturedCoalition(0.7f, 0.5f));
            Assert.IsFalse(CoalitionFaultlineRules.IsFracturedCoalition(0.3f, 0.5f));
        }

        /// <summary>
        /// 物語テスト：亀裂の深い連合は初期忠誠が低く外圧で割れるが、共通の脅威があれば結束する。
        /// 旧怨と主導権争いに満ちた部族連合（高亀裂）は出だしから忠誠が低く、カエサルの直接圧力で割れる。
        /// 同じ亀裂でも、強い共通の敵が迫れば部族は結束を取り戻す（外敵が連合を固める）。
        /// </summary>
        [Test]
        public void Narrative_亀裂の連合は外圧で割れ脅威で固まる()
        {
            // 旧怨0.9・利害0.7・主導権0.8 の不和に満ちた連合。
            float fault = CoalitionFaultlineRules.FaultlineScore(0.9f, 0.7f, 0.8f);
            Assert.Greater(fault, 0.5f, "不和に満ちた連合は亀裂が深い");
            Assert.IsTrue(CoalitionFaultlineRules.IsFracturedCoalition(fault, 0.5f), "閾値超＝切り崩しの標的");

            // 出だしから忠誠が削られている。
            float loyaltyMod = CoalitionFaultlineRules.InitialLoyaltyModifier(fault);
            Assert.Less(loyaltyMod, 0f, "亀裂が初期忠誠を蝕む");

            // 共通の脅威が無いと結束は低く、亀裂線を突く外圧で大きく割れる。
            float cohesionNoThreat = CoalitionFaultlineRules.TribalCohesion(0f, fault);
            float cohesionUnderThreat = CoalitionFaultlineRules.TribalCohesion(1f, fault);
            Assert.Greater(cohesionUnderThreat, cohesionNoThreat, "共通の脅威が結束を固める");

            // 直接の外圧（脅威でなく狙い撃ち）は亀裂の深い連合を割る。
            float fracture = CoalitionFaultlineRules.FractureUnderPressure(fault, 0.8f);
            Assert.Greater(fracture, 0.3f, "亀裂線に沿った外圧が連合を裂く");

            // 共通の脅威下の頑健性は、脅威なしの頑健性を上回る。
            float resilientUnderThreat = CoalitionFaultlineRules.CoalitionResilience(cohesionUnderThreat, 1f);
            float resilientNoThreat = CoalitionFaultlineRules.CoalitionResilience(cohesionNoThreat, 0f);
            Assert.Greater(resilientUnderThreat, resilientNoThreat, "外敵が連合を支える");
        }

        /// <summary>データ Params はクランプして保持する。</summary>
        [Test]
        public void CoalitionFaultlineParams_コンストラクタでクランプ()
        {
            var p = new CoalitionFaultlineParams(1.5f, -0.2f, 0.25f, 2f, -1f, 0.8f);
            Assert.AreEqual(1f, p.enmityWeight, Eps);
            Assert.AreEqual(0f, p.interestWeight, Eps);
            Assert.AreEqual(0.25f, p.rivalryWeight, Eps);
            Assert.AreEqual(1f, p.loyaltyErosionWeight, Eps);
            Assert.AreEqual(0f, p.threatCohesionWeight, Eps);
            Assert.AreEqual(0.8f, p.fracturePressureWeight, Eps);
        }
    }
}
