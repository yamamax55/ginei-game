using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 世論戦（プロパガンダ）を固定する：効果は到達×信用×主張×(1−検閲)、支持は自他効果の綱引きで動き、
    /// 真実との乖離が信用を浸食し（乖離無しは回復）、誇張で支えた高支持は「製造された合意」と判定される。
    /// 境界・クランプを担保。
    /// </summary>
    public class PropagandaRulesTests
    {
        private static readonly PropagandaParams P = PropagandaParams.Default; // 最大シフト0.1/浸食0.3/回復0.1/製造閾値0.5

        [Test]
        public void Effectiveness_ProductOfFactors()
        {
            // 到達0.5×信用1×主張1×(1−検閲0)=0.5
            Assert.AreEqual(0.5f, PropagandaRules.Effectiveness(0.5f, 1f, 1f, 0f, P), 1e-5f);
            // 検閲0.5で半減
            Assert.AreEqual(0.25f, PropagandaRules.Effectiveness(0.5f, 1f, 1f, 0.5f, P), 1e-5f);
            // 信用0なら効果0
            Assert.AreEqual(0f, PropagandaRules.Effectiveness(1f, 0f, 1f, 0f, P), 1e-5f);
        }

        [Test]
        public void SupportShift_TugOfWar()
        {
            // 自効果1・敵効果0＝net+1×0.1＝+0.1
            Assert.AreEqual(0.6f, PropagandaRules.SupportShift(0.5f, 1f, 0f, P), 1e-5f);
            // 拮抗＝据え置き
            Assert.AreEqual(0.5f, PropagandaRules.SupportShift(0.5f, 0.7f, 0.7f, P), 1e-5f);
            // 敵優勢＝低下＆クランプ
            Assert.AreEqual(0.4f, PropagandaRules.SupportShift(0.5f, 0f, 1f, P), 1e-5f);
            Assert.AreEqual(0f, PropagandaRules.SupportShift(0.05f, 0f, 1f, P), 1e-5f);
        }

        [Test]
        public void UpdateCredibility_ErodesOnLies_RecoversOnTruth()
        {
            // 乖離0.5は閾値ちょうど＝超過0＝回復側（+0.1×dt1）だが上限1
            Assert.AreEqual(1f, PropagandaRules.UpdateCredibility(1f, 0.5f, 1f, P), 1e-5f);
            Assert.AreEqual(0.6f, PropagandaRules.UpdateCredibility(0.5f, 0.5f, 1f, P), 1e-5f);
            // 乖離0.8は超過0.3＝0.3×浸食0.3×dt1=0.09 削れる
            Assert.AreEqual(0.91f, PropagandaRules.UpdateCredibility(1f, 0.8f, 1f, P), 1e-5f);
        }

        [Test]
        public void IsManufacturedConsent_HighSupportHeldByBigLie()
        {
            // 高支持(0.8)＋大乖離(0.6≥0.5)＝製造された合意
            Assert.IsTrue(PropagandaRules.IsManufacturedConsent(0.8f, 0.6f, P));
            // 乖離が小さければ本物の支持＝false
            Assert.IsFalse(PropagandaRules.IsManufacturedConsent(0.8f, 0.2f, P));
            // 支持が低ければそもそも支えていない＝false
            Assert.IsFalse(PropagandaRules.IsManufacturedConsent(0.3f, 0.9f, P));
        }

        [Test]
        public void PropagandaState_DefaultsAndClamp()
        {
            var s = new PropagandaState();
            Assert.AreEqual(1f, s.credibility, 1e-5f); // 既定で信用満タン
            var c = new PropagandaState(2f, 2f, -1f);
            Assert.AreEqual(1f, c.publicSupport, 1e-5f);
            Assert.AreEqual(1f, c.credibility, 1e-5f);
            Assert.AreEqual(0f, c.reach, 1e-5f);
        }
    }
}
