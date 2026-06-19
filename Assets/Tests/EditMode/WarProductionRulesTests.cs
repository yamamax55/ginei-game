using NUnit.Framework;
using UnityEngine;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 戦時生産を固定する：生産は工場容量・資源・労働力の最小律速で決まり、量産が進むほど習熟で効率が上がり（学習曲線）、
    /// 大ロットほど規模の経済が効き、最も不足する資源が全体を律速し、品目切替には再編時間が要り、量産は品質とのトレードオフ。
    /// 既定 Params で期待値を固定し、配列 null/空安全とクランプを担保する。
    /// </summary>
    public class WarProductionRulesTests
    {
        private static readonly WarProductionParams P = WarProductionParams.Default;
        // 既定：学習指数0.15/学習基準100/学習上限3/規模指数0.1/規模基準10/規模上限2/再編基礎5/変更追加10/ボトルネック閾値0.7

        [Test]
        public void ProductionRate_TakesMinimumLimitingFactor()
        {
            // 資源が最少＝資源が律速
            Assert.AreEqual(30f, WarProductionRules.ProductionRate(100f, 30f, 80f, P), 1e-4f);
            // 労働力が最少＝労働力が律速
            Assert.AreEqual(20f, WarProductionRules.ProductionRate(100f, 50f, 20f, P), 1e-4f);
            // 負入力は0扱い
            Assert.AreEqual(0f, WarProductionRules.ProductionRate(-5f, 50f, 80f, P), 1e-4f);
        }

        [Test]
        public void LearningCurveEfficiency_RisesWithCumulativeProduction()
        {
            // 累積0で1.0（まだ学んでいない）
            Assert.AreEqual(1f, WarProductionRules.LearningCurveEfficiency(0f, P), 1e-4f);
            // 累積100で pow(2,0.15)≈1.1096（習熟で効率↑）
            Assert.AreEqual(1.1096f, WarProductionRules.LearningCurveEfficiency(100f, P), 1e-3f);
            // さらに累積でさらに上がる（単調増加）
            Assert.Greater(WarProductionRules.LearningCurveEfficiency(900f, P),
                WarProductionRules.LearningCurveEfficiency(100f, P));
            // 負累積はクランプして1.0
            Assert.AreEqual(1f, WarProductionRules.LearningCurveEfficiency(-50f, P), 1e-4f);
        }

        [Test]
        public void EffectiveOutput_AppliesLearning()
        {
            // 生産速度30×習熟1.1096≈33.29
            Assert.AreEqual(33.29f, WarProductionRules.EffectiveOutput(30f, 1.1096f), 1e-2f);
            // 習熟1.0なら生産速度そのまま
            Assert.AreEqual(30f, WarProductionRules.EffectiveOutput(30f, 1f), 1e-4f);
        }

        [Test]
        public void BottleneckResource_FindsMostDeficientByRatio()
        {
            // 在庫/需要比＝10, 0.5, 4 ＝ 添字1（最も不足）が律速
            float[] levels = { 100f, 50f, 200f };
            float[] demands = { 10f, 100f, 50f };
            Assert.AreEqual(1, WarProductionRules.BottleneckResource(levels, demands));

            // 需要0の資源は律速対象外（添字0は需要0でスキップ→添字1が律速）
            float[] lvl2 = { 0f, 80f };
            float[] dmd2 = { 0f, 100f };
            Assert.AreEqual(1, WarProductionRules.BottleneckResource(lvl2, dmd2));
        }

        [Test]
        public void BottleneckResource_NullAndEmptySafe()
        {
            Assert.AreEqual(-1, WarProductionRules.BottleneckResource(null, null));
            Assert.AreEqual(-1, WarProductionRules.BottleneckResource(new float[0], new float[0]));
            Assert.AreEqual(-1, WarProductionRules.BottleneckResource(new float[] { 10f }, null));
            // 全需要0＝有効資源なし＝-1
            Assert.AreEqual(-1, WarProductionRules.BottleneckResource(new float[] { 10f, 20f }, new float[] { 0f, 0f }));
        }

        [Test]
        public void EconomyOfScale_RisesWithBatchSizeAndCaps()
        {
            // ロット0で1.0
            Assert.AreEqual(1f, WarProductionRules.EconomyOfScale(0f, P), 1e-4f);
            // ロット10で pow(2,0.1)≈1.0718
            Assert.AreEqual(1.0718f, WarProductionRules.EconomyOfScale(10f, P), 1e-3f);
            // 巨大ロットでも上限2を超えない
            Assert.LessOrEqual(WarProductionRules.EconomyOfScale(1e9f, P), 2f + 1e-4f);
        }

        [Test]
        public void RetoolingDelay_GrowsWithModelChange()
        {
            // 変更なしでも基礎時間5
            Assert.AreEqual(5f, WarProductionRules.RetoolingDelay(0f, P), 1e-4f);
            // 半分変更＝5＋0.5×10＝10
            Assert.AreEqual(10f, WarProductionRules.RetoolingDelay(0.5f, P), 1e-4f);
            // 全面変更＝5＋1×10＝15（変更量は0..1にクランプ）
            Assert.AreEqual(15f, WarProductionRules.RetoolingDelay(1.5f, P), 1e-4f);
        }

        [Test]
        public void ProductionVsQuality_SignedTradeoff()
        {
            Assert.AreEqual(1f, WarProductionRules.ProductionVsQuality(1f), 1e-4f);   // 量産全振り＝+1
            Assert.AreEqual(-1f, WarProductionRules.ProductionVsQuality(0f), 1e-4f);  // 品質全振り＝-1
            Assert.AreEqual(0f, WarProductionRules.ProductionVsQuality(0.5f), 1e-4f); // 中庸＝0
            // 範囲外はクランプ
            Assert.AreEqual(1f, WarProductionRules.ProductionVsQuality(2f), 1e-4f);
            Assert.AreEqual(-1f, WarProductionRules.ProductionVsQuality(-1f), 1e-4f);
        }

        [Test]
        public void IsProductionBottlenecked_ThresholdGate()
        {
            Assert.IsTrue(WarProductionRules.IsProductionBottlenecked(0.8f));   // 既定閾値0.7以上＝詰まり
            Assert.IsFalse(WarProductionRules.IsProductionBottlenecked(0.5f));  // 閾値未満＝順調
            Assert.IsTrue(WarProductionRules.IsProductionBottlenecked(0.7f));   // 境界は詰まり
        }

        [Test]
        public void Narrative_MassProductionLearnsAndOutpacesEarlyRuns()
        {
            // 物語：開戦初期は累積も浅く小ロット、終盤は習熟＋大ロットで同じ律速でもより多く作れる。
            float earlyRate = WarProductionRules.ProductionRate(100f, 60f, 80f, P); // 律速＝資源60
            float earlyEff = WarProductionRules.LearningCurveEfficiency(0f, P);
            float earlyScale = WarProductionRules.EconomyOfScale(2f, P);
            float earlyOutput = WarProductionRules.EffectiveOutput(earlyRate, earlyEff) * earlyScale;

            float lateRate = WarProductionRules.ProductionRate(100f, 60f, 80f, P); // 同じ律速
            float lateEff = WarProductionRules.LearningCurveEfficiency(1000f, P);  // 量産で習熟
            float lateScale = WarProductionRules.EconomyOfScale(200f, P);          // 大ロット
            float lateOutput = WarProductionRules.EffectiveOutput(lateRate, lateEff) * lateScale;

            // 同じ律速でも、習熟と規模の経済で終盤の実効生産が上回る＝戦時量産の積み上がり
            Assert.Greater(lateOutput, earlyOutput);
            Assert.AreEqual(60f, lateRate, 1e-4f); // 律速は資源で固定（学んでも律速は越えない）
        }
    }
}
