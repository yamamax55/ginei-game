using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 主人公の死後の帰結（採用「主人公の死で詰まない」・#907 の解答・<see cref="ProtagonistSuccessionRules"/>）を固定する：
    /// 平民＝一代記完結→新規／貴族・王家＝継承（後継者が居れば）／継承身分でも後継者不在は断絶＝新規。
    /// </summary>
    public class ProtagonistSuccessionRulesTests
    {
        [Test]
        public void Commoner_AlwaysFreshGame()
        {
            Assert.AreEqual(ProtagonistDeathOutcome.一代記完結_新規,
                ProtagonistSuccessionRules.OnDeath(PersonOrigin.平民, hasHeir: true));
            Assert.AreEqual(ProtagonistDeathOutcome.一代記完結_新規,
                ProtagonistSuccessionRules.OnDeath(PersonOrigin.平民, hasHeir: false));
            Assert.IsFalse(ProtagonistSuccessionRules.ContinuesAsHeir(PersonOrigin.平民, true));
        }

        [Test]
        public void NobleRoyal_WithHeir_Inherits()
        {
            Assert.AreEqual(ProtagonistDeathOutcome.継承_世継ぎ,
                ProtagonistSuccessionRules.OnDeath(PersonOrigin.貴族, hasHeir: true));
            Assert.AreEqual(ProtagonistDeathOutcome.継承_世継ぎ,
                ProtagonistSuccessionRules.OnDeath(PersonOrigin.王家, hasHeir: true));
            Assert.IsTrue(ProtagonistSuccessionRules.ContinuesAsHeir(PersonOrigin.王家, true));
        }

        [Test]
        public void NobleRoyal_NoHeir_LineEnds_FreshGame()
        {
            Assert.AreEqual(ProtagonistDeathOutcome.一代記完結_新規,
                ProtagonistSuccessionRules.OnDeath(PersonOrigin.貴族, hasHeir: false));
            Assert.AreEqual(ProtagonistDeathOutcome.一代記完結_新規,
                ProtagonistSuccessionRules.OnDeath(PersonOrigin.王家, hasHeir: false));
        }
    }
}
