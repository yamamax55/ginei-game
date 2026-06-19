using NUnit.Framework;
using UnityEngine;

namespace Ginei.Tests
{
    /// <summary>最後通牒・瀬戸際外交の純ロジック（UltimatumRules）の検証。</summary>
    public class UltimatumRulesTests
    {
        const float Eps = 1e-4f;

        [Test]
        public void Severity_RequestTimesDeadline()
        {
            // 0.8*0.5 + 0.6*0.5 = 0.7
            Assert.AreEqual(0.7f, UltimatumRules.UltimatumSeverity(0.8f, 0.6f), Eps);
        }

        [Test]
        public void Credibility_ReadinessResolveReputation()
        {
            // 0.9*0.4 + 0.8*0.35 + 0.6*0.25 = 0.36+0.28+0.15 = 0.79
            Assert.AreEqual(0.79f, UltimatumRules.Credibility(0.9f, 0.8f, 0.6f), Eps);
        }

        [Test]
        public void Compliance_ProductWithCapitulation()
        {
            // cap = Lerp(0.4,1,0.5)=0.7 ; 0.7*0.79*0.7 = 0.3871
            Assert.AreEqual(0.3871f, UltimatumRules.ComplianceLikelihood(0.7f, 0.79f, 0.5f), Eps);
        }

        [Test]
        public void Compliance_ZeroCredibilityIsBluff()
        {
            // 信憑性ゼロの空脅しは呑まれない
            Assert.AreEqual(0f, UltimatumRules.ComplianceLikelihood(1f, 0f, 1f), Eps);
        }

        [Test]
        public void Defiance_PrideTimesStrength()
        {
            // 0.8*0.55 + 0.6*0.45 = 0.44+0.27 = 0.71
            Assert.AreEqual(0.71f, UltimatumRules.DefianceResponse(0.8f, 0.6f), Eps);
        }

        [Test]
        public void FaceSavingExit_ProductGate()
        {
            // 0.5*0.4 = 0.2 ; 調停余地ゼロなら退路もゼロ
            Assert.AreEqual(0.2f, UltimatumRules.FaceSavingExit(0.5f, 0.4f), Eps);
            Assert.AreEqual(0f, UltimatumRules.FaceSavingExit(1f, 0f), Eps);
        }

        [Test]
        public void Miscalculation_BrinkTimesFog()
        {
            // 0.8 * (1-0.3) * 1.0 = 0.56
            Assert.AreEqual(0.56f, UltimatumRules.MiscalculationRisk(0.8f, 0.3f), Eps);
            // 意思疎通が完璧なら誤算ゼロ
            Assert.AreEqual(0f, UltimatumRules.MiscalculationRisk(1f, 1f), Eps);
        }

        [Test]
        public void CommitmentTrap_StakesTimesReputation()
        {
            // 0.6*0.5 = 0.3
            Assert.AreEqual(0.3f, UltimatumRules.CommitmentTrap(0.6f, 0.5f), Eps);
        }

        [Test]
        public void Outcome_ClampsAndSign()
        {
            // 受諾だけで反発・誤算ゼロなら成功(+)
            Assert.Greater(UltimatumRules.UltimatumOutcome(0.9f, 0f, 0f), 0f);
            // 反発・誤算が勝てば破綻(-1にクランプ)
            Assert.AreEqual(-1f, UltimatumRules.UltimatumOutcome(0f, 1f, 1f), Eps);
        }

        [Test]
        public void Inputs_ClampedAndNullSafeRange()
        {
            // 範囲外入力は内部 Clamp（要求2→1, 期限-1→0）→ severity = 1*0.5 = 0.5
            Assert.AreEqual(0.5f, UltimatumRules.UltimatumSeverity(2f, -1f), Eps);
            // 全出力は範囲内
            float o = UltimatumRules.UltimatumOutcome(2f, 2f, 2f);
            Assert.LessOrEqual(o, 1f);
            Assert.GreaterOrEqual(o, -1f);
        }

        [Test]
        public void Story_BrinkmanshipMiscalculationTriggersWar()
        {
            // 物語：高慢で強い相手に過大要求＋短期限の通牒を突きつける。
            // 賭け金を公にして退路を焼き、瀬戸際で意思疎通を欠く。
            // → 相手は反発し、誤算で意図せぬ戦争へ転がる（帰結は負＝破綻）。
            var p = UltimatumParams.Default;

            float severity = UltimatumRules.UltimatumSeverity(0.95f, 0.9f, p);   // 厳しい
            float cred = UltimatumRules.Credibility(0.85f, 0.9f, 0.7f, p);       // 信憑あり
            float compliance = UltimatumRules.ComplianceLikelihood(severity, cred, 0.2f, p); // 屈服度低い
            float defiance = UltimatumRules.DefianceResponse(0.95f, 0.85f, p);  // 強く反発
            float trap = UltimatumRules.CommitmentTrap(0.9f, 0.85f, p);         // 引くに引けない
            float misc = UltimatumRules.MiscalculationRisk(0.9f, 0.15f, p);     // 誤算高い
            float exit = UltimatumRules.FaceSavingExit(0.1f, 0.1f, p);          // 退路ほぼ無し
            float outcome = UltimatumRules.UltimatumOutcome(compliance, defiance, misc, p);

            // 反発・罠・誤算リスクは高く、退路は乏しく、帰結は破綻（負）。
            Assert.Greater(defiance, 0.6f);
            Assert.Greater(trap, 0.6f);
            Assert.Greater(misc, 0.5f);
            Assert.Less(exit, 0.1f);
            Assert.Less(outcome, 0f);

            // 対照：要求を曖昧にし調停を呼び込めば退路が大きく開く。
            float calmExit = UltimatumRules.FaceSavingExit(0.8f, 0.8f, p);
            Assert.Greater(calmExit, exit);
        }
    }
}
