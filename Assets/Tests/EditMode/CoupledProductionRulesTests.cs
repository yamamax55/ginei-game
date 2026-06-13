using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 連産品（#1110）を固定する：最大稼働は最も制約的な投入が律速（リービッヒの最小律）・固定比の同時産出
    /// （主産物も従産物も不可分に出る）・主産物の需要に引きずられて従産物が強制的に湧く（連産の宿命）。
    /// 律速インデックス・投入消費・設備稼働率（既定Params capacityPerRun=1.0）とクランプを担保。
    /// </summary>
    public class CoupledProductionRulesTests
    {
        private static readonly CoupledProductionParams P = CoupledProductionParams.Default; // capacityPerRun=1.0

        // 石油精製の連産モデル：原油2を投入すると、主産物=軽油3・従産物=重油1 が固定比で同時に出る。
        private static JointRecipe Refinery()
            => new JointRecipe(new[] { 2f }, new[] { 3f, 1f }, "精製");

        // 二投入レシピ：原油2＋触媒1で 軽油3・重油1。律速テスト用。
        private static JointRecipe TwoInput()
            => new JointRecipe(new[] { 2f, 1f }, new[] { 3f, 1f }, "精製二投入");

        [Test]
        public void MaxRuns_LimitedByScarcestInput()
        {
            // 原油10・触媒3 → 原油律速10/2=5、触媒律速3/1=3 → 最小=3稼働（リービッヒの最小律）
            Assert.AreEqual(3, CoupledProductionRules.MaxRuns(TwoInput(), new[] { 10f, 3f }));
            // 原油7のみ → 7/2=3.5 → 切り捨て3稼働
            Assert.AreEqual(3, CoupledProductionRules.MaxRuns(Refinery(), new[] { 7f }));
            // 投入不足は0稼働
            Assert.AreEqual(0, CoupledProductionRules.MaxRuns(Refinery(), new[] { 1f }));
        }

        [Test]
        public void BottleneckInput_IdentifiesScarcest()
        {
            // 触媒が律速（インデックス1）
            Assert.AreEqual(1, CoupledProductionRules.BottleneckInput(TwoInput(), new[] { 10f, 3f }));
            // 原油が律速（インデックス0）：原油4/2=2 < 触媒10/1=10
            Assert.AreEqual(0, CoupledProductionRules.BottleneckInput(TwoInput(), new[] { 4f, 10f }));
        }

        [Test]
        public void Produce_FixedRatioSimultaneousOutput()
        {
            // 4稼働 → 軽油3×4=12・重油1×4=4 が同時に出る（不可分）
            float[] outs = CoupledProductionRules.Produce(Refinery(), 4);
            Assert.AreEqual(2, outs.Length);
            Assert.AreEqual(12f, outs[0], 1e-4f); // 主産物=軽油
            Assert.AreEqual(4f, outs[1], 1e-4f);  // 従産物=重油
        }

        [Test]
        public void InputConsumption_ScalesWithRuns()
        {
            // 5稼働 → 原油2×5=10・触媒1×5=5 を消費
            float[] cons = CoupledProductionRules.InputConsumption(TwoInput(), 5);
            Assert.AreEqual(2, cons.Length);
            Assert.AreEqual(10f, cons[0], 1e-4f);
            Assert.AreEqual(5f, cons[1], 1e-4f);
        }

        [Test]
        public void ForcedByproduct_PrimaryDemandDragsOutByproduct()
        {
            // 軽油（主産物）を10欲しい → 必要稼働=ceil(10/3)=4 → 軽油12・重油4 が道連れに出る（連産の宿命）。
            float[] outs = CoupledProductionRules.ForcedByproduct(Refinery(), 10f);
            Assert.AreEqual(12f, outs[0], 1e-4f); // 需要10を満たす（端数切り上げで12）
            Assert.AreEqual(4f, outs[1], 1e-4f);  // 望まずとも重油4が強制産出
        }

        [Test]
        public void ForcedByproduct_ZeroDemand_NothingForced()
        {
            // 需要0なら従産物も出ない
            float[] outs = CoupledProductionRules.ForcedByproduct(Refinery(), 0f);
            Assert.AreEqual(0f, outs[0], 1e-5f);
            Assert.AreEqual(0f, outs[1], 1e-5f);
        }

        [Test]
        public void CapacityUtilization_RunsOverCapacity_Clamped()
        {
            // 3稼働／設備上限10 → 0.3
            Assert.AreEqual(0.3f, CoupledProductionRules.CapacityUtilization(3, 10f, P), 1e-4f);
            // 過負荷（15稼働／上限10）は1.0頭打ち
            Assert.AreEqual(1f, CoupledProductionRules.CapacityUtilization(15, 10f, P), 1e-4f);
            // 上限0は0
            Assert.AreEqual(0f, CoupledProductionRules.CapacityUtilization(5, 0f, P), 1e-5f);
        }

        [Test]
        public void Negatives_AreClamped()
        {
            // 稼働負はクランプ＝産出0
            float[] outs = CoupledProductionRules.Produce(Refinery(), -3);
            Assert.AreEqual(0f, outs[0], 1e-5f);
            Assert.AreEqual(0f, outs[1], 1e-5f);
            // 投入係数0のレシピ（無投入）は律速なし＝int.MaxValue
            var free = new JointRecipe(new float[0], new[] { 5f });
            Assert.AreEqual(int.MaxValue, CoupledProductionRules.MaxRuns(free, null));
            Assert.AreEqual(-1, CoupledProductionRules.BottleneckInput(free, null));
        }
    }
}
