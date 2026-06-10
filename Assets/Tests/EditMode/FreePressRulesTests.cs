using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 報道の自由を固定する：自由は腐敗を早期発見し都度排出、統制下は見えない腐敗が全量蓄積、政権の恥と
    /// 体制の健全配当、検閲の爆発はチェルノブイリ型相乗、資本集中の籠絡（検閲なき統制）。境界を担保。
    /// </summary>
    public class FreePressRulesTests
    {
        private static readonly FreePressParams P = FreePressParams.Default;
        // 発見上限0.8/排出1/恥1/配当0.05/爆発単価0.1/相乗0.25/籠絡1

        [Test]
        public void CorruptionDetectionRate_ScalesWithFreedom()
        {
            Assert.AreEqual(0.8f, FreePressRules.CorruptionDetectionRate(1f, P), 1e-5f);
            Assert.AreEqual(0f, FreePressRules.CorruptionDetectionRate(0f, P), 1e-5f); // 統制下は露見しない
            Assert.AreEqual(0.4f, FreePressRules.CorruptionDetectionRate(0.5f, P), 1e-5f);
        }

        [Test]
        public void HiddenCorruptionTick_AccumulatesUnderControl_PurgesWhenFree()
        {
            // 統制下（自由0）＝流入が全量溜まる
            Assert.AreEqual(10f, FreePressRules.HiddenCorruptionTick(0f, 10f, 0f, 1f, P), 1e-4f);
            // 自由（0.8発見）＝低位でしか溜まらない
            Assert.AreEqual(2f, FreePressRules.HiddenCorruptionTick(0f, 10f, 1f, 1f, P), 1e-4f);
            // 既存ストックは自由下で排出される：100→100−80=20
            Assert.AreEqual(20f, FreePressRules.HiddenCorruptionTick(100f, 0f, 1f, 1f, P), 1e-4f);
        }

        [Test]
        public void RegimeEmbarrassment_PainfulToRegime()
        {
            Assert.AreEqual(1f, FreePressRules.RegimeEmbarrassment(1f, 1f, P), 1e-5f);
            Assert.AreEqual(0f, FreePressRules.RegimeEmbarrassment(0f, 1f, P), 1e-5f); // 統制下は恥ゼロ（静穏）
            Assert.AreEqual(0.5f, FreePressRules.RegimeEmbarrassment(1f, 0.5f, P), 1e-5f);
        }

        [Test]
        public void SystemicHealthDividend_FriendToTheSystem()
        {
            Assert.AreEqual(0.05f, FreePressRules.SystemicHealthDividend(1f, 1f, P), 1e-5f);
            Assert.AreEqual(0f, FreePressRules.SystemicHealthDividend(0f, 1f, P), 1e-5f);
        }

        [Test]
        public void CensorshipBlowback_ChernobylCompounding()
        {
            Assert.AreEqual(0.1f, FreePressRules.CensorshipBlowback(1, 1f, P), 1e-5f);
            // 10件同時露見＝1+0.25×9=3.25倍＝3.25（個別合算1.0を超える）
            Assert.AreEqual(3.25f, FreePressRules.CensorshipBlowback(10, 1f, P), 1e-4f);
            Assert.Greater(FreePressRules.CensorshipBlowback(10, 1f, P), 10 * 0.1f); // 隠蔽の総決算
            Assert.AreEqual(0f, FreePressRules.CensorshipBlowback(0, 1f, P), 1e-5f);
        }

        [Test]
        public void PressCapture_AndEffectiveFreedom()
        {
            // 資本集中の籠絡は二乗＝分散はほぼ無害・独占で急増
            Assert.AreEqual(0.25f, FreePressRules.PressCaptureRisk(0.5f, P), 1e-5f);
            Assert.AreEqual(1f, FreePressRules.PressCaptureRisk(1f, P), 1e-5f);
            // 法的自由でも資本独占なら実効自由ゼロ＝検閲なき統制
            Assert.AreEqual(1f, FreePressRules.EffectiveFreedom(1f, 0f, P), 1e-5f);
            Assert.AreEqual(0f, FreePressRules.EffectiveFreedom(1f, 1f, P), 1e-5f);
            Assert.AreEqual(0.75f, FreePressRules.EffectiveFreedom(1f, 0.5f, P), 1e-5f);
        }
    }
}
