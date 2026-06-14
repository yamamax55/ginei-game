using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>CrisisEffectRules（金融危機度→国庫収縮/産出低下/支持低下の橋渡し）の EditMode テスト。</summary>
    public class CrisisEffectRulesTests
    {
        private const float Eps = 1e-4f;

        [Test]
        public void Severity0_打撃なし()
        {
            Assert.AreEqual(0f, CrisisEffectRules.TreasuryContraction(0f), Eps);
            Assert.AreEqual(1f, CrisisEffectRules.OutputFactor(0f), Eps);
            Assert.AreEqual(0f, CrisisEffectRules.SupportDelta(0f), Eps);
        }

        [Test]
        public void 危機度増で国庫収縮が単調増()
        {
            float low = CrisisEffectRules.TreasuryContraction(0.3f);
            float high = CrisisEffectRules.TreasuryContraction(0.7f);
            Assert.Greater(high, low);
            Assert.Greater(low, 0f);
        }

        [Test]
        public void 危機度増で産出が単調に低下()
        {
            float low = CrisisEffectRules.OutputFactor(0.3f);
            float high = CrisisEffectRules.OutputFactor(0.7f);
            Assert.Less(high, low);     // 危機が深いほど産出倍率は小さい
            Assert.Less(low, 1f);
        }

        [Test]
        public void 危機度増で支持低下が単調に深まる()
        {
            float low = CrisisEffectRules.SupportDelta(0.3f);
            float high = CrisisEffectRules.SupportDelta(0.7f);
            Assert.Less(high, low);     // delta は負＝深い危機ほどより負
            Assert.Less(low, 0f);
        }

        [Test]
        public void 上限クランプ()
        {
            var p = CrisisEffectParams.Default;
            // severity>1 でも既定上限/下限で頭打ち。
            Assert.AreEqual(p.maxTreasuryContraction, CrisisEffectRules.TreasuryContraction(5f), Eps);
            Assert.AreEqual(p.minOutputFactor, CrisisEffectRules.OutputFactor(5f), Eps);
            Assert.AreEqual(-p.maxSupportDrop, CrisisEffectRules.SupportDelta(5f), Eps);
        }

        [Test]
        public void 下限クランプ_負のseverityは無害()
        {
            // severity<0 は 0 と同じ＝打撃なし。
            Assert.AreEqual(0f, CrisisEffectRules.TreasuryContraction(-1f), Eps);
            Assert.AreEqual(1f, CrisisEffectRules.OutputFactor(-1f), Eps);
            Assert.AreEqual(0f, CrisisEffectRules.SupportDelta(-1f), Eps);
        }

        [Test]
        public void カスタムparamsが効く()
        {
            var p = new CrisisEffectParams(0.8f, 0.2f, 50f);
            Assert.AreEqual(0.8f, CrisisEffectRules.TreasuryContraction(1f, p), Eps);
            Assert.AreEqual(0.2f, CrisisEffectRules.OutputFactor(1f, p), Eps);
            Assert.AreEqual(-50f, CrisisEffectRules.SupportDelta(1f, p), Eps);
        }
    }
}
