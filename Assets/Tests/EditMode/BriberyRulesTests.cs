using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>上官への賄賂：費用（階級比例）・支払い可否・露見判定・親愛/遺恨上昇・強度クランプ。</summary>
    public class BriberyRulesTests
    {
        private static readonly BribeParams P = BribeParams.Default; // 基礎2000＋階級×500／親愛0.00005(上限0.15)／露見20%

        [Test]
        public void Cost_ScalesWithSuperiorTier()
        {
            Assert.AreEqual(5500, BriberyRules.Cost(7, P)); // 2000+500×7
            Assert.AreEqual(2000, BriberyRules.Cost(0, P));
            Assert.AreEqual(2000, BriberyRules.Cost(-3, P)); // 非負クランプ
        }

        [Test]
        public void CanAfford_ComparesCash()
        {
            Assert.IsTrue(BriberyRules.CanAfford(5500f, 7, P));
            Assert.IsFalse(BriberyRules.CanAfford(5499f, 7, P));
        }

        [Test]
        public void IsExposed_ByRollVsChance()
        {
            Assert.IsTrue(BriberyRules.IsExposed(0.1f, P));   // <0.2
            Assert.IsFalse(BriberyRules.IsExposed(0.2f, P));  // 境界は露見しない
            Assert.IsFalse(BriberyRules.IsExposed(0.9f, P));
        }

        [Test]
        public void FavorGain_ProportionalCapped()
        {
            Assert.AreEqual(0.1f, BriberyRules.FavorGain(2000, P), 1e-4f);  // 2000×0.00005
            Assert.AreEqual(0.15f, BriberyRules.FavorGain(5500, P), 1e-4f); // 上限0.15でクランプ
            Assert.AreEqual(0.15f, BriberyRules.ResentmentGain(5500, P), 1e-4f); // 露見時も同量を遺恨へ
        }

        [Test]
        public void NewStrength_ClampedTo01()
        {
            Assert.AreEqual(0.6f, BriberyRules.NewStrength(0.5f, 0.1f), 1e-4f);
            Assert.AreEqual(1f, BriberyRules.NewStrength(0.95f, 0.15f), 1e-4f);  // 上クランプ
            Assert.AreEqual(0f, BriberyRules.NewStrength(0.05f, -0.2f), 1e-4f);  // 下クランプ
        }
    }
}
