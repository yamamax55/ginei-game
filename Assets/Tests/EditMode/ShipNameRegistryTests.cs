using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>旗艦名台帳：払い出し・返却・永久欠番・プール枯渇フォールバック。</summary>
    public class ShipNameRegistryTests
    {
        [SetUp]
        public void Setup() => ShipNameRegistry.Clear();

        [TearDown]
        public void Cleanup() => ShipNameRegistry.Clear();

        [Test]
        public void Pool_HasAroundTwoHundredUniqueNames()
        {
            Assert.GreaterOrEqual(HeritageShipNames.Count, 180);
            // 重複なし。
            var set = new System.Collections.Generic.HashSet<string>(HeritageShipNames.Names);
            Assert.AreEqual(HeritageShipNames.Count, set.Count);
        }

        [Test]
        public void Assign_GivesPoolOrderMinimumAvailable()
        {
            string a = ShipNameRegistry.Assign();
            Assert.AreEqual(HeritageShipNames.Names[0], a);
            Assert.IsTrue(ShipNameRegistry.IsInUse(a));
            string b = ShipNameRegistry.Assign();
            Assert.AreEqual(HeritageShipNames.Names[1], b);
            Assert.AreNotEqual(a, b);
        }

        [Test]
        public void Release_ReturnsNameToPool()
        {
            string a = ShipNameRegistry.Assign();
            ShipNameRegistry.Release(a);
            Assert.IsFalse(ShipNameRegistry.IsInUse(a));
            // 返却された名は再び最小の空きとして払い出される。
            string again = ShipNameRegistry.Assign();
            Assert.AreEqual(a, again);
        }

        [Test]
        public void Retire_PermanentlyExcludesName()
        {
            string a = ShipNameRegistry.Assign();
            ShipNameRegistry.Retire(a);
            Assert.IsTrue(ShipNameRegistry.IsRetired(a));
            Assert.IsFalse(ShipNameRegistry.IsInUse(a));
            // 永久欠番は再払い出しされない。
            string next = ShipNameRegistry.Assign();
            Assert.AreNotEqual(a, next);
            Assert.AreEqual(HeritageShipNames.Names[1], next);
        }

        [Test]
        public void AvailableCount_TracksUseAndRetire()
        {
            int total = HeritageShipNames.Count;
            Assert.AreEqual(total, ShipNameRegistry.AvailableCount);
            ShipNameRegistry.Assign();           // 使用中で-1
            Assert.AreEqual(total - 1, ShipNameRegistry.AvailableCount);
            string b = ShipNameRegistry.Assign();
            ShipNameRegistry.Retire(b);          // 永久欠番でも-1（使用中から外れて欠番へ）
            Assert.AreEqual(total - 2, ShipNameRegistry.AvailableCount);
        }

        [Test]
        public void Assign_FallsBackToSuffixWhenPoolExhausted()
        {
            int total = HeritageShipNames.Count;
            for (int i = 0; i < total; i++) ShipNameRegistry.Assign();
            Assert.AreEqual(0, ShipNameRegistry.AvailableCount);
            // 枯渇後も命名は止まらない（連番サフィックス）。
            string overflow = ShipNameRegistry.Assign();
            Assert.IsTrue(overflow.EndsWith(" II"), overflow);
            Assert.IsTrue(ShipNameRegistry.IsInUse(overflow));
        }
    }
}
