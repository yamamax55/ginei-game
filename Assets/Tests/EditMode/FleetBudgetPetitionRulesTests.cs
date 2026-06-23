using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>自艦隊予算具申の effectKey 直列化・復号・増額目安。</summary>
    public class FleetBudgetPetitionRulesTests
    {
        [Test]
        public void Encode_Decode_RoundTrips()
        {
            string key = FleetBudgetPetitionRules.EncodeEffectKey(3, 5000);
            Assert.AreEqual("fleet.budget:3:5000", key);
            Assert.IsTrue(FleetBudgetPetitionRules.TryDecode(key, out int num, out int amt));
            Assert.AreEqual(3, num);
            Assert.AreEqual(5000, amt);
        }

        [Test]
        public void Encode_ClampsNegativeToZero()
        {
            Assert.AreEqual("fleet.budget:0:0", FleetBudgetPetitionRules.EncodeEffectKey(-1, -100));
        }

        [Test]
        public void IsBudgetPetition_DistinguishesKeys()
        {
            Assert.IsTrue(FleetBudgetPetitionRules.IsBudgetPetition("fleet.budget:0:1000"));
            Assert.IsFalse(FleetBudgetPetitionRules.IsBudgetPetition("career.petition"));
            Assert.IsFalse(FleetBudgetPetitionRules.IsBudgetPetition(null));
        }

        [Test]
        public void TryDecode_RejectsMalformed()
        {
            Assert.IsFalse(FleetBudgetPetitionRules.TryDecode("career.petition", out _, out _));
            Assert.IsFalse(FleetBudgetPetitionRules.TryDecode("fleet.budget:3", out _, out _));       // 区切り不足
            Assert.IsFalse(FleetBudgetPetitionRules.TryDecode("fleet.budget:x:y", out _, out _));     // 非数値
        }

        [Test]
        public void RecommendedIncrease_FractionOfBudget()
        {
            Assert.AreEqual(2000, FleetBudgetPetitionRules.RecommendedIncrease(10000)); // 既定20%
            Assert.AreEqual(3000, FleetBudgetPetitionRules.RecommendedIncrease(10000, 0.3f));
            Assert.AreEqual(0, FleetBudgetPetitionRules.RecommendedIncrease(-5));
        }
    }
}
