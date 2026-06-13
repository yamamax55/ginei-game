using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 占領地レジスタンスを固定する：強度＝未統合×上限（亡命支援で増幅・上限クランプ）、破壊被害と情報漏れ、
    /// 弾圧（速いが怨恨で統合後退）vs 懐柔（遅いが根治）のジレンマ。境界を担保。
    /// </summary>
    public class ResistanceRulesTests
    {
        private static readonly ResistanceParams P = ResistanceParams.Default;
        // 上限0.8/破壊0.3/漏れ0.5/弾圧0.2/怨恨0.05/懐柔0.05

        [Test]
        public void Intensity_FromUnintegratedGround()
        {
            // 占領直後（統合0）＝上限0.8
            Assert.AreEqual(0.8f, ResistanceRules.Intensity(0f, 0f, P), 1e-5f);
            // 半統合＝0.4
            Assert.AreEqual(0.4f, ResistanceRules.Intensity(0.5f, 0f, P), 1e-5f);
            // 完全統合＝土壌なし
            Assert.AreEqual(0f, ResistanceRules.Intensity(1f, 0f, P), 1e-5f);
        }

        [Test]
        public void Intensity_ExileSupportBoosts_ButCapped()
        {
            // 半統合0.4 に亡命支援1.0＝0.8（倍）
            Assert.AreEqual(0.8f, ResistanceRules.Intensity(0.5f, 1f, P), 1e-5f);
            // 統合0＋支援＝上限0.8 でクランプ
            Assert.AreEqual(0.8f, ResistanceRules.Intensity(0f, 1f, P), 1e-5f);
            // 完全統合なら支援があっても呼応しない
            Assert.AreEqual(0f, ResistanceRules.Intensity(1f, 1f, P), 1e-5f);
        }

        [Test]
        public void SabotageAndIntelLeak_ScaleWithIntensity()
        {
            Assert.AreEqual(0.24f, ResistanceRules.SabotageLoss(0.8f, P), 1e-5f); // 0.8×0.3
            Assert.AreEqual(0.4f, ResistanceRules.IntelLeak(0.8f, P), 1e-5f);     // 0.8×0.5
            Assert.AreEqual(0f, ResistanceRules.SabotageLoss(0f, P), 1e-5f);
        }

        [Test]
        public void CrackdownTick_FastButResented()
        {
            // 弾圧は速い：0.8→0.6（0.2×1×1）
            Assert.AreEqual(0.6f, ResistanceRules.CrackdownTick(0.8f, 1f, 1f, P), 1e-5f);
            // 怨恨＝統合が0.05 後退する分を呼び出し側が引く
            Assert.AreEqual(0.05f, ResistanceRules.CrackdownResentment(1f, 1f, P), 1e-5f);
            // 弾圧しなければ怨恨もなし
            Assert.AreEqual(0f, ResistanceRules.CrackdownResentment(0f, 1f, P), 1e-5f);
        }

        [Test]
        public void ConciliationTick_SlowButCures()
        {
            // 懐柔は統合を進める：0.5→0.55
            Assert.AreEqual(0.55f, ResistanceRules.ConciliationTick(0.5f, 1f, 1f, P), 1e-5f);
            Assert.AreEqual(1f, ResistanceRules.ConciliationTick(0.99f, 1f, 1f, P), 1e-5f); // 上限1
            // 統合が進めば強度の土壌が痩せる（根治の確認）
            float before = ResistanceRules.Intensity(0.5f, 0f, P);
            float after = ResistanceRules.Intensity(ResistanceRules.ConciliationTick(0.5f, 1f, 1f, P), 0f, P);
            Assert.Less(after, before);
        }
    }
}
