using NUnit.Framework;
using UnityEngine;

namespace Ginei.Tests
{
    /// <summary>
    /// 諸兵科連合（戦闘艦種の組合せ相乗）の純ロジック検証。既定 Params で期待値固定。
    /// </summary>
    public class CombinedArmsRulesTests
    {
        const float Eps = 1e-4f;
        const float PowEps = 1e-3f; // Sqrt（幾何平均）箇所のみ緩める

        [Test]
        public void Balance_Even_IsOne_Mono_IsZero()
        {
            Assert.AreEqual(1f, CombinedArmsRules.Balance(1f / 3f, 1f / 3f, 1f / 3f), Eps);
            Assert.AreEqual(0f, CombinedArmsRules.Balance(1f, 0f, 0f), Eps);
            // 二艦種半々は中間（0.5）。
            Assert.AreEqual(0.5f, CombinedArmsRules.Balance(0.5f, 0.5f, 0f), Eps);
        }

        [Test]
        public void Balance_NormalizesAndHandlesZeroSum()
        {
            // 合計0は均等扱い＝完全バランス。
            Assert.AreEqual(1f, CombinedArmsRules.Balance(0f, 0f, 0f), Eps);
            // スケール不変（2,2,2 は 1/3,1/3,1/3 と同じ）。
            Assert.AreEqual(CombinedArmsRules.Balance(1f / 3f, 1f / 3f, 1f / 3f),
                            CombinedArmsRules.Balance(2f, 2f, 2f), Eps);
        }

        [Test]
        public void SynergyBonus_ScalesWithBalance()
        {
            Assert.AreEqual(0.30f, CombinedArmsRules.SynergyBonus(1f), Eps);
            Assert.AreEqual(0.15f, CombinedArmsRules.SynergyBonus(0.5f), Eps);
            Assert.AreEqual(0f, CombinedArmsRules.SynergyBonus(0f), Eps);
        }

        [Test]
        public void RoleCoverage_CountsPresentRoles()
        {
            Assert.AreEqual(1f, CombinedArmsRules.RoleCoverage(1f / 3f, 1f / 3f, 1f / 3f), Eps);
            Assert.AreEqual(1f / 3f, CombinedArmsRules.RoleCoverage(1f, 0f, 0f), Eps);
            Assert.AreEqual(2f / 3f, CombinedArmsRules.RoleCoverage(0.5f, 0.5f, 0f), Eps);
        }

        [Test]
        public void WeaknessExposure_GrowsWithMissingRoles()
        {
            Assert.AreEqual(0f, CombinedArmsRules.WeaknessExposure(0), Eps);
            Assert.AreEqual(0.2f, CombinedArmsRules.WeaknessExposure(1), Eps);
            Assert.AreEqual(0.6f, CombinedArmsRules.WeaknessExposure(3), Eps);
            // クランプ（範囲外は3扱い）。
            Assert.AreEqual(0.6f, CombinedArmsRules.WeaknessExposure(5), Eps);
        }

        [Test]
        public void ScreenAndStrike_BalancedPairPeaks()
        {
            Assert.AreEqual(1f, CombinedArmsRules.ScreenAndStrike(0.5f, 0.5f), PowEps);
            Assert.AreEqual(0f, CombinedArmsRules.ScreenAndStrike(1f, 0f), PowEps);
            Assert.AreEqual(0.8f, CombinedArmsRules.ScreenAndStrike(0.2f, 0.8f), PowEps);
        }

        [Test]
        public void MonoculturePenalty_OnlyAboveThreshold()
        {
            Assert.AreEqual(0f, CombinedArmsRules.MonoculturePenalty(0.5f), Eps);   // 閾値0.6未満は無罰
            Assert.AreEqual(0.25f, CombinedArmsRules.MonoculturePenalty(1f), Eps);  // 完全単一で最大
            Assert.AreEqual(0.125f, CombinedArmsRules.MonoculturePenalty(0.8f), Eps); // (0.8-0.6)/0.4*0.25
        }

        [Test]
        public void CombinedEffectiveness_RewardsCoverage()
        {
            // 連携ボーナス0.30・役割充足1.0 → 1.30
            Assert.AreEqual(1.30f, CombinedArmsRules.CombinedEffectiveness(0.30f, 1f), Eps);
            // 同ボーナスでも役割充足が穴(1/3)だと連携しきれず低い → 1.2
            Assert.AreEqual(1.2f, CombinedArmsRules.CombinedEffectiveness(0.30f, 1f / 3f), Eps);
            // 連携無し＝基準1.0
            Assert.AreEqual(1f, CombinedArmsRules.CombinedEffectiveness(0f, 1f), Eps);
        }

        [Test]
        public void IsBalancedComposition_DefaultThreshold()
        {
            Assert.IsTrue(CombinedArmsRules.IsBalancedComposition(0.7f));
            Assert.IsFalse(CombinedArmsRules.IsBalancedComposition(0.5f));
            Assert.IsTrue(CombinedArmsRules.IsBalancedComposition(0.6f)); // ちょうど閾値は含む
        }

        // 物語テスト：バランス編成は連携ボーナスを得るが、単一艦種偏重は弱点を晒し連携が崩れる。
        [Test]
        public void Story_BalancedFleetOutperformsMonoculture()
        {
            // バランス艦隊（戦艦/巡航艦/駆逐艦が均等）
            float balBalance = CombinedArmsRules.Balance(0.34f, 0.33f, 0.33f);
            float balSynergy = CombinedArmsRules.SynergyBonus(balBalance);
            float balCoverage = CombinedArmsRules.RoleCoverage(0.34f, 0.33f, 0.33f);
            float balEff = CombinedArmsRules.CombinedEffectiveness(balSynergy, balCoverage);
            Assert.IsTrue(CombinedArmsRules.IsBalancedComposition(balBalance), "均等編成はバランス編成と判定される");

            // 戦艦単一艦隊（駆逐欠如＝前衛無し、巡航艦欠如＝汎用穴）
            float monoBalance = CombinedArmsRules.Balance(1f, 0f, 0f);
            float monoSynergy = CombinedArmsRules.SynergyBonus(monoBalance);
            float monoCoverage = CombinedArmsRules.RoleCoverage(1f, 0f, 0f);
            float monoEff = CombinedArmsRules.CombinedEffectiveness(monoSynergy, monoCoverage);
            float monoWeakness = CombinedArmsRules.WeaknessExposure(2); // 巡航艦・駆逐の2役割欠如
            float monoPenalty = CombinedArmsRules.MonoculturePenalty(1f);
            float monoScreen = CombinedArmsRules.ScreenAndStrike(0f, 1f); // 駆逐前衛が無く前衛打撃連携が崩れる

            // バランス編成のほうが実効戦闘力で勝る
            Assert.Greater(balEff, monoEff);
            // 単一艦種は弱点を晒し、偏重ペナルティを負い、前衛打撃連携が成立しない
            Assert.Greater(monoWeakness, 0f);
            Assert.Greater(monoPenalty, 0f);
            Assert.AreEqual(0f, monoScreen, PowEps);
            // バランス艦隊は連携ボーナスで基準1.0を超える
            Assert.Greater(balEff, 1f);
        }
    }
}
