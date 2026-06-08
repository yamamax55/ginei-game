using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// FactionRelations.IsHostile（敵対判定の唯一の窓口）の純ロジック版の現状動作を固定する。
    /// IShipTarget を要する多重定義はシーン依存のためここでは扱わず、
    /// FactionData/enum 版（後方互換フォールバックの核）のみ検証する。
    /// </summary>
    public class FactionRelationsTests
    {
        private FactionData MakeFaction(string name)
        {
            var f = ScriptableObject.CreateInstance<FactionData>();
            f.factionName = name;
            return f;
        }

        [Test]
        public void EnumFallback_DifferentLegacy_IsHostile()
        {
            // 双方 FactionData 無し → enum で判定（帝国≠同盟＝敵）。
            Assert.IsTrue(FactionRelations.IsHostile(null, Faction.帝国, null, Faction.同盟));
        }

        [Test]
        public void EnumFallback_SameLegacy_IsNotHostile()
        {
            Assert.IsFalse(FactionRelations.IsHostile(null, Faction.帝国, null, Faction.帝国));
        }

        [Test]
        public void BothFactionData_DistinctFactions_AreHostile()
        {
            var a = MakeFaction("A");
            var b = MakeFaction("B");
            Assert.IsTrue(FactionRelations.IsHostile(a, Faction.帝国, b, Faction.帝国));
        }

        [Test]
        public void BothFactionData_NonHostileListed_AreNotHostile()
        {
            var a = MakeFaction("A");
            var b = MakeFaction("B");
            a.nonHostileFactions = new List<FactionData> { b };
            Assert.IsFalse(FactionRelations.IsHostile(a, Faction.帝国, b, Faction.同盟));
        }

        [Test]
        public void MixedData_OneNull_FallsBackToEnum()
        {
            // 片方のみ FactionData → enum 判定にフォールバック（帝国≠同盟＝敵）。
            var a = MakeFaction("A");
            Assert.IsTrue(FactionRelations.IsHostile(a, Faction.帝国, null, Faction.同盟));
        }
    }
}
