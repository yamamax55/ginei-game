using NUnit.Framework;
using UnityEngine;

namespace Ginei.Tests
{
    /// <summary>
    /// TargetPriorityRules（射撃目標優先度＝集中砲火/仕留め/斬首/側背面）の EditMode テスト。
    /// 既定 Params で期待値を固定。
    /// </summary>
    public class TargetPriorityRulesTests
    {
        private const float Eps = 1e-4f;

        [Test]
        public void Proximity_NearIsHigh_FarIsZero()
        {
            var p = TargetPriorityParams.Default; // maxRange=10
            Assert.AreEqual(1f, TargetPriorityRules.Proximity(0f, p), Eps);
            Assert.AreEqual(0.5f, TargetPriorityRules.Proximity(5f, p), Eps);
            Assert.AreEqual(0f, TargetPriorityRules.Proximity(10f, p), Eps);
            // 射程外も0でクランプ。
            Assert.AreEqual(0f, TargetPriorityRules.Proximity(20f, p), Eps);
        }

        [Test]
        public void FinishValue_LowRemainingIsHigh()
        {
            // 残存10/100 → 仕留め価値0.9。
            Assert.AreEqual(0.9f, TargetPriorityRules.FinishValue(10f, 100f), Eps);
            // 満タンは0。
            Assert.AreEqual(0f, TargetPriorityRules.FinishValue(100f, 100f), Eps);
            // max=0 は満タン扱い（0価値）。
            Assert.AreEqual(0f, TargetPriorityRules.FinishValue(0f, 0f), Eps);
        }

        [Test]
        public void FocusModifier_RampsUpToCapThenOverkillPenalty()
        {
            var p = TargetPriorityParams.Default; // cap=3, bonus=0.3, overkill=0.25
            Assert.AreEqual(0f, TargetPriorityRules.FocusModifier(0f, p), Eps);
            Assert.AreEqual(0.1f, TargetPriorityRules.FocusModifier(1f, p), Eps);   // 0.3*(1/3)
            Assert.AreEqual(0.3f, TargetPriorityRules.FocusModifier(3f, p), Eps);   // cap でピーク
            // cap+2 = 5 → 0.3 - 0.25*2 = -0.2（過剰集中で減点）。
            Assert.AreEqual(-0.2f, TargetPriorityRules.FocusModifier(5f, p), Eps);
        }

        [Test]
        public void IsOverkill_AboveCap()
        {
            var p = TargetPriorityParams.Default; // cap=3
            Assert.IsFalse(TargetPriorityRules.IsOverkill(3f, p));
            Assert.IsTrue(TargetPriorityRules.IsOverkill(4f, p));
        }

        [Test]
        public void PriorityScore_FlagshipBeatsEquivalentEscort()
        {
            // 同条件（距離・残存・側背面・攻撃数）で旗艦は加点ぶん高い。
            float flag = TargetPriorityRules.PriorityScore(5f, 100f, 100f, true, 0f, 0f);
            float esc = TargetPriorityRules.PriorityScore(5f, 100f, 100f, false, 0f, 0f);
            Assert.Greater(flag, esc);
            Assert.AreEqual(TargetPriorityParams.Default.flagshipWeight, flag - esc, Eps);
        }

        [Test]
        public void PriorityScore_WoundedTargetPreferredOverHealthyNearby()
        {
            // 瀕死の遠い敵 vs 満タンの近い敵：仕留め加点で瀕死が勝ちうる。
            float wounded = TargetPriorityRules.PriorityScore(8f, 5f, 100f, false, 0f, 0f);
            float healthy = TargetPriorityRules.PriorityScore(2f, 100f, 100f, false, 0f, 0f);
            // proximity: 0.2 vs 0.8。finish: 0.8*0.95=0.76 vs 0。
            // wounded=0.2+0.76=0.96, healthy=0.8 → wounded 優先。
            Assert.Greater(wounded, healthy);
        }

        [Test]
        public void PriorityScore_NeverNegative()
        {
            // 過剰オーバーキルでも0未満にならない。
            float s = TargetPriorityRules.PriorityScore(10f, 100f, 100f, false, 0f, 100f);
            Assert.GreaterOrEqual(s, 0f);
        }

        [Test]
        public void PriorityScore_FlankAddsValue()
        {
            float front = TargetPriorityRules.PriorityScore(5f, 100f, 100f, false, 0f, 0f);
            float rear = TargetPriorityRules.PriorityScore(5f, 100f, 100f, false, 1f, 0f);
            Assert.AreEqual(TargetPriorityParams.Default.flankWeight, rear - front, Eps);
        }

        [Test]
        public void Prefer_HigherScoreWins_TieBreaksByDistance()
        {
            // A スコア高 → A 優先。
            Assert.AreEqual(-1, TargetPriorityRules.Prefer(2f, 5f, 1f, 1f));
            // B スコア高 → B 優先。
            Assert.AreEqual(1, TargetPriorityRules.Prefer(1f, 1f, 2f, 5f));
            // 同点 → 近い方（A=距離3）優先。
            Assert.AreEqual(-1, TargetPriorityRules.Prefer(2f, 3f, 2f, 9f));
            // 完全同等 → 0。
            Assert.AreEqual(0, TargetPriorityRules.Prefer(2f, 5f, 2f, 5f));
        }

        [Test]
        public void Params_ClampsInvalidInput()
        {
            // maxRange<=0・cap<1・負の重みはクランプされる。
            var p = new TargetPriorityParams(-5f, -1f, -1f, -1f, 0f, -1f, -1f);
            Assert.GreaterOrEqual(p.maxRange, 0.01f);
            Assert.GreaterOrEqual(p.focusFireCap, 1f);
            Assert.AreEqual(0f, p.flagshipWeight, Eps);
            Assert.AreEqual(0f, p.focusBonus, Eps);
        }
    }
}
