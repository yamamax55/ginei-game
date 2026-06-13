using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 艦載機を固定する：打撃力＝機数×技量、防空が削る割合（防空ゼロは素通し・圧倒で上限）、
    /// 実効打撃、出撃損耗（固い防空ほど帰ってこない）、母艦喪失の宿無し損失。境界を担保。
    /// </summary>
    public class CarrierRulesTests
    {
        private static readonly CarrierParams P = CarrierParams.Default;
        // 打撃1/機・迎撃上限0.8・出撃損耗0.05・宿無し0.8

        [Test]
        public void RawStrikePower_CraftTimesSkill()
        {
            Assert.AreEqual(100f, CarrierRules.RawStrikePower(100, 1f, P), 1e-4f);
            Assert.AreEqual(120f, CarrierRules.RawStrikePower(100, 1.2f, P), 1e-4f); // エース部隊は1超可
            Assert.AreEqual(0f, CarrierRules.RawStrikePower(0, 1f, P), 1e-5f);
        }

        [Test]
        public void InterceptedRatio_AirDefenseDuel()
        {
            // 防空ゼロ＝素通し
            Assert.AreEqual(0f, CarrierRules.InterceptedRatio(100f, 0f, P), 1e-5f);
            // 互角＝上限の半分＝0.4
            Assert.AreEqual(0.4f, CarrierRules.InterceptedRatio(100f, 100f, P), 1e-5f);
            // 打撃ゼロに対する防空＝上限0.8
            Assert.AreEqual(0.8f, CarrierRules.InterceptedRatio(0f, 100f, P), 1e-5f);
        }

        [Test]
        public void EffectiveStrike_WhatGetsThrough()
        {
            // 互角：100×(1−0.4)=60
            Assert.AreEqual(60f, CarrierRules.EffectiveStrike(100f, 100f, P), 1e-4f);
            // 防空なし＝素通し
            Assert.AreEqual(100f, CarrierRules.EffectiveStrike(100f, 0f, P), 1e-4f);
        }

        [Test]
        public void SortieLosses_WorseAgainstHardDefense()
        {
            // 防空なし＝基礎損耗のみ：100×0.05=5
            Assert.AreEqual(5, CarrierRules.SortieLosses(100, 100f, 0f, P));
            // 互角防空＝0.05+0.4×0.5=0.25 → 25機
            Assert.AreEqual(25, CarrierRules.SortieLosses(100, 100f, 100f, P));
        }

        [Test]
        public void OrphanedLosses_MostCraftLostWithCarrier()
        {
            Assert.AreEqual(80, CarrierRules.OrphanedLosses(100, P)); // 母艦喪失＝8割が宿無し
            Assert.AreEqual(0, CarrierRules.OrphanedLosses(0, P));
        }
    }
}
