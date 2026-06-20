using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 指揮系統の情報フィルタ（ENDR-2 #2352）を固定する：フィルタ品質の単調減少・境界[0,1]・
    /// 認識ギャップ係数の単調増加・閾値判定・null/clamp 安全。純関数（決定論）。
    /// </summary>
    public class CommandIntelFilterRulesTests
    {
        const float Eps = 1e-4f;

        // --- FilteredIntelQuality：フィルタ段階で単調減少 ---

        [Test]
        public void Quality_DecreasesWithFilterLevel()
        {
            float full = CommandIntelFilterRules.FilteredIntelQuality(1f, InfoFilterLevel.完全開示, 50f);
            float partial = CommandIntelFilterRules.FilteredIntelQuality(1f, InfoFilterLevel.部分開示, 50f);
            float hidden = CommandIntelFilterRules.FilteredIntelQuality(1f, InfoFilterLevel.隠蔽, 50f);

            Assert.Greater(full, partial);
            Assert.Greater(partial, hidden);
        }

        // --- FilteredIntelQuality：階層が深いほど劣化 ---

        [Test]
        public void Quality_DecreasesWithHierarchyDepth()
        {
            float shallow = CommandIntelFilterRules.FilteredIntelQuality(1f, InfoFilterLevel.部分開示, 50f, 0);
            float deep = CommandIntelFilterRules.FilteredIntelQuality(1f, InfoFilterLevel.部分開示, 50f, 4);

            Assert.Greater(shallow, deep);
        }

        // --- 情報能力が高いほど品質を補う ---

        [Test]
        public void Quality_HigherIntelligence_RecoversSome()
        {
            float dull = CommandIntelFilterRules.FilteredIntelQuality(1f, InfoFilterLevel.部分開示, 0f);
            float sharp = CommandIntelFilterRules.FilteredIntelQuality(1f, InfoFilterLevel.部分開示, 100f);

            Assert.Greater(sharp, dull);
        }

        // --- 境界[0,1] ---

        [Test]
        public void Quality_StaysWithinBounds()
        {
            // 過大入力・深い階層・能力上振れでも 0..1 に収まる
            float hi = CommandIntelFilterRules.FilteredIntelQuality(5f, InfoFilterLevel.完全開示, 999f, 0);
            Assert.LessOrEqual(hi, 1f);
            Assert.GreaterOrEqual(hi, 0f);

            // 最悪条件でも非負
            float lo = CommandIntelFilterRules.FilteredIntelQuality(1f, InfoFilterLevel.隠蔽, 0f, 50);
            Assert.GreaterOrEqual(lo, 0f);
            Assert.LessOrEqual(lo, 1f);
        }

        [Test]
        public void Quality_NoInfo_IsZero()
        {
            float q = CommandIntelFilterRules.FilteredIntelQuality(0f, InfoFilterLevel.完全開示, 100f, 0);
            Assert.AreEqual(0f, q, Eps);
        }

        [Test]
        public void Quality_FullDisclosure_DirectChannel_PreservesSignal()
        {
            // 完全開示・直属・中庸能力 → 入力をほぼそのまま保持
            float q = CommandIntelFilterRules.FilteredIntelQuality(1f, InfoFilterLevel.完全開示, 50f, 0);
            Assert.AreEqual(1f, q, Eps);
        }

        [Test]
        public void Quality_NegativeDepth_TreatedAsZero()
        {
            float neg = CommandIntelFilterRules.FilteredIntelQuality(1f, InfoFilterLevel.部分開示, 50f, -10);
            float zero = CommandIntelFilterRules.FilteredIntelQuality(1f, InfoFilterLevel.部分開示, 50f, 0);
            Assert.AreEqual(zero, neg, Eps);
        }

        // --- GapMagnitude / IntelGapEffect ---

        [Test]
        public void Gap_Magnitude_IsAbsoluteDifference()
        {
            Assert.AreEqual(0.3f, CommandIntelFilterRules.GapMagnitude(0.2f, 0.5f), Eps);
            // 対称（符号によらない）
            Assert.AreEqual(
                CommandIntelFilterRules.GapMagnitude(0.5f, 0.2f),
                CommandIntelFilterRules.GapMagnitude(0.2f, 0.5f),
                Eps);
        }

        [Test]
        public void GapEffect_NoGap_IsNeutral()
        {
            // ギャップ0 → 判断誤差係数1.0（正常）
            Assert.AreEqual(1f, CommandIntelFilterRules.IntelGapEffect(0.4f, 0.4f), Eps);
        }

        [Test]
        public void GapEffect_MonotonicWithGap()
        {
            // ギャップが増えるほど判断誤差は単調増加（小さなギャップ < 大きなギャップ）
            float small = CommandIntelFilterRules.IntelGapEffect(0.5f, 0.6f);
            float large = CommandIntelFilterRules.IntelGapEffect(0.0f, 1.0f);
            Assert.Greater(large, small);
            Assert.GreaterOrEqual(small, 1f);
        }

        [Test]
        public void GapEffect_Clamped_NeverExceedsMax()
        {
            // 過大入力でも上限を超えない
            float e = CommandIntelFilterRules.IntelGapEffect(-5f, 5f);
            Assert.LessOrEqual(e, CommandIntelFilterRules.MaxErrorFactor);
            Assert.GreaterOrEqual(e, 1f);
        }

        // --- RevealThreshold ---

        [Test]
        public void Reveal_ExceedsThreshold_True()
        {
            Assert.IsTrue(CommandIntelFilterRules.RevealThreshold(0.9f, 0.5f));
        }

        [Test]
        public void Reveal_BelowThreshold_False()
        {
            Assert.IsFalse(CommandIntelFilterRules.RevealThreshold(0.2f, 0.5f));
        }

        [Test]
        public void Reveal_AtThreshold_False()
        {
            // ちょうど閾値は超過扱いしない（厳密超過）
            Assert.IsFalse(CommandIntelFilterRules.RevealThreshold(0.5f, 0.5f));
        }

        [Test]
        public void Reveal_DefaultThreshold_Works()
        {
            Assert.IsTrue(CommandIntelFilterRules.RevealThreshold(0.99f));
            Assert.IsFalse(CommandIntelFilterRules.RevealThreshold(0.0f));
        }

        [Test]
        public void Reveal_ClampsOutOfRangeInputs()
        {
            // 負ギャップは0扱い→どんな閾値でも非超過
            Assert.IsFalse(CommandIntelFilterRules.RevealThreshold(-1f, 0.5f));
        }

        // --- 段階の意味づけ：隠蔽は能力で完全には補えない ---

        [Test]
        public void Reveal_HiddenCausesLargerGapPotential()
        {
            // 隠蔽の方が品質が低い → 認識(品質)と真実のギャップが大きくなりうる
            float hiddenQ = CommandIntelFilterRules.FilteredIntelQuality(1f, InfoFilterLevel.隠蔽, 100f);
            float openQ = CommandIntelFilterRules.FilteredIntelQuality(1f, InfoFilterLevel.完全開示, 100f);

            float hiddenGap = CommandIntelFilterRules.GapMagnitude(hiddenQ, 1f);
            float openGap = CommandIntelFilterRules.GapMagnitude(openQ, 1f);
            Assert.Greater(hiddenGap, openGap);
        }
    }
}
