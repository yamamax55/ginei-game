using NUnit.Framework;
using UnityEngine;

namespace Ginei.Tests
{
    /// <summary>拡大共和国の安定（マディソンのパラドックス #1485）の純ロジックテスト。</summary>
    public class ExtendedRepublicRulesTests
    {
        const float Eps = 0.0001f;

        /// <summary>利害の多様性＝版図と人口が大きいほど多元化（既定重み0.6/0.4で加重平均）。</summary>
        [Test]
        public void InterestDiversity_LargerTerritoryAndPopulationIsMoreDiverse()
        {
            // 版図1.0・人口1.0＝完全に多様
            Assert.AreEqual(1f, ExtendedRepublicRules.InterestDiversity(1f, 1f), Eps);
            // 版図0・人口0＝多様性ゼロ
            Assert.AreEqual(0f, ExtendedRepublicRules.InterestDiversity(0f, 0f), Eps);
            // 版図1.0・人口0＝0.6（版図重みのみ）
            Assert.AreEqual(0.6f, ExtendedRepublicRules.InterestDiversity(1f, 0f), Eps);
            // 大きい共和国の方が多様
            float large = ExtendedRepublicRules.InterestDiversity(0.9f, 0.9f);
            float small = ExtendedRepublicRules.InterestDiversity(0.2f, 0.2f);
            Assert.Greater(large, small);
        }

        /// <summary>多数派形成の困難＝多様性が高いほどまとまりにくい（単調増・冪で非線形）。</summary>
        [Test]
        public void MajorityFormationDifficulty_HigherDiversityIsHarder()
        {
            float low = ExtendedRepublicRules.MajorityFormationDifficulty(0.2f);
            float high = ExtendedRepublicRules.MajorityFormationDifficulty(0.8f);
            Assert.Greater(high, low);
            // 端点
            Assert.AreEqual(0f, ExtendedRepublicRules.MajorityFormationDifficulty(0f), Eps);
            Assert.AreEqual(1f, ExtendedRepublicRules.MajorityFormationDifficulty(1f), Eps);
            // 既定冪指数1.5＝0.25^(1/1.5)=0.3969...
            Assert.AreEqual(Mathf.Pow(0.25f, 1f / 1.5f),
                ExtendedRepublicRules.MajorityFormationDifficulty(0.25f), Eps);
        }

        /// <summary>派閥の中和＝多様性が高いほど脅威を薄める（拡大共和国の核）。</summary>
        [Test]
        public void FactionNeutralization_DiversityDampensThreat()
        {
            // 多様性1.0＝脅威がそのまま中和される（脅威0.8→0.8打ち消し）
            Assert.AreEqual(0.8f, ExtendedRepublicRules.FactionNeutralization(1f, 0.8f), Eps);
            // 多様性0＝中和なし（小共和国は派閥の害を防げない）
            Assert.AreEqual(0f, ExtendedRepublicRules.FactionNeutralization(0f, 0.8f), Eps);
            // 多様性0.5・脅威0.6＝0.3
            Assert.AreEqual(0.3f, ExtendedRepublicRules.FactionNeutralization(0.5f, 0.6f), Eps);
        }

        /// <summary>結託の困難＝広域＋高連絡コストで共謀が妨げられる（既定結託重み0.7）。</summary>
        [Test]
        public void CollusionDifficulty_WideAreaAndHighCostHindersCollusion()
        {
            // 版図1.0・コスト1.0＝0.7
            Assert.AreEqual(0.7f, ExtendedRepublicRules.CollusionDifficulty(1f, 1f), Eps);
            // 版図0＝結託困難なし（狭ければ結託容易）
            Assert.AreEqual(0f, ExtendedRepublicRules.CollusionDifficulty(0f, 1f), Eps);
            // 版図0.8・コスト0.5＝0.28
            Assert.AreEqual(0.28f, ExtendedRepublicRules.CollusionDifficulty(0.8f, 0.5f), Eps);
        }

        /// <summary>安定ボーナス＝派閥中和が安定に寄与し実効値≥1.0（既定上限0.5）。</summary>
        [Test]
        public void StabilityBonus_NeutralizationLiftsStability()
        {
            // 中和0＝1.0（無補正）
            Assert.AreEqual(1f, ExtendedRepublicRules.StabilityBonus(0f), Eps);
            // 中和1.0＝1.5（最大ボーナス）
            Assert.AreEqual(1.5f, ExtendedRepublicRules.StabilityBonus(1f), Eps);
            // 中和0.4＝1.2
            Assert.AreEqual(1.2f, ExtendedRepublicRules.StabilityBonus(0.4f), Eps);
        }

        /// <summary>規模vs統治＝統治能力が規模に追いつかないと割り引かれる（過拡張への接続）。</summary>
        [Test]
        public void ScaleVsCohesion_GovernanceMustKeepUpWithScale()
        {
            // 統治が規模以上＝1.0（規模の利点を活かせる）
            Assert.AreEqual(1f, ExtendedRepublicRules.ScaleVsCohesion(0.5f, 0.8f), Eps);
            Assert.AreEqual(1f, ExtendedRepublicRules.ScaleVsCohesion(0.7f, 0.7f), Eps);
            // 規模1.0・統治0.6＝不足0.4ぶん割り引き＝0.6
            Assert.AreEqual(0.6f, ExtendedRepublicRules.ScaleVsCohesion(1f, 0.6f), Eps);
        }

        /// <summary>代表制の濾過＝広域×代表制比率で派閥熱を冷ます（既定濾過0.5）。</summary>
        [Test]
        public void RepresentationFilter_RepublicCoolsFactionalHeat()
        {
            // 版図1.0・代表制1.0＝0.5
            Assert.AreEqual(0.5f, ExtendedRepublicRules.RepresentationFilter(1f, 1f), Eps);
            // 代表制0＝濾過なし（直接民主は派閥熱を冷ませない）
            Assert.AreEqual(0f, ExtendedRepublicRules.RepresentationFilter(1f, 0f), Eps);
            // 版図0.8・代表制0.5＝0.2
            Assert.AreEqual(0.2f, ExtendedRepublicRules.RepresentationFilter(0.8f, 0.5f), Eps);
        }

        /// <summary>拡大共和国の安定判定＝派閥中和と統治能力の双方が閾値以上で成立。</summary>
        [Test]
        public void IsStableExtendedRepublic_NeedsBothNeutralizationAndGovernance()
        {
            // 双方が閾値以上＝安定
            Assert.IsTrue(ExtendedRepublicRules.IsStableExtendedRepublic(0.7f, 0.7f, 0.5f));
            // 中和は高いが統治が及ばない＝不安定（大きすぎて統治不能）
            Assert.IsFalse(ExtendedRepublicRules.IsStableExtendedRepublic(0.9f, 0.3f, 0.5f));
            // 統治は及ぶが中和が低い＝不安定（小共和国は派閥に弱い）
            Assert.IsFalse(ExtendedRepublicRules.IsStableExtendedRepublic(0.3f, 0.9f, 0.5f));
            // 閾値ちょうど＝成立
            Assert.IsTrue(ExtendedRepublicRules.IsStableExtendedRepublic(0.5f, 0.5f, 0.5f));
        }
    }
}
