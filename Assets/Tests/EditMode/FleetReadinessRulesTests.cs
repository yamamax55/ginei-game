using NUnit.Framework;
using UnityEngine;

namespace Ginei.Tests
{
    /// <summary>
    /// 艦隊即応性（<see cref="FleetReadinessRules"/>）の純ロジックテスト。既定
    /// <see cref="FleetReadinessParams.Default"/> で期待値を固定（整備重み0.5・乗員重み0.5・準備基準2.0・
    /// 警戒下限0.1・消耗レート0.5・増勢上限0.5・即応しきい0.6）。Sqrt 箇所のみ許容差を緩める。
    /// </summary>
    public class FleetReadinessRulesTests
    {
        const float Eps = 1e-4f;
        const float SqrtEps = 1e-3f;

        [Test]
        public void MaterialReadiness_PulledTowardWorstFactor()
        {
            // weighted=0.5*1+0.5*0.5=0.75, worst=0.5 → lerp(0.5,0.75,0.5)=0.625
            Assert.AreEqual(0.625f, FleetReadinessRules.MaterialReadiness(1f, 0.5f), Eps);
            // 補給0なら物的は大きく落ちる：weighted=0.5, worst=0 → 0.25
            Assert.AreEqual(0.25f, FleetReadinessRules.MaterialReadiness(0f, 1f), Eps);
        }

        [Test]
        public void PersonnelReadiness_PulledTowardWorstFactor()
        {
            // weighted=0.5*1+0.5*0.6=0.8, worst=0.6 → lerp(0.6,0.8,0.5)=0.7
            Assert.AreEqual(0.7f, FleetReadinessRules.PersonnelReadiness(1f, 0.6f), Eps);
        }

        [Test]
        public void OverallReadiness_IsGeometricMean()
        {
            // sqrt(0.64*0.81)=sqrt(0.5184)=0.72
            Assert.AreEqual(0.72f, FleetReadinessRules.OverallReadiness(0.64f, 0.81f), SqrtEps);
            // 人的0なら総合0（艦が直っても人が居なければ出られない）
            Assert.AreEqual(0f, FleetReadinessRules.OverallReadiness(0.5f, 0f), Eps);
        }

        [Test]
        public void SortiePreparationTime_ShorterWhenReadyAndAlert()
        {
            // 2.0*(1-0.6)/0.5 = 1.6
            Assert.AreEqual(1.6f, FleetReadinessRules.SortiePreparationTime(0.6f, 0.5f), Eps);
            // 即応満タンなら0（即出撃）
            Assert.AreEqual(0f, FleetReadinessRules.SortiePreparationTime(1f, 0.5f), Eps);
            // 警戒0は下限0.1へ：2.0*1/0.1=20（発散しない）
            Assert.AreEqual(20f, FleetReadinessRules.SortiePreparationTime(0f, 0f), Eps);
        }

        [Test]
        public void StandbyAttrition_GrowsWithAlertAndDuration()
        {
            // 0.8*2.5*0.5=1.0（張り詰め続けの限界）
            Assert.AreEqual(1.0f, FleetReadinessRules.StandbyAttrition(0.8f, 2.5f), Eps);
            // 低警戒の待機は緩やか：0.2*1*0.5=0.1
            Assert.AreEqual(0.1f, FleetReadinessRules.StandbyAttrition(0.2f, 1f), Eps);
        }

        [Test]
        public void RotationCoverage_RatioClampedToTotal()
        {
            // 3/4=0.75
            Assert.AreEqual(0.75f, FleetReadinessRules.RotationCoverage(3, 4), Eps);
            // 即応数が総数超過しても頭打ち：4/4=1
            Assert.AreEqual(1f, FleetReadinessRules.RotationCoverage(5, 4), Eps);
            // 総数0は0（ゼロ除算回避）
            Assert.AreEqual(0f, FleetReadinessRules.RotationCoverage(2, 0), Eps);
        }

        [Test]
        public void SurgeCapacity_ScalesWithReadinessUpToMax()
        {
            // 1.0*0.5=0.5（満タンで最大増勢）
            Assert.AreEqual(0.5f, FleetReadinessRules.SurgeCapacity(1f), Eps);
            // 0.5*0.5=0.25
            Assert.AreEqual(0.25f, FleetReadinessRules.SurgeCapacity(0.5f), Eps);
            // 即応0なら無理は利かない
            Assert.AreEqual(0f, FleetReadinessRules.SurgeCapacity(0f), Eps);
        }

        [Test]
        public void IsCombatReady_HonorsThreshold()
        {
            // 既定しきい0.6：0.72は即応
            Assert.IsTrue(FleetReadinessRules.IsCombatReady(0.72f));
            // 0.5は足切り
            Assert.IsFalse(FleetReadinessRules.IsCombatReady(0.5f));
            // 明示しきいも反映
            Assert.IsTrue(FleetReadinessRules.IsCombatReady(0.5f, 0.4f));
        }

        [Test]
        public void Story_PeacetimeRotationVsWartimeSurge()
        {
            // 平時：低警戒で一部即応ローテ。整備十分・補給十分・乗員十分・士気平常。
            float matPeace = FleetReadinessRules.MaterialReadiness(0.9f, 0.9f);   // 0.9
            float perPeace = FleetReadinessRules.PersonnelReadiness(0.9f, 0.8f);  // weighted0.85,worst0.8→0.825
            float peace = FleetReadinessRules.OverallReadiness(matPeace, perPeace);
            Assert.IsTrue(FleetReadinessRules.IsCombatReady(peace), "平時でも要員ローテで即応は保たれる");

            // 平時は低警戒で長期待機しても消耗が緩やか＝持続できる
            float peaceAttrition = FleetReadinessRules.StandbyAttrition(0.2f, 3f); // 0.3
            Assert.Less(peaceAttrition, 0.5f, "低警戒の長期待機は消耗が緩い");

            // 半数だけ即応ローテに置いてもカバー率は半分は守れる
            Assert.AreEqual(0.5f, FleetReadinessRules.RotationCoverage(4, 8), Eps);

            // 戦時：高警戒に切替えると準備時間は短くなる（即応態勢が前進）
            float prepPeace = FleetReadinessRules.SortiePreparationTime(peace, 0.3f);
            float prepWar = FleetReadinessRules.SortiePreparationTime(peace, 0.9f);
            Assert.Less(prepWar, prepPeace, "高警戒ほど出撃準備が速い");

            // だが高警戒で張り詰め続ければ消耗が積もる（平時の緩い待機より大きい）
            float warAttrition = FleetReadinessRules.StandbyAttrition(0.9f, 3f);
            Assert.Greater(warAttrition, peaceAttrition, "戦時の高警戒待機は消耗が大きい");

            // 即応度が高ければ緊急増勢の余地もある＝無理が利く
            float surge = FleetReadinessRules.SurgeCapacity(peace);
            Assert.Greater(surge, 0f, "態勢が整っていれば緊急増勢が利く");
        }
    }
}
