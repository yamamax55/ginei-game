using NUnit.Framework;
using UnityEngine;

namespace Ginei.Tests
{
    /// <summary>監視（持続的観察と行動把握）純ロジックの EditMode テスト。既定 Params で期待値固定。</summary>
    public class SurveillanceRulesTests
    {
        const float Eps = 1e-4f;

        [Test]
        public void Continuity_AssetsAndHandoff()
        {
            // depth=clamp01(3*0.25=0.75)=0.75; +0.25*0.8*0.5=0.1 → 0.85
            Assert.AreEqual(0.85f, SurveillanceRules.SurveillanceContinuity(3, 0.8f), Eps);
            // 資産0・引き継ぎ完全：depth=0 → 0 + 1*1*0.5 = 0.5
            Assert.AreEqual(0.5f, SurveillanceRules.SurveillanceContinuity(0, 1f), Eps);
            // 資産過多はclamp（5*0.25=1.25→1.0）＝完全継続
            Assert.AreEqual(1f, SurveillanceRules.SurveillanceContinuity(5, 0f), Eps);
            // 負の資産数は0扱い・安全
            Assert.AreEqual(0f, SurveillanceRules.SurveillanceContinuity(-3, 0f), Eps);
        }

        [Test]
        public void PatternRecognition_TimeAndContinuity()
        {
            // saturate=60/(60+60)=0.5; *0.85 = 0.425
            Assert.AreEqual(0.425f, SurveillanceRules.PatternRecognition(60f, 0.85f), Eps);
            // 観察時間0なら把握0
            Assert.AreEqual(0f, SurveillanceRules.PatternRecognition(0f, 1f), Eps);
        }

        [Test]
        public void ExposureRisk_ProximityTimesVigilance()
        {
            // 0.5*0.6*1 = 0.3
            Assert.AreEqual(0.3f, SurveillanceRules.ExposureRisk(0.5f, 0.6f), Eps);
            // 警戒0なら発覚しない
            Assert.AreEqual(0f, SurveillanceRules.ExposureRisk(1f, 0f), Eps);
        }

        [Test]
        public void TargetEvasion_VigilanceTimesExposure()
        {
            // 0.6*0.3 = 0.18
            Assert.AreEqual(0.18f, SurveillanceRules.TargetEvasion(0.6f, 0.3f), Eps);
        }

        [Test]
        public void ShakeOff_EvasionOverSkill()
        {
            // 0.18/(0.18+0.42) = 0.3
            Assert.AreEqual(0.3f, SurveillanceRules.ShakeOff(0.18f, 0.42f), Eps);
            // 両者0なら撒けない
            Assert.AreEqual(0f, SurveillanceRules.ShakeOff(0f, 0f), Eps);
            // 技量だけ高い：回避0 → 0
            Assert.AreEqual(0f, SurveillanceRules.ShakeOff(0f, 1f), Eps);
        }

        [Test]
        public void IntelAccumulation_RisesButCapped()
        {
            // saturate=0.5; 0.425*0.5*1 = 0.2125
            Assert.AreEqual(0.2125f, SurveillanceRules.IntelAccumulation(0.425f, 60f), Eps);
            // 時間が長くても上限intelCap=1.0を超えない
            float big = SurveillanceRules.IntelAccumulation(1f, 1_000_000f);
            Assert.LessOrEqual(big, 1f + Eps);
            // 時間0なら蓄積0
            Assert.AreEqual(0f, SurveillanceRules.IntelAccumulation(1f, 0f), Eps);
        }

        [Test]
        public void Compromise_TracksExposure()
        {
            Assert.AreEqual(0.3f, SurveillanceRules.CompromiseFromExposure(0.3f), Eps);
            Assert.AreEqual(1f, SurveillanceRules.CompromiseFromExposure(5f), Eps); // clamp
        }

        [Test]
        public void IsEffective_IntelMustBeatShakeOff()
        {
            // 蓄積0.6 撒かれ0.3 差0.3 >= 0.2 → 機能
            Assert.IsTrue(SurveillanceRules.IsSurveillanceEffective(0.6f, 0.3f));
            // 蓄積0.2125 撒かれ0.3 差-0.0875 → 非機能
            Assert.IsFalse(SurveillanceRules.IsSurveillanceEffective(0.2125f, 0.3f));
            // 明示閾値委譲：閾値0なら同値で機能
            Assert.IsTrue(SurveillanceRules.IsSurveillanceEffective(0.3f, 0.3f, 0f));
        }

        /// <summary>物語：厚い監視網が長期で対象を読み切るが、近づきすぎてバレ、対象が巻きにかかる。</summary>
        [Test]
        public void Narrative_PersistentTailReadThenCompromised()
        {
            // 4資産・引き継ぎ滑らか → 継続性ほぼ満点
            float cont = SurveillanceRules.SurveillanceContinuity(4, 0.9f);
            Assert.AreEqual(1f, cont, Eps); // depth=clamp01(4*0.25=1.0)=1.0

            // 長期観察でパターンを把握
            float pat = SurveillanceRules.PatternRecognition(180f, cont); // 180/240=0.75
            Assert.AreEqual(0.75f, pat, Eps);

            // 蓄積は逓増だが上限以下
            float intel = SurveillanceRules.IntelAccumulation(pat, 180f); // 0.75*0.75 = 0.5625
            Assert.AreEqual(0.5625f, intel, Eps);

            // だが近づきすぎ＋対象が警戒 → 発覚
            float exp = SurveillanceRules.ExposureRisk(0.5f, 0.6f); // 0.3
            Assert.AreEqual(0.3f, exp, Eps);
            float compromise = SurveillanceRules.CompromiseFromExposure(exp);
            Assert.AreEqual(0.3f, compromise, Eps);

            // 対象が回避を試みるが、高い監視技量が振り切られを抑える
            float evade = SurveillanceRules.TargetEvasion(0.6f, exp); // 0.6*0.3 = 0.18
            Assert.AreEqual(0.18f, evade, Eps);
            float shake = SurveillanceRules.ShakeOff(evade, 0.9f); // 0.18/(0.18+0.9)=0.18/1.08
            Assert.AreEqual(0.18f / 1.08f, shake, Eps); // ≈0.1667

            // 既に読み切った蓄積(0.5625)が撒かれ度(≈0.1667)を閾値0.2以上上回る → なお機能
            Assert.Greater(intel - shake, SurveillanceRules.DefaultEffectiveThreshold);
            Assert.IsTrue(SurveillanceRules.IsSurveillanceEffective(intel, shake));
        }
    }
}
