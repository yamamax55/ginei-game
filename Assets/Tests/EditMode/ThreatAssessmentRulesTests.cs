using NUnit.Framework;
using UnityEngine;

namespace Ginei.Tests
{
    /// <summary>
    /// ThreatAssessmentRules（脅威評価＝AIの交戦/撤退判断）の EditMode テスト。
    /// 既定 Params で期待値を固定。
    /// </summary>
    public class ThreatAssessmentRulesTests
    {
        private const float Eps = 1e-4f;

        [Test]
        public void DistanceFactor_NearIsOne_FarIsFloor()
        {
            var p = ThreatAssessmentParams.Default; // range=12, floor=0.2
            Assert.AreEqual(1f, ThreatAssessmentRules.DistanceFactor(0f, p), Eps);
            Assert.AreEqual(0.2f, ThreatAssessmentRules.DistanceFactor(12f, p), Eps);
            // 遠方でも下限を割らない。
            Assert.AreEqual(0.2f, ThreatAssessmentRules.DistanceFactor(30f, p), Eps);
            // 中間（距離6）→ near=0.5 → Lerp(0.2,1,0.5)=0.6。
            Assert.AreEqual(0.6f, ThreatAssessmentRules.DistanceFactor(6f, p), Eps);
        }

        [Test]
        public void ThreatScore_NearEnemyMoreDangerous()
        {
            float near = ThreatAssessmentRules.ThreatScore(100f, 0f, 0f, false);
            float far = ThreatAssessmentRules.ThreatScore(100f, 12f, 0f, false);
            Assert.Greater(near, far);
            // near=100*1=100, far=100*0.2=20。
            Assert.AreEqual(100f, near, Eps);
            Assert.AreEqual(20f, far, Eps);
        }

        [Test]
        public void ThreatScore_FlankExposureIncreasesThreat()
        {
            float safe = ThreatAssessmentRules.ThreatScore(100f, 0f, 0f, false);
            float flanked = ThreatAssessmentRules.ThreatScore(100f, 0f, 1f, false);
            // flank: 1+0.5*1 = 1.5 倍。
            Assert.AreEqual(150f, flanked, Eps);
            Assert.Greater(flanked, safe);
        }

        [Test]
        public void ThreatScore_EngagedEnemyIsLessThreatening()
        {
            float free = ThreatAssessmentRules.ThreatScore(100f, 0f, 0f, false);
            float busy = ThreatAssessmentRules.ThreatScore(100f, 0f, 0f, true);
            // engagedRelief=0.6 → 60。
            Assert.AreEqual(60f, busy, Eps);
            Assert.Less(busy, free);
        }

        [Test]
        public void MoreDangerous_HigherThreatWins_TieByDistance()
        {
            Assert.AreEqual(-1, ThreatAssessmentRules.MoreDangerous(100f, 5f, 50f, 1f));
            Assert.AreEqual(1, ThreatAssessmentRules.MoreDangerous(50f, 1f, 100f, 5f));
            // 同点 → 近い方（A=距離2）危険。
            Assert.AreEqual(-1, ThreatAssessmentRules.MoreDangerous(80f, 2f, 80f, 9f));
            Assert.AreEqual(0, ThreatAssessmentRules.MoreDangerous(80f, 5f, 80f, 5f));
        }

        [Test]
        public void RetreatPressure_ZeroWhenNotOutnumbered_RampsWhenOverwhelmed()
        {
            var p = ThreatAssessmentParams.Default; // overwhelmRatio=1.5
            // 脅威=防御 → 比1.0 → 圧力0。
            Assert.AreEqual(0f, ThreatAssessmentRules.RetreatPressure(100f, 100f, p), Eps);
            // 比1.5 → 圧力1.0（劣勢上限）。
            Assert.AreEqual(1f, ThreatAssessmentRules.RetreatPressure(150f, 100f, p), Eps);
            // 比1.25（中間）→ 0.5。
            Assert.AreEqual(0.5f, ThreatAssessmentRules.RetreatPressure(125f, 100f, p), Eps);
            // 比2.0（超過）→ 1.0 クランプ。
            Assert.AreEqual(1f, ThreatAssessmentRules.RetreatPressure(200f, 100f, p), Eps);
        }

        [Test]
        public void IsOverwhelmed_AboveRatio()
        {
            // 既定 overwhelmRatio=1.5。
            Assert.IsFalse(ThreatAssessmentRules.IsOverwhelmed(140f, 100f));
            Assert.IsTrue(ThreatAssessmentRules.IsOverwhelmed(160f, 100f));
        }

        [Test]
        public void Params_ClampsInvalidInput()
        {
            var p = new ThreatAssessmentParams(-5f, 5f, -1f, 5f, 0.5f);
            Assert.GreaterOrEqual(p.threatRange, 0.01f);
            Assert.LessOrEqual(p.minDistanceFactor, 1f);   // 5→1 にクランプ
            Assert.AreEqual(0f, p.flankThreatWeight, Eps); // 負→0
            Assert.LessOrEqual(p.engagedRelief, 1f);       // 5→1
            Assert.GreaterOrEqual(p.overwhelmRatio, 1f);   // 0.5→1
        }
    }
}
