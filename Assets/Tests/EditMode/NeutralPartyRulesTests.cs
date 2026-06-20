using NUnit.Framework;
using UnityEngine;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 中立勢力（フェザーン型）の力学を固定する：中立成立度は均衡×経済てこ、交易繁栄は交易量×中立度、
    /// 影響力は経済規模×不可欠性、戦略価値は要衝×繁栄、圧力は価値×必要度、崩壊リスクは圧力−影響力、
    /// 参戦利得は勝者期待−中立喪失、判断は成立度−参戦利得。既定 Params で期待値固定（手計算検算）。
    /// </summary>
    public class NeutralPartyRulesTests
    {
        private static readonly NeutralPartyParams P = NeutralPartyParams.Default;
        // 均衡鋭さ1.0／てこ重み0.5／中立指数1.0／繁栄重み0.6／圧力勾配1.0／中立喪失0.7

        [Test]
        public void NeutralityViability_BalancedIsBest()
        {
            // 拮抗(balance=0)＝最大1.0、一強(|balance|=1)かつてこ無し＝0
            Assert.AreEqual(1f, NeutralPartyRules.NeutralityViability(0f, 0f, P), 1e-4f);
            Assert.AreEqual(0f, NeutralPartyRules.NeutralityViability(1f, 0f, P), 1e-4f);
            Assert.AreEqual(0f, NeutralPartyRules.NeutralityViability(-1f, 0f, P), 1e-4f); // 偏りは対称
            // 均衡崩壊でも経済てこが下支え：balance=1,lev=1 → 0+(1)*0.5*1 = 0.5
            Assert.AreEqual(0.5f, NeutralPartyRules.NeutralityViability(1f, 1f, P), 1e-4f);
            // 半端：tilt=0.5→base0.5、floor=0.5*0.5=0.25 → 0.5+0.5*0.25 = 0.625
            Assert.AreEqual(0.625f, NeutralPartyRules.NeutralityViability(0.5f, 0.5f, P), 1e-4f);
        }

        [Test]
        public void TradeProsperity_ThroughputTimesNeutrality()
        {
            Assert.AreEqual(0.4f, NeutralPartyRules.TradeProsperity(0.8f, 0.5f, P), 1e-4f); // 0.8*0.5
            Assert.AreEqual(0f, NeutralPartyRules.TradeProsperity(1f, 0f, P), 1e-4f);       // 中立喪失＝商えない
            Assert.AreEqual(0.6f, NeutralPartyRules.TradeProsperity(0.6f, 1f, P), 1e-4f);   // 完全中立＝交易量そのまま
            Assert.AreEqual(1f, NeutralPartyRules.TradeProsperity(2f, 2f, P), 1e-4f);       // 入力クランプ
        }

        [Test]
        public void Leverage_WeightTimesIndispensability()
        {
            Assert.AreEqual(0.4f, NeutralPartyRules.Leverage(0.8f, 0.5f, P), 1e-4f);
            Assert.AreEqual(1f, NeutralPartyRules.Leverage(1f, 1f, P), 1e-4f);
            Assert.AreEqual(0f, NeutralPartyRules.Leverage(1f, 0f, P), 1e-4f); // 替えが利く＝影響力なし
            Assert.AreEqual(0f, NeutralPartyRules.Leverage(0f, 1f, P), 1e-4f); // 経済小＝影響力なし
        }

        [Test]
        public void StrategicValue_LocationWithProsperity()
        {
            // location=1, prosperity=0.5 → 1*(0.4+0.6*0.5)=0.7
            Assert.AreEqual(0.7f, NeutralPartyRules.StrategicValue(1f, 0.5f, P), 1e-4f);
            // location=0.5, prosperity=1 → 0.5*(0.4+0.6)=0.5
            Assert.AreEqual(0.5f, NeutralPartyRules.StrategicValue(0.5f, 1f, P), 1e-4f);
            // 要衝でなければ価値なし
            Assert.AreEqual(0f, NeutralPartyRules.StrategicValue(0f, 1f, P), 1e-4f);
        }

        [Test]
        public void PressureFromBelligerent_ValueTimesNeed()
        {
            Assert.AreEqual(0.56f, NeutralPartyRules.PressureFromBelligerent(0.7f, 0.8f, P), 1e-4f);
            Assert.AreEqual(0f, NeutralPartyRules.PressureFromBelligerent(0.7f, 0f, P), 1e-4f); // 不要なら無圧
        }

        [Test]
        public void NeutralityBreakRisk_PressureOverLeverage()
        {
            // net=0.8-0.3=0.5, risk=1-1/1.5 = 0.33333
            Assert.AreEqual(0.3333f, NeutralPartyRules.NeutralityBreakRisk(0.8f, 0.3f, P), 1e-3f);
            // 影響力が圧力を上回れば net=0 → リスク0
            Assert.AreEqual(0f, NeutralPartyRules.NeutralityBreakRisk(0.3f, 0.8f, P), 1e-4f);
            // 圧力最大・影響力無し：net=1, risk=1-1/2=0.5
            Assert.AreEqual(0.5f, NeutralPartyRules.NeutralityBreakRisk(1f, 0f, P), 1e-3f);
        }

        [Test]
        public void EntryPayoff_GainMinusNeutralityLoss()
        {
            // gain=0.8*0.5=0.4, loss=0.7*0.5=0.35 → 0.05
            Assert.AreEqual(0.05f, NeutralPartyRules.EntryPayoff(0.8f, 0.5f, 0.5f, P), 1e-4f);
            // 勝てそうにない＆中立価値高い → 大きく負
            Assert.AreEqual(-0.64f, NeutralPartyRules.EntryPayoff(0.2f, 0.3f, 1f, P), 1e-4f);
            // 中立価値ゼロなら純利得：gain=0.9*0.9=0.81
            Assert.AreEqual(0.81f, NeutralPartyRules.EntryPayoff(0.9f, 0.9f, 0f, P), 1e-4f);
        }

        [Test]
        public void ShouldStayNeutral_ComparesViabilityAndPayoff()
        {
            // 成立度0.625 − 参戦利得0.05 = 0.575 ≥ 0 → 中立維持
            Assert.IsTrue(NeutralPartyRules.ShouldStayNeutral(0.625f, 0.05f, 0f));
            // 成立度低く参戦が得 → 参戦
            Assert.IsFalse(NeutralPartyRules.ShouldStayNeutral(0.3f, 0.8f, 0f));
            // 閾値委譲版（既定0）
            Assert.IsTrue(NeutralPartyRules.ShouldStayNeutral(0.6f, 0.1f));
        }

        [Test]
        public void DefaultDelegation_MatchesExplicitParams()
        {
            Assert.AreEqual(NeutralPartyRules.NeutralityViability(0.5f, 0.5f, P),
                NeutralPartyRules.NeutralityViability(0.5f, 0.5f), 1e-4f);
            Assert.AreEqual(NeutralPartyRules.TradeProsperity(0.8f, 0.5f, P),
                NeutralPartyRules.TradeProsperity(0.8f, 0.5f), 1e-4f);
            Assert.AreEqual(NeutralPartyRules.StrategicValue(1f, 0.5f, P),
                NeutralPartyRules.StrategicValue(1f, 0.5f), 1e-4f);
            Assert.AreEqual(NeutralPartyRules.NeutralityBreakRisk(0.8f, 0.3f, P),
                NeutralPartyRules.NeutralityBreakRisk(0.8f, 0.3f), 1e-4f);
            Assert.AreEqual(NeutralPartyRules.EntryPayoff(0.8f, 0.5f, 0.5f, P),
                NeutralPartyRules.EntryPayoff(0.8f, 0.5f, 0.5f), 1e-4f);
        }

        /// <summary>
        /// 物語：フェザーンは帝国と同盟が拮抗する間（balance≈0）、交易の要衝として繁栄し
        /// 両陣営に影響力を持つ＝高い中立成立度。圧力は影響力で相殺され崩壊リスクは低い。
        /// やがて均衡が一方へ崩れると中立は痩せ、勝ち馬への参戦が割に合い始める。
        /// </summary>
        [Test]
        public void Narrative_BalanceErodesAndNeutralityBecomesUntenable()
        {
            // 平時：拮抗(balance=0)・てこ十分・要衝で繁盛
            float prosperity = NeutralPartyRules.TradeProsperity(0.9f, 1f);   // 完全中立で交易繁盛
            float lev = NeutralPartyRules.Leverage(0.8f, 0.8f);               // 大きく不可欠
            float viaPeace = NeutralPartyRules.NeutralityViability(0f, lev);  // 拮抗→ほぼ最大
            float sv = NeutralPartyRules.StrategicValue(0.9f, prosperity);
            float pressure = NeutralPartyRules.PressureFromBelligerent(sv, 0.6f);
            float riskPeace = NeutralPartyRules.NeutralityBreakRisk(pressure, lev);
            float payoffPeace = NeutralPartyRules.EntryPayoff(0.5f, 0.4f, viaPeace);

            Assert.AreEqual(1f, viaPeace, 1e-4f);                 // 拮抗＝中立成立度最大
            Assert.Less(riskPeace, 0.3f);                        // 影響力で圧力を吸収＝低リスク
            Assert.IsTrue(NeutralPartyRules.ShouldStayNeutral(viaPeace, payoffPeace)); // 中立が得

            // 戦況が一方へ大きく傾く：均衡崩壊で中立成立度が痩せ、勝ち馬参戦が有利に
            float viaWar = NeutralPartyRules.NeutralityViability(1f, lev);   // 一強→大きく低下
            float payoffWar = NeutralPartyRules.EntryPayoff(0.9f, 0.9f, viaWar); // 勝てる側へ厚い取り分

            Assert.Less(viaWar, viaPeace);                       // 中立成立度は落ちた
            Assert.IsFalse(NeutralPartyRules.ShouldStayNeutral(viaWar, payoffWar)); // 参戦へ転ぶ
        }
    }
}
