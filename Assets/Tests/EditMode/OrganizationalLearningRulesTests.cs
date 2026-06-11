using NUnit.Framework;
using UnityEngine;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 組織学習能力（SHP-2 #1375・『失敗の本質』型）の純ロジック検証。
    /// 失敗分析能力・敗北からの学習・ドクトリン改善・失敗の神話化・反復失敗リスク・
    /// シングルvsダブルループ・適応の優位・学習する組織判定を既定Params具体値で固定する。
    /// </summary>
    public class OrganizationalLearningRulesTests
    {
        const float Eps = 1e-4f;

        /// <summary>失敗分析能力＝自己批判×率直さ×analysisScale(1.0)。どちらか欠ければ直視されない（積）。</summary>
        [Test]
        public void FailureAnalysisCapacity_自己批判と率直さの積()
        {
            // 0.8×0.5×1.0 = 0.4
            Assert.AreEqual(0.4f, OrganizationalLearningRules.FailureAnalysisCapacity(0.8f, 0.5f), Eps);
            // 率直さ0なら直視されない
            Assert.AreEqual(0f, OrganizationalLearningRules.FailureAnalysisCapacity(0.9f, 0f), Eps);
            // 両方満点で最大
            Assert.AreEqual(1f, OrganizationalLearningRules.FailureAnalysisCapacity(1f, 1f), Eps);
        }

        /// <summary>敗北からの学習＝深刻さ×分析能力×learningScale(1.0)。痛い敗北を分析できれば学びが大きい。</summary>
        [Test]
        public void LearningFromDefeat_深刻な敗北を分析できれば学びが大きい()
        {
            // 0.9×0.8×1.0 = 0.72
            Assert.AreEqual(0.72f, OrganizationalLearningRules.LearningFromDefeat(0.9f, 0.8f), Eps);
            // 分析できなければ無駄に終わる
            Assert.AreEqual(0f, OrganizationalLearningRules.LearningFromDefeat(1f, 0f), Eps);
        }

        /// <summary>ドクトリン改善＝learning×adaptationRate(0.1)×dt ぶん上積み（教訓を制度化＝米軍型）。</summary>
        [Test]
        public void DoctrineAdaptationTick_学習が時間で作戦を改善する()
        {
            // 0.5 + 0.8×0.1×2 = 0.66
            Assert.AreEqual(0.66f, OrganizationalLearningRules.DoctrineAdaptationTick(0.5f, 0.8f, 2f), Eps);
            // 学習0なら改善しない
            Assert.AreEqual(0.5f, OrganizationalLearningRules.DoctrineAdaptationTick(0.5f, 0f, 5f), Eps);
            // 1で頭打ち
            Assert.AreEqual(1f, OrganizationalLearningRules.DoctrineAdaptationTick(0.95f, 1f, 100f), Eps);
        }

        /// <summary>失敗の神話化＝(1−分析能力)×体面圧力。糊塗できる組織は学ばない（日本軍型）。</summary>
        [Test]
        public void MythologizeFailure_分析能力が低く体面を守るほど神話で糊塗する()
        {
            // (1−0.2)×0.9 = 0.72
            Assert.AreEqual(0.72f, OrganizationalLearningRules.MythologizeFailure(0.2f, 0.9f), Eps);
            // 分析能力が満ちていれば糊塗しない
            Assert.AreEqual(0f, OrganizationalLearningRules.MythologizeFailure(1f, 1f), Eps);
        }

        /// <summary>反復失敗リスク＝硬直×(1−学習能力)。学習する組織は同じ過ちを避ける。</summary>
        [Test]
        public void RepeatedFailureRisk_硬直して学べないほど同じ失敗を繰り返す()
        {
            // 0.8×(1−0.25) = 0.6
            Assert.AreEqual(0.6f, OrganizationalLearningRules.RepeatedFailureRisk(0.8f, 0.25f), Eps);
            // 学習能力が満ちていればリスク0
            Assert.AreEqual(0f, OrganizationalLearningRules.RepeatedFailureRisk(1f, 1f), Eps);
        }

        /// <summary>シングルvsダブルループ＝表面修正に前提を問う根本学習(doubleLoopScale0.4)を上乗せ。</summary>
        [Test]
        public void SingleLoopVsDoubleLoop_前提を問い直すほど深く学ぶ()
        {
            // 0.5 + 0.5×0.4 = 0.7
            Assert.AreEqual(0.7f, OrganizationalLearningRules.SingleLoopVsDoubleLoop(0.5f, 0.5f), Eps);
            // 前提を問わなければ表面的修正のまま
            Assert.AreEqual(0.5f, OrganizationalLearningRules.SingleLoopVsDoubleLoop(0.5f, 0f), Eps);
        }

        /// <summary>適応の優位＝学習能力×戦争の長さ×adaptiveScale(0.5)。長期戦ほど学習する側が勝つ。</summary>
        [Test]
        public void AdaptiveAdvantage_長期戦ほど学習する組織が優位になる()
        {
            // 0.8×1.0×0.5 = 0.4（長期戦）
            Assert.AreEqual(0.4f, OrganizationalLearningRules.AdaptiveAdvantage(0.8f, 1f), Eps);
            // 短期決戦では差が出ない
            Assert.AreEqual(0f, OrganizationalLearningRules.AdaptiveAdvantage(0.8f, 0f), Eps);
            // 同じ学習能力でも長引くほど優位が増す
            Assert.Less(OrganizationalLearningRules.AdaptiveAdvantage(0.8f, 0.3f),
                OrganizationalLearningRules.AdaptiveAdvantage(0.8f, 0.9f));
        }

        /// <summary>学習する組織判定＝分析能力とドクトリン改善の双方が閾値(0.5)以上。糊塗組織は片方欠ける。</summary>
        [Test]
        public void IsLearningMilitary_分析でき改善できる組織のみ学習する組織()
        {
            // 双方が閾値以上＝米軍型（学習する組織）
            Assert.IsTrue(OrganizationalLearningRules.IsLearningMilitary(0.7f, 0.6f));
            // 分析できても改善できなければ偽
            Assert.IsFalse(OrganizationalLearningRules.IsLearningMilitary(0.9f, 0.3f));
            // 分析できなければ偽＝精神論で糊塗（日本軍型）
            Assert.IsFalse(OrganizationalLearningRules.IsLearningMilitary(0.2f, 0.9f));
        }
    }
}
