using NUnit.Framework;
using UnityEngine;

namespace Ginei.Tests
{
    public class VeteranReintegrationRulesTests
    {
        const float Eps = 1e-4f;

        [Test]
        public void ReintegrationDifficulty_DefaultExpected()
        {
            // 0.6*0.5 + 0.5*0.6 = 0.3 + 0.3 = 0.6
            float d = VeteranReintegrationRules.ReintegrationDifficulty(0.6f, 0.5f);
            Assert.AreEqual(0.6f, d, Eps);
        }

        [Test]
        public void EmploymentSupport_ProductWithWeight()
        {
            // 0.8*0.5*(1+0.55) = 0.4*1.55 = 0.62
            float e = VeteranReintegrationRules.EmploymentSupport(0.8f, 0.5f);
            Assert.AreEqual(0.62f, e, Eps);
        }

        [Test]
        public void EmploymentSupport_OneFactorZero_GivesZero()
        {
            // 機会ゼロなら制度があっても就労ゼロ（積）。
            float e = VeteranReintegrationRules.EmploymentSupport(1f, 0f);
            Assert.AreEqual(0f, e, Eps);
        }

        [Test]
        public void SocialAcceptance_GratitudeMinusStigma()
        {
            // 0.8*(1-0.25)*(0.5+0.45) = 0.8*0.75*0.95 = 0.6*0.95 = 0.57
            float s = VeteranReintegrationRules.SocialAcceptance(0.8f, 0.25f);
            Assert.AreEqual(0.57f, s, Eps);
        }

        [Test]
        public void SocialAcceptance_FullStigma_GivesZero()
        {
            // 全き汚名は感謝を打ち消す。
            float s = VeteranReintegrationRules.SocialAcceptance(1f, 1f);
            Assert.AreEqual(0f, s, Eps);
        }

        [Test]
        public void AdjustmentSuccess_SupportMinusDifficulty()
        {
            // EmploymentSupport(0.9,0.9)=0.81*1.55=1.2555→clamp01=1.0
            // SocialAcceptance(1,0)=1*1*0.95=0.95
            // ReintegrationDifficulty(0.2,0.1)=0.1+0.06=0.16
            // support=0.5*(1.0+0.95)=0.975 ; 0.975-0.16 = 0.815
            float emp = VeteranReintegrationRules.EmploymentSupport(0.9f, 0.9f);
            float acc = VeteranReintegrationRules.SocialAcceptance(1f, 0f);
            float diff = VeteranReintegrationRules.ReintegrationDifficulty(0.2f, 0.1f);
            float success = VeteranReintegrationRules.AdjustmentSuccess(emp, acc, diff);
            Assert.AreEqual(1f, emp, Eps);
            Assert.AreEqual(0.95f, acc, Eps);
            Assert.AreEqual(0.16f, diff, Eps);
            Assert.AreEqual(0.815f, success, Eps);
        }

        [Test]
        public void AdjustmentSuccess_DifficultyOverwhelms_ClampsToZero()
        {
            // support=0.5*(0.2+0.2)=0.2 ; diff=1 → 0.2-1=-0.8 → clamp01 → 0
            float success = VeteranReintegrationRules.AdjustmentSuccess(0.2f, 0.2f, 1f);
            Assert.AreEqual(0f, success, Eps);
        }

        [Test]
        public void VeteranGrievance_DefaultExpected()
        {
            // 0.7*0.7 + 0.6*0.5 = 0.49 + 0.30 = 0.79
            float g = VeteranReintegrationRules.VeteranGrievance(0.7f, 0.6f);
            Assert.AreEqual(0.79f, g, Eps);
        }

        [Test]
        public void Politicization_GrievanceTimesNumbers()
        {
            // 0.79*0.5*(1+0.6) = 0.395*1.6 = 0.632
            float pol = VeteranReintegrationRules.Politicization(0.79f, 0.5f);
            Assert.AreEqual(0.632f, pol, Eps);
        }

        [Test]
        public void ComradeNetworkSupport_Product()
        {
            // 0.8*0.6 = 0.48
            float c = VeteranReintegrationRules.ComradeNetworkSupport(0.8f, 0.6f);
            Assert.AreEqual(0.48f, c, Eps);
        }

        [Test]
        public void IsReintegrationStable_DefaultThreshold()
        {
            // 成功0.6>=0.5 かつ 政治化0.3<0.5 → 安定
            Assert.IsTrue(VeteranReintegrationRules.IsReintegrationStable(0.6f, 0.3f));
            // 政治化が高ければ不安定
            Assert.IsFalse(VeteranReintegrationRules.IsReintegrationStable(0.6f, 0.7f));
            // 成功が低ければ不安定
            Assert.IsFalse(VeteranReintegrationRules.IsReintegrationStable(0.3f, 0.1f));
        }

        [Test]
        public void Inputs_AreClamped()
        {
            // 範囲外入力でも破綻しない。
            float d = VeteranReintegrationRules.ReintegrationDifficulty(5f, -5f);
            Assert.AreEqual(0.5f, d, Eps); // 1*0.5 + 0*0.6
            float e = VeteranReintegrationRules.EmploymentSupport(-1f, 2f);
            Assert.AreEqual(0f, e, Eps);
        }

        [Test]
        public void Narrative_BetrayedVeterans_Politicize_WhileSupportedOnesStabilize()
        {
            // 物語：勝った側＝感謝と就労支援、敗れて見捨てられた側＝汚名と反故の約束。
            // 厚遇された退役兵：制度も機会も社会の感謝もあり、困難を支えが上回り社会復帰は安定する。
            float wellEmp = VeteranReintegrationRules.EmploymentSupport(0.9f, 0.85f);
            float wellAcc = VeteranReintegrationRules.SocialAcceptance(0.9f, 0.1f);
            float wellDiff = VeteranReintegrationRules.ReintegrationDifficulty(0.3f, 0.2f);
            float wellSuccess = VeteranReintegrationRules.AdjustmentSuccess(wellEmp, wellAcc, wellDiff);
            float wellGrievance = VeteranReintegrationRules.VeteranGrievance(1f - wellSuccess, 0.1f);
            float wellPol = VeteranReintegrationRules.Politicization(wellGrievance, 0.6f);
            Assert.IsTrue(VeteranReintegrationRules.IsReintegrationStable(wellSuccess, wellPol),
                "厚遇された退役兵は社会復帰が安定するはず");

            // 見捨てられた退役兵：支援薄く汚名を着せられ、約束は反故。適応に失敗し不満が募る。
            float lostEmp = VeteranReintegrationRules.EmploymentSupport(0.2f, 0.3f);
            float lostAcc = VeteranReintegrationRules.SocialAcceptance(0.3f, 0.8f);
            float lostDiff = VeteranReintegrationRules.ReintegrationDifficulty(0.8f, 0.8f);
            float lostSuccess = VeteranReintegrationRules.AdjustmentSuccess(lostEmp, lostAcc, lostDiff);
            float lostGrievance = VeteranReintegrationRules.VeteranGrievance(1f - lostSuccess, 0.9f);
            float lostPol = VeteranReintegrationRules.Politicization(lostGrievance, 0.8f);
            Assert.IsFalse(VeteranReintegrationRules.IsReintegrationStable(lostSuccess, lostPol),
                "見捨てられ約束を反故にされた退役兵は不安定（政治化）するはず");

            // 厚遇された側より見捨てられた側の方が政治化が高い。
            Assert.Greater(lostPol, wellPol);
            // 戦友会の支えは独立に効く（孤独をやわらげる紐帯）。
            float comrade = VeteranReintegrationRules.ComradeNetworkSupport(0.7f, 0.8f);
            Assert.Greater(comrade, 0f);
        }
    }
}
