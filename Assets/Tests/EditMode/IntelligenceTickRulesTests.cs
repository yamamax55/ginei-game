using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 諜報オーケストレータ（IntelligenceTickRules）を固定する：予算で能力↑（飽和・上限超えなし）／
    /// 高capabilityで可視↑・高counterで可視↓／工作成否が roll で決定論／null 安全。委譲先の数式は各 Rules のテストが担保。
    /// </summary>
    public class IntelligenceTickRulesTests
    {
        // --- TickYear（予算で育成・飽和） ---

        [Test]
        public void TickYear_Funding_GrowsCapabilityAndCounter()
        {
            var s = new IntelState(0f, 0f);
            IntelligenceTickRules.TickYear(s, intelFunding: 1f, dt: 1f);
            Assert.Greater(s.capability, 0f, "予算で攻め能力が伸びる");
            Assert.Greater(s.counterIntel, 0f, "予算で防諜も伸びる");
            Assert.LessOrEqual(s.capability, 1f);
            Assert.LessOrEqual(s.counterIntel, 1f);
        }

        [Test]
        public void TickYear_ZeroFunding_NoGrowth()
        {
            var s = new IntelState(0.3f, 0.3f);
            IntelligenceTickRules.TickYear(s, intelFunding: 0f, dt: 1f);
            Assert.AreEqual(0.3f, s.capability, 1e-5f);
            Assert.AreEqual(0.3f, s.counterIntel, 1e-5f);
        }

        [Test]
        public void TickYear_Saturates_NeverExceedsOne()
        {
            var s = new IntelState(0.99f, 0.99f);
            // 多数tickでも上限を超えない（飽和＝残余幅に比例して鈍る）
            for (int i = 0; i < 100; i++)
                IntelligenceTickRules.TickYear(s, intelFunding: 1f, dt: 1f);
            Assert.LessOrEqual(s.capability, 1f);
            Assert.LessOrEqual(s.counterIntel, 1f);
            Assert.Greater(s.capability, 0.99f, "1へ漸近する");
        }

        // --- Visibility（高capで可視↑・高counterで可視↓） ---

        [Test]
        public void Visibility_HigherCapability_RaisesVisibility()
        {
            float low = IntelligenceTickRules.Visibility(0.2f, 0.3f);
            float high = IntelligenceTickRules.Visibility(0.8f, 0.3f);
            Assert.Greater(high, low, "観測能力が高いほど相手が見える");
        }

        [Test]
        public void Visibility_HigherCounterIntel_LowersVisibility()
        {
            float weak = IntelligenceTickRules.Visibility(0.7f, 0.1f);
            float strong = IntelligenceTickRules.Visibility(0.7f, 0.9f);
            Assert.Greater(weak, strong, "相手の防諜が高いほど見えない");
            // 値域 0..1
            Assert.GreaterOrEqual(strong, 0f);
            Assert.LessOrEqual(weak, 1f);
        }

        // --- SabotageSuccess（roll で決定論） ---

        [Test]
        public void SabotageSuccess_Deterministic_ByRoll()
        {
            // 成功率 = EspionageRules.MissionSuccessChance(cap, counter)。capability 0.8 / counter 0.0。
            float chance = EspionageRules.MissionSuccessChance(0.8f, 0f);
            Assert.IsTrue(IntelligenceTickRules.SabotageSuccess(0.8f, 0f, chance - 0.01f), "roll<成功率で成功");
            Assert.IsFalse(IntelligenceTickRules.SabotageSuccess(0.8f, 0f, chance + 0.01f), "roll≥成功率で失敗");
        }

        [Test]
        public void SabotageSuccess_HighCounter_HardensTarget()
        {
            // 防諜が高い相手には同じ roll でも工作が通りにくい（単調）
            float roll = 0.5f;
            bool vsWeak = IntelligenceTickRules.SabotageSuccess(0.6f, 0.1f, roll);
            bool vsStrong = IntelligenceTickRules.SabotageSuccess(0.6f, 0.9f, roll);
            Assert.IsTrue(vsWeak, "防諜の低い相手には成功");
            Assert.IsFalse(vsStrong, "防諜の高い相手には失敗");
            // 効果量は委譲（能力に比例・0..1）
            Assert.Greater(IntelligenceTickRules.SabotageEffect(0.8f), IntelligenceTickRules.SabotageEffect(0.2f));
        }

        // --- null 安全 ---

        [Test]
        public void TickYear_NullState_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => IntelligenceTickRules.TickYear(null, 1f, 1f));
        }
    }
}
