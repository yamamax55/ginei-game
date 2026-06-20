using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 禁輸効果を固定する：打撃＝maxBite×貿易支配率×(1−自給度)。
    /// 貿易支配率が高いほど打撃大、自給度が高いほど打撃小、自給度=1で打撃ゼロ。
    /// 打撃は maxBite で頭打ち、反作用は打撃に比例、正味圧力＝打撃−反作用。クランプを担保。
    /// </summary>
    public class EmbargoEffectRulesTests
    {
        private static readonly EmbargoEffectRules.Params P = EmbargoEffectRules.Params.Default;
        // 最大打撃0.6/反作用割合0.3

        [Test]
        public void EconomicBite_IsDeterministic()
        {
            float a = EmbargoEffectRules.EconomicBite(0.5f, 0.2f, P);
            float b = EmbargoEffectRules.EconomicBite(0.5f, 0.2f, P);
            Assert.AreEqual(a, b, 1e-5f);
            Assert.AreEqual(0.6f * 0.5f * 0.8f, a, 1e-5f); // 0.24
        }

        [Test]
        public void EconomicBite_MoreTradeShareMoreBite()
        {
            float low = EmbargoEffectRules.EconomicBite(0.2f, 0.3f, P);
            float high = EmbargoEffectRules.EconomicBite(0.8f, 0.3f, P);
            Assert.Less(low, high); // 貿易支配率が高いほど打撃大
        }

        [Test]
        public void EconomicBite_HigherSelfSufficiencyLessBite()
        {
            float low = EmbargoEffectRules.EconomicBite(0.7f, 0.2f, P);
            float high = EmbargoEffectRules.EconomicBite(0.7f, 0.8f, P);
            Assert.Greater(low, high); // 自給度が高いほど打撃小
        }

        [Test]
        public void EconomicBite_FullSelfSufficiencyIsZero()
        {
            Assert.AreEqual(0f, EmbargoEffectRules.EconomicBite(1f, 1f, P), 1e-5f); // 完全自給なら効かない
        }

        [Test]
        public void EconomicBite_ClampedToMaxBite()
        {
            // 入力が範囲外でもクランプされ、上限を超えない
            float v = EmbargoEffectRules.EconomicBite(5f, -1f, P);
            Assert.AreEqual(P.maxBite, v, 1e-5f);
        }

        [Test]
        public void EconomicBite_DefaultOverloadMatches()
        {
            Assert.AreEqual(
                EmbargoEffectRules.EconomicBite(0.5f, 0.2f, P),
                EmbargoEffectRules.EconomicBite(0.5f, 0.2f),
                1e-5f);
        }

        [Test]
        public void Blowback_ProportionalToBite()
        {
            Assert.AreEqual(0.3f * 0.4f, EmbargoEffectRules.Blowback(0.4f, P), 1e-5f);
            Assert.AreEqual(0.3f * 0.2f, EmbargoEffectRules.Blowback(0.2f, P), 1e-5f);
            Assert.AreEqual(0f, EmbargoEffectRules.Blowback(-1f, P), 1e-5f); // 負の打撃はゼロへ
        }

        [Test]
        public void NetPressure_EqualsBiteMinusBlowback()
        {
            float bite = EmbargoEffectRules.EconomicBite(0.6f, 0.1f, P);
            float expected = bite - EmbargoEffectRules.Blowback(bite, P);
            Assert.AreEqual(expected, EmbargoEffectRules.NetPressure(0.6f, 0.1f, P), 1e-5f);
            Assert.AreEqual(expected, EmbargoEffectRules.NetPressure(0.6f, 0.1f), 1e-5f); // 既定オーバーロード一致
        }
    }
}
