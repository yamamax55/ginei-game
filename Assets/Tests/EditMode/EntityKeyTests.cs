using NUnit.Framework;
using UnityEngine;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// <see cref="EntityKey"/>＝Unity 6000.6 の <c>GetEntityId</c>（64bit）移行にともなう同一性キーの単一窓口。
    /// 成長/武名/叙勲の台帳キーがここから採られるため、①null は <see cref="EntityKey.None"/>、
    /// ②別インスタンスは別キー、③同一インスタンスは何度呼んでも同じキー、を固定する。
    /// ③が崩れると会戦のたびに成長が別人の台帳へ積まれる（＝武勲・叙勲が消える）。
    /// </summary>
    public class EntityKeyTests
    {
        [Test]
        public void Null_ReturnsNone()
        {
            Assert.AreEqual(EntityKey.None, EntityKey.Of(null));
            Assert.AreEqual(0L, EntityKey.None);
        }

        [Test]
        public void SameInstance_ReturnsSameKey()
        {
            var a = ScriptableObject.CreateInstance<AdmiralData>();
            Assert.AreEqual(EntityKey.Of(a), EntityKey.Of(a));
            Assert.AreNotEqual(EntityKey.None, EntityKey.Of(a));
        }

        [Test]
        public void DifferentInstances_ReturnDifferentKeys()
        {
            var a = ScriptableObject.CreateInstance<AdmiralData>();
            var b = ScriptableObject.CreateInstance<AdmiralData>();
            Assert.AreNotEqual(EntityKey.Of(a), EntityKey.Of(b));
        }

        [Test]
        public void LedgerRoundTrip_UsesTheSameKey()
        {
            GrowthRegistry.Clear();
            var a = ScriptableObject.CreateInstance<AdmiralData>();
            // 会戦で経験を積む→別経路（セーブ復元と同じ引き方）で同じ台帳に届く。
            GrowthRegistry.GainExperience(EntityKey.Of(a), GrowthArchetype.叩き上げ, 10f, 1f);
            Assert.IsTrue(GrowthRegistry.Has(EntityKey.Of(a)));
            Assert.IsNotNull(GrowthRegistry.Get(EntityKey.Of(a)));
            GrowthRegistry.Clear();
        }
    }
}
