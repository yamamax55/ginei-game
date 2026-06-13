using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 恩赦を固定する：和解利得（範囲×敗者人口）、正義の不満（範囲×被害の深さ）、再発リスク
    /// （範囲×原因未解決＝許す前に直せ）、純効果の損益分岐。境界を担保。
    /// </summary>
    public class AmnestyRulesTests
    {
        private static readonly AmnestyParams P = AmnestyParams.Default;
        // 和解0.4/不満0.3/再発0.6

        [Test]
        public void ReconciliationGain_ScopeTimesDefeated()
        {
            Assert.AreEqual(0.4f, AmnestyRules.ReconciliationGain(1f, 1f, P), 1e-5f);
            Assert.AreEqual(0.1f, AmnestyRules.ReconciliationGain(0.5f, 0.5f, P), 1e-5f);
            Assert.AreEqual(0f, AmnestyRules.ReconciliationGain(0f, 1f, P), 1e-5f); // 許さなければ利得なし
        }

        [Test]
        public void JusticeGrievance_DeepWoundsRejectMercy()
        {
            Assert.AreEqual(0.3f, AmnestyRules.JusticeGrievance(1f, 1f, P), 1e-5f);
            // 被害が浅ければ寛容も呑める
            Assert.AreEqual(0f, AmnestyRules.JusticeGrievance(1f, 0f, P), 1e-5f);
        }

        [Test]
        public void RecidivismRisk_FixCauseBeforeForgiving()
        {
            // 原因未解決のまま全面恩赦＝最大再発0.6
            Assert.AreEqual(0.6f, AmnestyRules.RecidivismRisk(1f, 0f, P), 1e-5f);
            // 原因を断ってから許せばリスクなし
            Assert.AreEqual(0f, AmnestyRules.RecidivismRisk(1f, 1f, P), 1e-5f);
            Assert.AreEqual(0.3f, AmnestyRules.RecidivismRisk(1f, 0.5f, P), 1e-5f);
        }

        [Test]
        public void NetStabilityEffect_TradeoffMath()
        {
            // 好条件（敗者多・被害浅・原因解決済）＝全面恩赦が引き合う：0.4−0−0=+0.4
            Assert.AreEqual(0.4f, AmnestyRules.NetStabilityEffect(1f, 1f, 0f, 1f, P), 1e-5f);
            // 悪条件（被害深・原因未解決）＝全面恩赦は逆効果：0.4−0.3−0.6=−0.5
            Assert.AreEqual(-0.5f, AmnestyRules.NetStabilityEffect(1f, 1f, 1f, 0f, P), 1e-5f);
            // 恩赦しない＝すべてゼロ（基準点）
            Assert.AreEqual(0f, AmnestyRules.NetStabilityEffect(0f, 1f, 1f, 0f, P), 1e-5f);
        }
    }
}
