using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// RankSystem（階級 tier 判定の唯一の窓口）の現状動作を固定する特性テスト。
    /// 既定ラダーに合わせ tier: 5准将/6少将/7中将/8大将/10元帥（9＝上級大将は欠番）で検証する。
    /// </summary>
    public class RankSystemTests
    {
        // 同盟型（9＝上級大将が欠番）の階級表を持つ FactionData を作る。
        private FactionData MakeAllianceLikeFaction()
        {
            var f = ScriptableObject.CreateInstance<FactionData>();
            f.ranks = new List<FactionData.RankEntry>
            {
                new FactionData.RankEntry(5, "准将"),
                new FactionData.RankEntry(6, "少将"),
                new FactionData.RankEntry(7, "中将"),
                new FactionData.RankEntry(8, "大将"),
                new FactionData.RankEntry(10, "元帥"),
            };
            return f;
        }

        [Test]
        public void AreEquivalent_SameTier_IsTrue()
        {
            Assert.IsTrue(RankSystem.AreEquivalent(8, 8));
        }

        [Test]
        public void AreEquivalent_DifferentTier_IsFalse()
        {
            Assert.IsFalse(RankSystem.AreEquivalent(8, 7));
        }

        [Test]
        public void Compare_And_IsHigher_FollowTierOrder()
        {
            Assert.Greater(RankSystem.Compare(10, 8), 0);
            Assert.Less(RankSystem.Compare(7, 8), 0);
            Assert.AreEqual(0, RankSystem.Compare(8, 8));
            Assert.IsTrue(RankSystem.IsHigher(10, 8));
            Assert.IsFalse(RankSystem.IsHigher(8, 10));
        }

        [Test]
        public void NextRankTier_ReturnsSmallestHigher()
        {
            var f = MakeAllianceLikeFaction();
            Assert.AreEqual(6, RankSystem.NextRankTier(f, 5)); // 准将→少将
            Assert.AreEqual(10, RankSystem.NextRankTier(f, 8)); // 大将→元帥（9欠番を飛ばす）
        }

        [Test]
        public void NextRankTier_AtTop_StaysSame()
        {
            var f = MakeAllianceLikeFaction();
            Assert.AreEqual(10, RankSystem.NextRankTier(f, 10));
        }

        [Test]
        public void NextRankTier_NullFaction_StaysSame()
        {
            Assert.AreEqual(8, RankSystem.NextRankTier(null, 8));
        }

        [Test]
        public void PreviousRankTier_ReturnsLargestLower()
        {
            var f = MakeAllianceLikeFaction();
            Assert.AreEqual(7, RankSystem.PreviousRankTier(f, 8));
        }

        [Test]
        public void PreviousRankTier_AtBottom_StaysSame()
        {
            var f = MakeAllianceLikeFaction();
            Assert.AreEqual(5, RankSystem.PreviousRankTier(f, 5));
        }

        [Test]
        public void ResolveTier_ExactMatch_ReturnsSame()
        {
            var f = MakeAllianceLikeFaction();
            Assert.AreEqual(8, RankSystem.ResolveTier(f, 8));
        }

        [Test]
        public void ResolveTier_MissingTier_SnapsToNearestLower()
        {
            var f = MakeAllianceLikeFaction();
            // 9（上級大将）は同盟型に無い → 直近下位の 8（大将）へ。
            Assert.AreEqual(8, RankSystem.ResolveTier(f, 9));
        }

        [Test]
        public void ResolveTier_BelowAll_SnapsToNearestHigher()
        {
            var f = MakeAllianceLikeFaction();
            // 3 は最下位(5)より下 → 直近下位が無いので直近上位の 5 へ。
            Assert.AreEqual(5, RankSystem.ResolveTier(f, 3));
        }

        [Test]
        public void ResolveTier_NoRanks_ReturnsSame()
        {
            var f = ScriptableObject.CreateInstance<FactionData>();
            f.ranks = new List<FactionData.RankEntry>();
            Assert.AreEqual(42, RankSystem.ResolveTier(f, 42));
        }
    }
}
