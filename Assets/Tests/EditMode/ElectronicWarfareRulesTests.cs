using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 電子戦を固定する：実効妨害＝出力−ECCM（相殺で0）、命中・探知の低下倍率、発信源の被逆探知
    /// （ECCMでは消えない）、焚く価値の損益判定。境界を担保。
    /// </summary>
    public class ElectronicWarfareRulesTests
    {
        private static readonly EwParams P = EwParams.Default;
        // 命中低下0.5/探知低下0.7/逆探知0.8

        [Test]
        public void EffectiveJamming_OffsetByEccm()
        {
            Assert.AreEqual(1f, ElectronicWarfareRules.EffectiveJamming(1f, 0f), 1e-5f);
            Assert.AreEqual(0.5f, ElectronicWarfareRules.EffectiveJamming(1f, 0.5f), 1e-5f);
            Assert.AreEqual(0f, ElectronicWarfareRules.EffectiveJamming(0.5f, 0.5f), 1e-5f);  // 完全相殺
            Assert.AreEqual(0f, ElectronicWarfareRules.EffectiveJamming(0.3f, 0.8f), 1e-5f);  // ECCM優勢＝無効
        }

        [Test]
        public void AccuracyFactor_DegradedByJamming()
        {
            Assert.AreEqual(0.5f, ElectronicWarfareRules.AccuracyFactor(1f, 0f, P), 1e-5f);  // 最大半減
            Assert.AreEqual(1f, ElectronicWarfareRules.AccuracyFactor(0f, 0f, P), 1e-5f);    // 妨害なし
            Assert.AreEqual(0.75f, ElectronicWarfareRules.AccuracyFactor(1f, 0.5f, P), 1e-5f);
        }

        [Test]
        public void DetectionFactor_DegradedMore()
        {
            Assert.AreEqual(0.3f, ElectronicWarfareRules.DetectionFactor(1f, 0f, P), 1e-5f); // 探知は最大70%低下
            Assert.AreEqual(1f, ElectronicWarfareRules.DetectionFactor(0f, 0f, P), 1e-5f);
        }

        [Test]
        public void EmitterExposure_EccmCannotHideEmission()
        {
            Assert.AreEqual(0.8f, ElectronicWarfareRules.EmitterExposure(1f, P), 1e-5f);
            Assert.AreEqual(0.4f, ElectronicWarfareRules.EmitterExposure(0.5f, P), 1e-5f);
            Assert.AreEqual(0f, ElectronicWarfareRules.EmitterExposure(0f, P), 1e-5f); // 沈黙＝隠密
        }

        [Test]
        public void WorthJamming_GainVsExposureCost()
        {
            // 敵に対輻射打撃なし（threat=0）＝焚き得
            Assert.IsTrue(ElectronicWarfareRules.WorthJamming(1f, 0f, 0f, P));
            // 強い脅威＋ECCMで相殺された妨害＝焚くだけ損（gain0 < cost）
            Assert.IsFalse(ElectronicWarfareRules.WorthJamming(0.5f, 0.5f, 1f, P));
            // 脅威があっても利得が上回れば焚く：gain1 > cost0.8×1
            Assert.IsTrue(ElectronicWarfareRules.WorthJamming(1f, 0f, 1f, P));
        }
    }
}
