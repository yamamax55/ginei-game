using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 包囲を固定する：包囲度＝遮断方向の割合、完全包囲判定、士気減は包囲度比例、降伏は完全包囲＋低士気
    /// のみ（逃げ道がある限り0）、突囲損害は包囲度×戦力比（上限2倍）。境界・roll決定論を担保。
    /// </summary>
    public class EncirclementRulesTests
    {
        private static readonly EncirclementParams P = EncirclementParams.Default;
        // 完全包囲0.9/士気減0.05/降伏上限0.8/突囲基礎0.3

        [Test]
        public void Coverage_FractionOfBlockedDirections()
        {
            Assert.AreEqual(0.5f, EncirclementRules.Coverage(4, 8), 1e-5f);
            Assert.AreEqual(1f, EncirclementRules.Coverage(8, 8), 1e-5f);
            Assert.AreEqual(0f, EncirclementRules.Coverage(0, 8), 1e-5f);
            Assert.AreEqual(0f, EncirclementRules.Coverage(4, 0), 1e-5f); // 総方向0＝包囲なし
        }

        [Test]
        public void IsFullySurrounded_AtThreshold()
        {
            Assert.IsTrue(EncirclementRules.IsFullySurrounded(0.9f, P));
            Assert.IsFalse(EncirclementRules.IsFullySurrounded(0.89f, P));
        }

        [Test]
        public void MoraleDrain_ProportionalToCoverage()
        {
            Assert.AreEqual(0.05f, EncirclementRules.MoraleDrain(1f, 1f, P), 1e-5f);
            Assert.AreEqual(0.025f, EncirclementRules.MoraleDrain(0.5f, 1f, P), 1e-5f);
            Assert.AreEqual(0f, EncirclementRules.MoraleDrain(0f, 1f, P), 1e-5f);
        }

        [Test]
        public void SurrenderChance_OnlyWhenFullySurrounded()
        {
            // 逃げ道がある限り降伏しない
            Assert.AreEqual(0f, EncirclementRules.SurrenderChance(0.8f, 0f, P), 1e-5f);
            // 完全包囲＋士気ゼロ＝上限0.8
            Assert.AreEqual(0.8f, EncirclementRules.SurrenderChance(1f, 0f, P), 1e-5f);
            // 士気が高ければ完全包囲でも戦う
            Assert.AreEqual(0f, EncirclementRules.SurrenderChance(1f, 1f, P), 1e-5f);
            Assert.AreEqual(0.4f, EncirclementRules.SurrenderChance(1f, 0.5f, P), 1e-5f);
        }

        [Test]
        public void Surrenders_DeterministicByRoll()
        {
            Assert.IsTrue(EncirclementRules.Surrenders(1f, 0f, 0.79f, P));
            Assert.IsFalse(EncirclementRules.Surrenders(1f, 0f, 0.81f, P));
        }

        [Test]
        public void BreakoutCasualtyRatio_CoverageTimesStrengthRatio()
        {
            // 完全包囲・互角＝0.3×1×1=0.3
            Assert.AreEqual(0.3f, EncirclementRules.BreakoutCasualtyRatio(1f, 100f, 100f, P), 1e-5f);
            // 薄い包囲＝安く抜ける
            Assert.AreEqual(0.15f, EncirclementRules.BreakoutCasualtyRatio(0.5f, 100f, 100f, P), 1e-5f);
            // 包囲側が圧倒的でも倍率は2倍で頭打ち＝0.3×1×2=0.6
            Assert.AreEqual(0.6f, EncirclementRules.BreakoutCasualtyRatio(1f, 1000f, 100f, P), 1e-5f);
        }
    }
}
