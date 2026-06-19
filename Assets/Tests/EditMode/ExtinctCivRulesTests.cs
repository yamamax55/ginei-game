using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 先行文明の記録（#2399 IHER-3）：技術遺産の単調性・遺跡の地勢解決・滅亡原因の気質補正を固定する。
    /// すべて純ロジック（倍率を返すだけ・基準値非破壊）。null/空は中立。
    /// </summary>
    public class ExtinctCivRulesTests
    {
        // 星系 0,1 のみ存在するマップ（StrategyRulesTests.MakeMap に倣う）。
        private GalaxyMap MakeMap()
        {
            var m = new GalaxyMap();
            m.AddSystem(new StarSystem(0, "X", Vector2.zero, Faction.帝国));
            m.AddSystem(new StarSystem(1, "Y", Vector2.right, Faction.帝国));
            return m;
        }

        // ───────── TechHeritage ─────────

        [Test]
        public void TechHeritage_MonotonicInPeakTier()
        {
            var low = new ExtinctCivilization(1, "古", peakTechTier: 2);
            var high = new ExtinctCivilization(2, "超古", peakTechTier: 8);
            Assert.Greater(
                ExtinctCivRules.TechHeritage(high, ResearchField.生産),
                ExtinctCivRules.TechHeritage(low, ResearchField.生産));
            // tier 0 は中立。
            var zero = new ExtinctCivilization(3, "零", peakTechTier: 0);
            Assert.AreEqual(ExtinctCivRules.NeutralFactor,
                ExtinctCivRules.TechHeritage(zero, ResearchField.生産), 1e-4f);
        }

        [Test]
        public void TechHeritage_InfoFieldHasExtraBias()
        {
            var ec = new ExtinctCivilization(1, "古", peakTechTier: 5);
            Assert.Greater(
                ExtinctCivRules.TechHeritage(ec, ResearchField.情報),
                ExtinctCivRules.TechHeritage(ec, ResearchField.生産));
        }

        [Test]
        public void TechHeritage_Null_IsNeutral()
        {
            Assert.AreEqual(ExtinctCivRules.NeutralFactor,
                ExtinctCivRules.TechHeritage(null, ResearchField.軍事), 1e-4f);
        }

        // ───────── GeoFootprint ─────────

        [Test]
        public void GeoFootprint_ReturnsOnlyExistingSystems()
        {
            var m = MakeMap();
            var ec = new ExtinctCivilization(1, "古");
            ec.territorySystemIds = new List<int> { 0, 99, 1 }; // 99 は存在しない
            var result = new List<StarSystem>(ExtinctCivRules.GeoFootprint(ec, m));
            Assert.AreEqual(2, result.Count);
            Assert.IsTrue(result.Contains(m.GetSystem(0)));
            Assert.IsTrue(result.Contains(m.GetSystem(1)));
        }

        [Test]
        public void GeoFootprint_Null_IsEmpty()
        {
            var m = MakeMap();
            Assert.AreEqual(0, new List<StarSystem>(ExtinctCivRules.GeoFootprint(null, m)).Count);
            var ec = new ExtinctCivilization(1, "古");
            Assert.AreEqual(0, new List<StarSystem>(ExtinctCivRules.GeoFootprint(ec, null)).Count);
        }

        // ───────── ExtinctionCauseEffect ─────────

        [Test]
        public void ExtinctionCauseEffect_DiffersByCause()
        {
            var war = new ExtinctCivilization(1, "戦", extinctionCause: ExtinctionCause.戦争);
            var env = new ExtinctCivilization(2, "環", extinctionCause: ExtinctionCause.環境);
            var unk = new ExtinctCivilization(3, "謎", extinctionCause: ExtinctionCause.不明);

            float w = ExtinctCivRules.ExtinctionCauseEffect(war, null);
            float e = ExtinctCivRules.ExtinctionCauseEffect(env, null);
            float u = ExtinctCivRules.ExtinctionCauseEffect(unk, null);

            Assert.Greater(w, u);                 // 戦争＝軍事偏重
            Assert.Less(e, u);                    // 環境＝慎重
            Assert.AreEqual(ExtinctCivRules.NeutralFactor, u, 1e-4f); // 不明＝中立
        }

        [Test]
        public void ExtinctionCauseEffect_Null_IsNeutral()
        {
            Assert.AreEqual(ExtinctCivRules.NeutralFactor,
                ExtinctCivRules.ExtinctionCauseEffect(null, null), 1e-4f);
        }
    }
}
