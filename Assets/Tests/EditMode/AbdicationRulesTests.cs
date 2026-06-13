using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>AbdicationRules（生前継承・摂政・引き際）の EditMode テスト。</summary>
    public class AbdicationRulesTests
    {
        private const float Eps = 1e-4f;

        // ─── AbdicationWill ────────────────────────────────────────────

        [Test]
        public void AbdicationWill_全入力ゼロなら0()
        {
            float result = AbdicationRules.AbdicationWill(0f, 0f, 0f, 0f);
            Assert.AreEqual(0f, result, Eps);
        }

        [Test]
        public void AbdicationWill_全入力が1のとき権力返上しやすさに従う()
        {
            var p = AbdicationParams.Default;
            // 全要素1.0 → 加重和 / sum × powerReleaseEase = 1.0 × 0.5 = 0.5
            float result = AbdicationRules.AbdicationWill(1f, 1f, 1f, 1f, p);
            Assert.AreEqual(0.5f, result, Eps);
        }

        [Test]
        public void AbdicationWill_powerReleaseEase0なら常に0()
        {
            var p = new AbdicationParams(0.35f, 0.25f, 0.2f, 0.2f, 0.6f, 0f, 0.5f);
            float result = AbdicationRules.AbdicationWill(1f, 1f, 1f, 1f, p);
            Assert.AreEqual(0f, result, Eps);
        }

        [Test]
        public void AbdicationWill_結果は0から1の範囲に収まる()
        {
            float result = AbdicationRules.AbdicationWill(2f, 2f, 2f, 2f);
            Assert.IsTrue(result >= 0f && result <= 1f, $"範囲外: {result}");
        }

        // ─── IsPlannedAbdication ────────────────────────────────────────

        [Test]
        public void IsPlannedAbdication_意思が閾値以上でtrue()
        {
            var p = AbdicationParams.Default; // plannedThreshold = 0.6
            Assert.IsTrue(AbdicationRules.IsPlannedAbdication(0.6f, p));
            Assert.IsTrue(AbdicationRules.IsPlannedAbdication(1.0f, p));
        }

        [Test]
        public void IsPlannedAbdication_意思が閾値未満でfalse()
        {
            var p = AbdicationParams.Default; // plannedThreshold = 0.6
            Assert.IsFalse(AbdicationRules.IsPlannedAbdication(0.59f, p));
            Assert.IsFalse(AbdicationRules.IsPlannedAbdication(0f, p));
        }

        // ─── NeedsRegentBridge ────────────────────────────────────────

        [Test]
        public void NeedsRegentBridge_準備度が閾値未満でtrue()
        {
            var p = AbdicationParams.Default; // regentBridgeThreshold = 0.5
            Assert.IsTrue(AbdicationRules.NeedsRegentBridge(0.49f, p));
            Assert.IsTrue(AbdicationRules.NeedsRegentBridge(0f, p));
        }

        [Test]
        public void NeedsRegentBridge_準備度が閾値以上でfalse()
        {
            var p = AbdicationParams.Default;
            Assert.IsFalse(AbdicationRules.NeedsRegentBridge(0.5f, p));
            Assert.IsFalse(AbdicationRules.NeedsRegentBridge(1f, p));
        }

        // ─── TransitionLength ────────────────────────────────────────

        [Test]
        public void TransitionLength_準備十分なら0()
        {
            var p = AbdicationParams.Default; // regentBridgeThreshold = 0.5
            float result = AbdicationRules.TransitionLength(0.5f, p);
            Assert.AreEqual(0f, result, Eps);
        }

        [Test]
        public void TransitionLength_準備ゼロなら最大1()
        {
            var p = AbdicationParams.Default; // regentBridgeThreshold = 0.5
            float result = AbdicationRules.TransitionLength(0f, p);
            Assert.AreEqual(1f, result, Eps);
        }

        [Test]
        public void TransitionLength_中間値は単調減少()
        {
            var p = AbdicationParams.Default;
            float less = AbdicationRules.TransitionLength(0.1f, p);
            float more = AbdicationRules.TransitionLength(0.4f, p);
            Assert.IsTrue(less > more, $"単調減少でない: {less} vs {more}");
        }

        // ─── LegacyGift ────────────────────────────────────────────────

        [Test]
        public void LegacyGift_非計画退位なら0()
        {
            // plannedThreshold=0.6 なので 0.5 は非計画退位
            float result = AbdicationRules.LegacyGift(0.5f, 1f);
            Assert.AreEqual(0f, result, Eps);
        }

        [Test]
        public void LegacyGift_計画退位かつ準備ゼロなら0()
        {
            float result = AbdicationRules.LegacyGift(0.8f, 0f);
            Assert.AreEqual(0f, result, Eps);
        }

        [Test]
        public void LegacyGift_計画退位かつ準備十分で正の値()
        {
            float result = AbdicationRules.LegacyGift(0.8f, 0.9f);
            Assert.IsTrue(result > 0f, $"正の値でない: {result}");
        }

        [Test]
        public void LegacyGift_結果は0から1の範囲()
        {
            float result = AbdicationRules.LegacyGift(1f, 1f);
            Assert.IsTrue(result >= 0f && result <= 1f);
        }

        // ─── PowerClinging ────────────────────────────────────────────

        [Test]
        public void PowerClinging_退位意思1なら0()
        {
            float result = AbdicationRules.PowerClinging(1f);
            Assert.AreEqual(0f, result, Eps);
        }

        [Test]
        public void PowerClinging_退位意思0で権力執着文化なら最大()
        {
            // powerReleaseEase=0 → (1-0)*(1-0)=1
            var p = new AbdicationParams(0.35f, 0.25f, 0.2f, 0.2f, 0.6f, 0f, 0.5f);
            float result = AbdicationRules.PowerClinging(0f, p);
            Assert.AreEqual(1f, result, Eps);
        }

        [Test]
        public void PowerClinging_結果は0から1の範囲()
        {
            float result = AbdicationRules.PowerClinging(0.3f);
            Assert.IsTrue(result >= 0f && result <= 1f);
        }

        // ─── CoregencyPowerRatio ──────────────────────────────────────

        [Test]
        public void CoregencyPowerRatio_摂政不要なら常に0()
        {
            var p = AbdicationParams.Default; // regentBridgeThreshold=0.5
            // heirReadiness=0.5 は閾値ちょうど → NeedsRegentBridge=false
            float result = AbdicationRules.CoregencyPowerRatio(0f, 0.5f, p);
            Assert.AreEqual(0f, result, Eps);
        }

        [Test]
        public void CoregencyPowerRatio_移行開始直後は権力が旧主にある()
        {
            var p = AbdicationParams.Default; // regentBridgeThreshold=0.5
            // heirReadiness=0.1 → 摂政必要・transition=0 → 旧主の権力が100%
            float result = AbdicationRules.CoregencyPowerRatio(0f, 0.1f, p);
            Assert.AreEqual(1f, result, Eps);
        }

        [Test]
        public void CoregencyPowerRatio_移行完了で権力ゼロ()
        {
            var p = AbdicationParams.Default;
            float result = AbdicationRules.CoregencyPowerRatio(1f, 0.1f, p);
            Assert.AreEqual(0f, result, Eps);
        }

        // ─── デフォルトオーバーロードのスモークテスト ────────────────

        [Test]
        public void デフォルトオーバーロードは明示Defaultと同値()
        {
            var p = AbdicationParams.Default;
            float explicit_ = AbdicationRules.AbdicationWill(0.6f, 0.4f, 0.3f, 0.7f, p);
            float implicit_ = AbdicationRules.AbdicationWill(0.6f, 0.4f, 0.3f, 0.7f);
            Assert.AreEqual(explicit_, implicit_, Eps);
        }
    }
}
