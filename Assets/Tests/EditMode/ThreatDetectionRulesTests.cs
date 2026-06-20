using NUnit.Framework;
using UnityEngine;

namespace Ginei.Tests
{
    /// <summary>
    /// 脅威探知の純ロジック検証（既定Params・期待値は手計算で固定）。
    /// 既定：探知ゲイン1 / 識別下限0.1 / 近接重み1 / 猶予重み1 / 疲れ感度1.5 / 認識しきい値0.3。
    /// </summary>
    public class ThreatDetectionRulesTests
    {
        private const float Tol = 1e-4f;
        private const float PowTol = 1e-3f;

        [Test]
        public void ThreatDetection_CoverageTimesSignature()
        {
            // 0.8 * 0.5 * 1 = 0.4
            Assert.AreEqual(0.4f, ThreatDetectionRules.ThreatDetection(0.8f, 0.5f), Tol);
        }

        [Test]
        public void ThreatDetection_BlindOrNoSignal_IsZero()
        {
            // 死角（カバー0）も無信号（信号0）も探知できない
            Assert.AreEqual(0f, ThreatDetectionRules.ThreatDetection(0f, 1f), Tol);
            Assert.AreEqual(0f, ThreatDetectionRules.ThreatDetection(1f, 0f), Tol);
        }

        [Test]
        public void IdentificationConfidence_ScalesByIffQuality()
        {
            // iff = Lerp(0.1, 1, 0.5) = 0.55 ; 0.6 * 0.55 = 0.33
            Assert.AreEqual(0.33f, ThreatDetectionRules.IdentificationConfidence(0.6f, 0.5f), Tol);
            // 探知0なら識別も0
            Assert.AreEqual(0f, ThreatDetectionRules.IdentificationConfidence(0f, 1f), Tol);
        }

        [Test]
        public void ThreatLevel_StrengthProximityIntent()
        {
            // prox = 0.5^1 = 0.5 ; 0.8 * 0.5 * 0.5 = 0.2
            Assert.AreEqual(0.2f, ThreatDetectionRules.ThreatLevel(0.8f, 0.5f, 0.5f), PowTol);
            // 敵意0なら脅威0（平時）
            Assert.AreEqual(0f, ThreatDetectionRules.ThreatLevel(1f, 1f, 0f), Tol);
        }

        [Test]
        public void ThreatPrioritization_VulnerabilityLiftsPriority()
        {
            // 0.6 * (1 + 0.5) * 0.5 = 0.45
            Assert.AreEqual(0.45f, ThreatDetectionRules.ThreatPrioritization(0.6f, 0.5f), Tol);
            // 脆弱性0なら脅威度の半分
            Assert.AreEqual(0.3f, ThreatDetectionRules.ThreatPrioritization(0.6f, 0f), Tol);
            // 脆弱性1なら脅威度そのもの
            Assert.AreEqual(0.6f, ThreatDetectionRules.ThreatPrioritization(0.6f, 1f), Tol);
        }

        [Test]
        public void SurpriseDetection_StealthAndWarningTime()
        {
            // exposed = 1 - 0.25 = 0.75 ; warn = 0.5^1 = 0.5 ; 0.8 * 0.75 * 0.5 = 0.3
            Assert.AreEqual(0.3f, ThreatDetectionRules.SurpriseDetection(0.8f, 0.25f, 0.5f), PowTol);
            // 完全隠密は察知0（奇襲が刺さる）
            Assert.AreEqual(0f, ThreatDetectionRules.SurpriseDetection(1f, 1f, 1f), Tol);
        }

        [Test]
        public void AlertGeneration_ThreatTimesIdentification()
        {
            // 0.6 * 0.5 = 0.3 ; 識別が曖昧だと警報は鈍る
            Assert.AreEqual(0.3f, ThreatDetectionRules.AlertGeneration(0.6f, 0.5f), Tol);
        }

        [Test]
        public void AlarmFatigue_AccumulatesFromFalseAlarms()
        {
            // 0.4 * 1.5 = 0.6 ; 蓄積過多はクランプ（0.8*1.5=1.2 -> 1）
            Assert.AreEqual(0.6f, ThreatDetectionRules.AlarmFatigue(0.4f), Tol);
            Assert.AreEqual(1f, ThreatDetectionRules.AlarmFatigue(0.8f), Tol);
        }

        [Test]
        public void IsThreatRecognized_NetAlertAgainstThreshold()
        {
            // 0.8 - 0.3 = 0.5 > 0.3 -> 認識
            Assert.IsTrue(ThreatDetectionRules.IsThreatRecognized(0.8f, 0.3f));
            // 0.4 - 0.3 = 0.1 <= 0.3 -> 疲れが警報を呑む
            Assert.IsFalse(ThreatDetectionRules.IsThreatRecognized(0.4f, 0.3f));
        }

        [Test]
        public void Story_CloakedRaidSlipsPastFatiguedPicket()
        {
            // 物語：誤警報で疲れ切った哨戒線へ、隠密の通商破壊艦が迫る。
            // センサーは広く張れているが（カバー0.9）、敵の信号は薄い（0.3）。
            float detection = ThreatDetectionRules.ThreatDetection(0.9f, 0.3f); // 0.27
            Assert.AreEqual(0.27f, detection, Tol);

            // 探知が薄いので識別も曖昧（iff質0.5）。
            float idc = ThreatDetectionRules.IdentificationConfidence(detection, 0.5f); // 0.27*0.55=0.1485
            Assert.AreEqual(0.1485f, idc, Tol);

            // 高隠密・短い猶予で奇襲はほぼ刺さる。
            float surprise = ThreatDetectionRules.SurpriseDetection(detection, 0.8f, 0.2f); // 0.27*0.2*0.2=0.0108
            Assert.AreEqual(0.0108f, surprise, PowTol);

            // 脅威度は高い（強敵が近く敵意明白）が…
            float level = ThreatDetectionRules.ThreatLevel(0.9f, 0.9f, 1f); // 0.81
            Assert.AreEqual(0.81f, level, PowTol);

            // 識別が曖昧なので警報は弱い。
            float alert = ThreatDetectionRules.AlertGeneration(level, idc); // 0.81*0.1485=0.1203
            Assert.AreEqual(0.120285f, alert, Tol);

            // 度重なる誤警報で哨戒は疲れている。
            float fatigue = ThreatDetectionRules.AlarmFatigue(0.5f); // 0.75
            Assert.AreEqual(0.75f, fatigue, Tol);

            // 弱い警報は疲れに呑まれ、奇襲を許す（オオカミ少年）。
            Assert.IsFalse(ThreatDetectionRules.IsThreatRecognized(alert, fatigue));

            // 一方、誤警報の無い精鋭哨戒（疲れ0）かつ明瞭な識別なら、同じ脅威を認識できる。
            float clearIdc = ThreatDetectionRules.IdentificationConfidence(0.9f, 1f); // 0.9
            float clearAlert = ThreatDetectionRules.AlertGeneration(level, clearIdc); // 0.81*0.9=0.729
            Assert.IsTrue(ThreatDetectionRules.IsThreatRecognized(clearAlert, 0f));
        }
    }
}
