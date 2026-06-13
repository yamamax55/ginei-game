using NUnit.Framework;
using UnityEngine;

namespace Ginei.Tests
{
    /// <summary>
    /// SignalIntelligenceRules（通信傍受・信号諜報）の純ロジックテスト。既定 Params で期待値を固定。
    /// 全乗算なので許容誤差は 1e-4f で十分。
    /// </summary>
    public class SignalIntelligenceRulesTests
    {
        const float Tol = 1e-4f;

        [Test]
        public void Interception_EmissionTimesCoverage()
        {
            // 0.8 × 0.5 × 1.0 = 0.4
            Assert.AreEqual(0.4f, SignalIntelligenceRules.Interception(0.8f, 0.5f), Tol);
        }

        [Test]
        public void Interception_SilentEnemyYieldsNothing()
        {
            // 無線封止（emission=0）なら傍受網が完璧でも拾えない
            Assert.AreEqual(0f, SignalIntelligenceRules.Interception(0f, 1f), Tol);
        }

        [Test]
        public void Decryption_StrongCipherReducesReadable()
        {
            // 傍受 0.4 のうち暗号強度 0.25 で読めるのは 0.4 × 0.75 = 0.3
            Assert.AreEqual(0.3f, SignalIntelligenceRules.Decryption(0.4f, 0.25f), Tol);
            // 完全暗号（1.0）なら何も読めない
            Assert.AreEqual(0f, SignalIntelligenceRules.Decryption(0.4f, 1f), Tol);
        }

        [Test]
        public void Forewarning_FastTempoRaisesValue()
        {
            // 0.3 × (0.5 + 0.5×0.6) = 0.3 × 0.8 = 0.24
            Assert.AreEqual(0.24f, SignalIntelligenceRules.Forewarning(0.3f, 0.6f), Tol);
            // 同じ解読でもテンポが速いほど価値が増す（0.6 < 1.0）
            Assert.Less(SignalIntelligenceRules.Forewarning(0.3f, 0.6f),
                        SignalIntelligenceRules.Forewarning(0.3f, 1f));
        }

        [Test]
        public void DeceptionRisk_OnlyFooledByWhatYouRead()
        {
            // 0.3 × 0.5 × 0.7 = 0.105
            Assert.AreEqual(0.105f, SignalIntelligenceRules.DeceptionRisk(0.3f, 0.5f), Tol);
            // 何も解読していなければ（decryption=0）偽情報を掴まされない
            Assert.AreEqual(0f, SignalIntelligenceRules.DeceptionRisk(0f, 1f), Tol);
        }

        [Test]
        public void NetIntelValue_DiscountsByDeception()
        {
            // 0.24 × (1 − 0.105) = 0.24 × 0.895 = 0.2148
            Assert.AreEqual(0.2148f, SignalIntelligenceRules.NetIntelValue(0.24f, 0.105f), Tol);
        }

        [Test]
        public void CounterIntelHardening_WeightsEmconAndCipher()
        {
            // 0.25 × 0.4 + 0.5 × 0.6 = 0.1 + 0.3 = 0.4
            Assert.AreEqual(0.4f, SignalIntelligenceRules.CounterIntelHardening(0.25f, 0.5f), Tol);
        }

        [Test]
        public void IsCompromised_ThresholdCrossing()
        {
            // 既定閾値 0.5
            Assert.IsTrue(SignalIntelligenceRules.IsCompromised(0.5f));
            Assert.IsFalse(SignalIntelligenceRules.IsCompromised(0.49f));
            Assert.IsTrue(SignalIntelligenceRules.IsCompromised(0.8f, 0.7f));
        }

        [Test]
        public void Story_InterceptAndDecryptToSeizeInitiative()
        {
            // 物語：敵が活発に発信（emission=0.8）し、こちらの傍受網は中程度（coverage=0.5）。
            // 弱い暗号（cipher=0.25）を破って解読し、速い敵（tempo=0.6）の意図を先読み、
            // 欺瞞努力は控えめ（effort=0.5）、こちらの反応は速い（reaction=0.8）。
            float interception = SignalIntelligenceRules.Interception(0.8f, 0.5f);          // 0.4
            float decryption = SignalIntelligenceRules.Decryption(interception, 0.25f);     // 0.3
            float forewarning = SignalIntelligenceRules.Forewarning(decryption, 0.6f);      // 0.24
            float risk = SignalIntelligenceRules.DeceptionRisk(decryption, 0.5f);           // 0.105
            float net = SignalIntelligenceRules.NetIntelValue(forewarning, risk);           // 0.2148
            float advantage = SignalIntelligenceRules.PreemptiveAdvantage(net, 0.8f);       // 0.17184

            Assert.AreEqual(0.17184f, advantage, Tol);

            // 欺瞞が激しい（effort=1.0）と正味価値が下がり先手有利も削れる＝偽情報に騙されると逆効果
            float heavyRisk = SignalIntelligenceRules.DeceptionRisk(decryption, 1f);
            float heavyNet = SignalIntelligenceRules.NetIntelValue(forewarning, heavyRisk);
            float heavyAdvantage = SignalIntelligenceRules.PreemptiveAdvantage(heavyNet, 0.8f);
            Assert.Less(heavyAdvantage, advantage);

            // 通信秘匿：暗号もEMCONも徹底すれば被傍受（＝筒抜け）を防げる
            float hardening = SignalIntelligenceRules.CounterIntelHardening(0.9f, 0.9f);     // 0.9
            Assert.Greater(hardening, 0.8f);
            // 強い暗号下では傍受しても解読率が低く、自軍は筒抜けにならない
            float enemyRead = SignalIntelligenceRules.Decryption(0.4f, 0.9f);               // 0.04
            Assert.IsFalse(SignalIntelligenceRules.IsCompromised(enemyRead));
        }
    }
}
