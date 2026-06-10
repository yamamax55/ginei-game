using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 抑止を固定する：報復能力＝戦力比×第二撃残存性、信憑性＝実績0.6＋死活度0.4、
    /// 抑止力＝能力×信憑性の積（どちらかゼロなら無意味）、開戦誘惑＝利得−抑止、
    /// コミットメントの罠＝投資×0.5。クランプを担保。
    /// </summary>
    public class DeterrenceRulesTests
    {
        private static readonly DeterrenceParams P = DeterrenceParams.Default;
        // 満点戦力比1.0/実績重み0.6/死活度重み0.4/罠係数0.5

        [Test]
        public void RetaliationCapability_ForceTimesSurvivability()
        {
            Assert.AreEqual(1f, DeterrenceRules.RetaliationCapability(1f, 1f, P), 1e-5f);    // 同等戦力・完全残存＝満点
            Assert.AreEqual(0.5f, DeterrenceRules.RetaliationCapability(0.5f, 1f, P), 1e-5f); // 半分の戦力
            Assert.AreEqual(0.5f, DeterrenceRules.RetaliationCapability(1f, 0.5f, P), 1e-5f); // 半分が先制で潰される
            Assert.AreEqual(0f, DeterrenceRules.RetaliationCapability(1f, 0f, P), 1e-5f);     // 全滅＝報復は撃てない
            Assert.AreEqual(1f, DeterrenceRules.RetaliationCapability(3f, 1f, P), 1e-5f);     // 戦力比は満点で頭打ち
        }

        [Test]
        public void Credibility_RecordPlusStakes()
        {
            Assert.AreEqual(1f, DeterrenceRules.Credibility(1f, 1f, P), 1e-5f);    // 0.6+0.4
            Assert.AreEqual(0.6f, DeterrenceRules.Credibility(1f, 0f, P), 1e-5f);  // 実績だけ
            Assert.AreEqual(0.4f, DeterrenceRules.Credibility(0f, 1f, P), 1e-5f);  // 空脅しの前科＝実績分を失う
            Assert.AreEqual(0f, DeterrenceRules.Credibility(0f, 0f, P), 1e-5f);
            Assert.AreEqual(1f, DeterrenceRules.Credibility(2f, 5f, P), 1e-5f);    // 入力クランプ
        }

        [Test]
        public void DeterrenceStrength_ZeroCredibilityKillsDeterrence()
        {
            // 能力があっても信憑性ゼロなら抑止ゼロ（積＝意志なき能力は恐れられない）
            Assert.AreEqual(0f, DeterrenceRules.DeterrenceStrength(1f, 0f), 1e-5f);
            // 逆も同じ＝能力なき意志も無意味
            Assert.AreEqual(0f, DeterrenceRules.DeterrenceStrength(0f, 1f), 1e-5f);
            Assert.AreEqual(0.4f, DeterrenceRules.DeterrenceStrength(0.8f, 0.5f), 1e-5f);
            Assert.AreEqual(1f, DeterrenceRules.DeterrenceStrength(1f, 1f), 1e-5f);
        }

        [Test]
        public void AttackTemptation_GainMinusDeterrence()
        {
            Assert.AreEqual(0.5f, DeterrenceRules.AttackTemptation(0.9f, 0.4f), 1e-5f);
            Assert.AreEqual(-0.8f, DeterrenceRules.AttackTemptation(0.2f, 1f), 1e-5f); // 抑止が利得を圧倒
            Assert.AreEqual(1f, DeterrenceRules.AttackTemptation(5f, 0f), 1e-5f);      // 入力クランプ
        }

        [Test]
        public void IsDeterred_TemptationBelowThreshold()
        {
            Assert.IsTrue(DeterrenceRules.IsDeterred(-0.3f, 0f));   // 誘惑が負＝思いとどまる
            Assert.IsFalse(DeterrenceRules.IsDeterred(0.5f, 0f));   // 利得が抑止を超える＝開戦
            Assert.IsFalse(DeterrenceRules.IsDeterred(0f, 0f));     // 閾値ちょうど＝抑止不成立（未満のみ）
            Assert.IsTrue(DeterrenceRules.IsDeterred(0.1f, 0.2f));  // 慎重な相手（高閾値）は弱い誘惑では動かない
        }

        [Test]
        public void CommitmentTrap_InvestmentBurnsRetreat()
        {
            Assert.AreEqual(0.5f, DeterrenceRules.CommitmentTrap(1f, P), 1e-5f);  // 全力で退路を焼く＝最大リスク
            Assert.AreEqual(0.2f, DeterrenceRules.CommitmentTrap(0.4f, P), 1e-5f);
            Assert.AreEqual(0f, DeterrenceRules.CommitmentTrap(0f, P), 1e-5f);    // 縛らなければ罠もない
            Assert.AreEqual(0.5f, DeterrenceRules.CommitmentTrap(3f, P), 1e-5f);  // 入力クランプ
        }

        [Test]
        public void Story_EmptyThreatsInviteWar_RecordDeters()
        {
            // 同じ大艦隊でも：空脅しの前科国家（実績0・死活度0.5）は攻められ、
            // 有言実行の国家（実績1・死活度0.5）は同じ利得でも抑止が成立する
            float capability = DeterrenceRules.RetaliationCapability(1f, 1f, P); // 1.0
            float gain = 0.5f;

            float credWeak = DeterrenceRules.Credibility(0f, 0.5f, P);           // 0.2
            float tWeak = DeterrenceRules.AttackTemptation(gain, DeterrenceRules.DeterrenceStrength(capability, credWeak));
            Assert.IsFalse(DeterrenceRules.IsDeterred(tWeak, 0f));               // 0.5-0.2=0.3 → 開戦

            float credStrong = DeterrenceRules.Credibility(1f, 0.5f, P);         // 0.8
            float tStrong = DeterrenceRules.AttackTemptation(gain, DeterrenceRules.DeterrenceStrength(capability, credStrong));
            Assert.IsTrue(DeterrenceRules.IsDeterred(tStrong, 0f));              // 0.5-0.8=-0.3 → 抑止成立
        }
    }
}
