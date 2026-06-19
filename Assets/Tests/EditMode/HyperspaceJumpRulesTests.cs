using NUnit.Framework;
using UnityEngine;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 亜空間跳躍（ワープ航行）を固定する：充填時間＝距離/出力、精度＝航法/(1+距離)で遠距離ほどズレる、
    /// 到着誤差＝(1−精度)×距離、跳躍直後の無防備＝距離/(1+堅牢性)、連続跳躍負荷＝跳躍数/(1+冷却)、
    /// 奇襲利得は誤差小×敵無警戒で正・誤差大か敵警戒で負（−1..1）、練度で無防備を短縮、安全判定。境界を担保。
    /// </summary>
    public class HyperspaceJumpRulesTests
    {
        private static readonly HyperspaceJumpParams P = HyperspaceJumpParams.Default;
        // 充填1/距離重み1/誤差1/無防備1/負荷1/奇襲利得1

        [Test]
        public void ChargeTime_DistanceOverOutput()
        {
            Assert.AreEqual(2f, HyperspaceJumpRules.ChargeTime(10f, 5f, P), 1e-4f);   // 10/5
            Assert.AreEqual(10f, HyperspaceJumpRules.ChargeTime(10f, 0f, P), 1e-4f);  // 出力0→1へクランプ
            Assert.AreEqual(0f, HyperspaceJumpRules.ChargeTime(0f, 5f, P), 1e-4f);    // 距離0
        }

        [Test]
        public void JumpAccuracy_FallsWithDistance()
        {
            Assert.AreEqual(1f, HyperspaceJumpRules.JumpAccuracy(0f, 1f, P), 1e-4f);    // 至近＝完全
            Assert.AreEqual(0.5f, HyperspaceJumpRules.JumpAccuracy(1f, 1f, P), 1e-4f);  // 1/(1+1)
            Assert.AreEqual(0.2f, HyperspaceJumpRules.JumpAccuracy(3f, 0.8f, P), 1e-4f); // 0.8/4
        }

        [Test]
        public void ArrivalError_GrowsWithImprecisionAndDistance()
        {
            Assert.AreEqual(0f, HyperspaceJumpRules.ArrivalError(1f, 5f, P), 1e-4f);    // 完全精度＝誤差0
            Assert.AreEqual(2f, HyperspaceJumpRules.ArrivalError(0.5f, 4f, P), 1e-4f);  // 0.5×4
            Assert.AreEqual(2.4f, HyperspaceJumpRules.ArrivalError(0.2f, 3f, P), 1e-4f); // 0.8×3
        }

        [Test]
        public void PostJumpVulnerability_ReducedByRobustness()
        {
            Assert.AreEqual(2f, HyperspaceJumpRules.PostJumpVulnerability(10f, 4f, P), 1e-4f); // 10/5
            Assert.AreEqual(6f, HyperspaceJumpRules.PostJumpVulnerability(6f, 0f, P), 1e-4f);  // 6/1
        }

        [Test]
        public void ConsecutiveJumpStrain_ReducedByCooling()
        {
            Assert.AreEqual(1f, HyperspaceJumpRules.ConsecutiveJumpStrain(3f, 2f, P), 1e-4f); // 3/3
            Assert.AreEqual(4f, HyperspaceJumpRules.ConsecutiveJumpStrain(4f, 0f, P), 1e-4f); // 4/1
        }

        [Test]
        public void SurpriseJumpBonus_SignedByErrorAndPreparedness()
        {
            Assert.AreEqual(1f, HyperspaceJumpRules.SurpriseJumpBonus(0f, 0f, P), 1e-4f);    // 誤差なし×無警戒＝最大
            Assert.AreEqual(0f, HyperspaceJumpRules.SurpriseJumpBonus(0f, 1f, P), 1e-4f);    // 敵が万全＝利得なし
            Assert.AreEqual(0f, HyperspaceJumpRules.SurpriseJumpBonus(0.5f, 0.5f, P), 1e-4f);
            Assert.AreEqual(-1f, HyperspaceJumpRules.SurpriseJumpBonus(1f, 1f, P), 1e-4f);   // 大誤差×警戒＝不利でクランプ
        }

        [Test]
        public void JumpExitReadiness_CrewShortensVulnerability()
        {
            Assert.AreEqual(1f, HyperspaceJumpRules.JumpExitReadiness(2f, 1f, P), 1e-4f);        // 精鋭は即応
            Assert.AreEqual(1f / 3f, HyperspaceJumpRules.JumpExitReadiness(2f, 0f, P), 1e-4f);   // 1/(1+2)
            Assert.AreEqual(0.4f, HyperspaceJumpRules.JumpExitReadiness(3f, 0.5f, P), 1e-4f);    // 1/(1+1.5)
            Assert.AreEqual(1f, HyperspaceJumpRules.JumpExitReadiness(0f, 0f, P), 1e-4f);        // 無防備0＝常に万全
        }

        [Test]
        public void IsJumpSafe_BothWithinThreshold()
        {
            Assert.IsTrue(HyperspaceJumpRules.IsJumpSafe(0.5f, 0.5f, 1f));   // 誤差・負荷とも許容内
            Assert.IsFalse(HyperspaceJumpRules.IsJumpSafe(2f, 0.5f, 1f));    // 誤差超過
            Assert.IsFalse(HyperspaceJumpRules.IsJumpSafe(0.5f, 2f, 1f));    // 負荷超過
            Assert.IsTrue(HyperspaceJumpRules.IsJumpSafe(0.9f, 0.9f));       // 既定閾値1.0委譲版
        }

        // 物語テスト：精鋭艦隊が短距離跳躍で敵後背へ無警戒奇襲、無能側は遠距離跳躍で迷子になり各個撃破。
        [Test]
        public void Narrative_ShortPreciseSurpriseVsLongLostJump()
        {
            // 練達の旗艦＝近距離(1)・高航法(0.9)で跳躍
            float accGood = HyperspaceJumpRules.JumpAccuracy(1f, 0.9f, P);     // 0.9/2 = 0.45
            float errGood = HyperspaceJumpRules.ArrivalError(accGood, 1f, P);  // (1-0.45)*1 = 0.55
            // 凡庸な敵は遠距離(8)・低航法(0.3)で跳躍
            float accBad = HyperspaceJumpRules.JumpAccuracy(8f, 0.3f, P);      // 0.3/9 ≈ 0.0333
            float errBad = HyperspaceJumpRules.ArrivalError(accBad, 8f, P);    // ≈ 7.73
            Assert.Less(errGood, errBad, "短距離・高航法ほど到着誤差が小さい");

            // 無警戒の敵への奇襲は利得＞0、警戒済みなら利得は下がる
            float surprise = HyperspaceJumpRules.SurpriseJumpBonus(errGood, 0f, P);
            float guarded = HyperspaceJumpRules.SurpriseJumpBonus(errGood, 0.6f, P);
            Assert.Greater(surprise, 0f, "誤差小×敵無警戒で奇襲は有利");
            Assert.Less(guarded, surprise, "敵が警戒すると奇襲の利得は目減りする");

            // 精鋭の跳躍は安全、迷子の遠距離跳躍は危険
            Assert.IsTrue(HyperspaceJumpRules.IsJumpSafe(errGood, 0.5f), "短距離の精密跳躍は安全");
            Assert.IsFalse(HyperspaceJumpRules.IsJumpSafe(errBad, 0.5f), "遠距離で迷子の跳躍は危険");
        }
    }
}
