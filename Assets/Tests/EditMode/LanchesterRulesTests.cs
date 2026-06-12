using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>ランチェスター集中倍率：局所火力比でダメージが増減（拮抗1.0・優勢↑・劣勢↓・クランプ）。</summary>
    public class LanchesterRulesTests
    {
        [Test]
        public void Concentration_ParityIsOne()
        {
            Assert.AreEqual(1.0f, LanchesterRules.ConcentrationFactor(100f, 100f), 1e-4f);
        }

        [Test]
        public void Concentration_SuperiorityBoostsInferiorityReduces()
        {
            // 既定＝指数0.5（平方根）・倍率0.5〜2.0。
            Assert.AreEqual(2.0f, LanchesterRules.ConcentrationFactor(400f, 100f), 1e-4f); // 局所4:1 → √4=2.0
            Assert.AreEqual(0.5f, LanchesterRules.ConcentrationFactor(100f, 400f), 1e-4f); // 局所1:4 → √0.25=0.5
            Assert.AreEqual(Mathf2.Sqrt2, LanchesterRules.ConcentrationFactor(200f, 100f), 1e-3f); // 2:1 → √2
        }

        [Test]
        public void Concentration_ClampedAtBounds()
        {
            // 9:1 → √9=3 だが上限2.0でクランプ。
            Assert.AreEqual(2.0f, LanchesterRules.ConcentrationFactor(900f, 100f), 1e-4f);
            // 1:9 → √(1/9)=0.333 だが下限0.5でクランプ。
            Assert.AreEqual(0.5f, LanchesterRules.ConcentrationFactor(100f, 900f), 1e-4f);
        }

        [Test]
        public void Concentration_EdgeCases()
        {
            // 敵火力ゼロ＝最大集中。
            Assert.AreEqual(2.0f, LanchesterRules.ConcentrationFactor(300f, 0f), 1e-4f);
            // 味方火力ゼロ＝最小。
            Assert.AreEqual(0.5f, LanchesterRules.ConcentrationFactor(0f, 300f), 1e-4f);
            // 負入力は0クランプ（敵負＝敵0扱い＝最大）。
            Assert.AreEqual(2.0f, LanchesterRules.ConcentrationFactor(100f, -50f), 1e-4f);
        }

        [Test]
        public void Concentration_LinearExponentIsStronger()
        {
            // 指数1.0（線形）：4:1 → 4 だが上限でクランプ可。倍率上限を上げて線形を確認。
            var p = new LanchesterParams(1.0f, 0.25f, 4.0f);
            Assert.AreEqual(4.0f, LanchesterRules.ConcentrationFactor(400f, 100f, p), 1e-4f); // 線形＝二乗則を強く効かせる
            Assert.AreEqual(0.25f, LanchesterRules.ConcentrationFactor(100f, 400f, p), 1e-4f);
        }
    }

    /// <summary>テスト用の定数（√2）。</summary>
    internal static class Mathf2
    {
        public const float Sqrt2 = 1.41421356f;
    }
}
