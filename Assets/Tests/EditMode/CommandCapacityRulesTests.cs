using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 階級ごとの指揮可能規模（RANKCMD-2 #1712・銀英伝準拠）を固定する：中将=一個艦隊(1.2万)・大将=1.5万・
    /// 元帥=宇宙艦隊(6万)、規模で配属ゲート（過大兵力は下位階級が率いれない）、兵力→必要階級。
    /// </summary>
    public class CommandCapacityRulesTests
    {
        [Test]
        public void MaxStrength_FollowsRankLadder()
        {
            Assert.AreEqual(12000, CommandCapacityRules.MaxStrengthForTier(7));  // 中将＝一個艦隊の下限
            Assert.AreEqual(15000, CommandCapacityRules.MaxStrengthForTier(8));  // 大将
            Assert.AreEqual(60000, CommandCapacityRules.MaxStrengthForTier(10)); // 元帥＝宇宙艦隊
            Assert.AreEqual(3000, CommandCapacityRules.MaxStrengthForTier(5));   // 准将＝分艦隊
            Assert.AreEqual(60000, CommandCapacityRules.MaxStrengthForTier(11)); // 10超＝元帥級
            Assert.AreEqual(1000, CommandCapacityRules.MaxStrengthForTier(0));   // 准将未満＝最小
        }

        [Test]
        public void MaxStrength_IsMonotonic()
        {
            for (int t = 5; t < 10; t++)
                Assert.LessOrEqual(CommandCapacityRules.MaxStrengthForTier(t),
                                   CommandCapacityRules.MaxStrengthForTier(t + 1));
        }

        [Test]
        public void CanCommand_GatesByCapacity()
        {
            Assert.IsTrue(CommandCapacityRules.CanCommand(7, 12000));  // 中将は一個艦隊まで
            Assert.IsFalse(CommandCapacityRules.CanCommand(7, 15000)); // 中将は大艦隊を率いれない
            Assert.IsTrue(CommandCapacityRules.CanCommand(8, 15000));  // 大将なら可
            Assert.IsTrue(CommandCapacityRules.CanCommand(10, 60000)); // 元帥＝宇宙艦隊
        }

        [Test]
        public void RequiredTier_ForStrength()
        {
            Assert.AreEqual(7, CommandCapacityRules.RequiredTierForStrength(12000)); // 一個艦隊＝中将
            Assert.AreEqual(8, CommandCapacityRules.RequiredTierForStrength(12001)); // 超なら大将
            Assert.AreEqual(8, CommandCapacityRules.RequiredTierForStrength(15000));
            Assert.AreEqual(9, CommandCapacityRules.RequiredTierForStrength(30000));
            Assert.AreEqual(10, CommandCapacityRules.RequiredTierForStrength(60001));
            Assert.AreEqual(5, CommandCapacityRules.RequiredTierForStrength(100)); // 小規模でも下限は准将
        }

        /// <summary>
        /// 階級→自然な梯団の段（RANKCMD-4 #1714・銀英伝準拠）：准将/少将=分艦隊、中将/大将=艦隊（大将も艦隊）、
        /// 上級大将=軍団（艦隊群）、元帥=軍集団（宇宙艦隊）。
        /// </summary>
        [Test]
        public void EchelonForTier_FollowsLoghLadder()
        {
            Assert.AreEqual(EchelonType.分艦隊, CommandCapacityRules.EchelonForTier(5));  // 准将
            Assert.AreEqual(EchelonType.分艦隊, CommandCapacityRules.EchelonForTier(6));  // 少将
            Assert.AreEqual(EchelonType.艦隊,   CommandCapacityRules.EchelonForTier(7));  // 中将
            Assert.AreEqual(EchelonType.艦隊,   CommandCapacityRules.EchelonForTier(8));  // 大将＝艦隊司令が自然
            Assert.AreEqual(EchelonType.軍団,   CommandCapacityRules.EchelonForTier(9));  // 上級大将＝艦隊群/方面
            Assert.AreEqual(EchelonType.軍集団, CommandCapacityRules.EchelonForTier(10)); // 元帥＝宇宙艦隊
            Assert.AreEqual(EchelonType.軍集団, CommandCapacityRules.EchelonForTier(11)); // 10超も最上段
            Assert.AreEqual(EchelonType.分艦隊, CommandCapacityRules.EchelonForTier(0));  // 准将未満は最下段
        }
    }
}
