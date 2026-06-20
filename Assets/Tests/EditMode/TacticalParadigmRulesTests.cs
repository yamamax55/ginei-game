using NUnit.Framework;
using UnityEngine;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 戦術パラダイムの優位と適応（ENDR-3 #2356）を固定する：
    /// 新機軸 vs 未対応の敵で優位>1、敵が対応済なら1.0、適応進捗とひらめき確率の単調性と境界。
    /// </summary>
    public class TacticalParadigmRulesTests
    {
        // --- ParadigmAdvantage ---

        [Test]
        public void ParadigmAdvantage_FrameshiftVsRegular_GreaterThanOne()
        {
            float adv = TacticalParadigmRules.ParadigmAdvantage(
                TacticalParadigm.フレームシフト済, TacticalParadigm.正規教義);
            Assert.Greater(adv, 1.0f);
            Assert.AreEqual(TacticalParadigmRules.AdvantagePeak, adv, 1e-5f);
        }

        [Test]
        public void ParadigmAdvantage_AdaptedEnemy_IsOne()
        {
            float adv = TacticalParadigmRules.ParadigmAdvantage(
                TacticalParadigm.フレームシフト済, TacticalParadigm.対応済);
            Assert.AreEqual(1.0f, adv, 1e-5f);
        }

        [Test]
        public void ParadigmAdvantage_NoFrameshift_IsOne()
        {
            Assert.AreEqual(1.0f, TacticalParadigmRules.ParadigmAdvantage(
                TacticalParadigm.正規教義, TacticalParadigm.正規教義), 1e-5f);
            Assert.AreEqual(1.0f, TacticalParadigmRules.ParadigmAdvantage(
                TacticalParadigm.対応済, TacticalParadigm.正規教義), 1e-5f);
        }

        // --- ParadigmAdvantageAdapting：進捗で1.0へ収束 ---

        [Test]
        public void ParadigmAdvantageAdapting_ConvergesToOneAsProgressRises()
        {
            float full = TacticalParadigmRules.ParadigmAdvantageAdapting(
                TacticalParadigm.フレームシフト済, TacticalParadigm.正規教義, 0f);
            float mid = TacticalParadigmRules.ParadigmAdvantageAdapting(
                TacticalParadigm.フレームシフト済, TacticalParadigm.正規教義, 0.5f);
            float done = TacticalParadigmRules.ParadigmAdvantageAdapting(
                TacticalParadigm.フレームシフト済, TacticalParadigm.正規教義, 1f);

            Assert.AreEqual(TacticalParadigmRules.AdvantagePeak, full, 1e-5f);
            Assert.Less(mid, full);
            Assert.Greater(mid, done);
            Assert.AreEqual(1.0f, done, 1e-5f);
        }

        [Test]
        public void ParadigmAdvantageAdapting_NoBaseAdvantage_StaysOne()
        {
            Assert.AreEqual(1.0f, TacticalParadigmRules.ParadigmAdvantageAdapting(
                TacticalParadigm.正規教義, TacticalParadigm.正規教義, 0f), 1e-5f);
        }

        // --- AdaptationProgress：単調・境界 ---

        [Test]
        public void AdaptationProgress_Bounds()
        {
            Assert.AreEqual(0f, TacticalParadigmRules.AdaptationProgress(0f, 0f), 1e-5f);
            float maxed = TacticalParadigmRules.AdaptationProgress(5f, 9999f); // 入力過大でもクランプ
            Assert.LessOrEqual(maxed, 1.0f);
            Assert.AreEqual(1.0f, maxed, 1e-5f);
        }

        [Test]
        public void AdaptationProgress_MonotonicInIntelligence()
        {
            float lo = TacticalParadigmRules.AdaptationProgress(0.2f, 10f);
            float hi = TacticalParadigmRules.AdaptationProgress(0.8f, 10f);
            Assert.Greater(hi, lo);
        }

        [Test]
        public void AdaptationProgress_MonotonicInAge()
        {
            float young = TacticalParadigmRules.AdaptationProgress(0.5f, 1f);
            float old = TacticalParadigmRules.AdaptationProgress(0.5f, 20f);
            Assert.Greater(old, young);
        }

        // --- DiscoveryChance：単調・境界 ---

        [Test]
        public void DiscoveryChance_Bounds()
        {
            Assert.AreEqual(0f, TacticalParadigmRules.DiscoveryChance(0f, 0f), 1e-5f);
            float maxed = TacticalParadigmRules.DiscoveryChance(999f, 5f); // 入力過大でもクランプ
            Assert.LessOrEqual(maxed, 1.0f);
            Assert.AreEqual(1.0f, maxed, 1e-5f);
        }

        [Test]
        public void DiscoveryChance_MonotonicInMobility()
        {
            float lo = TacticalParadigmRules.DiscoveryChance(20f, 0.5f);
            float hi = TacticalParadigmRules.DiscoveryChance(90f, 0.5f);
            Assert.Greater(hi, lo);
        }

        [Test]
        public void DiscoveryChance_MonotonicInAdversity()
        {
            float calm = TacticalParadigmRules.DiscoveryChance(50f, 0.1f);
            float dire = TacticalParadigmRules.DiscoveryChance(50f, 0.9f);
            Assert.Greater(dire, calm);
        }
    }
}
