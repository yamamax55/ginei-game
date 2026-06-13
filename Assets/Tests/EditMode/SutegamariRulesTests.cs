using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>島津の捨てがまり：献身度（統率×部下への態度）と発動可否（無能/尊大は散る）。</summary>
    public class SutegamariRulesTests
    {
        [Test]
        public void Devotion_GeometricMean_BothNeeded()
        {
            // 統率70×態度70 → √(0.7×0.7)=0.7
            Assert.AreEqual(0.7f, SutegamariRules.Devotion(70f, 70f), 1e-3f);
            // 有能だが尊大（統率80・態度20）→ √(0.8×0.2)=0.4 ＝ 献身が崩れる
            Assert.AreEqual(0.4f, SutegamariRules.Devotion(80f, 20f), 1e-3f);
            // 無能だが謙虚（統率20・態度80）→ 同じく0.4 ＝ 能力も要る
            Assert.AreEqual(0.4f, SutegamariRules.Devotion(20f, 80f), 1e-3f);
            // 名将＋謙虚 → 高い献身
            Assert.AreEqual(0.9f, SutegamariRules.Devotion(90f, 90f), 1e-3f);
        }

        [Test]
        public void WillPerform_GoodLeaderSacrifices_BadScatters()
        {
            Assert.IsTrue(SutegamariRules.WillPerformSutegamari(SutegamariRules.Devotion(70f, 70f)));  // 0.7≥0.45
            Assert.IsTrue(SutegamariRules.WillPerformSutegamari(SutegamariRules.Devotion(50f, 50f)));  // 0.5≥0.45
            // 無能 → 散る
            Assert.IsFalse(SutegamariRules.WillPerformSutegamari(SutegamariRules.Devotion(20f, 80f))); // 0.4<0.45
            // 尊大 → 散る
            Assert.IsFalse(SutegamariRules.WillPerformSutegamari(SutegamariRules.Devotion(80f, 20f))); // 0.4<0.45
        }

        [Test]
        public void ScatterFraction_LowDevotionScattersMore()
        {
            Assert.AreEqual(0.3f, SutegamariRules.ScatterFraction(0.7f), 1e-4f);
            Assert.AreEqual(0.6f, SutegamariRules.ScatterFraction(0.4f), 1e-4f);
            Assert.AreEqual(0f, SutegamariRules.ScatterFraction(1f), 1e-4f);
            Assert.AreEqual(1f, SutegamariRules.ScatterFraction(0f), 1e-4f);
        }

        [Test]
        public void Devotion_ClampsInputs()
        {
            Assert.AreEqual(1f, SutegamariRules.Devotion(200f, 200f), 1e-4f); // 上限クランプ
            Assert.AreEqual(0f, SutegamariRules.Devotion(-50f, 80f), 1e-4f);  // 負はゼロ
            Assert.AreEqual(SutegamariRules.CoverFactor(0.7f), 0.7f, 1e-4f);
        }
    }
}
