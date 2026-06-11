using NUnit.Framework;
using UnityEngine;

namespace Ginei.Tests
{
    /// <summary>風土と政体の相性ルールのテスト（#1443・モンテスキュー風土論）。既定Params具体値で期待値固定。</summary>
    public class ClimatePolityFitRulesTests
    {
        const float Eps = 1e-4f;

        /// <summary>気質：過酷な環境は活力ある気質、豊かな環境は穏やかな気質を育てる。</summary>
        [Test]
        public void ClimateTemperament_過酷で活力_豊かで穏やか()
        {
            // 過酷1・豊か0 → 0.5 + 0.5*(0.6) = 0.8
            Assert.AreEqual(0.8f, ClimatePolityFitRules.ClimateTemperament(1f, 0f), Eps);
            // 過酷0・豊か1 → 0.5 + 0.5*(-0.4) = 0.3
            Assert.AreEqual(0.3f, ClimatePolityFitRules.ClimateTemperament(0f, 1f), Eps);
            // 中立（両ゼロ）→ 0.5
            Assert.AreEqual(0.5f, ClimatePolityFitRules.ClimateTemperament(0f, 0f), Eps);
            // 過酷ほど活力が上がる（単調）
            Assert.Greater(ClimatePolityFitRules.ClimateTemperament(1f, 0f),
                ClimatePolityFitRules.ClimateTemperament(0.5f, 0f));
        }

        /// <summary>風土政体適合：気質と政体原動力が近いほど適合度が高い。</summary>
        [Test]
        public void PolityClimateFit_気質と原動力の近さで測る()
        {
            // 完全一致 → 1
            Assert.AreEqual(1f, ClimatePolityFitRules.PolityClimateFit(0.7f, 0.7f), Eps);
            // 乖離0.4 → 1-0.4 = 0.6
            Assert.AreEqual(0.6f, ClimatePolityFitRules.PolityClimateFit(0.8f, 0.4f), Eps);
            // 真逆 → 0
            Assert.AreEqual(0f, ClimatePolityFitRules.PolityClimateFit(1f, 0f), Eps);
        }

        /// <summary>適合の安定：適合すれば安定ボーナス、ミスマッチは不安定。</summary>
        [Test]
        public void StabilityFromFit_適合で安定_不適合で不安定()
        {
            // 適合1 → 上限 maxStability=1.1 はClamp01で1.0
            Assert.AreEqual(1f, ClimatePolityFitRules.StabilityFromFit(1f), Eps);
            // 適合0 → 下限 minStability=0.6
            Assert.AreEqual(0.6f, ClimatePolityFitRules.StabilityFromFit(0f), Eps);
            // 適合0.5 → maxStabilityはctorでClamp01され1.0、Lerp(0.6, 1.0, 0.5) = 0.8
            Assert.AreEqual(0.8f, ClimatePolityFitRules.StabilityFromFit(0.5f), Eps);
        }

        /// <summary>専制の地形親和：広大で単調な地ほど専制に向く（積で効く）。</summary>
        [Test]
        public void DespotismTerrainAffinity_広大単調で最大()
        {
            // 広大1・単調1 → 1
            Assert.AreEqual(1f, ClimatePolityFitRules.DespotismTerrainAffinity(1f, 1f), Eps);
            // どちらか欠けると0
            Assert.AreEqual(0f, ClimatePolityFitRules.DespotismTerrainAffinity(1f, 0f), Eps);
            // 広大0.8・単調0.5 → 0.4
            Assert.AreEqual(0.4f, ClimatePolityFitRules.DespotismTerrainAffinity(0.8f, 0.5f), Eps);
        }

        /// <summary>自由の地形親和：険しく分断された地形ほど自由を育てる（積で効く）。</summary>
        [Test]
        public void FreedomTerrainAffinity_険しく分断で最大()
        {
            // 険しい1・分断1 → 1（山岳は自由の砦）
            Assert.AreEqual(1f, ClimatePolityFitRules.FreedomTerrainAffinity(1f, 1f), Eps);
            // 平坦（険しさ0）なら0
            Assert.AreEqual(0f, ClimatePolityFitRules.FreedomTerrainAffinity(0f, 1f), Eps);
            // 険しい0.6・分断0.5 → 0.3
            Assert.AreEqual(0.3f, ClimatePolityFitRules.FreedomTerrainAffinity(0.6f, 0.5f), Eps);
        }

        /// <summary>ミスマッチペナルティ：風土に合わない政体ほど統治効率が落ちる。</summary>
        [Test]
        public void ClimateMismatchPenalty_不適合で統治効率低下()
        {
            // 完全適合 → ペナルティ0
            Assert.AreEqual(0f, ClimatePolityFitRules.ClimateMismatchPenalty(1f), Eps);
            // 完全不適合 → (1-0)*0.5 = 0.5（最大ペナルティ）
            Assert.AreEqual(0.5f, ClimatePolityFitRules.ClimateMismatchPenalty(0f), Eps);
            // 適合0.4 → (1-0.4)*0.5 = 0.3
            Assert.AreEqual(0.3f, ClimatePolityFitRules.ClimateMismatchPenalty(0.4f), Eps);
        }

        /// <summary>時間での適応：政体の原動力が国民の気質へ徐々に近づく（基準非破壊＝新値を返す）。</summary>
        [Test]
        public void AdaptationOverTime_政体が風土へ馴染む()
        {
            // 気質0.8へ、原動力0.3 から adaptationRate0.1×dt1 = 0.1 ぶん近づく → 0.4
            Assert.AreEqual(0.4f, ClimatePolityFitRules.AdaptationOverTime(0.8f, 0.3f, 1f), Eps);
            // dt0 なら不変
            Assert.AreEqual(0.3f, ClimatePolityFitRules.AdaptationOverTime(0.8f, 0.3f, 0f), Eps);
            // 行き過ぎず目標で止まる（dt大でも気質を越えない）
            Assert.AreEqual(0.8f, ClimatePolityFitRules.AdaptationOverTime(0.8f, 0.3f, 100f), Eps);
        }

        /// <summary>風土適合判定：適合度が閾値以上で適合した安定状態とみなす。</summary>
        [Test]
        public void IsClimaticallySuited_閾値で適合判定()
        {
            // 既定閾値0.5：以上で true
            Assert.IsTrue(ClimatePolityFitRules.IsClimaticallySuited(0.5f));
            Assert.IsTrue(ClimatePolityFitRules.IsClimaticallySuited(0.7f));
            // 閾値未満で false
            Assert.IsFalse(ClimatePolityFitRules.IsClimaticallySuited(0.49f));
            // 明示閾値
            Assert.IsTrue(ClimatePolityFitRules.IsClimaticallySuited(0.8f, 0.8f));
            Assert.IsFalse(ClimatePolityFitRules.IsClimaticallySuited(0.79f, 0.8f));
        }
    }
}
