using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 技術伝播を固定する：漏出速度は格差×接触に比例し機密が絞る（が止め切れない＝独占は時限）、
    /// 後発は差に比例して追い上げ（後発者利益・先進は超えない）、独占余命は接触ゼロのみ無限大、
    /// 模倣は自前研究より安く、格差が大きいほどリープフロッグしやすい。境界・クランプを担保。
    /// </summary>
    public class InnovationDiffusionRulesTests
    {
        private static readonly DiffusionParams P = DiffusionParams.Default;
        // 基準漏出0.1/交易重み0.6/諜報重み0.4/機密最大遮断0.8/模倣コスト0.5/跳躍スケール0.5

        [Test]
        public void DiffusionRate_ScalesWithGapAndContact()
        {
            // 格差1・交易1・諜報1・機密0：接触=clamp01(0.6+0.4)=1 ⇒ 0.1×1×1×1=0.1
            Assert.AreEqual(0.1f, InnovationDiffusionRules.DiffusionRate(1f, 1f, 1f, 0f, P), 1e-5f);
            // 格差0.5・交易0.5のみ：接触=0.6×0.5=0.3 ⇒ 0.1×0.5×0.3=0.015
            Assert.AreEqual(0.015f, InnovationDiffusionRules.DiffusionRate(0.5f, 0.5f, 0f, 0f, P), 1e-5f);
            // 格差ゼロ／接触ゼロは漏れない
            Assert.AreEqual(0f, InnovationDiffusionRules.DiffusionRate(0f, 1f, 1f, 0f, P), 1e-5f);
            Assert.AreEqual(0f, InnovationDiffusionRules.DiffusionRate(1f, 0f, 0f, 0f, P), 1e-5f);
        }

        [Test]
        public void DiffusionRate_SecrecyThrottlesButNeverStops()
        {
            // 機密全力でも最大遮断0.8 ⇒ 0.1×(1−0.8)=0.02＝必ず漏れる（独占は時限）
            float rate = InnovationDiffusionRules.DiffusionRate(1f, 1f, 1f, 1f, P);
            Assert.AreEqual(0.02f, rate, 1e-5f);
            Assert.Greater(rate, 0f);
            // ctor も遮断1.0を許さない（上限0.95にクランプ）⇒ 0.1×0.05=0.005
            var maxSecrecy = new DiffusionParams(0.1f, 0.6f, 0.4f, 1.5f, 0.5f, 0.5f);
            Assert.AreEqual(0.005f, InnovationDiffusionRules.DiffusionRate(1f, 1f, 1f, 1f, maxSecrecy), 1e-5f);
        }

        [Test]
        public void DiffusionRate_ClampsWildInputs()
        {
            // 範囲外入力（格差2・交易5・諜報5・機密−1）は 1,1,1,0 と同じ
            Assert.AreEqual(0.1f, InnovationDiffusionRules.DiffusionRate(2f, 5f, 5f, -1f, P), 1e-5f);
        }

        [Test]
        public void CatchUpTick_LatecomerAdvantage()
        {
            // 差0.6×rate0.1×dt1=+0.06 ⇒ 0.26
            Assert.AreEqual(0.26f, InnovationDiffusionRules.CatchUpTick(0.2f, 0.8f, 0.1f, 1f), 1e-5f);
            // 差が大きいほど一歩が大きい＝後発者利益
            float farGain = InnovationDiffusionRules.CatchUpTick(0.1f, 0.9f, 0.1f, 1f) - 0.1f;
            float nearGain = InnovationDiffusionRules.CatchUpTick(0.7f, 0.9f, 0.1f, 1f) - 0.7f;
            Assert.Greater(farGain, nearGain);
        }

        [Test]
        public void CatchUpTick_NeverOvershootsLeader()
        {
            // 大きな dt でも先進水準でクランプ＝伝播では追い越せない
            Assert.AreEqual(0.8f, InnovationDiffusionRules.CatchUpTick(0.2f, 0.8f, 1f, 100f), 1e-5f);
            // すでに並走・先行なら変化しない（流入元が無い）
            Assert.AreEqual(0.9f, InnovationDiffusionRules.CatchUpTick(0.9f, 0.5f, 1f, 1f), 1e-5f);
        }

        [Test]
        public void MonopolyDuration_FiniteUnlessNoContact()
        {
            // 機密0・接触1：1/0.1=10
            Assert.AreEqual(10f, InnovationDiffusionRules.MonopolyDuration(0f, 1f, P), 1e-4f);
            // 機密全力でも有限：1/0.02=50＝独占は延びるが永続しない
            Assert.AreEqual(50f, InnovationDiffusionRules.MonopolyDuration(1f, 1f, P), 1e-4f);
            // 接触ゼロのみ永久独占＝無限大
            Assert.IsTrue(float.IsPositiveInfinity(InnovationDiffusionRules.MonopolyDuration(0f, 0f, P)));
            // 漏出速度ゼロの世界（baseLeakRate=0）も無限大
            var noLeak = new DiffusionParams(0f, 0.6f, 0.4f, 0.8f, 0.5f, 0.5f);
            Assert.IsTrue(float.IsPositiveInfinity(InnovationDiffusionRules.MonopolyDuration(0f, 1f, noLeak)));
        }

        [Test]
        public void ImitationDiscount_CheaperThanOwnResearch()
        {
            // 既定0.5＝自前研究の半額
            Assert.AreEqual(0.5f, InnovationDiffusionRules.ImitationDiscount(), 1e-5f);
            Assert.Less(InnovationDiffusionRules.ImitationDiscount(P), 1f);
            // ctor は 0..1 へクランプ（2.0→1.0＝割引なし止まり）
            var noDiscount = new DiffusionParams(0.1f, 0.6f, 0.4f, 0.8f, 2f, 0.5f);
            Assert.AreEqual(1f, InnovationDiffusionRules.ImitationDiscount(noDiscount), 1e-5f);
        }

        [Test]
        public void LeapfrogPotential_GrowsWithGap()
        {
            // 最大格差（0 vs 1）＝1×0.5=0.5
            Assert.AreEqual(0.5f, InnovationDiffusionRules.LeapfrogPotential(0f, 1f, P), 1e-5f);
            // 小さい格差（0.6 vs 0.8）＝0.2×0.5=0.1
            Assert.AreEqual(0.1f, InnovationDiffusionRules.LeapfrogPotential(0.6f, 0.8f, P), 1e-5f);
            // 並走・先行は跳べない
            Assert.AreEqual(0f, InnovationDiffusionRules.LeapfrogPotential(1f, 1f, P), 1e-5f);
            Assert.AreEqual(0f, InnovationDiffusionRules.LeapfrogPotential(0.8f, 0.5f, P), 1e-5f);
        }
    }
}
