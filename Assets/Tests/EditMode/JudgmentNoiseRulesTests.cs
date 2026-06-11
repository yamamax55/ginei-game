using NUnit.Framework;
using UnityEngine;

namespace Ginei.Tests
{
    /// <summary>判断ノイズ（KAHN-2 #1834・カーネマン『NOISE』）の純ロジックを既定Paramsの具体値で固定するテスト。</summary>
    public class JudgmentNoiseRulesTests
    {
        const float Eps = 0.0001f;
        const float SqrtEps = 0.001f;

        /// <summary>水準ノイズ＝悲観で水準が下がり楽観で上がる（既定levelNoiseScale0.2）。</summary>
        [Test]
        public void LevelNoise_judge_pessimism_shifts_level()
        {
            // 悲観0.5: 1.0 - 0.5*0.2 = 0.9
            Assert.AreEqual(0.9f, JudgmentNoiseRules.LevelNoise(1f, 0.5f), Eps);
            // 楽観(負)0.5: 1.0 - (-0.5)*0.2 = 1.1
            Assert.AreEqual(1.1f, JudgmentNoiseRules.LevelNoise(1f, -0.5f), Eps);
            // 中立はぶれなし
            Assert.AreEqual(1f, JudgmentNoiseRules.LevelNoise(1f, 0f), Eps);
            // クランプ（過大な悲観でも[-1,1]）: 1.0 - 1*0.2 = 0.8
            Assert.AreEqual(0.8f, JudgmentNoiseRules.LevelNoise(1f, 5f), Eps);
        }

        /// <summary>機会ノイズ＝平均0でroll0.5でゼロ、両端で±振幅（既定amplitude0.15）。</summary>
        [Test]
        public void OccasionNoise_zero_mean_around_half()
        {
            Assert.AreEqual(0f, JudgmentNoiseRules.OccasionNoise(0.5f), Eps);   // ぶれなし
            Assert.AreEqual(0.15f, JudgmentNoiseRules.OccasionNoise(1f), Eps);  // +max
            Assert.AreEqual(-0.15f, JudgmentNoiseRules.OccasionNoise(0f), Eps); // -max
        }

        /// <summary>パターンノイズ＝判断者×文脈の積（既定patternNoiseScale0.1）。</summary>
        [Test]
        public void PatternNoise_judge_context_interaction()
        {
            // judge1(->+1) x context1(->+1) x 0.1 = 0.1
            Assert.AreEqual(0.1f, JudgmentNoiseRules.PatternNoise(1f, 1f), Eps);
            // judge1(->+1) x context0(->-1) x 0.1 = -0.1
            Assert.AreEqual(-0.1f, JudgmentNoiseRules.PatternNoise(1f, 0f), Eps);
            // 中点(->0)同士は相互作用なし
            Assert.AreEqual(0f, JudgmentNoiseRules.PatternNoise(0.5f, 0.5f), Eps);
        }

        /// <summary>総誤差＝バイアスとノイズの直交合成sqrt(b^2+n^2)。3-4-5の直角三角形で検算。</summary>
        [Test]
        public void TotalError_orthogonal_combination()
        {
            // sqrt(3^2+4^2) = 5
            Assert.AreEqual(5f, JudgmentNoiseRules.TotalError(3f, 4f), SqrtEps);
            // バイアスのみ
            Assert.AreEqual(3f, JudgmentNoiseRules.TotalError(3f, 0f), SqrtEps);
            // ノイズのみ（バイアスゼロでも誤差は残る＝直交）
            Assert.AreEqual(4f, JudgmentNoiseRules.TotalError(0f, 4f), SqrtEps);
        }

        /// <summary>ノイズ込み観測＝真値+バイアス+機会ノイズ。バイアスは一方へ、ノイズは時点で散る。</summary>
        [Test]
        public void NoisyJudgment_bias_and_noise_separately()
        {
            // 真値10 + バイアス2 + roll1(+1)*振幅3 = 15
            Assert.AreEqual(15f, JudgmentNoiseRules.NoisyJudgment(10f, 2f, 3f, 1f), Eps);
            // roll0.5でノイズゼロ＝真値+バイアスのみ = 12
            Assert.AreEqual(12f, JudgmentNoiseRules.NoisyJudgment(10f, 2f, 3f, 0.5f), Eps);
            // roll0(-1)で下振れ: 10+2-3 = 9
            Assert.AreEqual(9f, JudgmentNoiseRules.NoisyJudgment(10f, 2f, 3f, 0f), Eps);
        }

        /// <summary>集約でノイズが1/sqrt(n)に縮む＝集合知（バイアスは消えない）。</summary>
        [Test]
        public void NoiseReductionByAggregation_one_over_sqrt_n()
        {
            // 単一ノイズ1.0、4人: 1/sqrt(4) = 0.5
            Assert.AreEqual(0.5f, JudgmentNoiseRules.NoiseReductionByAggregation(1f, 4), SqrtEps);
            // 1人なら不変
            Assert.AreEqual(1f, JudgmentNoiseRules.NoiseReductionByAggregation(1f, 1), SqrtEps);
            // 0以下の人数は1へ丸め
            Assert.AreEqual(1f, JudgmentNoiseRules.NoiseReductionByAggregation(1f, 0), SqrtEps);
            // 人数が増えるほど縮む（単調）
            float few = JudgmentNoiseRules.NoiseReductionByAggregation(1f, 4);
            float many = JudgmentNoiseRules.NoiseReductionByAggregation(1f, 16);
            Assert.Less(many, few);
        }

        /// <summary>判断の構造化でノイズが縮む（既定hygieneScale0.8）。バイアスには効かない。</summary>
        [Test]
        public void DecisionHygieneEffect_structure_reduces_noise()
        {
            // ノイズ1.0、構造化0.5: 1*(1-0.5*0.8) = 0.6
            Assert.AreEqual(0.6f, JudgmentNoiseRules.DecisionHygieneEffect(1f, 0.5f), Eps);
            // 構造化なしは不変
            Assert.AreEqual(1f, JudgmentNoiseRules.DecisionHygieneEffect(1f, 0f), Eps);
            // 完全構造化: 1*(1-0.8) = 0.2
            Assert.AreEqual(0.2f, JudgmentNoiseRules.DecisionHygieneEffect(1f, 1f), Eps);
        }

        /// <summary>ノイズ対バイアス比と支配判定＝どちらが大きいか。</summary>
        [Test]
        public void NoiseToBiasRatio_and_dominance()
        {
            // ノイズ2 / バイアス4 = 0.5（バイアス支配）
            Assert.AreEqual(0.5f, JudgmentNoiseRules.NoiseToBiasRatio(2f, 4f), Eps);
            Assert.IsFalse(JudgmentNoiseRules.IsNoiseDominant(2f, 4f, 1f));
            // ノイズ5 / バイアス2 = 2.5（ノイズ支配）
            Assert.AreEqual(2.5f, JudgmentNoiseRules.NoiseToBiasRatio(5f, 2f), Eps);
            Assert.IsTrue(JudgmentNoiseRules.IsNoiseDominant(5f, 2f, 1f));
            // バイアスほぼゼロ＝ノイズ完全支配
            Assert.IsTrue(JudgmentNoiseRules.IsNoiseDominant(1f, 0f, 1f));
        }

        /// <summary>
        /// 物語テスト：同一の真値でも判断者ごとに観測が散る（ノイズ）が、
        /// 複数判断の平均でノイズは1/sqrt(n)に縮む＝群衆の知恵はノイズだけを削る。
        /// </summary>
        [Test]
        public void Narrative_noise_scatters_judgments_but_aggregation_shrinks_it()
        {
            float trueValue = 50f;
            float bias = 0f; // バイアスなし＝純粋にノイズの散らばりを見る
            float amp = 6f;

            // 同一真値・同一バイアスでも、時点(roll)が違えば観測が散る
            float judgeA = JudgmentNoiseRules.NoisyJudgment(trueValue, bias, amp, 1f);   // +6 -> 56
            float judgeB = JudgmentNoiseRules.NoisyJudgment(trueValue, bias, amp, 0f);   // -6 -> 44
            Assert.AreNotEqual(judgeA, judgeB, "同一情報でも判断はばらつく（ノイズ）");
            Assert.AreEqual(56f, judgeA, Eps);
            Assert.AreEqual(44f, judgeB, Eps);

            // そのばらつき(単一ノイズ振幅6)は、9人の独立判断の平均で 6/sqrt(9)=2 に縮む
            float aggregated = JudgmentNoiseRules.NoiseReductionByAggregation(amp, 9);
            Assert.AreEqual(2f, aggregated, SqrtEps);
            Assert.Less(aggregated, amp, "集約はノイズを縮める＝群衆の知恵");
        }
    }
}
