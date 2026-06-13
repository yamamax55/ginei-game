using System.Collections.Generic;
using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>軍団陣形の配置：前線適性（戦闘力/功名心/士気）・軍団長の見立て（能力で精度）・前線志願。</summary>
    public class CorpsDeploymentRulesTests
    {
        [Test]
        public void FrontSuitability_MonotonicInEachInput()
        {
            float baseScore = CorpsDeploymentRules.FrontSuitability(50f, 50f, 0.5f);
            Assert.Greater(CorpsDeploymentRules.FrontSuitability(90f, 50f, 0.5f), baseScore); // 戦闘力↑
            Assert.Greater(CorpsDeploymentRules.FrontSuitability(50f, 90f, 0.5f), baseScore); // 功名心↑
            Assert.Greater(CorpsDeploymentRules.FrontSuitability(50f, 50f, 0.9f), baseScore); // 士気↑
        }

        [Test]
        public void IsFrontVolunteer_HighAmbition()
        {
            Assert.IsTrue(CorpsDeploymentRules.IsFrontVolunteer(70f));
            Assert.IsTrue(CorpsDeploymentRules.IsFrontVolunteer(95f));
            Assert.IsFalse(CorpsDeploymentRules.IsFrontVolunteer(50f));
        }

        [Test]
        public void PerceivedSuitability_SkillBlendsTrueAndRoll()
        {
            Assert.AreEqual(0.8f, CorpsDeploymentRules.PerceivedSuitability(0.8f, 1f, 0.1f), 1e-4f); // 有能＝真値
            Assert.AreEqual(0.1f, CorpsDeploymentRules.PerceivedSuitability(0.8f, 0f, 0.1f), 1e-4f); // 無能＝当て推量
            Assert.AreEqual(0.45f, CorpsDeploymentRules.PerceivedSuitability(0.8f, 0.5f, 0.1f), 1e-4f); // 中間
        }

        [Test]
        public void OrderFrontToBack_CompetentCommanderPutsStrongFleetsFront()
        {
            var cands = new List<DeploymentCandidate>
            {
                new DeploymentCandidate(1, 30f, 50f, 0.5f),  // 弱兵
                new DeploymentCandidate(2, 90f, 50f, 0.5f),  // 強兵
                new DeploymentCandidate(3, 60f, 50f, 0.5f),  // 中
            };
            // 有能な軍団長(skill=1)＝roll 無視で真値どおり：強兵2→中3→弱兵1
            int[] order = CorpsDeploymentRules.OrderFrontToBack(cands, 1f, _ => 0.5f);
            Assert.AreEqual(new[] { 2, 3, 1 }, order);
        }

        [Test]
        public void OrderFrontToBack_MeritSeekerPushesToFront()
        {
            var cands = new List<DeploymentCandidate>
            {
                new DeploymentCandidate(1, 60f, 10f, 0.5f),  // 戦闘力やや上だが功名心低
                new DeploymentCandidate(2, 55f, 95f, 0.5f),  // 功名心が非常に高い＝前線志願
            };
            int[] order = CorpsDeploymentRules.OrderFrontToBack(cands, 1f, _ => 0.5f);
            Assert.AreEqual(2, order[0]); // 功を求める提督が前線へ
        }

        [Test]
        public void OrderFrontToBack_IncompetentCommanderFollowsRoll()
        {
            var cands = new List<DeploymentCandidate>
            {
                new DeploymentCandidate(1, 90f, 50f, 0.5f),  // 真値は強兵
                new DeploymentCandidate(2, 30f, 50f, 0.5f),  // 真値は弱兵
            };
            // 無能(skill=0)＝roll に流される。roll で弱兵2を高く評価→誤って前へ。
            int[] order = CorpsDeploymentRules.OrderFrontToBack(cands, 0f, id => id == 2 ? 0.9f : 0.1f);
            Assert.AreEqual(2, order[0]); // 弱兵を前に出す誤配置
        }
    }
}
