using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 戦後復興を固定する：投資が荒廃を削る（投資ゼロでも微小な自然回復）・放置の積み上がりと
    /// 固定化（回復不能＝Tick据え置き・所要時間無限大）・復興需要ブーストは荒廃が深いほど限界効用大・
    /// 産出倍率＝1−荒廃・完全回復所要時間は浅いうちほど短い。クランプと無限大ケースを担保。
    /// </summary>
    public class ReconstructionRulesTests
    {
        private static readonly ReconstructionParams P = ReconstructionParams.Default; // 自然0.01/投資係数0.1/固定化50/ブースト0.5/最小投資0.05

        [Test]
        public void RecoveryTick_InvestmentSpeedsRecovery()
        {
            // 投資0.5 → 速度 0.01+0.5×0.1=0.06：荒廃0.5 → 0.44
            Assert.AreEqual(0.44f, ReconstructionRules.RecoveryTick(0.5f, 0.5f, 1f, P), 1e-4f);
            // 投資ゼロでも自然回復 0.01×10dt＝0.1：荒廃0.5 → 0.4
            Assert.AreEqual(0.4f, ReconstructionRules.RecoveryTick(0.5f, 0f, 10f, P), 1e-4f);
            // 下限0でクランプ（回復しすぎない）
            Assert.AreEqual(0f, ReconstructionRules.RecoveryTick(0.05f, 1f, 1f, P), 1e-4f);
        }

        [Test]
        public void RecoveryTick_OssifiedNeverRecovers()
        {
            // 固定化後は投資しても自然にも回復しない＝据え置き
            Assert.AreEqual(0.5f, ReconstructionRules.RecoveryTick(0.5f, 1f, 100f, true, P), 1e-5f);
        }

        [Test]
        public void OssificationTick_NeglectAccumulates_InvestmentResets()
        {
            // 放置（投資なし）は積み上がる
            Assert.AreEqual(15f, ReconstructionRules.OssificationTick(10f, 0f, 5f, P), 1e-4f);
            // 最小投資未満（0.04 < 0.05）は放置扱い
            Assert.AreEqual(15f, ReconstructionRules.OssificationTick(10f, 0.04f, 5f, P), 1e-4f);
            // 手を付ければリセット
            Assert.AreEqual(0f, ReconstructionRules.OssificationTick(10f, 0.5f, 5f, P), 1e-5f);
        }

        [Test]
        public void IsOssified_ThresholdBoundary()
        {
            Assert.IsTrue(ReconstructionRules.IsOssified(50f, P));   // 閾値ちょうどで固定化
            Assert.IsFalse(ReconstructionRules.IsOssified(49.9f, P));
        }

        [Test]
        public void ReconstructionBoom_DeeperDevastationBiggerMarginalUtility()
        {
            Assert.AreEqual(1.5f, ReconstructionRules.ReconstructionBoom(1f, 1f, P), 1e-4f);
            Assert.AreEqual(1.1f, ReconstructionRules.ReconstructionBoom(1f, 0.2f, P), 1e-4f);
            Assert.AreEqual(1f, ReconstructionRules.ReconstructionBoom(0f, 1f, P), 1e-5f); // 投資なし＝ブーストなし
            // 同じ投資1.0 の限界効用：深い荒廃0.8（+0.4）＞浅い荒廃0.2（+0.1）
            float deep = ReconstructionRules.ReconstructionBoom(1f, 0.8f, P) - ReconstructionRules.ReconstructionBoom(0f, 0.8f, P);
            float shallow = ReconstructionRules.ReconstructionBoom(1f, 0.2f, P) - ReconstructionRules.ReconstructionBoom(0f, 0.2f, P);
            Assert.Greater(deep, shallow);
        }

        [Test]
        public void OutputFactor_OneMinusDevastation_Clamped()
        {
            Assert.AreEqual(0.7f, ReconstructionRules.OutputFactor(0.3f), 1e-4f);
            Assert.AreEqual(0f, ReconstructionRules.OutputFactor(1.5f), 1e-5f);  // 過大入力はクランプ
            Assert.AreEqual(1f, ReconstructionRules.OutputFactor(-0.2f), 1e-5f); // 負もクランプ
        }

        [Test]
        public void TimeToRecover_EarlyIsCheap_OssifiedIsForever()
        {
            // 投資0.5 → 速度0.06：荒廃0.5 で 8.3333時間
            Assert.AreEqual(0.5f / 0.06f, ReconstructionRules.TimeToRecover(0.5f, 0.5f, P), 1e-3f);
            // 早く手を付けるほど安い：浅い荒廃0.2 ＜ 深い荒廃0.8
            Assert.Less(ReconstructionRules.TimeToRecover(0.2f, 0.5f, P), ReconstructionRules.TimeToRecover(0.8f, 0.5f, P));
            // 投資ゼロは自然回復のみ＝0.5/0.01=50時間
            Assert.AreEqual(50f, ReconstructionRules.TimeToRecover(0.5f, 0f, P), 1e-3f);
            Assert.AreEqual(0f, ReconstructionRules.TimeToRecover(0f, 1f, P), 1e-5f); // 無傷＝0
            // 固定化済みは永遠に戻らない
            Assert.IsTrue(float.IsPositiveInfinity(ReconstructionRules.TimeToRecover(0.5f, 1f, true, P)));
            // 自然回復ゼロの世界で投資ゼロも永遠
            var noNature = new ReconstructionParams(0f, 0.1f, 50f, 0.5f, 0.05f);
            Assert.IsTrue(float.IsPositiveInfinity(ReconstructionRules.TimeToRecover(0.5f, 0f, noNature)));
        }
    }
}
