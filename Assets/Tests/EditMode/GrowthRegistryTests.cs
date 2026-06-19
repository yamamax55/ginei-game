using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 提督の成長台帳（#2477 P1-b・<see cref="GrowthRegistry"/>）を固定する：id ごとの経験蓄積（捨てない）、
    /// 未登録は null・初回生成でアーキタイプ確定、加算の線形性（<see cref="GrowthRules"/> 委譲）、Clear。
    /// </summary>
    public class GrowthRegistryTests
    {
        [SetUp]
        public void Reset() => GrowthRegistry.Clear();

        [Test]
        public void Get_AbsentIsNull_CreateRegistersWithArchetype()
        {
            Assert.IsNull(GrowthRegistry.Get(1));
            Assert.IsFalse(GrowthRegistry.Has(1));

            Growth g = GrowthRegistry.GetOrCreate(1, GrowthArchetype.老練型);
            Assert.IsNotNull(g);
            Assert.AreEqual(GrowthArchetype.老練型, g.archetype);
            Assert.AreSame(g, GrowthRegistry.Get(1), "同一インスタンスを返す");
            Assert.IsTrue(GrowthRegistry.Has(1));
        }

        [Test]
        public void GainExperience_AccumulatesPerId_NotDiscarded()
        {
            GrowthRegistry.GainExperience(7, GrowthArchetype.叩き上げ, 100f, 1f);
            float one = GrowthRegistry.Get(7).experience;
            Assert.Greater(one, 0f, "経験が蓄えられる（捨てられない）");

            GrowthRegistry.GainExperience(7, GrowthArchetype.叩き上げ, 100f, 1f);
            float two = GrowthRegistry.Get(7).experience;
            Assert.AreEqual(2f * one, two, 1e-3, "同一 id へ線形に累積");
        }

        [Test]
        public void GainExperience_KeepsFirstArchetype()
        {
            GrowthRegistry.GainExperience(3, GrowthArchetype.首席型, 10f, 1f);
            // 2回目で別アーキタイプを渡しても、初回生成の型を維持（同一人物の成長は一貫）。
            GrowthRegistry.GainExperience(3, GrowthArchetype.叩き上げ, 10f, 1f);
            Assert.AreEqual(GrowthArchetype.首席型, GrowthRegistry.Get(3).archetype);
        }

        [Test]
        public void SeparateIds_AreIndependent()
        {
            GrowthRegistry.GainExperience(1, GrowthArchetype.叩き上げ, 100f, 1f);
            Assert.IsTrue(GrowthRegistry.Has(1));
            Assert.IsFalse(GrowthRegistry.Has(2));
            Assert.AreEqual(1, GrowthRegistry.Count);
        }

        [Test]
        public void Clear_Empties()
        {
            GrowthRegistry.GetOrCreate(1, GrowthArchetype.叩き上げ);
            GrowthRegistry.GetOrCreate(2, GrowthArchetype.叩き上げ);
            Assert.AreEqual(2, GrowthRegistry.Count);
            GrowthRegistry.Clear();
            Assert.AreEqual(0, GrowthRegistry.Count);
            Assert.IsNull(GrowthRegistry.Get(1));
        }
    }
}
