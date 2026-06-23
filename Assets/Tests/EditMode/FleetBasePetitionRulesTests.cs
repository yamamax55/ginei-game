using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>自艦隊の根拠地変更具申の effectKey 直列化・復号（星系ID -1=一任を許容）。</summary>
    public class FleetBasePetitionRulesTests
    {
        [Test]
        public void Encode_Decode_RoundTrips_WithSpecificSystem()
        {
            string key = FleetBasePetitionRules.EncodeEffectKey(3, 12);
            Assert.AreEqual("fleet.base:3:12", key);
            Assert.IsTrue(FleetBasePetitionRules.TryDecode(key, out int num, out int sys));
            Assert.AreEqual(3, num);
            Assert.AreEqual(12, sys);
        }

        [Test]
        public void Encode_AllowsUnspecifiedSystem_AndClampsNegativeNumber()
        {
            string key = FleetBasePetitionRules.EncodeEffectKey(-1, FleetBasePetitionRules.UnspecifiedSystem);
            Assert.AreEqual("fleet.base:0:-1", key); // 番号は0クランプ・星系IDは -1 を保持
            Assert.IsTrue(FleetBasePetitionRules.TryDecode(key, out int num, out int sys));
            Assert.AreEqual(0, num);
            Assert.AreEqual(-1, sys);
        }

        [Test]
        public void IsBasePetition_DistinguishesKeys()
        {
            Assert.IsTrue(FleetBasePetitionRules.IsBasePetition("fleet.base:0:-1"));
            Assert.IsFalse(FleetBasePetitionRules.IsBasePetition("fleet.budget:0:1000"));
            Assert.IsFalse(FleetBasePetitionRules.IsBasePetition("career.petition"));
            Assert.IsFalse(FleetBasePetitionRules.IsBasePetition(null));
        }

        [Test]
        public void TryDecode_RejectsMalformed()
        {
            Assert.IsFalse(FleetBasePetitionRules.TryDecode("fleet.base:3", out _, out _));    // 区切り不足
            Assert.IsFalse(FleetBasePetitionRules.TryDecode("fleet.base:x:y", out _, out _));  // 非数値
            Assert.IsFalse(FleetBasePetitionRules.TryDecode("career.petition", out _, out _));
        }
    }
}
