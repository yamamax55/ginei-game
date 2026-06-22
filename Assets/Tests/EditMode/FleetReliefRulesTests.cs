using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>艦隊慰労（私財の労い）：兵力比例の費用・支払い可否・士気上昇。</summary>
    public class FleetReliefRulesTests
    {
        private static readonly FleetReliefParams P = FleetReliefParams.Default; // 兵力×0.5／士気+10

        [Test]
        public void Cost_ScalesWithStrength()
        {
            Assert.AreEqual(5000, FleetReliefRules.Cost(10000, P));
            Assert.AreEqual(0, FleetReliefRules.Cost(-100, P)); // 非負
        }

        [Test]
        public void CanAfford_ComparesPrivateCash()
        {
            Assert.IsTrue(FleetReliefRules.CanAfford(5000f, 10000, P));  // ちょうど足りる
            Assert.IsFalse(FleetReliefRules.CanAfford(4999f, 10000, P)); // 1足りない
            Assert.IsTrue(FleetReliefRules.CanAfford(99999f, 10000, P));
        }

        [Test]
        public void MoraleGain_IsNonNegativeParam()
        {
            Assert.AreEqual(10, FleetReliefRules.MoraleGain(P));
            Assert.AreEqual(0, FleetReliefRules.MoraleGain(new FleetReliefParams(0.5f, -5)));
        }
    }
}
