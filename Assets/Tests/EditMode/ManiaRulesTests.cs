using NUnit.Framework;
using UnityEngine;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// マニア（集団的熱狂・信念）の感染モデル（MNIA-1 #1620・SIR 型）を固定する：Tick の SIR 保存則
    /// （susceptible+infected+recovered を保つ）、基本再生産数 R0＝β/γ と流行/消滅判定（R0&gt;1 で広がり
    /// &lt;1 で消える）、集団免疫閾値 1−1/R0、流行ピーク近似、感受性（思想共鳴↑既往感染↓で感染しやすい）、
    /// 横断合成熱狂度。境界・クランプ・各分岐を担保する。
    /// </summary>
    public class ManiaRulesTests
    {
        private static ManiaParams P => ManiaParams.Default;

        // --- Tick / 保存則 ---

        [Test]
        public void Tick_ConservesTotalAndSpreadsThenRecovers()
        {
            // 種火 infected=0.1（S=0.9, R=0）。β=0.6, γ=0.1, dt=1。
            ManiaState s = ManiaState.Seed(0.1f);
            float before = s.Total;
            s = ManiaRules.Tick(s, 0.6f, 0.1f, 1f);

            // 保存則：合計は保たれる。
            Assert.AreEqual(before, s.Total, 1e-4f);
            // 新規感染 βSI=0.6×0.9×0.1=0.054、回復 γI=0.1×0.1=0.01。
            Assert.AreEqual(0.9f - 0.054f, s.susceptible, 1e-4f);
            Assert.AreEqual(0.1f + 0.054f - 0.01f, s.infected, 1e-4f);
            Assert.AreEqual(0.01f, s.recovered, 1e-4f);
        }

        [Test]
        public void Tick_NonPositiveDt_ReturnsUnchanged()
        {
            ManiaState s = ManiaState.Seed(0.2f);
            ManiaState r = ManiaRules.Tick(s, 0.5f, 0.1f, 0f);
            Assert.AreEqual(s.susceptible, r.susceptible, 1e-6f);
            Assert.AreEqual(s.infected, r.infected, 1e-6f);
            Assert.AreEqual(s.recovered, r.recovered, 1e-6f);
        }

        [Test]
        public void Tick_NoSusceptible_NoNewInfection()
        {
            // 感受性0＝もう広がらない（全員が感染済み/回復済み）。
            ManiaState s = new ManiaState(0f, 0.3f, 0.7f);
            float before = s.Total;
            s = ManiaRules.Tick(s, 1f, 0.1f, 1f);
            Assert.AreEqual(before, s.Total, 1e-4f);
            Assert.AreEqual(0f, s.susceptible, 1e-6f);     // 増えも減りもしない
            Assert.AreEqual(0.3f - 0.03f, s.infected, 1e-4f); // 回復だけ進む γI=0.03
        }

        // --- R0 / 流行判定 ---

        [Test]
        public void BasicReproduction_AndWillSpread()
        {
            Assert.AreEqual(3f, ManiaRules.BasicReproduction(0.6f, 0.2f), 1e-4f);   // β/γ=3
            Assert.IsTrue(ManiaRules.WillSpread(3f));                               // R0>1＝流行
            // β=0.1, γ=0.2 → R0=0.5 <1＝消える
            Assert.AreEqual(0.5f, ManiaRules.BasicReproduction(0.1f, 0.2f), 1e-4f);
            Assert.IsFalse(ManiaRules.WillSpread(0.5f));
            // γ=0＝回復しない＝暴走（大きな値）
            Assert.IsTrue(ManiaRules.BasicReproduction(0.5f, 0f) > 1f);
        }

        // --- 集団免疫閾値 ---

        [Test]
        public void HerdImmunityThreshold_IsOneMinusInverseR0()
        {
            Assert.AreEqual(1f - 1f / 3f, ManiaRules.HerdImmunityThreshold(3f), 1e-4f); // ≈0.6667
            Assert.AreEqual(0.5f, ManiaRules.HerdImmunityThreshold(2f), 1e-4f);
            Assert.AreEqual(0f, ManiaRules.HerdImmunityThreshold(1f), 1e-6f);           // R0≤1＝流行せず
            Assert.AreEqual(0f, ManiaRules.HerdImmunityThreshold(0.5f), 1e-6f);
        }

        // --- ピーク ---

        [Test]
        public void PeakInfected_RisesWithR0_ZeroBelowOne()
        {
            Assert.AreEqual(0f, ManiaRules.PeakInfected(0.8f, 1f), 1e-6f);  // R0<1＝ピークなし
            // R0=2, s0=1 → (1-0.5)^2 = 0.25
            Assert.AreEqual(0.25f, ManiaRules.PeakInfected(2f, 1f), 1e-4f);
            // R0 が大きいほど高い
            Assert.Greater(ManiaRules.PeakInfected(5f, 1f), ManiaRules.PeakInfected(2f, 1f));
            // s0 に比例して下がる
            Assert.AreEqual(0.125f, ManiaRules.PeakInfected(2f, 0.5f), 1e-4f);
        }

        // --- 感受性 ---

        [Test]
        public void Susceptibility_HighResonanceLowPriorExposure()
        {
            // 共鳴0.8・既往感染0＝そのまま 0.8
            Assert.AreEqual(0.8f, ManiaRules.Susceptibility(0.8f, 0f, P), 1e-4f);
            // 既往感染1（醒めた者）＝免疫で0へ
            Assert.AreEqual(0f, ManiaRules.Susceptibility(0.8f, 1f, P), 1e-4f);
            // 既往感染0.5＝半減 0.8×0.5=0.4
            Assert.AreEqual(0.4f, ManiaRules.Susceptibility(0.8f, 0.5f, P), 1e-4f);
        }

        // --- 横断合成 ---

        [Test]
        public void CrossDomainIntensity_WeightedAverage()
        {
            // 既定は均等重み＝単純平均。経済0.9/政治0.3/宗教0.6 → 0.6
            Assert.AreEqual(0.6f, ManiaRules.CrossDomainIntensity(0.9f, 0.3f, 0.6f, P), 1e-4f);
            // どれかが燃えれば全体が上がる
            Assert.Greater(ManiaRules.CrossDomainIntensity(1f, 0f, 0f, P), 0f);
        }
    }
}
