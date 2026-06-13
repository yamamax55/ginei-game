using NUnit.Framework;
using UnityEngine;

namespace Ginei.Tests
{
    /// <summary>経済計算問題の純ロジックテスト（ミーゼス・ハイエク型・HAYK-2 #1544）。</summary>
    public class CalculationProblemRulesTests
    {
        const float Eps = 1e-4f;

        /// <summary>価格シグナルの質＝市場価格決定×(1−歪み×効き)。統制で歪むほど劣化する。</summary>
        [Test]
        public void PriceSignalQuality_市場価格満点で歪みゼロなら1_歪みで劣化()
        {
            // 市場決定1・歪み0 → 1
            Assert.AreEqual(1f, CalculationProblemRules.PriceSignalQuality(1f, 0f), Eps);
            // 市場決定1・歪み0.5 → 1×(1−0.5) = 0.5
            Assert.AreEqual(0.5f, CalculationProblemRules.PriceSignalQuality(1f, 0.5f), Eps);
            // 統制で価格決定が薄い（0.4）・歪み0.5 → 0.4×0.5 = 0.2
            Assert.AreEqual(0.2f, CalculationProblemRules.PriceSignalQuality(0.4f, 0.5f), Eps);
        }

        /// <summary>経済計算能力＝価格シグナル質×(1−計画化²×効き)。計画化が二乗で計算能力を削る。</summary>
        [Test]
        public void CalculationCapacity_計画化が非線形に計算能力を削る()
        {
            // 価格質1・計画化0 → 1
            Assert.AreEqual(1f, CalculationProblemRules.CalculationCapacity(1f, 0f), Eps);
            // 価格質1・計画化0.5 → 1×(1−0.25) = 0.75（軽い計画は害が小さい）
            Assert.AreEqual(0.75f, CalculationProblemRules.CalculationCapacity(1f, 0.5f), Eps);
            // 価格質1・計画化1.0 → 1×(1−1) = 0（全面計画は暗中模索）
            Assert.AreEqual(0f, CalculationProblemRules.CalculationCapacity(1f, 1f), Eps);
        }

        /// <summary>価格シグナルが死んでいれば計画化が低くても計算能力は出ない（価格が計算の前提）。</summary>
        [Test]
        public void CalculationCapacity_価格シグナルなしは計画化が低くても計算できない()
        {
            // 価格質0・計画化0.2 → 0（価格こそが計算の前提）
            Assert.AreEqual(0f, CalculationProblemRules.CalculationCapacity(0f, 0.2f), Eps);
        }

        /// <summary>配分効率＝計算能力で 0.2〜1.0 を線形補間。計算能力が低いほど暗中模索。</summary>
        [Test]
        public void AllocationEfficiency_計算能力で下駄0_2から1へ補間()
        {
            // 計算能力0 → 下駄0.2
            Assert.AreEqual(0.2f, CalculationProblemRules.AllocationEfficiency(0f), Eps);
            // 計算能力0.5 → Lerp(0.2,1,0.5) = 0.6
            Assert.AreEqual(0.6f, CalculationProblemRules.AllocationEfficiency(0.5f), Eps);
            // 計算能力1 → 1
            Assert.AreEqual(1f, CalculationProblemRules.AllocationEfficiency(1f), Eps);
        }

        /// <summary>生産性ペナルティ＝(1−配分効率)×0.6。非効率な配分が生産性を削る。</summary>
        [Test]
        public void ProductivityPenalty_配分効率が低いほど生産性を削る()
        {
            // 配分効率1 → 罰なし
            Assert.AreEqual(0f, CalculationProblemRules.ProductivityPenalty(1f), Eps);
            // 配分効率0 → (1−0)×0.6 = 0.6（最大ペナルティ）
            Assert.AreEqual(0.6f, CalculationProblemRules.ProductivityPenalty(0f), Eps);
            // 配分効率0.5 → 0.5×0.6 = 0.3
            Assert.AreEqual(0.3f, CalculationProblemRules.ProductivityPenalty(0.5f), Eps);
        }

        /// <summary>誤配分の累積＝率×計画化×(1−現誤配分)×dt。計画化が進むほど溜まる。</summary>
        [Test]
        public void MisallocationTick_計画化が高いほど誤配分が累積_計画ゼロは不変()
        {
            // 誤配分0・計画化1・dt1 → 0 + 0.1×1×1×1 = 0.1
            Assert.AreEqual(0.1f, CalculationProblemRules.MisallocationTick(0f, 1f, 1f), Eps);
            // 既に0.5・計画化1・dt1 → 0.5 + 0.1×1×0.5×1 = 0.55（伸びしろに比例して飽和へ）
            Assert.AreEqual(0.55f, CalculationProblemRules.MisallocationTick(0.5f, 1f, 1f), Eps);
            // 計画化0 → 市場が配分するので不変
            Assert.AreEqual(0.3f, CalculationProblemRules.MisallocationTick(0.3f, 0f, 1f), Eps);
        }

        /// <summary>情報損失＝中央計画×分散知識（ハイエクの知識問題）。計画化×現場知識で取りこぼす。</summary>
        [Test]
        public void InformationLoss_中央計画が分散知識を取りこぼす()
        {
            // 計画化1・分散知識1 → 1（全量取りこぼし）
            Assert.AreEqual(1f, CalculationProblemRules.InformationLoss(1f, 1f), Eps);
            // 計画化0 → 現場で知識が活きて損失ゼロ
            Assert.AreEqual(0f, CalculationProblemRules.InformationLoss(0f, 0.8f), Eps);
            // 分散知識0 → 失うものがない
            Assert.AreEqual(0f, CalculationProblemRules.InformationLoss(0.8f, 0f), Eps);
            // 計画化0.5・分散知識0.6 → 0.3
            Assert.AreEqual(0.3f, CalculationProblemRules.InformationLoss(0.5f, 0.6f), Eps);
        }

        /// <summary>不足と過剰の同時発生＝誤配分×(1−誤配分)×4。中庸で最大の山なり。</summary>
        [Test]
        public void ShortageAndSurplus_誤配分が中庸で最大の山なり()
        {
            // 誤配分0 → 均衡で乖離なし
            Assert.AreEqual(0f, CalculationProblemRules.ShortageAndSurplus(0f), Eps);
            // 誤配分0.5 → 0.5×0.5×4 = 1（不足と過剰が最も顕著）
            Assert.AreEqual(1f, CalculationProblemRules.ShortageAndSurplus(0.5f), Eps);
            // 誤配分1 → 全面破綻で乖離としては鈍る
            Assert.AreEqual(0f, CalculationProblemRules.ShortageAndSurplus(1f), Eps);
            // 誤配分0.25 → 0.25×0.75×4 = 0.75
            Assert.AreEqual(0.75f, CalculationProblemRules.ShortageAndSurplus(0.25f), Eps);
        }

        /// <summary>計算混沌の判定＝計算能力が閾値を下回ると経済計算が破綻した混沌。</summary>
        [Test]
        public void IsCalculationChaos_計算能力が閾値割れで混沌()
        {
            // 計算能力0.1 < 閾値0.3 → 混沌
            Assert.IsTrue(CalculationProblemRules.IsCalculationChaos(0.1f, 0.3f));
            // 計算能力0.5 ≥ 閾値0.3 → 機能している
            Assert.IsFalse(CalculationProblemRules.IsCalculationChaos(0.5f, 0.3f));
            // 全面計画で計算能力0 → 混沌
            Assert.IsTrue(CalculationProblemRules.IsCalculationChaos(0f, 0.3f));
        }

        /// <summary>パイプライン統合＝統制経済は価格劣化→計算不能→低効率→生産性損失の連鎖をたどる。</summary>
        [Test]
        public void Pipeline_価格なき計画は生産性損失へ連鎖する()
        {
            // 統制強：市場価格薄く(0.3)・歪み大(0.7)・全面計画(0.9)
            float psq = CalculationProblemRules.PriceSignalQuality(0.3f, 0.7f);
            float cc = CalculationProblemRules.CalculationCapacity(psq, 0.9f);
            float ae = CalculationProblemRules.AllocationEfficiency(cc);
            float pen = CalculationProblemRules.ProductivityPenalty(ae);

            // 市場経済：価格決定満点・歪みなし・計画ゼロ
            float psqM = CalculationProblemRules.PriceSignalQuality(1f, 0f);
            float ccM = CalculationProblemRules.CalculationCapacity(psqM, 0f);
            float aeM = CalculationProblemRules.AllocationEfficiency(ccM);
            float penM = CalculationProblemRules.ProductivityPenalty(aeM);

            // 統制経済の方が計算能力・配分効率が低く、生産性ペナルティが重い
            Assert.Less(cc, ccM);
            Assert.Less(ae, aeM);
            Assert.Greater(pen, penM);
            Assert.AreEqual(0f, penM, Eps); // 価格が機能する世界は罰なし
        }
    }
}
