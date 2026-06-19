using NUnit.Framework;
using UnityEngine;

namespace Ginei.Tests
{
    /// <summary>欺瞞計画（偽の作戦・主攻方向・戦力を一貫して見せ敵に誤った全体像を信じ込ませる＝ボディガード作戦型）純ロジックの EditMode テスト（既定 Params 期待値固定）。</summary>
    public class DeceptionPlanRulesTests
    {
        const float Eps = 1e-4f;

        [Test]
        public void StoryConsistencyRisesWithSignalsAndFallsWithLeaks()
        {
            // 5*0.2 - 2*0.3 = 1.0 - 0.6 = 0.4。
            Assert.AreEqual(0.4f, DeceptionPlanRules.StoryConsistency(5, 2), Eps);
            // 漏れ無しで信号を重ねれば上限1.0：10*0.2=2.0 → clamp01。
            Assert.AreEqual(1f, DeceptionPlanRules.StoryConsistency(10, 0), Eps);
            // 綻び（漏れ）は一致より重く効き、筋が崩れると0。
            Assert.AreEqual(0f, DeceptionPlanRules.StoryConsistency(1, 5), Eps);
        }

        [Test]
        public void BelievabilityBlendsConsistencyAndPreconception()
        {
            // lerp(0.4, 0.8, 0.5) = 0.6。先入観に沿うほど信じる。
            Assert.AreEqual(0.6f, DeceptionPlanRules.Believability(0.4f, 0.8f), Eps);
            // 一貫していても先入観に反すれば信じやすさは中庸：lerp(1,0,0.5)=0.5。
            Assert.AreEqual(0.5f, DeceptionPlanRules.Believability(1f, 0f), Eps);
        }

        [Test]
        public void EnemyMisdirectionScalesWithBelievabilityAndMagnitude()
        {
            // 0.6*0.5 = 0.3。信じた敵ほど大きく誤る。
            Assert.AreEqual(0.3f, DeceptionPlanRules.EnemyMisdirection(0.6f, 0.5f), Eps);
            // 信じなければ誤らない。
            Assert.AreEqual(0f, DeceptionPlanRules.EnemyMisdirection(0f, 1f), Eps);
        }

        [Test]
        public void ResourceMisallocationIsMisdirectionTimesForce()
        {
            // 0.3*1000 = 300。誤った全体像のぶん別方向へ配置。
            Assert.AreEqual(300f, DeceptionPlanRules.ResourceMisallocation(0.3f, 1000f), Eps);
            // 敵戦力が無ければ誤配置も0。
            Assert.AreEqual(0f, DeceptionPlanRules.ResourceMisallocation(0.5f, 0f), Eps);
        }

        [Test]
        public void RealOperationSecurityIsBoostedByDistraction()
        {
            // headroom=1-0.4=0.6, 0.4 + 0.8*0.5*0.6 = 0.4+0.24 = 0.64。
            Assert.AreEqual(0.64f, DeceptionPlanRules.RealOperationSecurity(0.8f, 0.4f), Eps);
            // 素の秘匿が0でも欺瞞の注意逸らしで底上げ：0 + 1*0.5*1 = 0.5。
            Assert.AreEqual(0.5f, DeceptionPlanRules.RealOperationSecurity(1f, 0f), Eps);
        }

        [Test]
        public void ExposureRiskScalesWithLeaksAndCounterIntel()
        {
            // 0.5*0.6*1.0 = 0.3。綻びが多く敵の眼が利くほど露見。
            Assert.AreEqual(0.3f, DeceptionPlanRules.ExposureRisk(0.5f, 0.6f), Eps);
            // 漏れが無ければ見破られない。
            Assert.AreEqual(0f, DeceptionPlanRules.ExposureRisk(0f, 1f), Eps);
        }

        [Test]
        public void DeceptionPayoffDiscountsGainByExposure()
        {
            // 0.3*(1-0.3) = 0.21。露見リスクのぶん効果が削られる。
            Assert.AreEqual(0.21f, DeceptionPlanRules.DeceptionPayoff(0.3f, 0.3f), Eps);
            // 完全に見破られれば効果は消える。
            Assert.AreEqual(0f, DeceptionPlanRules.DeceptionPayoff(0.5f, 1f), Eps);
        }

        [Test]
        public void IsDeceptionBelievedUsesThreshold()
        {
            // 既定閾値0.5以上で信じられた。
            Assert.IsTrue(DeceptionPlanRules.IsDeceptionBelieved(0.6f));
            Assert.IsFalse(DeceptionPlanRules.IsDeceptionBelieved(0.4f));
        }

        [Test]
        public void BodyguardStory_ConsistentLieFoolsEnemyAndCoversRealOp()
        {
            // 一致信号を多く重ね綻びを抑えた整合した欺瞞は筋が通る。
            float story = DeceptionPlanRules.StoryConsistency(6, 1); // 6*0.2 - 1*0.3 = 1.2-0.3 = 0.9 → clamp01=0.9
            Assert.AreEqual(0.9f, story, Eps);
            // 敵の先入観（別方向を警戒）に沿わせると信じやすさが高い：lerp(0.9,0.8,0.5)=0.85。
            float believe = DeceptionPlanRules.Believability(story, 0.8f);
            Assert.AreEqual(0.85f, believe, Eps);
            Assert.IsTrue(DeceptionPlanRules.IsDeceptionBelieved(believe));
            // 信じた敵は大胆な欺瞞ぶん判断を誤る：0.85*0.8=0.68。
            float misdirect = DeceptionPlanRules.EnemyMisdirection(believe, 0.8f);
            Assert.AreEqual(0.68f, misdirect, Eps);
            // 誤った重点へ戦力を割く：0.68*1000=680。
            float misalloc = DeceptionPlanRules.ResourceMisallocation(misdirect, 1000f);
            Assert.AreEqual(680f, misalloc, Eps);
            // 露見リスクは小さく（漏れ0.1×防諜0.5×1.0=0.05）、欺瞞の正味効果は高い。
            float exposure = DeceptionPlanRules.ExposureRisk(0.1f, 0.5f);
            Assert.AreEqual(0.05f, exposure, Eps);
            float payoff = DeceptionPlanRules.DeceptionPayoff(misalloc, exposure); // misalloc clamp01=1, *(1-0.05)=0.95
            Assert.AreEqual(0.95f, payoff, Eps);
            // 欺瞞が敵の眼を引きつけ本作戦を覆う（秘匿が底上げされる）。
            float security = DeceptionPlanRules.RealOperationSecurity(0.8f, 0.4f);
            Assert.Greater(security, 0.4f);
        }
    }
}
