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

        // ───────── ResolveRankName（#14・HUD表示用） ─────────

        [Test]
        public void ResolveRankName_NullFaction_IsEmpty()
        {
            Assert.AreEqual("", RankSystem.ResolveRankName(null, 8));
        }

        [Test]
        public void ResolveRankName_UnsetTier_IsEmpty()
        {
            var f = MakeAllianceLikeFaction();
            // 0 以下＝未設定＝階級を出さない（後方互換）。
            Assert.AreEqual("", RankSystem.ResolveRankName(f, 0));
            Assert.AreEqual("", RankSystem.ResolveRankName(f, -1));
        }

        [Test]
        public void ResolveRankName_ExistingTier_ReturnsName()
        {
            var f = MakeAllianceLikeFaction();
            Assert.AreEqual("大将", RankSystem.ResolveRankName(f, 8));
            Assert.AreEqual("元帥", RankSystem.ResolveRankName(f, 10));
        }

        [Test]
        public void ResolveRankName_MissingTier_SnapsToNearest()
        {
            var f = MakeAllianceLikeFaction();
            // 9（上級大将）は同盟型に無い → 直近下位 8（大将）の名称。
            Assert.AreEqual("大将", RankSystem.ResolveRankName(f, 9));
        }

        [Test]
        public void ResolveRankName_EmptyRanks_IsEmpty()
        {
            var f = ScriptableObject.CreateInstance<FactionData>();
            f.ranks = new List<FactionData.RankEntry>();
            Assert.AreEqual("", RankSystem.ResolveRankName(f, 8));
        }

        // ───────── DefaultRankName / ResolveRankNameOrDefault（#14・フォールバック） ─────────

        [Test]
        public void DefaultRankName_KnownTiers()
        {
            Assert.AreEqual("中将", RankSystem.DefaultRankName(7));
            Assert.AreEqual("上級大将", RankSystem.DefaultRankName(9));
            Assert.AreEqual("元帥", RankSystem.DefaultRankName(10));
        }

        [Test]
        public void DefaultRankName_OutOfRange_IsEmpty()
        {
            Assert.AreEqual("", RankSystem.DefaultRankName(0));
            Assert.AreEqual("", RankSystem.DefaultRankName(11));
        }

        [Test]
        public void ResolveRankNameOrDefault_NullFaction_UsesDefaultLadder()
        {
            // FactionData 未割当でも既定ラダーで階級が出る（HUD用）。
            Assert.AreEqual("中将", RankSystem.ResolveRankNameOrDefault(null, 7));
        }

        [Test]
        public void ResolveRankNameOrDefault_FactionTablePreferred()
        {
            var f = MakeAllianceLikeFaction();
            // 勢力の階級表に該当があればそれを使う。
            Assert.AreEqual("中将", RankSystem.ResolveRankNameOrDefault(f, 7));
            // 同盟型に無い 9 は表側で直近下位 8（大将）へ丸め → 既定の「上級大将」ではなく「大将」。
            Assert.AreEqual("大将", RankSystem.ResolveRankNameOrDefault(f, 9));
        }

        [Test]
        public void ResolveRankNameOrDefault_UnsetTier_IsEmpty()
        {
            Assert.AreEqual("", RankSystem.ResolveRankNameOrDefault(null, 0));
            Assert.AreEqual("", RankSystem.ResolveRankNameOrDefault(MakeAllianceLikeFaction(), 0));
        }
    }
}
