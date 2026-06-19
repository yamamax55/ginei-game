using NUnit.Framework;
using UnityEngine;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 医療トリアージ（限られた医療資源の優先配分）を固定する。緊急度×救命可能性で治療優先度を出し、
    /// 重症かつ救命困難は見送り（黒タグ）、資源配分から最大救命数を導き、待機悪化を引いて効果を返す。
    /// 既定 Params 期待値・境界・クランプ・物語を決定論で検証。
    /// </summary>
    public class MedicalTriageRulesTests
    {
        private static readonly MedicalTriageParams P = MedicalTriageParams.Default;
        // 既定: urgencyScale1 / lethality1 / 黒タグ重症閾値0.8 / 黒タグ救命閾値0.25 / 悪化倍率1 / 待機ペナルティ1

        // --- Urgency ---

        [Test]
        public void Urgency_SeverityTimesRate()
        {
            // 0.8 * 0.5 * 1 = 0.4
            Assert.AreEqual(0.4f, MedicalTriageRules.Urgency(0.8f, 0.5f, P), 1e-4f);
        }

        [Test]
        public void Urgency_ClampsInputs()
        {
            // 過大入力でも 1 で頭打ち
            Assert.AreEqual(1f, MedicalTriageRules.Urgency(5f, 5f, P), 1e-4f);
            Assert.AreEqual(0f, MedicalTriageRules.Urgency(-1f, 0.5f, P), 1e-4f);
        }

        // --- Salvageability ---

        [Test]
        public void Salvageability_SurvivableTimesTreatment()
        {
            // survivable = 1 - 0.6*1 = 0.4 ; 0.4 * 0.8 = 0.32
            Assert.AreEqual(0.32f, MedicalTriageRules.Salvageability(0.6f, 0.8f, P), 1e-4f);
        }

        [Test]
        public void Salvageability_LethalInjuryIsUnsalvageable()
        {
            // 重さ1 → survivable 0 → 治療があっても救命可能性 0
            Assert.AreEqual(0f, MedicalTriageRules.Salvageability(1f, 1f, P), 1e-4f);
        }

        // --- TriagePriority ---

        [Test]
        public void TriagePriority_UrgencyTimesSalvageability()
        {
            // 0.4 * 0.32 = 0.128
            Assert.AreEqual(0.128f, MedicalTriageRules.TriagePriority(0.4f, 0.32f, P), 1e-4f);
        }

        // --- ExpectantCategory（見送り＝黒タグ） ---

        [Test]
        public void ExpectantCategory_SevereAndUnsalvageable_IsBlackTag()
        {
            // 重症0.9(>=0.8) かつ 救命可能性0.2(<=0.25) → 見送り
            Assert.IsTrue(MedicalTriageRules.ExpectantCategory(0.9f, 0.2f, P));
        }

        [Test]
        public void ExpectantCategory_NotSevereEnough_IsNotBlackTag()
        {
            // 重さ0.7 は閾値0.8 未満 → 救命可能性が低くても黒タグにしない
            Assert.IsFalse(MedicalTriageRules.ExpectantCategory(0.7f, 0.2f, P));
        }

        [Test]
        public void ExpectantCategory_SalvageableEvenIfSevere_IsNotBlackTag()
        {
            // 重症でも救命可能性0.3(>0.25) なら救う → 黒タグにしない
            Assert.IsFalse(MedicalTriageRules.ExpectantCategory(0.9f, 0.3f, P));
        }

        // --- ResourceAllocation ---

        [Test]
        public void ResourceAllocation_ScarceResourcesThinAllocation()
        {
            // ratio = 20/100 = 0.2 ; 優先度0.5 * 0.2 = 0.1
            Assert.AreEqual(0.1f, MedicalTriageRules.ResourceAllocation(0.5f, 20f, 100f, P), 1e-4f);
        }

        [Test]
        public void ResourceAllocation_SurplusResourcesCapAtPriority()
        {
            // 資源が需要超過 → ratio は 1 で頭打ち → 配分 = 優先度0.5
            Assert.AreEqual(0.5f, MedicalTriageRules.ResourceAllocation(0.5f, 200f, 100f, P), 1e-4f);
        }

        // --- LivesMaximized ---

        [Test]
        public void LivesMaximized_AllocationTimesSalvageability()
        {
            // 0.5 * 0.6 = 0.3
            Assert.AreEqual(0.3f, MedicalTriageRules.LivesMaximized(0.5f, 0.6f, P), 1e-4f);
        }

        // --- WaitingDeterioration ---

        [Test]
        public void WaitingDeterioration_WaitTimesRate()
        {
            // 0.5 * 0.4 * 1 = 0.2
            Assert.AreEqual(0.2f, MedicalTriageRules.WaitingDeterioration(0.5f, 0.4f, P), 1e-4f);
        }

        // --- TriageEffectiveness ---

        [Test]
        public void TriageEffectiveness_LivesMinusWaitingLoss()
        {
            // 0.3 - 0.2*1 = 0.1
            Assert.AreEqual(0.1f, MedicalTriageRules.TriageEffectiveness(0.3f, 0.2f, P), 1e-4f);
        }

        [Test]
        public void TriageEffectiveness_WaitingLossClampsAtZero()
        {
            // 待機悪化が救命を上回れば 0 に張り付く
            Assert.AreEqual(0f, MedicalTriageRules.TriageEffectiveness(0.1f, 0.5f, P), 1e-4f);
        }

        // --- Default 委譲版が Params 明示版と一致 ---

        [Test]
        public void DefaultOverloads_MatchExplicitParams()
        {
            Assert.AreEqual(MedicalTriageRules.Urgency(0.6f, 0.7f, P),
                MedicalTriageRules.Urgency(0.6f, 0.7f), 1e-4f);
            Assert.AreEqual(MedicalTriageRules.Salvageability(0.6f, 0.7f, P),
                MedicalTriageRules.Salvageability(0.6f, 0.7f), 1e-4f);
            Assert.AreEqual(MedicalTriageRules.ResourceAllocation(0.5f, 50f, 100f, P),
                MedicalTriageRules.ResourceAllocation(0.5f, 50f, 100f), 1e-4f);
            Assert.AreEqual(MedicalTriageRules.ExpectantCategory(0.9f, 0.2f, P),
                MedicalTriageRules.ExpectantCategory(0.9f, 0.2f));
        }

        // --- 物語テスト：重症だが救命可能な傷病者が資源不足と待機で救えなくなる ---

        [Test]
        public void Story_SevereButSalvageable_LostToScarcityAndDelay()
        {
            // 重症(0.7)・急速悪化(0.8)・治療あり(0.7) の傷病者
            float urgency = MedicalTriageRules.Urgency(0.7f, 0.8f, P);            // 0.56
            float salv = MedicalTriageRules.Salvageability(0.7f, 0.7f, P);        // 0.3*0.7 = 0.21
            float prio = MedicalTriageRules.TriagePriority(urgency, salv, P);     // 0.56*0.21 = 0.1176

            Assert.AreEqual(0.56f, urgency, 1e-4f);
            Assert.AreEqual(0.21f, salv, 1e-4f);
            Assert.AreEqual(0.1176f, prio, 1e-4f);

            // 重さ0.7 は閾値0.8 未満 → 黒タグではない（本来は救える）
            Assert.IsFalse(MedicalTriageRules.ExpectantCategory(0.7f, salv, P));

            // 資源は需要の半分 → 配分が薄い
            float alloc = MedicalTriageRules.ResourceAllocation(prio, 50f, 100f, P); // ratio0.5 ; 0.0588
            Assert.AreEqual(0.0588f, alloc, 1e-4f);

            float lives = MedicalTriageRules.LivesMaximized(alloc, salv, P);        // 0.0588*0.21 = 0.012348
            Assert.AreEqual(0.012348f, lives, 1e-4f);

            // 長い待機(0.3)で悪化 → 効果は 0 に沈む（救えたはずが間に合わない）
            float waiting = MedicalTriageRules.WaitingDeterioration(0.3f, 0.8f, P); // 0.24
            Assert.AreEqual(0.24f, waiting, 1e-4f);

            float eff = MedicalTriageRules.TriageEffectiveness(lives, waiting, P);  // 0.012348-0.24 → 0
            Assert.AreEqual(0f, eff, 1e-4f);
        }
    }
}
