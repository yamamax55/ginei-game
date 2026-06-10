using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 中間体バッファ（#1116）を固定する：貯蔵不能（溶融物・気体・生鮮）は緩衝在庫を持てず容量ゼロ・
    /// バッファが厚いほど上流変動を吸収し薄いほどそのまま下流へ伝える（バッファ無し=増幅1.0）・
    /// 貯蔵不能は上下流を秒単位で強制同期（許容0秒）・変動大×バッファ薄ほど投資が効く。
    /// 既定Params（syncSeconds=2/minCoupling=0.05/maxInvestmentValue=1.0）の具体値で期待値を固定。
    /// </summary>
    public class IntermediateBufferRulesTests
    {
        private const float Eps = 1e-4f;

        [Test]
        public void BufferCapacity_UnstorableHasZeroCapacity()
        {
            // 貯蔵不能（storability=0）はタンクをいくら持っても容量ゼロ＝バッファを持てない
            Assert.AreEqual(0f, IntermediateBufferRules.BufferCapacity(0f, 1f), Eps);
            // 貯蔵容易×大タンク＝満杯
            Assert.AreEqual(1f, IntermediateBufferRules.BufferCapacity(1f, 1f), Eps);
            // 半貯蔵×半タンク＝0.25
            Assert.AreEqual(0.25f, IntermediateBufferRules.BufferCapacity(0.5f, 0.5f), Eps);
        }

        [Test]
        public void ShockAmplification_NoBufferPassesThroughFully()
        {
            // バッファ無し＝上流変動0.8がそのまま下流へ（増幅1.0＝0.8のまま）
            Assert.AreEqual(0.8f, IntermediateBufferRules.ShockAmplification(0.8f, 0f), Eps);
            // バッファ満杯＝変動を吸収して0
            Assert.AreEqual(0f, IntermediateBufferRules.ShockAmplification(0.8f, 1f), Eps);
            // バッファ半分＝半分吸収（0.8×0.5=0.4）
            Assert.AreEqual(0.4f, IntermediateBufferRules.ShockAmplification(0.8f, 0.5f), Eps);
        }

        [Test]
        public void CouplingTightness_NoBufferIsTightlyCoupled()
        {
            // バッファ無し＝密結合（1.0）＝1工程の揺れが全体へ波及
            Assert.AreEqual(1f, IntermediateBufferRules.CouplingTightness(0f), Eps);
            // 満杯でも minCoupling=0.05 は残る（中間体は完全には切り離せない）
            Assert.AreEqual(0.05f, IntermediateBufferRules.CouplingTightness(1f), Eps);
            // 半分＝Lerp(1,0.05,0.5)=0.525
            Assert.AreEqual(0.525f, IntermediateBufferRules.CouplingTightness(0.5f), Eps);
        }

        [Test]
        public void ForcedSynchronization_UnstorableForcesInstantSync()
        {
            // 貯蔵不能（storability=0）＝許容0秒＝完全同期（上流停止で下流即停止）
            Assert.AreEqual(0f, IntermediateBufferRules.ForcedSynchronization(0f), Eps);
            // 貯蔵容易＝フルの余裕（syncSeconds=2）
            Assert.AreEqual(2f, IntermediateBufferRules.ForcedSynchronization(1f), Eps);
            // 半貯蔵＝1秒
            Assert.AreEqual(1f, IntermediateBufferRules.ForcedSynchronization(0.5f), Eps);
        }

        [Test]
        public void BufferInvestmentValue_HighVarianceThinBufferPaysOff()
        {
            // 変動大（0.9）×バッファ薄（0.1）＝投資が最も効く：1.0×0.9×0.9=0.81
            Assert.AreEqual(0.81f, IntermediateBufferRules.BufferInvestmentValue(0.9f, 0.1f), Eps);
            // 既に満杯なら投資価値0
            Assert.AreEqual(0f, IntermediateBufferRules.BufferInvestmentValue(0.9f, 1f), Eps);
            // 変動が無ければ投資価値0
            Assert.AreEqual(0f, IntermediateBufferRules.BufferInvestmentValue(0f, 0.1f), Eps);
        }

        [Test]
        public void Pipeline_StorabilityFlowsToCapacityAndAmplification()
        {
            // 貯蔵不能な中間体：storability=0→容量0→上流変動0.7をそのまま増幅して下流へ＝0.7
            float storability = IntermediateBufferRules.Storability(0f);
            float cap = IntermediateBufferRules.BufferCapacity(storability, 1f);
            Assert.AreEqual(0f, cap, Eps);
            Assert.AreEqual(0.7f, IntermediateBufferRules.ShockAmplification(0.7f, cap), Eps);
            // 同じ上流変動でも貯蔵可能な財はバッファで吸収できる
            float storableCap = IntermediateBufferRules.BufferCapacity(IntermediateBufferRules.Storability(1f), 1f);
            Assert.AreEqual(0f, IntermediateBufferRules.ShockAmplification(0.7f, storableCap), Eps);
        }

        [Test]
        public void Inputs_AreClampedDeterministic()
        {
            // 範囲外入力はクランプ：storability>1→1、変動<0→0
            Assert.AreEqual(1f, IntermediateBufferRules.Storability(5f), Eps);
            Assert.AreEqual(0f, IntermediateBufferRules.Storability(-3f), Eps);
            // 負の変動はショック0
            Assert.AreEqual(0f, IntermediateBufferRules.ShockAmplification(-1f, 0f), Eps);
            // バッファ容量>1はクランプして吸収しきる
            Assert.AreEqual(0f, IntermediateBufferRules.ShockAmplification(0.5f, 2f), Eps);
        }
    }
}
