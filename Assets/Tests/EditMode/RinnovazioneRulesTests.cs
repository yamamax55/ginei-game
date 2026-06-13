using NUnit.Framework;
using UnityEngine;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// リノヴァツィオーネ（刷新＝初心への回帰・マキャヴェッリ DISC-3 #1488）を固定する：制度は古く改革を
    /// 怠るほど疲弊し（放置すると腐る）、疲弊×危機が刷新ウィンドウを開き（危機が改革を可能にする）、
    /// 始原回帰は創設時の徳へ立ち返り（初心に戻ると若返る）、予防的刷新は規則性に比例して疲弊を削り、
    /// 刷新の契機は危機・傑物・良法の最大、疲弊が閾値超えで手遅れ（予防を逃すと革命）、コストは疲弊そのもの、
    /// 創設時の徳を取り戻せば刷新済み。物語テスト＝予防刷新を怠れば疲弊が募り革命でしか直せなくなる。
    /// クランプを担保。
    /// </summary>
    public class RinnovazioneRulesTests
    {
        private static readonly RinnovazioneParams P = RinnovazioneParams.Default;
        // 基礎疲弊0.02/年齢増幅0.05/放置増幅0.05/窓係数1.0/予防刷新0.1/手遅れ閾値0.8/刷新判定0.85

        [Test]
        public void InstitutionalFatigue_OldAndNeglectedAccumulatesMore()
        {
            // 古い制度(1)＋改革放置(1)：rate=0.02+0.05+0.05=0.12 → dt1で0.12
            Assert.AreEqual(0.12f, RinnovazioneRules.InstitutionalFatigue(1f, 1f, 1f, P), 1e-5f);
            // 建国直後（年齢0・放置0）でも基礎ぶんは疲弊：0.02
            Assert.AreEqual(0.02f, RinnovazioneRules.InstitutionalFatigue(0f, 0f, 1f, P), 1e-5f);
            // 怠るほど速い＝放置が疲弊を増やす
            float diligent = RinnovazioneRules.InstitutionalFatigue(1f, 0f, 1f, P);
            float neglected = RinnovazioneRules.InstitutionalFatigue(1f, 1f, 1f, P);
            Assert.Greater(neglected, diligent);
        }

        [Test]
        public void RenewalWindow_FatigueAndCrisisOpenTheWindow()
        {
            // 疲弊0.8×危機0.5×1.0=0.4＝危機が刷新を可能にする
            Assert.AreEqual(0.4f, RinnovazioneRules.RenewalWindow(0.8f, 0.5f, P), 1e-5f);
            // 危機がなければ窓は開かない（平時の安泰）
            Assert.AreEqual(0f, RinnovazioneRules.RenewalWindow(0.9f, 0f, P), 1e-5f);
            // 疲弊がなければ窓は開かない
            Assert.AreEqual(0f, RinnovazioneRules.RenewalWindow(0f, 1f, P), 1e-5f);
        }

        [Test]
        public void ReturnToOrigins_MovesTowardFoundingVirtue()
        {
            // 現在徳0.3から創設時0.9へ努力0.5ぶん回帰：0.3+(0.9-0.3)*0.5=0.6
            Assert.AreEqual(0.6f, RinnovazioneRules.ReturnToOrigins(0.3f, 0.9f, 0.5f), 1e-5f);
            // 努力0＝据え置き（自然には戻らない）
            Assert.AreEqual(0.3f, RinnovazioneRules.ReturnToOrigins(0.3f, 0.9f, 0f), 1e-5f);
            // 努力1＝創設時の徳へ完全回帰（始原＝上限）
            Assert.AreEqual(0.9f, RinnovazioneRules.ReturnToOrigins(0.3f, 0.9f, 1f), 1e-5f);
        }

        [Test]
        public void PreventiveRenewalTick_RegularRenewalShavesFatigue()
        {
            // 疲弊0.5・規則性1：0.5-0.1×1×1=0.4＝危機を待たず若返らせる
            Assert.AreEqual(0.4f, RinnovazioneRules.PreventiveRenewalTick(0.5f, 1f, 1f, P), 1e-5f);
            // 規則性0＝削れない（刷新を怠れば疲弊は溜まる一方）
            Assert.AreEqual(0.5f, RinnovazioneRules.PreventiveRenewalTick(0.5f, 0f, 1f, P), 1e-5f);
            // 下限0でクランプ
            Assert.AreEqual(0f, RinnovazioneRules.PreventiveRenewalTick(0.05f, 1f, 1f, P), 1e-5f);
        }

        [Test]
        public void RenewalTrigger_StrongestOfThreeMeans()
        {
            // 危機0.3・傑物0.9・良法0.5＝最大の0.9（傑出した個人が刷新を起こす）
            Assert.AreEqual(0.9f, RinnovazioneRules.RenewalTrigger(0.3f, 0.9f, 0.5f), 1e-5f);
            // 良法だけが強い＝良法で刷新
            Assert.AreEqual(0.8f, RinnovazioneRules.RenewalTrigger(0.1f, 0.2f, 0.8f), 1e-5f);
            // 三つとも弱ければ刷新の力も弱い
            Assert.AreEqual(0.2f, RinnovazioneRules.RenewalTrigger(0.1f, 0.2f, 0.15f), 1e-5f);
        }

        [Test]
        public void OverdueRenewalRisk_PastThresholdNeedsRevolution()
        {
            // 疲弊0.85＞0.8＝手遅れ＝暴力的崩壊でしか直せない
            Assert.IsTrue(RinnovazioneRules.OverdueRenewalRisk(0.85f, P.overdueThreshold));
            // 疲弊0.5＜0.8＝まだ予防で救える
            Assert.IsFalse(RinnovazioneRules.OverdueRenewalRisk(0.5f, P.overdueThreshold));
            // ちょうど閾値0.8＝手遅れ（境界）
            Assert.IsTrue(RinnovazioneRules.OverdueRenewalRisk(0.8f, P.overdueThreshold));
        }

        [Test]
        public void RenewalCost_DeeperFatigueCostsMore()
        {
            // 疲弊そのものがコスト＝早く手を打つほど安い
            Assert.AreEqual(0.2f, RinnovazioneRules.RenewalCost(0.2f), 1e-5f);
            Assert.AreEqual(0.9f, RinnovazioneRules.RenewalCost(0.9f), 1e-5f);
            // クランプ
            Assert.AreEqual(1f, RinnovazioneRules.RenewalCost(1.5f), 1e-5f);
            Assert.AreEqual(0f, RinnovazioneRules.RenewalCost(-0.5f), 1e-5f);
        }

        [Test]
        public void IsRenewedInstitution_RecoversFoundingVirtue()
        {
            // 創設時0.9×0.85=0.765 を現在徳0.8が超える＝刷新済み（初心を取り戻した）
            Assert.IsTrue(RinnovazioneRules.IsRenewedInstitution(0.8f, 0.9f, 0.85f, P));
            // 現在徳0.5は閾値0.765に届かない＝未刷新
            Assert.IsFalse(RinnovazioneRules.IsRenewedInstitution(0.5f, 0.9f, 0.85f, P));
            // 創設時の徳が0＝取り戻すべき始原がない＝成立しない
            Assert.IsFalse(RinnovazioneRules.IsRenewedInstitution(1f, 0f, 0.85f, P));
        }

        [Test]
        public void Story_NeglectBreedsRevolution_PreventionSaves()
        {
            // 古い制度(年齢0.9)を改革放置(1.0)で運用すると疲弊が募り、やがて手遅れ＝革命でしか直せなくなる。
            float neglectedFatigue = 0f;
            bool neglectOverdue = false;
            for (int t = 0; t < 12; t++)
            {
                neglectedFatigue = Mathf.Clamp01(
                    neglectedFatigue + RinnovazioneRules.InstitutionalFatigue(0.9f, 1f, 1f, P));
                if (RinnovazioneRules.OverdueRenewalRisk(neglectedFatigue, P.overdueThreshold))
                {
                    neglectOverdue = true;
                    break;
                }
            }
            // 放置は手遅れ（革命）に至る
            Assert.IsTrue(neglectOverdue);

            // 同じ古い制度でも、規則的な予防刷新を併用すれば疲弊が積み上がらず手遅れを免れる。
            float preventedFatigue = 0f;
            bool preventOverdue = false;
            for (int t = 0; t < 12; t++)
            {
                preventedFatigue = Mathf.Clamp01(
                    preventedFatigue + RinnovazioneRules.InstitutionalFatigue(0.9f, 1f, 1f, P));
                // 危機を待たず毎期規則的に初心へ立ち返る
                preventedFatigue = RinnovazioneRules.PreventiveRenewalTick(preventedFatigue, 1f, 1f, P);
                if (RinnovazioneRules.OverdueRenewalRisk(preventedFatigue, P.overdueThreshold))
                {
                    preventOverdue = true;
                    break;
                }
            }
            // 予防的刷新は暴力的崩壊を防ぐ＝初心に立ち返り続ければ若返り続ける
            Assert.IsFalse(preventOverdue);
            Assert.Less(preventedFatigue, neglectedFatigue);
        }
    }
}
