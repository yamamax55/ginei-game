using NUnit.Framework;
using UnityEngine;

namespace Ginei.Tests
{
    /// <summary>金融伝染の純ロジック（KNDB-3 #1615）のテスト。既定Paramsで期待値固定。</summary>
    public class FinancialContagionRulesTests
    {
        const float Eps = 1e-4f;

        /// <summary>伝染力＝エクスポージャー×震源の深刻度（既定exposureWeight=1で 0.5×0.8=0.4）。どちらか0なら伝わらない。</summary>
        [Test]
        public void TransmissionStrength_つながりと震源の積()
        {
            Assert.AreEqual(0.4f, FinancialContagionRules.TransmissionStrength(0.5f, 0.8f), Eps);
            Assert.AreEqual(0f, FinancialContagionRules.TransmissionStrength(0f, 1f), Eps);   // つながり無し＝伝わらない
            Assert.AreEqual(0f, FinancialContagionRules.TransmissionStrength(1f, 0f), Eps);   // 震源無し＝伝わらない
        }

        /// <summary>防火壁＝流動性供給(0.6)＋資本規制(0.4)の重み合成。</summary>
        [Test]
        public void FirewallEffectiveness_流動性と資本規制の合成()
        {
            Assert.AreEqual(0.6f, FinancialContagionRules.FirewallEffectiveness(1f, 0f), Eps);
            Assert.AreEqual(0.4f, FinancialContagionRules.FirewallEffectiveness(0f, 1f), Eps);
            Assert.AreEqual(0.5f, FinancialContagionRules.FirewallEffectiveness(0.5f, 0.5f), Eps);
            Assert.AreEqual(0f, FinancialContagionRules.FirewallEffectiveness(0f, 0f), Eps);
        }

        /// <summary>伝染1ステップ＝隣の高ストレスがつながりを伝って流入（防火壁0なら gap0.6×link0.5=0.3 加算）。</summary>
        [Test]
        public void ContagionTick_隣のストレスが流入する()
        {
            float next = FinancialContagionRules.ContagionTick(0.2f, 0.8f, 0.5f, 0f, 1f);
            Assert.AreEqual(0.5f, next, Eps);
            // 隣の方が低ければ流れ込まない（下方向には伝染しない）。
            Assert.AreEqual(0.8f, FinancialContagionRules.ContagionTick(0.8f, 0.2f, 0.5f, 0f, 1f), Eps);
        }

        /// <summary>防火壁が満タン(1.0)だと流入が完全に断たれる＝連鎖を断つ（自市場ストレス据え置き）。</summary>
        [Test]
        public void ContagionTick_防火壁が連鎖を断つ()
        {
            Assert.AreEqual(0.2f, FinancialContagionRules.ContagionTick(0.2f, 0.8f, 0.5f, 1f, 1f), Eps);
            // 半分の防火壁なら半分だけ流入（0.3×0.5=0.15）。
            Assert.AreEqual(0.35f, FinancialContagionRules.ContagionTick(0.2f, 0.8f, 0.5f, 0.5f, 1f), Eps);
        }

        /// <summary>相関崩壊＝危機が深いほど非線形に1へ（0.5→0.25・0.8→0.64・平時0は無相関）。</summary>
        [Test]
        public void CorrelationBreakdown_危機で相関が1へ崩壊()
        {
            Assert.AreEqual(0f, FinancialContagionRules.CorrelationBreakdown(0f), Eps);
            Assert.AreEqual(0.25f, FinancialContagionRules.CorrelationBreakdown(0.5f), Eps);
            Assert.AreEqual(0.64f, FinancialContagionRules.CorrelationBreakdown(0.8f), Eps);
            Assert.AreEqual(1f, FinancialContagionRules.CorrelationBreakdown(1f), Eps);
        }

        /// <summary>実効分散＝平時は名目どおり効くが危機では消える（分散の幻想）。</summary>
        [Test]
        public void EffectiveDiversification_危機で分散が消える()
        {
            Assert.AreEqual(1f, FinancialContagionRules.EffectiveDiversification(1f, 0f), Eps);     // 平時＝満額効く
            Assert.AreEqual(0.75f, FinancialContagionRules.EffectiveDiversification(1f, 0.5f), Eps); // 1×(1−0.25)
            Assert.AreEqual(0f, FinancialContagionRules.EffectiveDiversification(1f, 1f), Eps);     // 危機＝分散が消える
        }

        /// <summary>取付け判定＝確率 stress×(1−confidence) を roll が下回れば発生（決定論）。</summary>
        [Test]
        public void BankRunProbability_決定論の取付け()
        {
            // stress0.8・confidence0.5 → 確率0.4。
            Assert.IsTrue(FinancialContagionRules.BankRunProbability(0.8f, 0.5f, 0.39f));
            Assert.IsFalse(FinancialContagionRules.BankRunProbability(0.8f, 0.5f, 0.41f));
            // 信認満タンなら起きない。
            Assert.IsFalse(FinancialContagionRules.BankRunProbability(1f, 1f, 0f));
        }

        /// <summary>系崩壊リスク＝平均ストレス×平均接続。封じ込めは防火壁が閾値0.7以上で成功。</summary>
        [Test]
        public void SystemicCollapseとContainment()
        {
            Assert.AreEqual(0.3f, FinancialContagionRules.SystemicCollapseRisk(0.5f, 0.6f), Eps);
            Assert.AreEqual(0f, FinancialContagionRules.SystemicCollapseRisk(0.9f, 0f), Eps); // 分散しきった系
            Assert.IsTrue(FinancialContagionRules.ContainmentThreshold(0.7f));
            Assert.IsFalse(FinancialContagionRules.ContainmentThreshold(0.69f));
        }
    }
}
