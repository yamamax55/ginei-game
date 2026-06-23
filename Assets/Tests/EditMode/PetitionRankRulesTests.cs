using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>具申の階級ゲート：区分ごとの最低階級・尉官/将官の可否境界。</summary>
    public class PetitionRankRulesTests
    {
        [Test]
        public void MinTier_ByCategory()
        {
            Assert.AreEqual(0, PetitionRankRules.MinTier(PetitionCategory.建白));
            Assert.AreEqual(0, PetitionRankRules.MinTier(PetitionCategory.予算));
            Assert.AreEqual(3, PetitionRankRules.MinTier(PetitionCategory.編制));
            Assert.AreEqual(5, PetitionRankRules.MinTier(PetitionCategory.作戦));
            Assert.AreEqual(6, PetitionRankRules.MinTier(PetitionCategory.人事));
            Assert.AreEqual(7, PetitionRankRules.MinTier(PetitionCategory.政治));
        }

        [Test]
        public void JuniorOfficer_LimitedToUnitLevel()
        {
            int lt = 2; // 大尉（尉官）
            Assert.IsTrue(PetitionRankRules.CanRaise(PetitionCategory.建白, lt));
            Assert.IsTrue(PetitionRankRules.CanRaise(PetitionCategory.予算, lt));   // 自隊の予算/増援級は可
            Assert.IsFalse(PetitionRankRules.CanRaise(PetitionCategory.作戦, lt));  // 作戦は将官のみ
            Assert.IsFalse(PetitionRankRules.CanRaise(PetitionCategory.人事, lt));
            Assert.IsFalse(PetitionRankRules.CanRaise(PetitionCategory.政治, lt));
        }

        [Test]
        public void FlagOfficer_UnlocksOperationsPersonnelPolitics()
        {
            Assert.IsTrue(PetitionRankRules.CanRaise(PetitionCategory.作戦, 5));  // 准将で作戦解禁
            Assert.IsFalse(PetitionRankRules.CanRaise(PetitionCategory.人事, 5)); // 人事は少将から
            Assert.IsTrue(PetitionRankRules.CanRaise(PetitionCategory.人事, 6));
            Assert.IsFalse(PetitionRankRules.CanRaise(PetitionCategory.政治, 6)); // 政治は中将から
            Assert.IsTrue(PetitionRankRules.CanRaise(PetitionCategory.政治, 7));
        }

        [Test]
        public void ExistingCategories_HaveNoUpperBound()
        {
            Assert.AreEqual(int.MaxValue, PetitionRankRules.MaxTier(PetitionCategory.作戦));
            Assert.IsTrue(PetitionRankRules.CanRaise(PetitionCategory.政治, 10)); // 元帥でも政治は可
        }

        [Test]
        public void PirateHunt_IsJuniorOfficerOnly()
        {
            Assert.AreEqual(1, PetitionRankRules.MinTier(PetitionCategory.海賊狩り)); // 少尉から
            Assert.AreEqual(2, PetitionRankRules.MaxTier(PetitionCategory.海賊狩り)); // 大尉まで
            Assert.IsFalse(PetitionRankRules.CanRaise(PetitionCategory.海賊狩り, 0)); // 任官前は不可
            Assert.IsTrue(PetitionRankRules.CanRaise(PetitionCategory.海賊狩り, 1));  // 少尉
            Assert.IsTrue(PetitionRankRules.CanRaise(PetitionCategory.海賊狩り, 2));  // 大尉
            Assert.IsFalse(PetitionRankRules.CanRaise(PetitionCategory.海賊狩り, 3)); // 少佐以上は出せない（下級士官の任）
            Assert.IsFalse(PetitionRankRules.CanRaise(PetitionCategory.海賊狩り, 5)); // 将官には不要
        }
    }
}
