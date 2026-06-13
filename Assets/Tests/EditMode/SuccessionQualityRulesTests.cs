using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>SuccessionQualityRules（継承の質→次代の安定）の EditMode テスト。</summary>
    public class SuccessionQualityRulesTests
    {
        private const float Eps = 1e-4f;

        // ─── SuccessionQuality ────────────────────────────────────────

        [Test]
        public void SuccessionQuality_全入力ゼロで0()
        {
            float result = SuccessionQualityRules.SuccessionQuality(0f, 0f, 0f, 0f);
            Assert.AreEqual(0f, result, Eps);
        }

        [Test]
        public void SuccessionQuality_全入力最大かつ危機ゼロなら1()
        {
            float result = SuccessionQualityRules.SuccessionQuality(1f, 1f, 1f, 0f);
            Assert.AreEqual(1f, result, Eps);
        }

        [Test]
        public void SuccessionQuality_危機リスクが高いほど品質が下がる()
        {
            float low = SuccessionQualityRules.SuccessionQuality(0.8f, 0.8f, 0.8f, 0.2f);
            float high = SuccessionQualityRules.SuccessionQuality(0.8f, 0.8f, 0.8f, 0.8f);
            Assert.IsTrue(low > high, $"危機増加で品質減少でない: {low} vs {high}");
        }

        [Test]
        public void SuccessionQuality_後継資質の寄与は最大重み()
        {
            // heirMerit だけ高い場合
            float result = SuccessionQualityRules.SuccessionQuality(1f, 0f, 0f, 0f);
            Assert.IsTrue(result > 0f, $"後継資質が効かない: {result}");
        }

        [Test]
        public void SuccessionQuality_結果は0から1の範囲()
        {
            float result = SuccessionQualityRules.SuccessionQuality(2f, 2f, 2f, 2f);
            Assert.IsTrue(result >= 0f && result <= 1f, $"範囲外: {result}");
        }

        // ─── InitialLegitimacy ────────────────────────────────────────

        [Test]
        public void InitialLegitimacy_先代が高く品質が高いなら高い()
        {
            float result = SuccessionQualityRules.InitialLegitimacy(1f, 1f);
            Assert.AreEqual(1f, result, Eps);
        }

        [Test]
        public void InitialLegitimacy_品質ゼロなら正統性は引き継がれない()
        {
            float result = SuccessionQualityRules.InitialLegitimacy(1f, 0f);
            Assert.AreEqual(0f, result, Eps);
        }

        [Test]
        public void InitialLegitimacy_先代ゼロなら常に0()
        {
            float result = SuccessionQualityRules.InitialLegitimacy(0f, 1f);
            Assert.AreEqual(0f, result, Eps);
        }

        [Test]
        public void InitialLegitimacy_品質が高いほど正統性が多く引き継がれる()
        {
            float low = SuccessionQualityRules.InitialLegitimacy(0.8f, 0.3f);
            float high = SuccessionQualityRules.InitialLegitimacy(0.8f, 0.8f);
            Assert.IsTrue(low < high, $"品質増加で正統性増加でない: {low} vs {high}");
        }

        // ─── StabilityEffect ──────────────────────────────────────────

        [Test]
        public void StabilityEffect_品質が閾値超でボーナス_正の値()
        {
            var p = SuccessionQualityParams.Default; // stableThreshold=0.55
            float result = SuccessionQualityRules.StabilityEffect(1f, p);
            Assert.IsTrue(result > 0f, $"閾値超でボーナスが0以下: {result}");
        }

        [Test]
        public void StabilityEffect_品質ゼロでペナルティ_負の値()
        {
            var p = SuccessionQualityParams.Default;
            float result = SuccessionQualityRules.StabilityEffect(0f, p);
            Assert.IsTrue(result < 0f, $"品質ゼロでペナルティが0以上: {result}");
        }

        [Test]
        public void StabilityEffect_品質が閾値ちょうどで効果ゼロ()
        {
            var p = SuccessionQualityParams.Default; // stableThreshold=0.55
            float result = SuccessionQualityRules.StabilityEffect(0.55f, p);
            Assert.AreEqual(0f, result, Eps);
        }

        [Test]
        public void StabilityEffect_ボーナスはmaxStabilityBonusを超えない()
        {
            var p = SuccessionQualityParams.Default;
            float result = SuccessionQualityRules.StabilityEffect(1f, p);
            Assert.IsTrue(result <= p.maxStabilityBonus + Eps);
        }

        [Test]
        public void StabilityEffect_ペナルティはmaxStabilityPenaltyを超えない()
        {
            var p = SuccessionQualityParams.Default;
            float result = SuccessionQualityRules.StabilityEffect(0f, p);
            Assert.IsTrue(result >= -p.maxStabilityPenalty - Eps);
        }

        // ─── IsStableSuccession ──────────────────────────────────────

        [Test]
        public void IsStableSuccession_品質が閾値以上でtrue()
        {
            var p = SuccessionQualityParams.Default; // stableThreshold=0.55
            Assert.IsTrue(SuccessionQualityRules.IsStableSuccession(0.55f, p));
            Assert.IsTrue(SuccessionQualityRules.IsStableSuccession(1f, p));
        }

        [Test]
        public void IsStableSuccession_品質が閾値未満でfalse()
        {
            var p = SuccessionQualityParams.Default;
            Assert.IsFalse(SuccessionQualityRules.IsStableSuccession(0.54f, p));
            Assert.IsFalse(SuccessionQualityRules.IsStableSuccession(0f, p));
        }

        // ─── SuccessionBonusFactor ────────────────────────────────────

        [Test]
        public void SuccessionBonusFactor_高品質継承で1より大きい()
        {
            float result = SuccessionQualityRules.SuccessionBonusFactor(1f);
            Assert.IsTrue(result > 1f, $"高品質継承でボーナスが1以下: {result}");
        }

        [Test]
        public void SuccessionBonusFactor_低品質継承で1より小さい()
        {
            float result = SuccessionQualityRules.SuccessionBonusFactor(0f);
            Assert.IsTrue(result < 1f, $"低品質継承でボーナスが1以上: {result}");
        }

        [Test]
        public void SuccessionBonusFactor_結果は0_8から1_2の範囲()
        {
            float hi = SuccessionQualityRules.SuccessionBonusFactor(1f);
            float lo = SuccessionQualityRules.SuccessionBonusFactor(0f);
            Assert.IsTrue(hi <= 1.2f + Eps, $"上限超過: {hi}");
            Assert.IsTrue(lo >= 0.8f - Eps, $"下限超過: {lo}");
        }

        // ─── デフォルトオーバーロードのスモークテスト ────────────────

        [Test]
        public void デフォルトオーバーロードは明示Defaultと同値()
        {
            var p = SuccessionQualityParams.Default;
            float explicit_ = SuccessionQualityRules.SuccessionQuality(0.7f, 0.6f, 0.8f, 0.2f, p);
            float implicit_ = SuccessionQualityRules.SuccessionQuality(0.7f, 0.6f, 0.8f, 0.2f);
            Assert.AreEqual(explicit_, implicit_, Eps);
        }

        [Test]
        public void StabilityEffectデフォルトは明示Defaultと同値()
        {
            var p = SuccessionQualityParams.Default;
            float explicit_ = SuccessionQualityRules.StabilityEffect(0.7f, p);
            float implicit_ = SuccessionQualityRules.StabilityEffect(0.7f);
            Assert.AreEqual(explicit_, implicit_, Eps);
        }

        [Test]
        public void SuccessionBonusFactorデフォルトは明示Defaultと同値()
        {
            var p = SuccessionQualityParams.Default;
            float explicit_ = SuccessionQualityRules.SuccessionBonusFactor(0.7f, p);
            float implicit_ = SuccessionQualityRules.SuccessionBonusFactor(0.7f);
            Assert.AreEqual(explicit_, implicit_, Eps);
        }
    }
}
