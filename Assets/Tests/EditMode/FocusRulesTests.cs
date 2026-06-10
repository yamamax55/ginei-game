using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>三密＝身・口・意の同期（空海 #872）を固定する：3つ揃うと最大出力、一つ欠けると崩れる。</summary>
    public class FocusRulesTests
    {
        [Test]
        public void AllAligned_MaxSync()
        {
            Assert.AreEqual(1f, FocusRules.Sync(1f, 1f, 1f), 1e-4f);
            Assert.AreEqual(1.5f, FocusRules.OutputMultiplier(1f, 1f, 1f), 1e-4f); // base1 + 0.5
        }

        [Test]
        public void OneMissing_Collapses()
        {
            Assert.AreEqual(0f, FocusRules.Sync(1f, 1f, 0f), 1e-4f);        // 意が欠ける＝同期ゼロ
            Assert.AreEqual(1f, FocusRules.OutputMultiplier(1f, 1f, 0f), 1e-4f); // ボーナスなし＝基準のみ
        }

        [Test]
        public void Partial_ProductOfChannels()
        {
            Assert.AreEqual(0.125f, FocusRules.Sync(0.5f, 0.5f, 0.5f), 1e-4f); // 0.5^3
            Assert.AreEqual(1.0625f, FocusRules.OutputMultiplier(0.5f, 0.5f, 0.5f), 1e-4f);
        }

        [Test]
        public void Inputs_Clamped()
        {
            Assert.AreEqual(1f, FocusRules.Sync(2f, 2f, 2f), 1e-4f);   // 上限クランプ
            Assert.AreEqual(0f, FocusRules.Sync(-1f, 1f, 1f), 1e-4f);  // 下限クランプ
        }

        [Test]
        public void CustomBaseAndBonus()
        {
            Assert.AreEqual(2f, FocusRules.OutputMultiplier(1f, 1f, 1f, baseMult: 1f, bonus: 1f), 1e-4f);
        }
    }
}
