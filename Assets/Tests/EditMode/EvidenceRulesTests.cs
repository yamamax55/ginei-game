using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>証拠の収束評価（IHER-1 #2397）：総信頼性・分野多様性・収束倍率・実効信頼性・確証判定・null安全。</summary>
    public class EvidenceRulesTests
    {
        private static EvidenceBody Body(params EvidenceFragment[] frags)
        {
            var b = new EvidenceBody();
            foreach (var f in frags) b.Add(f);
            return b;
        }

        [Test]
        public void TotalCredibility_SumsCredibilityTimesWeight_ClampsNegative()
        {
            var b = Body(
                new EvidenceFragment("起源", ResearchField.軍事, 0.5f, 2f),   // 1.0
                new EvidenceFragment("起源", ResearchField.生産, 0.4f, 0.5f), // 0.2
                new EvidenceFragment("起源", ResearchField.情報, -0.9f, 1f)); // 負→0
            Assert.AreEqual(1.2f, EvidenceRules.TotalCredibility(b), 1e-4f);
        }

        [Test]
        public void FieldDiversity_CountsDistinctFields()
        {
            var b = Body(
                new EvidenceFragment("起源", ResearchField.軍事, 0.5f, 1f),
                new EvidenceFragment("起源", ResearchField.軍事, 0.5f, 1f), // 重複→1つ
                new EvidenceFragment("起源", ResearchField.生産, 0.5f, 1f),
                new EvidenceFragment("起源", ResearchField.情報, 0.5f, 1f));
            Assert.AreEqual(3, EvidenceRules.FieldDiversity(b));
        }

        [Test]
        public void ConvergenceMultiplier_SingleFieldIsOne_AndMonotonicRising()
        {
            Assert.AreEqual(1f, EvidenceRules.ConvergenceMultiplier(0), 1e-4f);
            Assert.AreEqual(1f, EvidenceRules.ConvergenceMultiplier(1), 1e-4f);
            float m2 = EvidenceRules.ConvergenceMultiplier(2);
            float m3 = EvidenceRules.ConvergenceMultiplier(3);
            float m4 = EvidenceRules.ConvergenceMultiplier(4);
            Assert.AreEqual(1f + EvidenceRules.ConvergencePerField, m2, 1e-4f); // 1.15
            Assert.Greater(m3, m2); // 単調増加
            Assert.Greater(m4, m3);
            Assert.LessOrEqual(m4, EvidenceRules.MaxConvergence + 1e-4f); // 上限以内
        }

        [Test]
        public void ConvergenceMultiplier_CapsAtMax()
        {
            Assert.AreEqual(EvidenceRules.MaxConvergence, EvidenceRules.ConvergenceMultiplier(99), 1e-4f);
        }

        [Test]
        public void EffectiveCredibility_IsTotalTimesMultiplier()
        {
            var b = Body(
                new EvidenceFragment("起源", ResearchField.軍事, 0.5f, 1f),  // 0.5
                new EvidenceFragment("起源", ResearchField.生産, 0.5f, 1f)); // 0.5・2分野
            float total = EvidenceRules.TotalCredibility(b);           // 1.0
            float mult = EvidenceRules.ConvergenceMultiplier(2);       // 1.15
            Assert.AreEqual(total * mult, EvidenceRules.EffectiveCredibility(b), 1e-4f);
            Assert.AreEqual(1.15f, EvidenceRules.EffectiveCredibility(b), 1e-4f);
        }

        [Test]
        public void EffectiveCredibility_ConvergenceCanCrossThresholdSingleFieldCannot()
        {
            // 単一分野＝倍率1.0で閾値未満、分野を散らすと収束で超える
            var single = Body(
                new EvidenceFragment("起源", ResearchField.軍事, 0.5f, 1f),
                new EvidenceFragment("起源", ResearchField.軍事, 0.5f, 1f)); // total 1.0・1分野→1.0
            var converged = Body(
                new EvidenceFragment("起源", ResearchField.軍事, 0.5f, 1f),
                new EvidenceFragment("起源", ResearchField.生産, 0.5f, 1f)); // total 1.0・2分野→1.15
            Assert.IsFalse(EvidenceRules.IsConclusive(single, 1.1f));
            Assert.IsTrue(EvidenceRules.IsConclusive(converged, 1.1f));
        }

        [Test]
        public void IsConclusive_ThresholdBoundary()
        {
            var b = Body(new EvidenceFragment("起源", ResearchField.社会, 0.8f, 1f)); // total 0.8・1分野→0.8
            Assert.IsTrue(EvidenceRules.IsConclusive(b, 0.8f));   // 境界＝確証
            Assert.IsTrue(EvidenceRules.IsConclusive(b, 0.7f));
            Assert.IsFalse(EvidenceRules.IsConclusive(b, 0.8001f));
        }

        [Test]
        public void NullSafety_ReturnsZeroAndEmpty()
        {
            Assert.AreEqual(0f, EvidenceRules.TotalCredibility(null), 1e-4f);
            Assert.AreEqual(0, EvidenceRules.FieldDiversity(null));
            Assert.AreEqual(0f, EvidenceRules.EffectiveCredibility(null), 1e-4f);
            Assert.IsFalse(EvidenceRules.IsConclusive(null, 0.1f)); // 実効0＜正の閾値→false

            var empty = new EvidenceBody();
            Assert.AreEqual(0f, EvidenceRules.TotalCredibility(empty), 1e-4f);
            Assert.AreEqual(0, EvidenceRules.FieldDiversity(empty));
            empty.Add(null); // null 断片は無視
            Assert.AreEqual(0f, EvidenceRules.EffectiveCredibility(empty), 1e-4f);
        }
    }
}
