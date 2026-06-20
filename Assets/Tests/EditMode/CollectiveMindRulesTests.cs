using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 集合意識型ドクトリン（ENDR-4 #2359）を固定する：同調ボーナスは集合意識型のみ・健在比率に単調増加、
    /// 中核崩壊リスクは健全度に単調減少で[0,1]、指揮継承は集合意識型のみ不能。
    /// </summary>
    public class CollectiveMindRulesTests
    {
        // --- CollectiveSyncBonus ---

        [Test]
        public void CollectiveSyncBonus_OtherDoctrines_AlwaysOne()
        {
            Assert.AreEqual(1f, CollectiveMindRules.CollectiveSyncBonus(CommandDoctrine.集団依存, 1f), 1e-5f);
            Assert.AreEqual(1f, CollectiveMindRules.CollectiveSyncBonus(CommandDoctrine.自律分散, 1f), 1e-5f);
            Assert.AreEqual(1f, CollectiveMindRules.CollectiveSyncBonus(CommandDoctrine.集団依存, 0f), 1e-5f);
        }

        [Test]
        public void CollectiveSyncBonus_Collective_ScalesWithIntactRatio()
        {
            Assert.AreEqual(1f, CollectiveMindRules.CollectiveSyncBonus(CommandDoctrine.集合意識型, 0f), 1e-5f);
            Assert.AreEqual(1f + CollectiveMindRules.MaxSyncBonus,
                CollectiveMindRules.CollectiveSyncBonus(CommandDoctrine.集合意識型, 1f), 1e-5f);
        }

        [Test]
        public void CollectiveSyncBonus_Collective_MonotonicNonDecreasing()
        {
            float prev = CollectiveMindRules.CollectiveSyncBonus(CommandDoctrine.集合意識型, 0f);
            for (int i = 1; i <= 10; i++)
            {
                float r = i / 10f;
                float cur = CollectiveMindRules.CollectiveSyncBonus(CommandDoctrine.集合意識型, r);
                Assert.GreaterOrEqual(cur, prev);
                prev = cur;
            }
        }

        [Test]
        public void CollectiveSyncBonus_Collective_ClampsOutOfRange()
        {
            // intactRatio>1 は 1 と同じ、負値は 0 と同じ
            Assert.AreEqual(1f + CollectiveMindRules.MaxSyncBonus,
                CollectiveMindRules.CollectiveSyncBonus(CommandDoctrine.集合意識型, 5f), 1e-5f);
            Assert.AreEqual(1f, CollectiveMindRules.CollectiveSyncBonus(CommandDoctrine.集合意識型, -3f), 1e-5f);
        }

        // --- CoreCollapseRisk ---

        [Test]
        public void CoreCollapseRisk_FullHealth_Zero_LowHealth_NearOne()
        {
            Assert.AreEqual(0f, CollectiveMindRules.CoreCollapseRisk(1f), 1e-5f);
            Assert.AreEqual(1f, CollectiveMindRules.CoreCollapseRisk(0f), 1e-5f);
        }

        [Test]
        public void CoreCollapseRisk_MonotonicDecreasingInHealth_Bounded()
        {
            float prev = CollectiveMindRules.CoreCollapseRisk(0f);
            for (int i = 1; i <= 10; i++)
            {
                float h = i / 10f;
                float cur = CollectiveMindRules.CoreCollapseRisk(h);
                Assert.LessOrEqual(cur, prev); // 健全度が上がるほどリスクは下がる
                Assert.GreaterOrEqual(cur, 0f);
                Assert.LessOrEqual(cur, 1f);
                prev = cur;
            }
        }

        [Test]
        public void CoreCollapseRisk_ClampsOutOfRange()
        {
            Assert.AreEqual(0f, CollectiveMindRules.CoreCollapseRisk(5f), 1e-5f);
            Assert.AreEqual(1f, CollectiveMindRules.CoreCollapseRisk(-2f), 1e-5f);
        }

        // --- IsSuccessionPossible ---

        [Test]
        public void IsSuccessionPossible_FalseOnlyForCollective()
        {
            Assert.IsFalse(CollectiveMindRules.IsSuccessionPossible(CommandDoctrine.集合意識型));
            Assert.IsTrue(CollectiveMindRules.IsSuccessionPossible(CommandDoctrine.集団依存));
            Assert.IsTrue(CollectiveMindRules.IsSuccessionPossible(CommandDoctrine.自律分散));
        }
    }
}
