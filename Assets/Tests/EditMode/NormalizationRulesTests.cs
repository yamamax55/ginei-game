using NUnit.Framework;
using UnityEngine;

namespace Ginei.Tests
{
    /// <summary>
    /// NormalizationRules（規律訓練と標準化＝フーコー規律権力・#1508）の純ロジックテスト。
    /// 既定 NormalizationParams の具体値で期待値を固定する。
    /// </summary>
    public class NormalizationRulesTests
    {
        const float Eps = 1e-4f;

        /// <summary>信頼性＝訓練×標準化×上限。両者が揃って初めて信頼性が立ち、片方0なら0。</summary>
        [Test]
        public void Reliability_訓練と標準化の積で信頼性が立つ()
        {
            // maxReliability=1.0
            Assert.AreEqual(0.8f * 0.5f, NormalizationRules.Reliability(0.8f, 0.5f), Eps);
            // 標準化が0なら信頼性も0（規格化されないと均質にならない）
            Assert.AreEqual(0f, NormalizationRules.Reliability(1f, 0f), Eps);
            // 両者最大で上限1.0
            Assert.AreEqual(1.0f, NormalizationRules.Reliability(1f, 1f), Eps);
        }

        /// <summary>創発シナジーのペナルティ＝標準化に比例して創発が削られる（画一化が自発性を奪う）。</summary>
        [Test]
        public void EmergentSynergyPenalty_標準化に比例して創発が削られる()
        {
            // maxSynergyPenalty=0.6
            Assert.AreEqual(0f, NormalizationRules.EmergentSynergyPenalty(0f), Eps);
            Assert.AreEqual(0.3f, NormalizationRules.EmergentSynergyPenalty(0.5f), Eps);
            Assert.AreEqual(0.6f, NormalizationRules.EmergentSynergyPenalty(1f), Eps);
        }

        /// <summary>予測可能性＝信頼性が高いほど行動が読め指揮しやすい。</summary>
        [Test]
        public void Predictability_信頼性が高いほど予測しやすい()
        {
            // maxPredictability=1.0
            Assert.AreEqual(0.7f, NormalizationRules.Predictability(0.7f), Eps);
            Assert.AreEqual(1.0f, NormalizationRules.Predictability(1f), Eps);
            // クランプ：1超は1.0で頭打ち
            Assert.AreEqual(1.0f, NormalizationRules.Predictability(1.5f), Eps);
        }

        /// <summary>従順な身体＝規律×監視×dt で時間とともに従順度が蓄積し、揃わないと進まない。</summary>
        [Test]
        public void DocileBody_規律と監視が揃うほど時間で従順化する()
        {
            // docilityRate=0.1。current=0.2 + 0.1*1*1*2 = 0.4
            Assert.AreEqual(0.4f, NormalizationRules.DocileBody(0.2f, 1f, 1f, 2f), Eps);
            // 監視が0なら進まない（積で表す）
            Assert.AreEqual(0.2f, NormalizationRules.DocileBody(0.2f, 1f, 0f, 5f), Eps);
            // 上限1.0でクランプ
            Assert.AreEqual(1.0f, NormalizationRules.DocileBody(0.95f, 1f, 1f, 10f), Eps);
        }

        /// <summary>画一化コスト＝標準化×多様性の価値で、柔軟な対応力を犠牲にする量。</summary>
        [Test]
        public void HomogenizationCost_標準化が多様性を犠牲にする()
        {
            // maxHomogenizationCost=0.5。0.5*0.8*0.5 = 0.2
            Assert.AreEqual(0.2f, NormalizationRules.HomogenizationCost(0.8f, 0.5f), Eps);
            // 多様性が0なら失うものがない＝コスト0
            Assert.AreEqual(0f, NormalizationRules.HomogenizationCost(1f, 0f), Eps);
            // 両者最大で上限0.5
            Assert.AreEqual(0.5f, NormalizationRules.HomogenizationCost(1f, 1f), Eps);
        }

        /// <summary>規格遵守＝内面化した規範×逸脱で、規格からの外れを正す矯正力。</summary>
        [Test]
        public void NormCompliance_逸脱を正常へ矯正する()
        {
            // maxNormCompliance=0.9。0.9*1*0.5 = 0.45
            Assert.AreEqual(0.45f, NormalizationRules.NormCompliance(1f, 0.5f), Eps);
            // 逸脱が0なら矯正する対象がない
            Assert.AreEqual(0f, NormalizationRules.NormCompliance(1f, 0f), Eps);
            // 規範が内面化されていなければ矯正力も立たない
            Assert.AreEqual(0f, NormalizationRules.NormCompliance(0f, 1f), Eps);
        }

        /// <summary>規律vs自律＝訓練が自律的創意を抑え、実効自律度が下がる（基準非破壊）。</summary>
        [Test]
        public void DisciplineVsInitiative_訓練が自律を抑える()
        {
            // maxInitiativeSuppression=0.7。autonomy=1, training=1 → 1*(1-0.7)=0.3
            Assert.AreEqual(0.3f, NormalizationRules.DisciplineVsInitiative(1f, 1f), Eps);
            // 訓練0なら自律はそのまま
            Assert.AreEqual(0.8f, NormalizationRules.DisciplineVsInitiative(0f, 0.8f), Eps);
            // 訓練0.5 → 0.6*(1-0.35)=0.39
            Assert.AreEqual(0.6f * (1f - 0.35f), NormalizationRules.DisciplineVsInitiative(0.5f, 0.6f), Eps);
        }

        /// <summary>最適標準化＝任務が複雑なほど標準化を緩め創発を許す（単純は標準化・複雑は自律）。</summary>
        [Test]
        public void OptimalStandardization_複雑な任務ほど標準化を緩める()
        {
            // 単純任務（complexity=0）は完全標準化
            Assert.AreEqual(1f, NormalizationRules.OptimalStandardization(0f), Eps);
            // 複雑任務（complexity=1）は標準化を捨てて自律に委ねる
            Assert.AreEqual(0f, NormalizationRules.OptimalStandardization(1f), Eps);
            // 中間 0.3 → 0.7
            Assert.AreEqual(0.7f, NormalizationRules.OptimalStandardization(0.3f), Eps);
        }
    }
}
