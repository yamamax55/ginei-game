using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>反乱（内政→戦略の創発ループ）：不穏スコアの蓄積・回復・離反/兆し判定を固定する。</summary>
    public class RebellionRulesTests
    {
        private static Province WithStability(float stability)
        {
            var p = new Province(1, "専制", 100f);
            p.stability = stability;
            return p;
        }

        [Test]
        public void Score_RisesWhenUnrest()
        {
            // 安定度0＝反乱圧1.0/年 → 1年で +1.0
            float s = RebellionRules.NextScore(0f, WithStability(0f));
            Assert.AreEqual(1.0f, s, 1e-4f);
            // 安定度12.5＝反乱圧0.5/年
            Assert.AreEqual(0.5f, RebellionRules.NextScore(0f, WithStability(12.5f)), 1e-4f);
        }

        [Test]
        public void Score_RecoversWhenStable()
        {
            // 安定（しきい値25以上）なら回復し、0 で下げ止まる。
            Assert.AreEqual(1.5f, RebellionRules.NextScore(2.0f, WithStability(60f)), 1e-4f);
            Assert.AreEqual(0f, RebellionRules.NextScore(0.2f, WithStability(60f)), 1e-4f);
        }

        [Test]
        public void Revolt_AtThreshold()
        {
            Assert.IsFalse(RebellionRules.ShouldRevolt(RebellionRules.RevoltThreshold - 0.01f));
            Assert.IsTrue(RebellionRules.ShouldRevolt(RebellionRules.RevoltThreshold));
        }

        [Test]
        public void Brewing_BeforeRevolt()
        {
            Assert.IsFalse(RebellionRules.IsBrewing(RebellionRules.RevoltThreshold * RebellionRules.WarnFraction - 0.01f));
            Assert.IsTrue(RebellionRules.IsBrewing(RebellionRules.RevoltThreshold * RebellionRules.WarnFraction));
            // 兆しは離反より早く立つ
            Assert.IsTrue(RebellionRules.IsBrewing(RebellionRules.RevoltThreshold) );
        }

        [Test]
        public void SustainedUnrest_EventuallyRevolts()
        {
            // 中程度の不穏（安定度10＝圧0.6/年）が続けば数年で離反に至る。
            float score = 0f;
            int years = 0;
            var p = WithStability(10f);
            while (!RebellionRules.ShouldRevolt(score) && years < 100) { score = RebellionRules.NextScore(score, p); years++; }
            Assert.Less(years, 10);              // 10年以内に離反
            Assert.IsTrue(RebellionRules.ShouldRevolt(score));
        }

        [Test]
        public void NullProvince_Decays()
        {
            Assert.AreEqual(0f, RebellionRules.NextScore(0.3f, null), 1e-4f);
        }
    }
}
