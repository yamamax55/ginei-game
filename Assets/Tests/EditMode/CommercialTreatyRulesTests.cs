using NUnit.Framework;
using UnityEngine;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 通商条約を固定する：関税引き下げ→貿易拡大→比較優位の利得、交渉力差による非対称、弱い側の取り分、
    /// 貿易依存→政治的影響、取り分のバランス＝互恵性、相互利益判定の境界を担保。既定Params期待値を手計算で検算。
    /// </summary>
    public class CommercialTreatyRulesTests
    {
        private static readonly CommercialTreatyParams P = CommercialTreatyParams.Default; // 全gain1.0/互恵閾値0.6

        [Test]
        public void TradeExpansion_TariffTimesMarket()
        {
            // 0.5 × 200 × 1 = 100
            Assert.AreEqual(100f, CommercialTreatyRules.TradeExpansion(0.5f, 200f, P), 1e-4f);
            // 関税ゼロ引き下げ＝拡大なし
            Assert.AreEqual(0f, CommercialTreatyRules.TradeExpansion(0f, 200f, P), 1e-4f);
            // 市場ゼロ＝拡大なし
            Assert.AreEqual(0f, CommercialTreatyRules.TradeExpansion(1f, 0f, P), 1e-4f);
            // クランプ（負入力）と委譲版一致
            Assert.AreEqual(0f, CommercialTreatyRules.TradeExpansion(-0.5f, 200f, P), 1e-4f);
            Assert.AreEqual(100f, CommercialTreatyRules.TradeExpansion(0.5f, 200f), 1e-4f);
        }

        [Test]
        public void ComparativeAdvantageGain_SpecializationTimesExpansion()
        {
            // 0.8 × 100 × 1 = 80
            Assert.AreEqual(80f, CommercialTreatyRules.ComparativeAdvantageGain(0.8f, 100f, P), 1e-4f);
            // 専門化ゼロ＝利得なし
            Assert.AreEqual(0f, CommercialTreatyRules.ComparativeAdvantageGain(0f, 100f, P), 1e-4f);
            Assert.AreEqual(80f, CommercialTreatyRules.ComparativeAdvantageGain(0.8f, 100f), 1e-4f);
        }

        [Test]
        public void TermsAsymmetry_BargainingGapSigned()
        {
            // A優位＝0.9−0.3＝0.6（正）
            Assert.AreEqual(0.6f, CommercialTreatyRules.TermsAsymmetry(0.9f, 0.3f, P), 1e-4f);
            // B優位＝負
            Assert.AreEqual(-0.6f, CommercialTreatyRules.TermsAsymmetry(0.3f, 0.9f, P), 1e-4f);
            // 対等＝0
            Assert.AreEqual(0f, CommercialTreatyRules.TermsAsymmetry(0.5f, 0.5f, P), 1e-4f);
            // -1..1 にクランプ
            Assert.GreaterOrEqual(CommercialTreatyRules.TermsAsymmetry(1f, 0f, P), -1f);
            Assert.LessOrEqual(CommercialTreatyRules.TermsAsymmetry(1f, 0f, P), 1f);
        }

        [Test]
        public void GainsDistribution_ShrinksWithAsymmetry()
        {
            // 対等＝利得満額
            Assert.AreEqual(80f, CommercialTreatyRules.GainsDistribution(80f, 0f, P), 1e-4f);
            // 非対称0.6＝80×(1−0.6)＝32
            Assert.AreEqual(32f, CommercialTreatyRules.GainsDistribution(80f, 0.6f, P), 1e-4f);
            // 符号は絶対値で効く＝負でも同じ
            Assert.AreEqual(32f, CommercialTreatyRules.GainsDistribution(80f, -0.6f, P), 1e-4f);
            Assert.AreEqual(32f, CommercialTreatyRules.GainsDistribution(80f, 0.6f), 1e-4f);
        }

        [Test]
        public void TradeDependency_ExpansionOverSize()
        {
            // 100 / 400 × 1 = 0.25
            Assert.AreEqual(0.25f, CommercialTreatyRules.TradeDependency(100f, 400f, P), 1e-4f);
            // 経済規模ゼロ＝依存0（安全）
            Assert.AreEqual(0f, CommercialTreatyRules.TradeDependency(100f, 0f, P), 1e-4f);
            // 0..1 クランプ（拡大が規模を超過）
            Assert.AreEqual(1f, CommercialTreatyRules.TradeDependency(500f, 400f, P), 1e-4f);
            Assert.AreEqual(0.25f, CommercialTreatyRules.TradeDependency(100f, 400f), 1e-4f);
        }

        [Test]
        public void PoliticalInfluence_DependencyTimesAsymmetry()
        {
            // 0.25 × 0.6 × 1 = 0.15
            Assert.AreEqual(0.15f, CommercialTreatyRules.PoliticalInfluence(0.25f, 0.6f, P), 1e-4f);
            // 対等条約＝影響なし
            Assert.AreEqual(0f, CommercialTreatyRules.PoliticalInfluence(0.25f, 0f, P), 1e-4f);
            // 依存なし＝影響なし
            Assert.AreEqual(0f, CommercialTreatyRules.PoliticalInfluence(0f, 0.6f, P), 1e-4f);
            Assert.AreEqual(0.15f, CommercialTreatyRules.PoliticalInfluence(0.25f, 0.6f), 1e-4f);
        }

        [Test]
        public void Reciprocity_BalanceOfShares()
        {
            // 取り分が等しい＝完全互恵
            Assert.AreEqual(1f, CommercialTreatyRules.Reciprocity(50f, 50f, P), 1e-4f);
            // 32 と 80 ＝ 1 − 48/112 ＝ 0.571428...
            Assert.AreEqual(0.5714286f, CommercialTreatyRules.Reciprocity(32f, 80f, P), 1e-4f);
            // 双方ゼロ＝互恵なし
            Assert.AreEqual(0f, CommercialTreatyRules.Reciprocity(0f, 0f, P), 1e-4f);
            // 片務的（一方ゼロ）＝0
            Assert.AreEqual(0f, CommercialTreatyRules.Reciprocity(0f, 80f, P), 1e-4f);
            Assert.AreEqual(0.5714286f, CommercialTreatyRules.Reciprocity(32f, 80f), 1e-4f);
        }

        [Test]
        public void IsTreatyMutuallyBeneficial_RequiresGainAndReciprocity()
        {
            // 利得正＋互恵0.8≥0.6＝成立
            Assert.IsTrue(CommercialTreatyRules.IsTreatyMutuallyBeneficial(80f, 0.8f, 0.6f));
            // 互恵0.57<0.6＝不成立（不平等条約）
            Assert.IsFalse(CommercialTreatyRules.IsTreatyMutuallyBeneficial(80f, 0.5714286f, 0.6f));
            // 利得ゼロ＝不成立
            Assert.IsFalse(CommercialTreatyRules.IsTreatyMutuallyBeneficial(0f, 1f, 0.6f));
            // 既定閾値委譲版（0.6）
            Assert.IsTrue(CommercialTreatyRules.IsTreatyMutuallyBeneficial(80f, 0.8f));
            Assert.IsFalse(CommercialTreatyRules.IsTreatyMutuallyBeneficial(80f, 0.5f));
        }

        [Test]
        public void Narrative_UnequalTreatyYieldsInfluenceNotMutualBenefit()
        {
            // 強国A（交渉力0.9）と弱国B（0.3）が関税0.5引き下げ・市場200で交易協定を結ぶ
            float expansion = CommercialTreatyRules.TradeExpansion(0.5f, 200f, P);          // 100
            float gain = CommercialTreatyRules.ComparativeAdvantageGain(0.8f, expansion, P); // 80
            float asym = CommercialTreatyRules.TermsAsymmetry(0.9f, 0.3f, P);               // 0.6（A有利）

            // 弱国Bの取り分は痩せ、強国Aは満額に近い
            float bShare = CommercialTreatyRules.GainsDistribution(gain, asym, P);          // 32
            float aShare = CommercialTreatyRules.GainsDistribution(gain, 0f, P);            // 80（A側は不利を被らない）
            Assert.AreEqual(32f, bShare, 1e-4f);
            Assert.Less(bShare, aShare);

            // 弱国Bは小経済（規模400）で交易に依存し、Aがそれを梃子に政治的影響を持つ
            float depB = CommercialTreatyRules.TradeDependency(expansion, 400f, P);         // 0.25
            float influence = CommercialTreatyRules.PoliticalInfluence(depB, asym, P);      // 0.15
            Assert.Greater(influence, 0f);

            // 取り分が偏るので互恵性は低く、相互利益的とは判定されない
            float rec = CommercialTreatyRules.Reciprocity(aShare, bShare, P);               // 0.571...
            Assert.Less(rec, P.reciprocityThreshold);
            Assert.IsFalse(CommercialTreatyRules.IsTreatyMutuallyBeneficial(gain, rec));
        }
    }
}
