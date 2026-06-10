using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// テラフォーミングを固定する：改造可能性は過酷さで線形に下がり上限以上は不可・投資が進捗を進める
    /// （過酷なほど遅い）・投資ゼロは進まずわずかに逆行（過酷なほど巻き返しが強い）・閾値で居住可能化・
    /// 完了費用は過酷な星ほど高く改造不能は無限大・途中放棄は自然に巻き戻され完成済みは退行しない。
    /// クランプと無限大ケースを担保。<see cref="ColonizationRules"/>（入植）の前段＝星造り側。
    /// </summary>
    public class TerraformingRulesTests
    {
        private static readonly TerraformingParams P = TerraformingParams.Default; // 投資係数0.1/巻き返し0.01/過酷上限0.9/閾値1.0/最小投資0.05

        [Test]
        public void Feasibility_HarsherIsLessFeasible_TooHarshIsZero()
        {
            Assert.AreEqual(1f, TerraformingRules.Feasibility(0f, P), 1e-5f);    // 温和な星は最大
            Assert.AreEqual(0.5f, TerraformingRules.Feasibility(0.45f, P), 1e-4f);
            Assert.AreEqual(0f, TerraformingRules.Feasibility(0.9f, P), 1e-5f);  // 上限ちょうどで不可
            Assert.AreEqual(0f, TerraformingRules.Feasibility(1.5f, P), 1e-5f);  // 過大入力はクランプ
            Assert.AreEqual(1f, TerraformingRules.Feasibility(-0.2f, P), 1e-5f); // 負もクランプ
        }

        [Test]
        public void ProgressTick_InvestmentAdvances_HarsherIsSlower()
        {
            // 温和（hostility 0・可能性1.0）：投資0.5 → 0.2 + 0.5×0.1×1×1 = 0.25
            Assert.AreEqual(0.25f, TerraformingRules.ProgressTick(0.2f, 0.5f, 0f, 1f, P), 1e-4f);
            // 過酷0.45（可能性0.5）：同じ投資で半分しか進まない → 0.225
            Assert.AreEqual(0.225f, TerraformingRules.ProgressTick(0.2f, 0.5f, 0.45f, 1f, P), 1e-4f);
            // 上限1でクランプ（進みすぎない）
            Assert.AreEqual(1f, TerraformingRules.ProgressTick(0.95f, 1f, 0f, 10f, P), 1e-5f);
        }

        [Test]
        public void ProgressTick_NoInvestmentRegresses_ClampedAtZero()
        {
            // 投資ゼロ＝環境の巻き返し：温和（h=0）で 0.2 − 0.01×(1+0)×1 = 0.19
            Assert.AreEqual(0.19f, TerraformingRules.ProgressTick(0.2f, 0f, 0f, 1f, P), 1e-4f);
            // 過酷な星ほど巻き返しが強い：h=1（クランプ後）で 0.2 − 0.01×2×1 = 0.18
            Assert.AreEqual(0.18f, TerraformingRules.ProgressTick(0.2f, 0f, 1f, 1f, P), 1e-4f);
            // 最小投資未満（0.04 < 0.05）は放棄扱い＝逆行
            Assert.AreEqual(0.19f, TerraformingRules.ProgressTick(0.2f, 0.04f, 0f, 1f, P), 1e-4f);
            // 下限0でクランプ（マイナスに沈まない）
            Assert.AreEqual(0f, TerraformingRules.ProgressTick(0.005f, 0f, 0f, 1f, P), 1e-5f);
        }

        [Test]
        public void ProgressTick_InfeasibleWorldNeverAdvances()
        {
            // 過酷さ上限以上（可能性0）は全力投資でも進まない＝据え置き
            Assert.AreEqual(0.2f, TerraformingRules.ProgressTick(0.2f, 1f, 0.9f, 100f, P), 1e-5f);
        }

        [Test]
        public void IsHabitable_ThresholdBoundary()
        {
            Assert.IsTrue(TerraformingRules.IsHabitable(1f, P));    // 閾値ちょうどで居住可能化
            Assert.IsFalse(TerraformingRules.IsHabitable(0.99f, P));
            Assert.IsTrue(TerraformingRules.IsHabitable(1.5f, P));  // 過大入力はクランプして判定
        }

        [Test]
        public void CostToComplete_HarsherCostsMore_InfeasibleIsInfinite()
        {
            // 温和（可能性1.0）：残り1.0／(0.1×1) = 10
            Assert.AreEqual(10f, TerraformingRules.CostToComplete(0f, 0f, P), 1e-3f);
            // 進んだぶん安い：残り0.5 → 5
            Assert.AreEqual(5f, TerraformingRules.CostToComplete(0.5f, 0f, P), 1e-3f);
            // 過酷0.45（可能性0.5）：同じ残り0.5でも倍の10＝過酷な星ほど高くつく
            Assert.AreEqual(10f, TerraformingRules.CostToComplete(0.5f, 0.45f, P), 1e-3f);
            // 改造不能の星は無限大＝どれだけ注いでも星にならない
            Assert.IsTrue(float.IsPositiveInfinity(TerraformingRules.CostToComplete(0.5f, 0.9f, P)));
            // 完了済みは0
            Assert.AreEqual(0f, TerraformingRules.CostToComplete(1f, 0f, P), 1e-5f);
        }

        [Test]
        public void AbandonmentRegression_NatureTakesBack_HabitableIsStable()
        {
            // 途中放棄は自然が取り戻す：0.5 − 0.01×10 = 0.4
            Assert.AreEqual(0.4f, TerraformingRules.AbandonmentRegression(0.5f, 10f, P), 1e-4f);
            // 長期放置は振り出し（下限0でクランプ）
            Assert.AreEqual(0f, TerraformingRules.AbandonmentRegression(0.1f, 50f, P), 1e-5f);
            // 完成済み（居住可能化）は自立した生態系＝退行しない
            Assert.AreEqual(1f, TerraformingRules.AbandonmentRegression(1f, 1000f, P), 1e-5f);
            // 負の放置時間はクランプ＝変化なし
            Assert.AreEqual(0.5f, TerraformingRules.AbandonmentRegression(0.5f, -5f, P), 1e-5f);
        }
    }
}
