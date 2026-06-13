using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>士気波及（panic propagation）：事象別の衝撃量・距離減衰・合成。</summary>
    public class MoraleShockRulesTests
    {
        [Test]
        public void Magnitude_PerEvent()
        {
            Assert.AreEqual(MoraleShockRules.Mag旗艦撃墜, MoraleShockRules.Magnitude(MoraleEvent.旗艦撃墜), 1e-4f);
            Assert.AreEqual(MoraleShockRules.Mag敗走, MoraleShockRules.Magnitude(MoraleEvent.敗走), 1e-4f);
            Assert.AreEqual(MoraleShockRules.Mag捨てがまり成功, MoraleShockRules.Magnitude(MoraleEvent.捨てがまり成功), 1e-4f);
            // 撃墜が最も大きい衝撃。
            Assert.Greater(MoraleShockRules.Magnitude(MoraleEvent.旗艦撃墜), MoraleShockRules.Magnitude(MoraleEvent.敗走));
        }

        [Test]
        public void Falloff_LinearWithinRadiusZeroBeyond()
        {
            Assert.AreEqual(1f, MoraleShockRules.Falloff(0f, 16f), 1e-4f);     // 中心＝最大
            Assert.AreEqual(0.5f, MoraleShockRules.Falloff(8f, 16f), 1e-4f);   // 半分
            Assert.AreEqual(0f, MoraleShockRules.Falloff(16f, 16f), 1e-4f);    // 端＝0
            Assert.AreEqual(0f, MoraleShockRules.Falloff(20f, 16f), 1e-4f);    // 外＝0
            Assert.AreEqual(1f, MoraleShockRules.Falloff(5f, 0f), 1e-4f);      // 半径0は中心扱い
        }

        [Test]
        public void ShockAt_IsMagnitudeTimesFalloff()
        {
            Assert.AreEqual(MoraleShockRules.Mag旗艦撃墜 * 0.5f,
                MoraleShockRules.ShockAt(MoraleEvent.旗艦撃墜, 8f, 16f), 1e-4f);
            Assert.AreEqual(0f, MoraleShockRules.ShockAt(MoraleEvent.敗走, 100f, 16f), 1e-4f);
        }
    }
}
