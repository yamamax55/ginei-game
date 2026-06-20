using NUnit.Framework;
using UnityEngine;
using Ginei;

namespace Ginei.Tests
{
    public class RefugeeFlowRulesTests
    {
        [Test]
        public void Outflow_WarAndFamine_RaiseOutflow()
        {
            var p = RefugeeFlowRules.Default;
            float baseline = RefugeeFlowRules.Outflow(1000f, 0f, 0f, p);
            float withWar = RefugeeFlowRules.Outflow(1000f, 0.5f, 0f, p);
            float withFamine = RefugeeFlowRules.Outflow(1000f, 0f, 0.5f, p);
            Assert.AreEqual(0f, baseline, 1e-5f); // 押し出し要因なしでゼロ
            Assert.Greater(withWar, baseline);
            Assert.Greater(withFamine, baseline);
        }

        [Test]
        public void Outflow_CappedAtMaxRate()
        {
            var p = RefugeeFlowRules.Default;
            // 戦争・飢饉とも最大でも maxOutflowRate*population で頭打ち
            float maxed = RefugeeFlowRules.Outflow(1000f, 1f, 1f, p);
            Assert.AreEqual(1000f * p.maxOutflowRate, maxed, 1e-5f);
        }

        [Test]
        public void Outflow_Deterministic()
        {
            float a = RefugeeFlowRules.Outflow(800f, 0.3f, 0.2f);
            float b = RefugeeFlowRules.Outflow(800f, 0.3f, 0.2f);
            Assert.AreEqual(a, b, 1e-5f);
        }

        [Test]
        public void Outflow_ZeroPopulation_NoOutflow()
        {
            Assert.AreEqual(0f, RefugeeFlowRules.Outflow(0f, 1f, 1f), 1e-5f);
        }

        [Test]
        public void HostStrain_RisesWithRefugees_FallsWithHostPop()
        {
            var p = new RefugeeFlowRules.Params(0.3f, 0.25f, 0.2f, 0.001f);
            float few = RefugeeFlowRules.HostStrain(100f, 100000f, p);
            float many = RefugeeFlowRules.HostStrain(500f, 100000f, p);
            Assert.Greater(many, few); // 難民増で逼迫上昇

            float small = RefugeeFlowRules.HostStrain(500f, 50000f, p);
            float big = RefugeeFlowRules.HostStrain(500f, 100000f, p);
            Assert.Greater(small, big); // 受け入れ人口が多いほど逼迫低下
        }

        [Test]
        public void HostStrain_Clamped01()
        {
            var p = new RefugeeFlowRules.Params(0.3f, 0.25f, 0.2f, 10f);
            float s = RefugeeFlowRules.HostStrain(1000f, 100f, p);
            Assert.LessOrEqual(s, 1f);
            Assert.GreaterOrEqual(s, 0f);
            Assert.AreEqual(1f, s, 1e-5f); // 過大入力でも1.0に飽和
        }

        [Test]
        public void HostStrain_ZeroHostPop_Guarded()
        {
            float s = RefugeeFlowRules.HostStrain(100f, 0f);
            Assert.IsFalse(float.IsNaN(s));
            Assert.IsFalse(float.IsInfinity(s));
            Assert.LessOrEqual(s, 1f);
            Assert.GreaterOrEqual(s, 0f);
        }

        [Test]
        public void OpinionShift_NonPositive_AndProportional()
        {
            float low = RefugeeFlowRules.OpinionShift(0.2f, 10f);
            float high = RefugeeFlowRules.OpinionShift(0.8f, 10f);
            Assert.LessOrEqual(low, 0f);
            Assert.LessOrEqual(high, 0f);
            Assert.Less(high, low); // 逼迫が強いほど心象悪化（より負）
            Assert.AreEqual(-2f, low, 1e-5f);
            Assert.AreEqual(-8f, high, 1e-5f);
        }

        [Test]
        public void OpinionShift_ClampsInputAndPenalty()
        {
            // 逼迫>1 はクランプ、ペナルティ負はゼロ床
            Assert.AreEqual(-10f, RefugeeFlowRules.OpinionShift(2f, 10f), 1e-5f);
            Assert.AreEqual(0f, RefugeeFlowRules.OpinionShift(0.5f, -5f), 1e-5f);
        }
    }
}
