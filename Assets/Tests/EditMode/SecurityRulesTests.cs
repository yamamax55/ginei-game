using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 秘密警察（シュタージ型・#166）を固定する：監視網＋密告者網が反対派を抑圧し陰謀を摘発する一方、
    /// 弾圧が支持を蝕む（恐怖統治のトレードオフ）。境界・各分岐・clamp を決定論で担保する。
    /// </summary>
    public class SecurityRulesTests
    {
        private static readonly SecurityParams P = SecurityParams.Default; // surv0.6 / inf0.4 / base0.1 / cost0.5

        [Test]
        public void DissentSuppression_WeightedReach_TimesDissent()
        {
            // reach = 0.5*0.6 + 0.5*0.4 = 0.5、dissent 0.4 → 0.2
            var a = new SecurityApparatus(1, surveillance: 0.5f, informantNetwork: 0.5f);
            Assert.AreEqual(0.2f, SecurityRules.DissentSuppression(a, 0.4f, P), 1e-4f);
        }

        [Test]
        public void DissentSuppression_NoDissent_OrNullApparatus_IsZero()
        {
            var a = new SecurityApparatus(1, surveillance: 1f, informantNetwork: 1f);
            Assert.AreEqual(0f, SecurityRules.DissentSuppression(a, 0f, P), 1e-4f); // 反対派なし＝抑圧の余地なし
            Assert.AreEqual(0f, SecurityRules.DissentSuppression(null, 0.5f, P), 1e-4f); // 装置なし＝0
        }

        [Test]
        public void DissentSuppression_ClampsInputs()
        {
            // surv/inf は ctor で clamp 済み、dissent は引数で clamp（過大入力でも 0..1）
            var a = new SecurityApparatus(1, surveillance: 2f, informantNetwork: 2f); // → 1/1、reach=1
            Assert.AreEqual(1f, SecurityRules.DissentSuppression(a, 5f, P), 1e-4f);    // dissent>1 → 1
        }

        [Test]
        public void CoupDetection_BaseOnly_WhenNoApparatus()
        {
            // 装置ゼロ・null でも基準率ぶんは偶発露見
            Assert.AreEqual(0.1f, SecurityRules.CoupDetectionChance(null, 5, P), 1e-4f);
            var empty = new SecurityApparatus(1);
            Assert.AreEqual(0.1f, SecurityRules.CoupDetectionChance(empty, 5, P), 1e-4f);
        }

        [Test]
        public void CoupDetection_RisesWithReachAndPlotters()
        {
            // reach=1、plotters=10 → scale=1 → 0.1 + 1*1 = 1.1 → clamp 1
            var a = new SecurityApparatus(1, surveillance: 1f, informantNetwork: 1f);
            Assert.AreEqual(1f, SecurityRules.CoupDetectionChance(a, 10, P), 1e-4f);

            // 謀議者が少ないほど漏れにくい：plotters=5 → scale=0.5 → 0.1 + 1*0.5 = 0.6
            Assert.AreEqual(0.6f, SecurityRules.CoupDetectionChance(a, 5, P), 1e-4f);
        }

        [Test]
        public void CoupDetection_PlotterScale_Saturates_AndClampsNegative()
        {
            var a = new SecurityApparatus(1, surveillance: 1f, informantNetwork: 1f);
            // plotters>PlotterScale でも scale は 1 で飽和（漏れやすさ一定）
            Assert.AreEqual(SecurityRules.CoupDetectionChance(a, SecurityRules.PlotterScale, P),
                            SecurityRules.CoupDetectionChance(a, 100, P), 1e-4f);
            // 負の謀議者数は基準率まで clamp（scale が負→0）
            Assert.AreEqual(0.1f, SecurityRules.CoupDetectionChance(a, -3, P), 1e-4f);
        }

        [Test]
        public void RepressionSupportPenalty_ScalesWithRepression_AndClamps()
        {
            Assert.AreEqual(0f, SecurityRules.RepressionSupportPenalty(0f, P), 1e-4f);
            Assert.AreEqual(0.25f, SecurityRules.RepressionSupportPenalty(0.5f, P), 1e-4f); // 0.5*0.5
            Assert.AreEqual(0.5f, SecurityRules.RepressionSupportPenalty(1f, P), 1e-4f);
            Assert.AreEqual(0.5f, SecurityRules.RepressionSupportPenalty(2f, P), 1e-4f);    // 入力 clamp
        }

        [Test]
        public void DefaultOverloads_MatchExplicitParams()
        {
            var a = new SecurityApparatus(1, surveillance: 0.5f, informantNetwork: 0.5f);
            Assert.AreEqual(SecurityRules.DissentSuppression(a, 0.4f, P),
                            SecurityRules.DissentSuppression(a, 0.4f), 1e-4f);
            Assert.AreEqual(SecurityRules.CoupDetectionChance(a, 5, P),
                            SecurityRules.CoupDetectionChance(a, 5), 1e-4f);
            Assert.AreEqual(SecurityRules.RepressionSupportPenalty(0.5f, P),
                            SecurityRules.RepressionSupportPenalty(0.5f), 1e-4f);
        }
    }
}
