using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>CDR-4 武功の物語化：連勝・通算戦歴・武名から動的に異名を解決。</summary>
    public class EpithetRulesTests
    {
        [Test]
        public void ResolveEpithet_ByDeeds()
        {
            Assert.AreEqual("不敗", EpithetRules.ResolveEpithet(10, 0, 0));   // 無敗かつ10勝
            Assert.AreEqual("常勝", EpithetRules.ResolveEpithet(25, 3, 50));  // 敗あり・通算20勝以上
            Assert.AreEqual("歴戦", EpithetRules.ResolveEpithet(5, 2, 92));   // 高武名
            Assert.AreEqual("", EpithetRules.ResolveEpithet(2, 1, 10));       // 該当なし
        }

        [Test]
        public void HasEarnedEpithet()
        {
            Assert.IsTrue(EpithetRules.HasEarnedEpithet(10, 0, 0));
            Assert.IsFalse(EpithetRules.HasEarnedEpithet(2, 1, 10));
        }
    }
}
