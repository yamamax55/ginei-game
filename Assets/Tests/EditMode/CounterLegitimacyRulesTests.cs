using NUnit.Framework;
using UnityEngine;

namespace Ginei.Tests
{
    /// <summary>
    /// 対抗的正統性（替天行道）の純ロジック検証（SHZ-2 #1358・水滸伝）。
    /// 体制の正統性喪失・対抗的正統性の成長・官逼民反・道徳的優位・民心の移動・限定的標的・
    /// 正統性の綱引き・義の反乱判定を既定 Params 具体値で担保する。
    /// </summary>
    public class CounterLegitimacyRulesTests
    {
        const float Eps = 1e-4f;

        /// <summary>体制の正統性喪失＝腐敗×0.5＋暴政×0.5。官が酷いほど自ら大義を失う。</summary>
        [Test]
        public void RegimeDelegitimation_腐敗と暴政で喪失する()
        {
            // 腐敗0.8・暴政0.6 → 0.8*0.5 + 0.6*0.5 = 0.7
            Assert.AreEqual(0.7f, CounterLegitimacyRules.RegimeDelegitimation(0.8f, 0.6f), Eps);
            // 清廉な体制（腐敗0・暴政0）は喪失なし。
            Assert.AreEqual(0f, CounterLegitimacyRules.RegimeDelegitimation(0f, 0f), Eps);
            // 上限はクランプされる。
            Assert.AreEqual(1f, CounterLegitimacyRules.RegimeDelegitimation(1f, 1f), Eps);
        }

        /// <summary>対抗的正統性は体制の正統性喪失×義賊の規律で能動的に成長する。</summary>
        [Test]
        public void CounterLegitimacyGrowth_官が酷く義賊が規律を保つほど育つ()
        {
            // 喪失0.8・規律0.5・dt2 → 0.15*(0.8*0.5)*2 = 0.12 加算
            float grown = CounterLegitimacyRules.CounterLegitimacyGrowth(0.2f, 0.8f, 0.5f, 2f);
            Assert.AreEqual(0.32f, grown, Eps);
            // 官が清廉（喪失0）なら育たない。
            Assert.AreEqual(0.2f, CounterLegitimacyRules.CounterLegitimacyGrowth(0.2f, 0f, 0.5f, 2f), Eps);
            // 義賊が無規律（conduct0）なら育たない。
            Assert.AreEqual(0.2f, CounterLegitimacyRules.CounterLegitimacyGrowth(0.2f, 0.8f, 0f, 2f), Eps);
        }

        /// <summary>官逼民反＝暴政×困窮で民が反乱へ追い込まれる。どちらか欠ければ立たない。</summary>
        [Test]
        public void OfficialDrivesRebellion_圧政と困窮が反乱を生む()
        {
            // 暴政0.9・困窮0.8・dt1 → 0.12*(0.9*0.8)*1 = 0.0864
            Assert.AreEqual(0.0864f, CounterLegitimacyRules.OfficialDrivesRebellion(0.9f, 0.8f, 1f), Eps);
            // 圧政があっても困窮が無ければ反乱化しない。
            Assert.AreEqual(0f, CounterLegitimacyRules.OfficialDrivesRebellion(0.9f, 0f, 1f), Eps);
        }

        /// <summary>道徳的優位＝対抗的正統性×体制の正統性喪失。両方揃って替天行道が成立。</summary>
        [Test]
        public void MoralHighGround_自らの正統性と相手の不正の積()
        {
            // 0.6 * 0.5 = 0.3
            Assert.AreEqual(0.3f, CounterLegitimacyRules.MoralHighGround(0.6f, 0.5f), Eps);
            // 官が清廉なら高地は立たない。
            Assert.AreEqual(0f, CounterLegitimacyRules.MoralHighGround(0.6f, 0f), Eps);
        }

        /// <summary>民心は義賊の正統性が体制支持を上回る分だけ義賊へ移る。</summary>
        [Test]
        public void PopularSupportShift_義賊が上回れば民心が流れる()
        {
            // counter0.6・regimeSupport0.2 → pull0.4・delta=0.1*0.4*2=0.08 → 0.6+0.08=0.68
            Assert.AreEqual(0.68f, CounterLegitimacyRules.PopularSupportShift(0.6f, 0.2f, 2f), Eps);
            // 体制支持が上回れば義賊から離れる方向（pull負）。
            float away = CounterLegitimacyRules.PopularSupportShift(0.3f, 0.9f, 2f);
            // pull=-0.6・delta=0.1*-0.6*2=-0.12 → 0.3-0.12=0.18
            Assert.AreEqual(0.18f, away, Eps);
        }

        /// <summary>限定的標的＝腐敗官だけを討つほど正統性が保たれる（皇帝でなく奸臣を討つ）。</summary>
        [Test]
        public void LimitedTarget_標的を絞るほど正統性を保つ()
        {
            // 規律0.5・focus0.8 → 0.5 + 0.8*0.3 = 0.74
            Assert.AreEqual(0.74f, CounterLegitimacyRules.LimitedTarget(0.5f, 0.8f), Eps);
            // 標的を絞らない（focus0）なら規律のまま。
            Assert.AreEqual(0.5f, CounterLegitimacyRules.LimitedTarget(0.5f, 0f), Eps);
        }

        /// <summary>正統性の綱引き＝対抗的正統性−体制の正統性。正なら義賊が大義を握る。</summary>
        [Test]
        public void LegitimacyContest_大義の綱引き()
        {
            // 義賊0.7・体制0.3 → +0.4（義賊が握る）
            Assert.AreEqual(0.4f, CounterLegitimacyRules.LegitimacyContest(0.7f, 0.3f), Eps);
            // 体制0.8・義賊0.2 → -0.6（体制が握る）
            Assert.AreEqual(-0.6f, CounterLegitimacyRules.LegitimacyContest(0.2f, 0.8f), Eps);
        }

        /// <summary>義の反乱判定＝対抗的正統性が閾値以上で替天行道として成立。</summary>
        [Test]
        public void IsRighteousRebellion_閾値で義の反乱が成立する()
        {
            Assert.IsTrue(CounterLegitimacyRules.IsRighteousRebellion(0.6f, 0.5f));
            Assert.IsFalse(CounterLegitimacyRules.IsRighteousRebellion(0.4f, 0.5f));
        }
    }
}
