using System;
using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 陸戦/地上軍の編制（ORBAT-5 #1721・惑星戦 #131 の地上戦力）を固定する：地上梯団の指揮官階級・人員規模の一表、
    /// 規模順（並び順）、自然な上位梯団（ParentOf）、人員レンジ判定。
    /// </summary>
    public class GroundForceRulesTests
    {
        [Test]
        public void ProfileFor_SampleEchelons()
        {
            var squad = GroundForceRules.ProfileFor(GroundEchelonType.分隊);
            Assert.AreEqual(0, squad.commanderTier);
            Assert.AreEqual(8, squad.minPersonnel);
            Assert.AreEqual(12, squad.maxPersonnel);
            Assert.IsTrue(squad.Contains(10));
            Assert.IsFalse(squad.Contains(100));

            var division = GroundForceRules.ProfileFor(GroundEchelonType.師団);
            Assert.AreEqual(6, division.commanderTier);  // 中将/少将
            Assert.AreEqual(10000, division.minPersonnel);
            Assert.AreEqual(20000, division.maxPersonnel);

            // 最上段（軍）は人員上限なし
            Assert.AreEqual(int.MaxValue, GroundForceRules.ProfileFor(GroundEchelonType.軍).maxPersonnel);
        }

        [Test]
        public void Ladder_IsMonotonic()
        {
            var all = (GroundEchelonType[])Enum.GetValues(typeof(GroundEchelonType));
            for (int i = 1; i < all.Length; i++)
            {
                var prev = GroundForceRules.ProfileFor(all[i - 1]);
                var cur = GroundForceRules.ProfileFor(all[i]);
                Assert.LessOrEqual(prev.commanderTier, cur.commanderTier, $"{all[i - 1]}→{all[i]} 指揮官tier");
                Assert.LessOrEqual(prev.minPersonnel, cur.minPersonnel, $"{all[i - 1]}→{all[i]} 人員下限");
                Assert.IsTrue(GroundForceRules.IsLarger(all[i], all[i - 1]));
            }
        }

        [Test]
        public void ParentOf_NextEchelonUp_TopIsNull()
        {
            Assert.AreEqual(GroundEchelonType.小隊, GroundForceRules.ParentOf(GroundEchelonType.分隊));
            Assert.AreEqual(GroundEchelonType.師団, GroundForceRules.ParentOf(GroundEchelonType.旅団));
            Assert.AreEqual(GroundEchelonType.軍団, GroundForceRules.ParentOf(GroundEchelonType.師団));
            Assert.IsNull(GroundForceRules.ParentOf(GroundEchelonType.軍)); // 最上段
        }

        [Test]
        public void CommanderTierFor_MatchesProfile()
        {
            Assert.AreEqual(8, GroundForceRules.CommanderTierFor(GroundEchelonType.軍団)); // 大将/中将
            Assert.AreEqual(9, GroundForceRules.CommanderTierFor(GroundEchelonType.軍));   // 元帥〜中将
        }

        [Test]
        public void LargestEchelonFor_ClassifiesAggregate()
        {
            Assert.AreEqual(GroundEchelonType.分隊, GroundForceRules.LargestEchelonFor(0));      // 下限未満は最小段
            Assert.AreEqual(GroundEchelonType.分隊, GroundForceRules.LargestEchelonFor(10));
            Assert.AreEqual(GroundEchelonType.中隊, GroundForceRules.LargestEchelonFor(200));
            Assert.AreEqual(GroundEchelonType.師団, GroundForceRules.LargestEchelonFor(15000)); // 1個師団規模
            Assert.AreEqual(GroundEchelonType.軍,   GroundForceRules.LargestEchelonFor(80000)); // 軍規模
        }

        [Test]
        public void NominalPersonnel_MidpointExceptOpenTop()
        {
            Assert.AreEqual(15000, GroundForceRules.ProfileFor(GroundEchelonType.師団).NominalPersonnel); // (10000+20000)/2
            // 上限なし（軍）は下限を返す
            Assert.AreEqual(50000, GroundForceRules.ProfileFor(GroundEchelonType.軍).NominalPersonnel);
        }
    }
}
