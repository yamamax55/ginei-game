using NUnit.Framework;
using UnityEngine;

namespace Ginei.Tests
{
    /// <summary>
    /// TreatyTermsRules（条約条項＝講和条件の苛烈さと持続性）の EditMode テスト。
    /// 既定 Params で期待値を固定（手計算で検算）。Pow/Sqrt は未使用のため許容誤差は 1e-4f。
    /// </summary>
    public class TreatyTermsRulesTests
    {
        const float Eps = 1e-4f;

        [Test]
        public void TermsSeverity_EqualInputs_AveragesToInput()
        {
            // 全て 0.5 なら加重平均も 0.5。
            float s = TreatyTermsRules.TermsSeverity(0.5f, 0.5f, 0.5f);
            Assert.AreEqual(0.5f, s, Eps);
        }

        [Test]
        public void TermsSeverity_OnlyTerritorial_UsesWeight()
        {
            // (1×0.4)/(0.4+0.3+0.3) = 0.4。
            float s = TreatyTermsRules.TermsSeverity(1f, 0f, 0f);
            Assert.AreEqual(0.4f, s, Eps);
        }

        [Test]
        public void LoserAcceptability_HarshLowCoercion_IsLow()
        {
            // termsSeverity=0.6→leniency=0.4・coercion=0.4：(0.4 + 0.4×0.5)/1.5 = 0.4。
            float a = TreatyTermsRules.LoserAcceptability(0.6f, 0.4f);
            Assert.AreEqual(0.4f, a, Eps);
        }

        [Test]
        public void LoserAcceptability_LenientHighCoercion_IsHigh()
        {
            // termsSeverity=0.2→leniency=0.8・coercion=0.8：(0.8 + 0.8×0.5)/1.5 = 0.8。
            float a = TreatyTermsRules.LoserAcceptability(0.2f, 0.8f);
            Assert.AreEqual(0.8f, a, Eps);
        }

        [Test]
        public void ResentmentSeeds_HarshAndHumiliating_IsHigh()
        {
            // 0.8×0.5×(1+0.6) = 0.64。
            float r = TreatyTermsRules.ResentmentSeeds(0.8f, 0.5f);
            Assert.AreEqual(0.64f, r, Eps);
        }

        [Test]
        public void ResentmentSeeds_NoSeverity_IsZero()
        {
            // 苛烈さ0なら屈辱が高くても遺恨なし。
            float r = TreatyTermsRules.ResentmentSeeds(0f, 1f);
            Assert.AreEqual(0f, r, Eps);
        }

        [Test]
        public void WinnerGains_OnlyTerritorial_UsesWeightRatio()
        {
            // (1×0.4)/(0.4+0.3) = 0.5714286。
            float g = TreatyTermsRules.WinnerGains(1f, 0f);
            Assert.AreEqual(0.4f / 0.7f, g, Eps);
        }

        [Test]
        public void EnforcementCapacity_OnlyPower_UsesWeight()
        {
            // (1×0.6)/(0.6+0.4) = 0.6。
            float e = TreatyTermsRules.EnforcementCapacity(1f, 0f);
            Assert.AreEqual(0.6f, e, Eps);
        }

        [Test]
        public void TreatyStability_AcceptedEnforcedLowResentment_IsStable()
        {
            // (0.8+0.6)/2 - 0.2×0.5 = 0.7 - 0.1 = 0.6。
            float s = TreatyTermsRules.TreatyStability(0.8f, 0.6f, 0.2f);
            Assert.AreEqual(0.6f, s, Eps);
        }

        [Test]
        public void TreatyStability_HighResentment_ClampsToZero()
        {
            // (0.2+0.3)/2 - 0.9×0.5 = 0.25 - 0.45 = -0.2 → clamp 0。
            float s = TreatyTermsRules.TreatyStability(0.2f, 0.3f, 0.9f);
            Assert.AreEqual(0f, s, Eps);
        }

        [Test]
        public void RevisionPressure_ResentmentTimesShift()
        {
            // 0.64×0.5 = 0.32。
            float rp = TreatyTermsRules.RevisionPressure(0.64f, 0.5f);
            Assert.AreEqual(0.32f, rp, Eps);
        }

        [Test]
        public void IsTreatyDurable_DefaultThreshold()
        {
            // 0.8-0.1=0.7 ≥ 0.5 → true／0.6-0.32=0.28 < 0.5 → false。
            Assert.IsTrue(TreatyTermsRules.IsTreatyDurable(0.8f, 0.1f));
            Assert.IsFalse(TreatyTermsRules.IsTreatyDurable(0.6f, 0.32f));
        }

        [Test]
        public void InputsAreClamped()
        {
            // 範囲外入力でも 0..1 に丸まる（例外なし）。
            float s = TreatyTermsRules.TermsSeverity(5f, -3f, 2f);
            Assert.GreaterOrEqual(s, 0f);
            Assert.LessOrEqual(s, 1f);
            float a = TreatyTermsRules.LoserAcceptability(-1f, 9f);
            Assert.GreaterOrEqual(a, 0f);
            Assert.LessOrEqual(a, 1f);
        }

        /// <summary>
        /// 物語：苛烈なヴェルサイユ型条約（重い割譲・賠償・屈辱）は受容されず遺恨を残し、
        /// 力関係が動けば破棄圧力で持続しない。対して寛大な条約は受け入れられ持続する。
        /// </summary>
        [Test]
        public void Narrative_HarshTreatyBreaksWhileLenientTreatyEndures()
        {
            // 苛烈な条約：重い割譲・賠償・武装制限。
            float harshSeverity = TreatyTermsRules.TermsSeverity(0.9f, 0.9f, 0.8f);
            float harshAccept = TreatyTermsRules.LoserAcceptability(harshSeverity, 0.3f);
            float harshResent = TreatyTermsRules.ResentmentSeeds(harshSeverity, 0.9f);
            float harshEnforce = TreatyTermsRules.EnforcementCapacity(0.6f, 0.5f);
            float harshStability = TreatyTermsRules.TreatyStability(harshAccept, harshEnforce, harshResent);
            // 後年、敗者へ力が傾く（再軍備）。
            float harshPressure = TreatyTermsRules.RevisionPressure(harshResent, 0.8f);
            bool harshDurable = TreatyTermsRules.IsTreatyDurable(harshStability, harshPressure);

            // 寛大な条約：軽い条件・屈辱も小さい。
            float mildSeverity = TreatyTermsRules.TermsSeverity(0.2f, 0.1f, 0.1f);
            float mildAccept = TreatyTermsRules.LoserAcceptability(mildSeverity, 0.5f);
            float mildResent = TreatyTermsRules.ResentmentSeeds(mildSeverity, 0.2f);
            float mildEnforce = TreatyTermsRules.EnforcementCapacity(0.7f, 0.6f);
            float mildStability = TreatyTermsRules.TreatyStability(mildAccept, mildEnforce, mildResent);
            float mildPressure = TreatyTermsRules.RevisionPressure(mildResent, 0.8f);
            bool mildDurable = TreatyTermsRules.IsTreatyDurable(mildStability, mildPressure);

            Assert.Greater(harshResent, mildResent, "苛烈な条約ほど遺恨が大きい");
            Assert.Less(harshAccept, mildAccept, "苛烈な条約ほど受容されない");
            Assert.Greater(harshPressure, mildPressure, "苛烈な条約ほど改定圧力が高い");
            Assert.IsFalse(harshDurable, "ヴェルサイユ型は持続しない");
            Assert.IsTrue(mildDurable, "寛大な条約は持続する");
        }
    }
}
