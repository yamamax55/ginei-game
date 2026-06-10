using NUnit.Framework;
using UnityEngine;

namespace Ginei.Tests
{
    /// <summary>覇権移行（トゥキディデスの罠）純ロジックのテスト。既定 HegemonyParams で期待値を固定。</summary>
    public class HegemonyRulesTests
    {
        private const float Eps = 0.0005f;

        /// <summary>力の比＝台頭国/覇権国。並ぶと1.0、覇権国0で台頭国が圧倒。</summary>
        [Test]
        public void PowerRatio_DividesRisingByHegemon()
        {
            Assert.AreEqual(1f, HegemonyRules.PowerRatio(100f, 100f), Eps);
            Assert.AreEqual(0.5f, HegemonyRules.PowerRatio(50f, 100f), Eps);
            Assert.AreEqual(2f, HegemonyRules.PowerRatio(200f, 100f), Eps);
            // 覇権国の力が消えれば台頭国が圧倒（大きな比でクランプ）。
            Assert.AreEqual(100f, HegemonyRules.PowerRatio(50f, 0f), Eps);
        }

        /// <summary>移行危険度は力の比1.0（交差の瞬間）でピークし、大差で安定する山形。</summary>
        [Test]
        public void TransitionDanger_PeaksAtCrossover()
        {
            float atCross = HegemonyRules.TransitionDanger(1.0f);
            float below = HegemonyRules.TransitionDanger(0.5f);
            float above = HegemonyRules.TransitionDanger(2.0f);
            Assert.AreEqual(1f, atCross, Eps);
            // 交差点が両側より危険＝追い越しの瞬間が最も危ない。
            Assert.Greater(atCross, below);
            Assert.Greater(atCross, above);
            // 大差ほど安定（圧倒的優劣は危険度低）。
            Assert.Less(above, below);
        }

        /// <summary>危険度の山は頂点1.0を挟んで対称（0.9と1.1で同じ）。</summary>
        [Test]
        public void TransitionDanger_IsSymmetricAroundPeak()
        {
            float left = HegemonyRules.TransitionDanger(0.9f);
            float right = HegemonyRules.TransitionDanger(1.1f);
            Assert.AreEqual(left, right, Eps);
            Assert.Greater(left, 0.9f);
        }

        /// <summary>覇権国の恐怖は急速な台頭ほど増す（スパルタの恐怖）。交差点で速いほど最大。</summary>
        [Test]
        public void HegemonFear_RisesWithRapidAscent()
        {
            float fast = HegemonyRules.HegemonFear(1.0f, 1.0f);
            float slow = HegemonyRules.HegemonFear(1.0f, 0.0f);
            Assert.AreEqual(1f, fast, Eps);
            Assert.AreEqual(0.5f, slow, Eps);
            Assert.Greater(fast, slow);
        }

        /// <summary>台頭国の強硬化は不満が高いほど増す（アテネの野心）。力ゼロなら強硬化なし。</summary>
        [Test]
        public void RisingPowerAssertiveness_GrowsWithGrievance()
        {
            float discontent = HegemonyRules.RisingPowerAssertiveness(1.0f, 1.0f);
            float content = HegemonyRules.RisingPowerAssertiveness(1.0f, 0.0f);
            float powerless = HegemonyRules.RisingPowerAssertiveness(0.0f, 1.0f);
            Assert.AreEqual(1f, discontent, Eps);
            Assert.AreEqual(0.5f, content, Eps);
            Assert.AreEqual(0f, powerless, Eps);
            Assert.Greater(discontent, content);
        }

        /// <summary>予防戦争の誘惑は機会の窓が閉じるほど増幅される（今叩かねば手遅れ）。</summary>
        [Test]
        public void PreventiveWarTemptation_AmplifiedByClosingWindow()
        {
            float urgent = HegemonyRules.PreventiveWarTemptation(0.8f, 1.0f);
            float calm = HegemonyRules.PreventiveWarTemptation(0.5f, 0.0f);
            Assert.AreEqual(1f, urgent, Eps);
            Assert.AreEqual(0.5f, calm, Eps);
            Assert.Greater(urgent, calm);
        }

        /// <summary>平和的移行の可能性は制度的紐帯が罠を緩める。交差点でも紐帯があれば余地が生まれる。</summary>
        [Test]
        public void PeacefulTransitionChance_EasedByInstitutions()
        {
            float bound = HegemonyRules.PeacefulTransitionChance(1.0f, 1.0f);
            float unbound = HegemonyRules.PeacefulTransitionChance(1.0f, 0.0f);
            // 交差点で紐帯ゼロなら危険度の裏返し＝平和の余地ほぼゼロ。
            Assert.AreEqual(0f, unbound, Eps);
            // 制度的紐帯が罠を緩め平和的移行の余地を生む（16事例中4事例は平和的）。
            Assert.AreEqual(0.6f, bound, Eps);
            Assert.Greater(bound, unbound);
            // 大差なら制度がなくても元から安定（高い平和可能性）。
            Assert.Greater(HegemonyRules.PeacefulTransitionChance(2.0f, 0.0f), 0.9f);
        }
    }
}
