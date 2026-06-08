using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// FactionData の階級ヘルパと敵対判定（IsHostileTo）の現状動作を固定する特性テスト。
    /// </summary>
    public class FactionDataTests
    {
        private FactionData MakeFaction(string name)
        {
            var f = ScriptableObject.CreateInstance<FactionData>();
            f.factionName = name;
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
        public void GetRankName_ExistingTier_ReturnsName()
        {
            Assert.AreEqual("大将", MakeFaction("A").GetRankName(8));
        }

        [Test]
        public void GetRankName_MissingTier_ReturnsEmpty()
        {
            Assert.AreEqual("", MakeFaction("A").GetRankName(9));
        }

        [Test]
        public void GetTier_KnownName_ReturnsTier()
        {
            Assert.AreEqual(10, MakeFaction("A").GetTier("元帥"));
        }

        [Test]
        public void GetTier_UnknownName_ReturnsMinusOne()
        {
            Assert.AreEqual(-1, MakeFaction("A").GetTier("大元帥"));
        }

        [Test]
        public void HighestTier_ReturnsLargest()
        {
            Assert.AreEqual(10, MakeFaction("A").HighestTier());
        }

        [Test]
        public void HighestTier_NoRanks_ReturnsMinusOne()
        {
            var f = ScriptableObject.CreateInstance<FactionData>();
            f.ranks = new List<FactionData.RankEntry>();
            Assert.AreEqual(-1, f.HighestTier());
        }

        [Test]
        public void IsHostileTo_Self_IsFalse()
        {
            var a = MakeFaction("A");
            Assert.IsFalse(a.IsHostileTo(a));
        }

        [Test]
        public void IsHostileTo_Null_IsFalse()
        {
            Assert.IsFalse(MakeFaction("A").IsHostileTo(null));
        }

        [Test]
        public void IsHostileTo_DifferentFaction_IsTrueByDefault()
        {
            var a = MakeFaction("A");
            var b = MakeFaction("B");
            Assert.IsTrue(a.IsHostileTo(b));
        }

        [Test]
        public void IsHostileTo_NonHostileListed_IsFalse()
        {
            var a = MakeFaction("A");
            var b = MakeFaction("B");
            a.nonHostileFactions = new List<FactionData> { b };
            Assert.IsFalse(a.IsHostileTo(b));
        }
    }
}
