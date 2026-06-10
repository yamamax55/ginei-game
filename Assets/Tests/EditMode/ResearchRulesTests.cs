using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 研究ツリー（#123-127）の純ロジックを固定する：研究産出（研究力×生産力）・進捗 Tick とクランプ・
    /// 完成判定・政体（思想）による研究の偏り（得意/不得意/中立）。決定論・境界・クランプを担保。
    /// </summary>
    public class ResearchRulesTests
    {
        // ===== ResearchOutput（研究産出） =====

        [Test]
        public void ResearchOutput_MultipliesPowerByFactor()
        {
            // 研究力10×生産力0.5×基準1.0 = 5
            Assert.AreEqual(5f, ResearchRules.ResearchOutput(10f, 0.5f, ResearchParams.Default), 1e-4f);
        }

        [Test]
        public void ResearchOutput_ClampsNegativeInputsToZero()
        {
            Assert.AreEqual(0f, ResearchRules.ResearchOutput(-10f, 1f), 1e-4f);
            Assert.AreEqual(0f, ResearchRules.ResearchOutput(10f, -1f), 1e-4f);
        }

        // ===== Tick（進捗） =====

        [Test]
        public void Tick_AddsProgressByOutputTimesDt()
        {
            var p = new ResearchProject(ResearchField.軍事, 100f, 30);
            ResearchRules.Tick(p, 10f, 2f); // 10×2 = 20
            Assert.AreEqual(20f, p.progress, 1e-4f);
        }

        [Test]
        public void Tick_ClampsProgressToCost()
        {
            var p = new ResearchProject(ResearchField.生産, 50f, 10);
            ResearchRules.Tick(p, 100f, 1f); // 100 だが cost=50 で頭打ち
            Assert.AreEqual(50f, p.progress, 1e-4f);
        }

        [Test]
        public void Tick_IgnoresNullOrNonPositiveDtOrNegativeOutput()
        {
            ResearchRules.Tick(null, 10f, 1f); // 例外を投げない
            var p = new ResearchProject(ResearchField.情報, 50f, 10);
            ResearchRules.Tick(p, 10f, 0f);   // dt=0 で不変
            Assert.AreEqual(0f, p.progress, 1e-4f);
            ResearchRules.Tick(p, -10f, 1f);  // 負の産出はクランプ＝不変
            Assert.AreEqual(0f, p.progress, 1e-4f);
        }

        // ===== IsComplete（完成判定） =====

        [Test]
        public void IsComplete_TrueWhenProgressReachesCost()
        {
            var p = new ResearchProject(ResearchField.社会, 40f, 20);
            Assert.IsFalse(ResearchRules.IsComplete(p));
            ResearchRules.Tick(p, 40f, 1f);
            Assert.IsTrue(ResearchRules.IsComplete(p));
            Assert.IsFalse(ResearchRules.IsComplete(null));
        }

        // ===== IdeologyBias（政体で偏る） =====

        [Test]
        public void IdeologyBias_MatchVsMismatchVsNeutral()
        {
            var prm = ResearchParams.Default;
            // 専制＝軍事が得意
            Assert.AreEqual(prm.biasMatch, ResearchRules.IdeologyBias(ResearchField.軍事, ResearchRules.Ideology専制), 1e-4f);
            // 専制で社会は不得意
            Assert.AreEqual(prm.biasMismatch, ResearchRules.IdeologyBias(ResearchField.社会, ResearchRules.Ideology専制), 1e-4f);
            // 民主＝社会が得意
            Assert.AreEqual(prm.biasMatch, ResearchRules.IdeologyBias(ResearchField.社会, ResearchRules.Ideology民主), 1e-4f);
        }

        [Test]
        public void IdeologyBias_UnknownOrEmptyIsNeutral()
        {
            var prm = ResearchParams.Default;
            Assert.AreEqual(prm.biasNeutral, ResearchRules.IdeologyBias(ResearchField.軍事, ""), 1e-4f);
            Assert.AreEqual(prm.biasNeutral, ResearchRules.IdeologyBias(ResearchField.軍事, null), 1e-4f);
            Assert.AreEqual(prm.biasNeutral, ResearchRules.IdeologyBias(ResearchField.軍事, "謎思想"), 1e-4f);
        }
    }
}
