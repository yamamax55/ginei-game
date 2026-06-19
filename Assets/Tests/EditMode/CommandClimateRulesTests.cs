using NUnit.Framework;
using UnityEngine;

namespace Ginei.Tests
{
    public class CommandClimateRulesTests
    {
        const float Eps = 1e-4f;

        [Test]
        public void TrustLevelWeightsIntegrityAndHardship()
        {
            // 誠実0.6/苦楽0.4 の加重和：0.8*0.6 + 0.6*0.4 = 0.72。
            Assert.AreEqual(0.72f, CommandClimateRules.TrustLevel(80f, 60f), Eps);
            // 中庸はそのまま 0.5。
            Assert.AreEqual(0.5f, CommandClimateRules.TrustLevel(50f, 50f), Eps);
            // 満点同士で 1.0、クランプも確認（入力過大）。
            Assert.AreEqual(1f, CommandClimateRules.TrustLevel(100f, 100f), Eps);
            Assert.AreEqual(1f, CommandClimateRules.TrustLevel(200f, 200f), Eps);
        }

        [Test]
        public void CohesionGrowsWithTrustAndTime()
        {
            // timeToBond=24：信頼1.0で時間満ちれば 1.0、半分なら 0.5。
            Assert.AreEqual(1f, CommandClimateRules.Cohesion(1f, 24f), Eps);
            Assert.AreEqual(0.5f, CommandClimateRules.Cohesion(1f, 12f), Eps);
            // 信頼0.8×時間半分=0.4。時間過多は頭打ち（1.0で飽和）。
            Assert.AreEqual(0.4f, CommandClimateRules.Cohesion(0.8f, 12f), Eps);
            Assert.AreEqual(0.8f, CommandClimateRules.Cohesion(0.8f, 1000f), Eps);
        }

        [Test]
        public void InitiativeNeedsTrustAndToleranceForMistakes()
        {
            // 信頼1.0×寛容満点=1.0、寛容ゼロでも下限0.5は出る。
            Assert.AreEqual(1f, CommandClimateRules.Initiative(1f, 100f), Eps);
            Assert.AreEqual(0.5f, CommandClimateRules.Initiative(1f, 0f), Eps);
            // 信頼0.72×(0.5+0.5*0.5)=0.72*0.75=0.54。
            Assert.AreEqual(0.54f, CommandClimateRules.Initiative(0.72f, 50f), Eps);
        }

        [Test]
        public void FearVsRespectIsSignedDifference()
        {
            // 純然たる恐怖支配=-1、純然たる敬服=+1、拮抗=0。
            Assert.AreEqual(-1f, CommandClimateRules.FearVsRespect(100f, 0f), Eps);
            Assert.AreEqual(1f, CommandClimateRules.FearVsRespect(0f, 100f), Eps);
            Assert.AreEqual(0f, CommandClimateRules.FearVsRespect(50f, 50f), Eps);
            // 敬意80・威圧20 → +0.6。
            Assert.AreEqual(0.6f, CommandClimateRules.FearVsRespect(20f, 80f), Eps);
        }

        [Test]
        public void BrittlenessRisesWithFear()
        {
            // 恐怖一色(-1)で最大0.8、敬服(+1)で0、中立(0)で0.4。
            Assert.AreEqual(0.8f, CommandClimateRules.BrittlenessFromFear(-1f), Eps);
            Assert.AreEqual(0f, CommandClimateRules.BrittlenessFromFear(1f), Eps);
            Assert.AreEqual(0.4f, CommandClimateRules.BrittlenessFromFear(0f), Eps);
        }

        [Test]
        public void MoraleFoundationSupportedByClimate()
        {
            // 信頼・結束満ちれば1.0、最悪でも下限0.3、半々で0.65。
            Assert.AreEqual(1f, CommandClimateRules.MoraleFoundation(1f, 1f), Eps);
            Assert.AreEqual(0.3f, CommandClimateRules.MoraleFoundation(0f, 0f), Eps);
            Assert.AreEqual(0.65f, CommandClimateRules.MoraleFoundation(0.5f, 0.5f), Eps);
        }

        [Test]
        public void ToxicLeadershipNeedsCoercionAndDishonesty()
        {
            // 高圧×不誠実で最大0.6、誠実なら高圧でも蝕まれない。
            Assert.AreEqual(0.6f, CommandClimateRules.ToxicLeadershipPenalty(100f, 0f), Eps);
            Assert.AreEqual(0f, CommandClimateRules.ToxicLeadershipPenalty(100f, 100f), Eps);
            // 高圧80×誠実50：0.8*(1-0.5)*0.6 = 0.24。
            Assert.AreEqual(0.24f, CommandClimateRules.ToxicLeadershipPenalty(80f, 50f), Eps);
        }

        [Test]
        public void IsHealthyClimateChecksTrustThreshold()
        {
            Assert.IsTrue(CommandClimateRules.IsHealthyClimate(0.7f, 0.6f));
            Assert.IsFalse(CommandClimateRules.IsHealthyClimate(0.5f, 0.6f));
            // しきい値ちょうどは健全。
            Assert.IsTrue(CommandClimateRules.IsHealthyClimate(0.6f, 0.6f));
        }

        [Test]
        public void TrustAndSharedHardshipBreedCohesionWhileFearStaysBrittle()
        {
            // 物語：誠実で苦楽を共にする指揮官の部隊は、信頼が結束と自発性を生む。
            float goodTrust = CommandClimateRules.TrustLevel(90f, 85f);   // 0.9*0.6+0.85*0.4=0.88
            float goodCohesion = CommandClimateRules.Cohesion(goodTrust, 24f);
            float goodInitiative = CommandClimateRules.Initiative(goodTrust, 80f);
            float goodFoundation = CommandClimateRules.MoraleFoundation(goodTrust, goodCohesion);

            // 恐怖で縛る指揮官：威圧的・不誠実で、形だけの服従＝逆境で崩れる脆さ。
            float fearTrust = CommandClimateRules.TrustLevel(20f, 10f);   // 0.2*0.6+0.1*0.4=0.16
            float fearCohesion = CommandClimateRules.Cohesion(fearTrust, 24f);
            float fvr = CommandClimateRules.FearVsRespect(90f, 15f);      // 0.15-0.9=-0.75
            float brittle = CommandClimateRules.BrittlenessFromFear(fvr);
            float fearFoundation = CommandClimateRules.MoraleFoundation(fearTrust, fearCohesion);

            // 良い風土は健全・結束も自発性も高い。
            Assert.IsTrue(CommandClimateRules.IsHealthyClimate(goodTrust, 0.6f));
            Assert.Greater(goodCohesion, fearCohesion);
            Assert.Greater(goodInitiative, 0.7f);

            // 恐怖支配は不健全・脆く・士気の土壌も痩せる。
            Assert.IsFalse(CommandClimateRules.IsHealthyClimate(fearTrust, 0.6f));
            Assert.Greater(brittle, 0.5f);
            Assert.Greater(goodFoundation, fearFoundation);
        }
    }
}
