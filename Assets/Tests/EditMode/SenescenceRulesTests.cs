using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 名将の衰え（加齢曲線）を固定する：峠までは満額・峠超過で漸減（下限あり）、体力系は早く・
    /// 判断系は遅く衰える、自己評価と実態の乖離（衰えに気づかない）、峠直後の引き際の窓。
    /// 決定論・境界を担保。
    /// </summary>
    public class SenescenceRulesTests
    {
        private static readonly SenescenceParams P = SenescenceParams.Default;
        // 峠45/低下0.01/下限0.6/判断峠+10/判断比0.5/自己認識遅れ8/窓5

        [Test]
        public void AgeFactor_FullUntilPeak()
        {
            Assert.AreEqual(1f, SenescenceRules.AgeFactor(0f, P), 1e-5f);
            Assert.AreEqual(1f, SenescenceRules.AgeFactor(30f, P), 1e-5f);
            Assert.AreEqual(1f, SenescenceRules.AgeFactor(45f, P), 1e-5f);  // 峠ちょうど＝まだ満額
            Assert.AreEqual(1f, SenescenceRules.AgeFactor(-5f, P), 1e-5f);  // 負はクランプ
        }

        [Test]
        public void AgeFactor_DeclinesAfterPeak()
        {
            Assert.AreEqual(0.9f, SenescenceRules.AgeFactor(55f, P), 1e-5f); // 超過10×0.01=−0.1
            Assert.AreEqual(0.8f, SenescenceRules.AgeFactor(65f, P), 1e-5f); // 超過20＝−0.2
        }

        [Test]
        public void AgeFactor_FloorAtMin()
        {
            Assert.AreEqual(0.6f, SenescenceRules.AgeFactor(85f, P), 1e-5f);  // 超過40＝−0.4＝下限ちょうど
            Assert.AreEqual(0.6f, SenescenceRules.AgeFactor(120f, P), 1e-5f); // 下限で止まる
        }

        [Test]
        public void PhysicalVsJudgment_JudgmentDeclinesLater()
        {
            // 全盛期は両系統とも満額
            var prime = SenescenceRules.PhysicalVsJudgment(45f, P);
            Assert.AreEqual(1f, prime.physical, 1e-5f);
            Assert.AreEqual(1f, prime.judgment, 1e-5f);

            // 60歳：体力系は峠45から15年＝0.85、判断系は峠55から5年×0.005＝0.975
            var aged = SenescenceRules.PhysicalVsJudgment(60f, P);
            Assert.AreEqual(0.85f, aged.physical, 1e-5f);
            Assert.AreEqual(0.975f, aged.judgment, 1e-5f);

            // 老将でも判断系は体力系を下回らない（手は鈍っても眼は曇りにくい）
            var old = SenescenceRules.PhysicalVsJudgment(120f, P);
            Assert.AreEqual(0.6f, old.physical, 1e-5f);   // 体力系は下限
            Assert.AreEqual(0.675f, old.judgment, 1e-5f); // 判断系はまだ下限手前
            Assert.GreaterOrEqual(old.judgment, old.physical);
        }

        [Test]
        public void SelfAwarenessGap_PeaksMidDecline()
        {
            // 峠の前＝衰えていない＝乖離なし
            Assert.AreEqual(0f, SenescenceRules.SelfAwarenessGap(45f, P), 1e-5f);
            // 下り坂の入り口：本人は8年前（42歳＝満額）のつもり、実態0.95＝乖離0.05
            Assert.AreEqual(0.05f, SenescenceRules.SelfAwarenessGap(50f, P), 1e-5f);
            // 下り坂の途中で最大級：60歳の実態0.85、本人は52歳（0.93）のつもり＝乖離0.08
            Assert.AreEqual(0.08f, SenescenceRules.SelfAwarenessGap(60f, P), 1e-5f);
            // 下限に達した晩年：本人も実態も下限＝乖離は0へ収束（さすがに自覚する）
            Assert.AreEqual(0f, SenescenceRules.SelfAwarenessGap(130f, P), 1e-5f);
        }

        [Test]
        public void IsPastPrime_Boundary()
        {
            Assert.IsFalse(SenescenceRules.IsPastPrime(30f, P));
            Assert.IsFalse(SenescenceRules.IsPastPrime(45f, P)); // 峠ちょうど＝まだ全盛期
            Assert.IsTrue(SenescenceRules.IsPastPrime(46f, P));
        }

        [Test]
        public void GracefulExitWindow_OpensJustAfterPeak()
        {
            Assert.IsFalse(SenescenceRules.GracefulExitWindow(45f, P)); // 峠前は開かない
            Assert.IsTrue(SenescenceRules.GracefulExitWindow(46f, P));  // 峠直後＝窓
            Assert.IsTrue(SenescenceRules.GracefulExitWindow(50f, P));  // 窓の終わり（45+5）ちょうど
            Assert.IsFalse(SenescenceRules.GracefulExitWindow(51f, P)); // 逃した＝衰えた名将として残る
        }
    }
}
