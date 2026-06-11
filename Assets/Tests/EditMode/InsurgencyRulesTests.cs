using NUnit.Framework;
using UnityEngine;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>占領地反乱組織化（SPW-2 #1394）の純ロジックを EditMode で担保する。</summary>
    public class InsurgencyRulesTests
    {
        const float Eps = 1e-4f;

        /// <summary>組織化：外部扇動×現地不満で時間成長し、どちらか0なら育たない。</summary>
        [Test]
        public void OrganizationTick_GrowsWithAgitationAndGrievance()
        {
            // organization 0.2 + 0.15*(0.8*0.5)*1 = 0.2 + 0.06 = 0.26
            float org = InsurgencyRules.OrganizationTick(0.2f, 0.8f, 0.5f, 1f);
            Assert.AreEqual(0.26f, org, Eps);

            // 外部扇動0＝育たない（散発的不満のまま）
            float noAgit = InsurgencyRules.OrganizationTick(0.2f, 0f, 1f, 1f);
            Assert.AreEqual(0.2f, noAgit, Eps);

            // 現地不満0＝育たない
            float noGrievance = InsurgencyRules.OrganizationTick(0.2f, 1f, 0f, 1f);
            Assert.AreEqual(0.2f, noGrievance, Eps);
        }

        /// <summary>外部支援の底上げ：武器・資金・指導の平均×組織化×重みで武装を引き上げる。</summary>
        [Test]
        public void ExternalSupportBoost_LiftsArmedStrength()
        {
            // support = (0.9+0.6+0.3)/3 = 0.6 ; boost = 0.5*0.6*0.6 = 0.18
            float boost = InsurgencyRules.ExternalSupportBoost(0.5f, 0.9f, 0.6f, 0.3f);
            Assert.AreEqual(0.18f, boost, Eps);

            // 組織化0＝受け皿が無く支援は活きない
            float noOrg = InsurgencyRules.ExternalSupportBoost(0f, 1f, 1f, 1f);
            Assert.AreEqual(0f, noOrg, Eps);
        }

        /// <summary>反乱圧力の増幅：組織化された反乱×占領者の脆弱性で内生の反乱圧を乗算で膨らませる。</summary>
        [Test]
        public void RebelPressureAmplification_ScalesPressure()
        {
            // t = 0.5*0.5 = 0.25 ; lerp(1,2,0.25) = 1.25
            float amp = InsurgencyRules.RebelPressureAmplification(0.5f, 0.5f);
            Assert.AreEqual(1.25f, amp, Eps);

            // 反乱が無い＝倍率1.0（増幅なし＝後方互換）
            float none = InsurgencyRules.RebelPressureAmplification(0f, 1f);
            Assert.AreEqual(1f, none, Eps);

            // 最大＝maxAmplification
            float full = InsurgencyRules.RebelPressureAmplification(1f, 1f);
            Assert.AreEqual(2f, full, Eps);
        }

        /// <summary>住民支持：占領者の暴虐と反乱統治で支持が反乱側へ動く（民心が反乱の海）。</summary>
        [Test]
        public void PopularSupportTick_MovesWithBrutalityAndGovernance()
        {
            // push = 0.6*0.5 + 0.5*0.4 = 0.3 + 0.2 = 0.5 ; delta = 0.1*0.5*1 = 0.05
            float sup = InsurgencyRules.PopularSupportTick(0.4f, 0.6f, 0.5f, 1f);
            Assert.AreEqual(0.45f, sup, Eps);

            // 暴虐も反乱統治も無ければ支持は動かない
            float still = InsurgencyRules.PopularSupportTick(0.4f, 0f, 0f, 1f);
            Assert.AreEqual(0.4f, still, Eps);
        }

        /// <summary>聖域：国境への近さ×隣国の安全地帯が反乱を持続させる。</summary>
        [Test]
        public void SafeHaven_SustainsNearBorderWithSanctuary()
        {
            // t = 0.8*0.5 = 0.4 ; haven = 0.4*0.5 = 0.2
            float haven = InsurgencyRules.SafeHaven(0.8f, 0.5f);
            Assert.AreEqual(0.2f, haven, Eps);

            // 隣国の安全地帯が無ければ聖域なし
            float none = InsurgencyRules.SafeHaven(1f, 0f);
            Assert.AreEqual(0f, none, Eps);
        }

        /// <summary>対反乱作戦：住民保護を伴う掃討は効くが、暴力一辺倒の掃討は逆効果で目減りする。</summary>
        [Test]
        public void CounterInsurgencyEffect_RewardsPopulationProtection()
        {
            // 保護1：backlash 0 ; effect = 0.2*0.8*1*1 = 0.16
            float protected_ = InsurgencyRules.CounterInsurgencyEffect(0.8f, 1f, 1f);
            Assert.AreEqual(0.16f, protected_, Eps);

            // 保護0：backlash = 1*0.4 = 0.4 ; effective = 0.8*(1-0.4)=0.48 ; effect = 0.2*0.48 = 0.096
            float heavyHanded = InsurgencyRules.CounterInsurgencyEffect(0.8f, 0f, 1f);
            Assert.AreEqual(0.096f, heavyHanded, Eps);

            // 保護を伴うほうが反乱をよく削る
            Assert.Greater(protected_, heavyHanded);
        }

        /// <summary>内戦への拡大：組織化された武装反乱だけがゲリラ戦を本格内戦へ押し上げる。</summary>
        [Test]
        public void InsurgencyEscalation_RequiresOrganizationAndArms()
        {
            // t = 0.6*0.5 = 0.3 ; esc = 0.08*0.3*1 = 0.024
            float esc = InsurgencyRules.InsurgencyEscalation(0.6f, 0.5f, 1f);
            Assert.AreEqual(0.024f, esc, Eps);

            // 武装が無ければ内戦へ拡大しない
            float unarmed = InsurgencyRules.InsurgencyEscalation(1f, 0f, 1f);
            Assert.AreEqual(0f, unarmed, Eps);
        }

        /// <summary>組織的反乱判定：組織化×外部支援が閾値以上＝外部支援された組織的反乱に成長した。</summary>
        [Test]
        public void IsOrganizedInsurgency_NeedsBothOrganizationAndSupport()
        {
            // 0.8*0.6 = 0.48 >= 0.4 ＝成立
            Assert.IsTrue(InsurgencyRules.IsOrganizedInsurgency(0.8f, 0.6f, 0.4f));

            // 散発的不満（組織化低）＝支援だけでは届かない（0.2*1=0.2 < 0.4）
            Assert.IsFalse(InsurgencyRules.IsOrganizedInsurgency(0.2f, 1f, 0.4f));

            // 外部支援が無ければ組織だけでは届かない（1*0.2=0.2 < 0.4）
            Assert.IsFalse(InsurgencyRules.IsOrganizedInsurgency(1f, 0.2f, 0.4f));
        }

        /// <summary>既定 Params の具体値を固定（回帰防止）。</summary>
        [Test]
        public void Default_HasExpectedValues()
        {
            var p = InsurgencyParams.Default;
            Assert.AreEqual(0.15f, p.organizationRate, Eps);
            Assert.AreEqual(0.6f, p.externalSupportWeight, Eps);
            Assert.AreEqual(2.0f, p.maxAmplification, Eps);
            Assert.AreEqual(0.1f, p.supportRate, Eps);
            Assert.AreEqual(0.5f, p.brutalityWeight, Eps);
            Assert.AreEqual(0.4f, p.insurgentGovWeight, Eps);
            Assert.AreEqual(0.5f, p.safeHavenWeight, Eps);
            Assert.AreEqual(0.2f, p.counterInsurgencyRate, Eps);
            Assert.AreEqual(0.4f, p.heavyHandedBacklash, Eps);
            Assert.AreEqual(0.08f, p.escalationRate, Eps);
        }
    }
}
