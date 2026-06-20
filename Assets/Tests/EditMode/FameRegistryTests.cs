using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>武名の実行時台帳（ADM-3 #2304・<see cref="FameRegistry"/>）を固定する：get/set・0〜100クランプ・clear。</summary>
    public class FameRegistryTests
    {
        [SetUp]
        public void Reset() => FameRegistry.Clear();

        [Test]
        public void Get_Unregistered_IsZero()
        {
            Assert.AreEqual(0, FameRegistry.Get(1));
            Assert.IsFalse(FameRegistry.Has(1));
        }

        [Test]
        public void Set_StoresAndClamps()
        {
            FameRegistry.Set(7, 42);
            Assert.AreEqual(42, FameRegistry.Get(7));
            FameRegistry.Set(7, 250);
            Assert.AreEqual(100, FameRegistry.Get(7));
            FameRegistry.Set(7, -5);
            Assert.AreEqual(0, FameRegistry.Get(7));
        }

        [Test]
        public void Clear_Empties()
        {
            FameRegistry.Set(1, 10);
            FameRegistry.Clear();
            Assert.AreEqual(0, FameRegistry.Count);
            Assert.IsFalse(FameRegistry.Has(1));
        }
    }
}
