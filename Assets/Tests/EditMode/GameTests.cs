using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>ゲーム会社（#2025・<see cref="GameRules"/>）：課金収益(GAME-1)・ヒット期待値(GAME-2)・ライブ減衰(GAME-3)・LTV(GAME-4)。</summary>
    public class GameTests
    {
        [Test]
        public void InApp_AndExpectedValue()
        {
            Assert.AreEqual(25000f, GameRules.InAppRevenue(1000f, 0.05f, 500f), 1e-1f); // 1000×0.05×500
            Assert.AreEqual(5000f, GameRules.ExpectedGameValue(0.1f, 100000f, 5000f), 1e-1f); // 0.1×10万−5千
        }

        [Test]
        public void LiveService_AndLtv()
        {
            // 1000×0.9^6=531.4 > 下限300 ⇒ 531.4
            Assert.AreEqual(531.441f, GameRules.LiveServiceRevenue(1000f, 6, 0.1f, 0.3f), 1e-2f);
            // 1000×0.9^12=282.4 < 下限300 ⇒ 300で底打ち
            Assert.AreEqual(300f, GameRules.LiveServiceRevenue(1000f, 12, 0.1f, 0.3f), 1e-1f);
            Assert.AreEqual(6000f, GameRules.LifetimeValue(500f, 12f), 1e-1f);
        }
    }
}
