using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 秘密結社（地球教型）を固定する：浸透tick＝絶望が深いほど速い、要職到達＝浸透の二乗、
    /// 政策の歪み＝到達×上限、露見＝静かな網は見えない、摘発＝解明度ゼロなら剥がせない（見えている分だけ）、
    /// 再生＝残った根から戻る・根絶なら戻らない。境界・決定論を担保。
    /// </summary>
    public class SecretSocietyRulesTests
    {
        private static readonly SecretSocietyParams P = SecretSocietyParams.Default;
        // 浸透0.1/絶望下限0.2/歪み上限0.5/露見0.8/摘発上限0.7/再生0.05

        [Test]
        public void InfiltrationTick_DespairFeedsTheNetwork()
        {
            // 絶望満点：0.5 + 0.1×1×1×(1-0.5) = 0.55
            Assert.AreEqual(0.55f, SecretSocietyRules.InfiltrationTick(0.5f, 1f, 1f, 1f, P), 1e-5f);
            // 絶望ゼロでも下限0.2の分だけ伸びる（迷い子は尽きない）：0.5 + 0.1×0.2×0.5 = 0.51
            Assert.AreEqual(0.51f, SecretSocietyRules.InfiltrationTick(0.5f, 1f, 0f, 1f, P), 1e-5f);
            // 吸引力ゼロ＝誰も招けない
            Assert.AreEqual(0.5f, SecretSocietyRules.InfiltrationTick(0.5f, 0f, 1f, 1f, P), 1e-5f);
            // 飽和＝勧誘先が尽きる
            Assert.AreEqual(1f, SecretSocietyRules.InfiltrationTick(1f, 1f, 1f, 10f, P), 1e-5f);
        }

        [Test]
        public void HighOfficeReach_SquaredPenetration()
        {
            // 深く浸透して初めて中枢に届く（浅い網は雑兵ばかり）
            Assert.AreEqual(0.25f, SecretSocietyRules.HighOfficeReach(0.5f), 1e-5f);
            Assert.AreEqual(0.01f, SecretSocietyRules.HighOfficeReach(0.1f), 1e-5f);
            Assert.AreEqual(1f, SecretSocietyRules.HighOfficeReach(1f), 1e-5f);
            Assert.AreEqual(0f, SecretSocietyRules.HighOfficeReach(0f), 1e-5f);
        }

        [Test]
        public void PolicyDistortion_CappedInvisibleHand()
        {
            Assert.AreEqual(0.125f, SecretSocietyRules.PolicyDistortion(0.25f, P), 1e-5f);
            Assert.AreEqual(0.5f, SecretSocietyRules.PolicyDistortion(1f, P), 1e-5f); // 上限＝国を丸ごとは操れない
            Assert.AreEqual(0f, SecretSocietyRules.PolicyDistortion(0f, P), 1e-5f);
        }

        [Test]
        public void VisibilityRisk_QuietNetworkIsInvisible()
        {
            // 静かに潜む網は完全に見えない＝見えない敵とは戦えない
            Assert.AreEqual(0f, SecretSocietyRules.VisibilityRisk(1f, 0f, P), 1e-5f);
            // 大きく動くほど見える：0.8×0.5×0.5 = 0.2
            Assert.AreEqual(0.2f, SecretSocietyRules.VisibilityRisk(0.5f, 0.5f, P), 1e-5f);
            // 全力活動でも確実には掴ませない（露見係数0.8）
            Assert.AreEqual(0.8f, SecretSocietyRules.VisibilityRisk(1f, 1f, P), 1e-5f);
        }

        [Test]
        public void CrackdownEffect_OnlyVisiblePartCanBeTorn()
        {
            // 解明度ゼロ＝何も見えていない当局は空振り（浸透は無傷）
            Assert.AreEqual(0.5f, SecretSocietyRules.CrackdownEffect(0.5f, 1f, 0f, P), 1e-5f);
            // 満点の摘発でも上限0.7＝根は残る：0.5×(1-0.7) = 0.15
            Assert.AreEqual(0.15f, SecretSocietyRules.CrackdownEffect(0.5f, 1f, 1f, P), 1e-5f);
            // 半分しか解明できていなければ半分しか剥がせない：0.5×(1-0.5) = 0.25
            Assert.AreEqual(0.25f, SecretSocietyRules.CrackdownEffect(0.5f, 1f, 0.5f, P), 1e-5f);
            // 強度×解明度の積：0.5×(1-0.25) = 0.375
            Assert.AreEqual(0.375f, SecretSocietyRules.CrackdownEffect(0.5f, 0.5f, 0.5f, P), 1e-5f);
        }

        [Test]
        public void RegrowthTick_SurvivingRootsRegrow()
        {
            // 0.5 + 0.05×0.5×(1-0.5) = 0.5125
            Assert.AreEqual(0.5125f, SecretSocietyRules.RegrowthTick(0.5f, 1f, P), 1e-5f);
            // 完全な根絶なら二度と戻らない
            Assert.AreEqual(0f, SecretSocietyRules.RegrowthTick(0f, 1f, P), 1e-5f);
            // 飽和は超えない
            Assert.AreEqual(1f, SecretSocietyRules.RegrowthTick(1f, 1f, P), 1e-5f);
        }

        [Test]
        public void DefaultOverloads_MatchExplicitParams()
        {
            Assert.AreEqual(
                SecretSocietyRules.InfiltrationTick(0.3f, 0.8f, 0.6f, 1f, P),
                SecretSocietyRules.InfiltrationTick(0.3f, 0.8f, 0.6f, 1f), 1e-6f);
            Assert.AreEqual(
                SecretSocietyRules.CrackdownEffect(0.4f, 0.7f, 0.5f, P),
                SecretSocietyRules.CrackdownEffect(0.4f, 0.7f, 0.5f), 1e-6f);
        }
    }
}
