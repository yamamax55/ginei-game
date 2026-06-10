using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 脱走を固定する：基礎率＋低士気上乗せ＋長期摩耗、補給切れで倍率、tick の脱走者数（兵力超なし）、
    /// 出血判定。境界を担保。
    /// </summary>
    public class DesertionRulesTests
    {
        private static readonly DesertionParams P = DesertionParams.Default;
        // 基礎0.001/低士気0.01/補給切れ×3/摩耗開始30/摩耗0.0002/出血閾値0.01

        [Test]
        public void DesertionRate_BaseOnly_WhenHealthy()
        {
            // 士気満タン・補給良好・短期＝基礎のみ
            Assert.AreEqual(0.001f, DesertionRules.DesertionRate(1f, true, 0f, P), 1e-6f);
        }

        [Test]
        public void DesertionRate_LowMoraleAdds()
        {
            // 士気ゼロ＝基礎+0.01
            Assert.AreEqual(0.011f, DesertionRules.DesertionRate(0f, true, 0f, P), 1e-6f);
            Assert.AreEqual(0.006f, DesertionRules.DesertionRate(0.5f, true, 0f, P), 1e-6f);
        }

        [Test]
        public void DesertionRate_FatigueAfterOnset()
        {
            // 30以前は摩耗なし
            Assert.AreEqual(0.001f, DesertionRules.DesertionRate(1f, true, 30f, P), 1e-6f);
            // 80日＝超過50×0.0002=+0.01
            Assert.AreEqual(0.011f, DesertionRules.DesertionRate(1f, true, 80f, P), 1e-6f);
        }

        [Test]
        public void DesertionRate_NoSupplyMultiplies()
        {
            // 士気ゼロ＋補給切れ＝0.011×3
            Assert.AreEqual(0.033f, DesertionRules.DesertionRate(0f, false, 0f, P), 1e-6f);
        }

        [Test]
        public void DesertersTick_ProportionalAndBounded()
        {
            // 健康な軍1万・dt1＝10人が消える
            Assert.AreEqual(10f, DesertionRules.DesertersTick(10000f, 1f, true, 0f, 1f, P), 1e-3f);
            // 兵力以上は消えない
            Assert.AreEqual(5f, DesertionRules.DesertersTick(5f, 0f, false, 999f, 1000f, P), 1e-4f);
        }

        [Test]
        public void IsHemorrhaging_AtThreshold()
        {
            // 士気ゼロ（0.011）＝出血
            Assert.IsTrue(DesertionRules.IsHemorrhaging(0f, true, 0f, P));
            // 健康（0.001）＝正常な漏れ
            Assert.IsFalse(DesertionRules.IsHemorrhaging(1f, true, 0f, P));
        }
    }
}
