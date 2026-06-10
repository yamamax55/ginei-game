using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 通信・指揮を固定する：遅延＝距離×妨害増幅、途絶閾値、命令の鮮度（半減期・静的戦況では腐らない）、
    /// 指揮実効度（途絶時は自律性が頼り＝集権軍は頭を断たれると止まる）。境界を担保。
    /// </summary>
    public class CommunicationsRulesTests
    {
        private static readonly CommsParams P = CommsParams.Default;
        // 遅延0.1/距離・妨害倍率3・途絶0.8・半減積5

        [Test]
        public void CommandDelay_DistanceAmplifiedByJamming()
        {
            Assert.AreEqual(1f, CommunicationsRules.CommandDelay(10f, 0f, P), 1e-5f);   // 10×0.1
            Assert.AreEqual(3f, CommunicationsRules.CommandDelay(10f, 1f, P), 1e-5f);   // 妨害満タン＝3倍
            Assert.AreEqual(2f, CommunicationsRules.CommandDelay(10f, 0.5f, P), 1e-5f); // 中間
            Assert.AreEqual(0f, CommunicationsRules.CommandDelay(0f, 1f, P), 1e-5f);    // 至近＝即達
        }

        [Test]
        public void IsCutOff_AtThreshold()
        {
            Assert.IsTrue(CommunicationsRules.IsCutOff(0.8f, P));
            Assert.IsFalse(CommunicationsRules.IsCutOff(0.79f, P));
        }

        [Test]
        public void OrderFreshness_HalfLifeByTempo()
        {
            // 遅延5×テンポ1＝半減積5＝鮮度0.5
            Assert.AreEqual(0.5f, CommunicationsRules.OrderFreshness(5f, 1f, P), 1e-5f);
            // 静的な戦況（tempo=0）＝古い命令も腐らない
            Assert.AreEqual(1f, CommunicationsRules.OrderFreshness(100f, 0f, P), 1e-5f);
            // 即達＝満額
            Assert.AreEqual(1f, CommunicationsRules.OrderFreshness(0f, 1f, P), 1e-5f);
        }

        [Test]
        public void CommandEffectiveness_CutoffFallsBackToAutonomy()
        {
            // 途絶：集権軍（自律0）は止まる
            Assert.AreEqual(0f, CommunicationsRules.CommandEffectiveness(10f, 1f, 1f, 0f, P), 1e-5f);
            // 途絶：自律軍は自律性で戦う
            Assert.AreEqual(0.8f, CommunicationsRules.CommandEffectiveness(10f, 1f, 1f, 0.8f, P), 1e-5f);
        }

        [Test]
        public void CommandEffectiveness_FreshnessWhenConnected()
        {
            // 通信下・自律0＝命令の鮮度そのまま（遅延1×テンポ1→1/(1+0.2)≈0.8333）
            float freshness = CommunicationsRules.OrderFreshness(1f, 1f, P);
            Assert.AreEqual(freshness, CommunicationsRules.CommandEffectiveness(10f, 0f, 1f, 0f, P), 1e-5f);
            // 自律性は通信下でも下支え＝自律0より高い
            Assert.Greater(CommunicationsRules.CommandEffectiveness(10f, 0f, 1f, 1f, P),
                           CommunicationsRules.CommandEffectiveness(10f, 0f, 1f, 0f, P));
        }
    }
}
