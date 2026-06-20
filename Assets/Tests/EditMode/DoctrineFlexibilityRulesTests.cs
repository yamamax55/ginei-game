using NUnit.Framework;
using Ginei;
using UnityEngine;

namespace Ginei.Tests
{
    /// <summary>
    /// ドクトリンの柔軟性 vs 硬直（規律と即応のトレードオフ）の純ロジック。既定 Params で期待値固定。
    /// FormationDoctrineRules（陣形自動切替）とは別＝組織特性／MissionCommandRules（任務戦術）の前提。
    /// </summary>
    public class DoctrineFlexibilityRulesTests
    {
        private const float Eps = 1e-4f;

        [Test]
        public void Adaptability_FlexibleWithInitiative_Combines()
        {
            // 0.8*(1-0.5+0.5*0.6)=0.8*0.8=0.64
            Assert.AreEqual(0.64f, DoctrineFlexibilityRules.Adaptability(0.8f, 0.6f), Eps);
            // 硬直（柔軟0）は主導性が高くても適応力0。
            Assert.AreEqual(0f, DoctrineFlexibilityRules.Adaptability(0f, 1f), Eps);
        }

        [Test]
        public void RigidityStrength_FloorAndFull()
        {
            Assert.AreEqual(0.3f, DoctrineFlexibilityRules.RigidityStrength(0f), Eps);   // 下限
            Assert.AreEqual(1f, DoctrineFlexibilityRules.RigidityStrength(1f), Eps);     // 完全硬直＝確実性1
            Assert.AreEqual(0.65f, DoctrineFlexibilityRules.RigidityStrength(0.5f), Eps); // 0.3+0.7*0.5
        }

        [Test]
        public void NovelSituationPenalty_RigidArmyFailsOnNovelty()
        {
            Assert.AreEqual(0.72f, DoctrineFlexibilityRules.NovelSituationPenalty(0.9f, 0.8f), Eps);
            Assert.AreEqual(0f, DoctrineFlexibilityRules.NovelSituationPenalty(0f, 1f), Eps); // 柔軟なら新奇でも平気
        }

        [Test]
        public void CoordinationCost_AcceleratesWithFlexibility()
        {
            Assert.AreEqual(0.25f, DoctrineFlexibilityRules.CoordinationCost(0.5f), Eps); // pow(0.5,2)
            Assert.AreEqual(1f, DoctrineFlexibilityRules.CoordinationCost(1f), Eps);
            Assert.AreEqual(0f, DoctrineFlexibilityRules.CoordinationCost(0f), Eps); // 硬直は統一が取れコスト0
        }

        [Test]
        public void DoctrineMatch_AlignedIsBestMismatchIsLow()
        {
            Assert.AreEqual(1f, DoctrineFlexibilityRules.DoctrineMatch(1, 1), Eps);
            Assert.AreEqual(0.25f, DoctrineFlexibilityRules.DoctrineMatch(0, 3, 4), Eps); // 1-3/4
        }

        [Test]
        public void LearningRate_NeedsBothFlexibilityAndExperience()
        {
            Assert.AreEqual(0.4f, DoctrineFlexibilityRules.LearningRate(0.8f, 0.5f), Eps);
            Assert.AreEqual(0f, DoctrineFlexibilityRules.LearningRate(0f, 1f), Eps); // 硬直軍は経験を積んでも学ばない
        }

        [Test]
        public void OptimalFlexibility_VolatileBattlefieldWithGoodCommandFavorsFlexibility()
        {
            // 1*0.8*1=0.8（荒れた戦場×名将＝柔軟を志向）
            Assert.AreEqual(0.8f, DoctrineFlexibilityRules.OptimalFlexibility(1f, 1f), Eps);
            // 0.2*0.8*0.5=0.08（静的な戦場×凡将＝規律寄り）
            Assert.AreEqual(0.08f, DoctrineFlexibilityRules.OptimalFlexibility(0.2f, 0.5f), Eps);
        }

        // 物語：硬直軍は規律ゆえ統一性が高いが新奇な事態で機能不全に陥る。柔軟軍は不意に強い半面、
        // まとめるのに統制コスト（統率）を要する。同じ戦場で両者の性格が分かれる。
        [Test]
        public void Story_RigidIsDisciplinedButBrittle_FlexibleIsAdaptiveButCostly()
        {
            float novelty = 0.9f; // 前例のない事態

            // 硬直軍：統一性は高いが、新奇な状況で教条主義的失敗に陥る。
            float rigidStrength = DoctrineFlexibilityRules.RigidityStrength(1f);
            Assert.AreEqual(1f, rigidStrength, Eps);
            Assert.IsTrue(DoctrineFlexibilityRules.IsDoctrinaireFailure(1f, novelty)); // penalty 0.9>=0.5

            // 柔軟軍：同じ新奇な状況に強く適応する（主導性が活きる）が、統制コスト（散漫リスク）を払う。
            float flexAdapt = DoctrineFlexibilityRules.Adaptability(1f, 1f);
            Assert.AreEqual(1f, flexAdapt, Eps);
            Assert.IsFalse(DoctrineFlexibilityRules.IsDoctrinaireFailure(0f, novelty)); // 柔軟は機能不全に陥らない
            Assert.Greater(DoctrineFlexibilityRules.CoordinationCost(1f), 0f); // 柔軟は統率を要する

            // 結論：硬直の適応力 < 柔軟の適応力（即応はトレードオフで柔軟側にある）。
            float rigidAdapt = DoctrineFlexibilityRules.Adaptability(0f, 1f);
            Assert.Less(rigidAdapt, flexAdapt);
        }
    }
}
