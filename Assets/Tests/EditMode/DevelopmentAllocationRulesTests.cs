using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 開発投資の最適配分（内政最適化・<see cref="DevelopmentAllocationRules"/>）の純ロジックを固定する：
    /// 不安定な惑星ほど優先／人口が多いほど優先／配分は予算以下でウォーターフィリング／投資は収穫逓減／
    /// 予算0・null/空は安全／決定論。
    /// </summary>
    public class DevelopmentAllocationRulesTests
    {
        private static Province Prov(int id, float stability, float population = 100f)
        {
            var p = new Province(id, "", population);
            p.stability = stability;
            p.integration = 1f;
            return p;
        }

        // --- DevelopmentPriority ---

        [Test]
        public void Priority_UnstableProvince_HigherThanStable()
        {
            var unstable = Prov(1, 10f);
            var stable = Prov(2, 90f);
            Assert.Greater(DevelopmentAllocationRules.DevelopmentPriority(unstable),
                           DevelopmentAllocationRules.DevelopmentPriority(stable));
        }

        [Test]
        public void Priority_LargerPopulation_RaisesPriority()
        {
            var small = Prov(1, 40f, 50f);
            var large = Prov(2, 40f, 400f);
            Assert.Greater(DevelopmentAllocationRules.DevelopmentPriority(large),
                           DevelopmentAllocationRules.DevelopmentPriority(small));
        }

        [Test]
        public void Priority_NullProvince_IsZero()
        {
            Assert.AreEqual(0f, DevelopmentAllocationRules.DevelopmentPriority(null), 1e-6f);
        }

        [Test]
        public void Priority_NonNegative()
        {
            Assert.GreaterOrEqual(DevelopmentAllocationRules.DevelopmentPriority(Prov(1, 100f, 0f)), 0f);
        }

        // --- AllocateBudget ---

        [Test]
        public void Allocate_SumDoesNotExceedBudget()
        {
            var provinces = new List<Province> { Prov(1, 10f), Prov(2, 30f), Prov(3, 60f) };
            float budget = 1000f;
            var alloc = DevelopmentAllocationRules.AllocateBudget(provinces, budget);
            float sum = 0f;
            foreach (var kv in alloc) sum += kv.Value;
            Assert.LessOrEqual(sum, budget + 1e-2f);
        }

        [Test]
        public void Allocate_UnstableGetsMoreThanStable()
        {
            // 人口同一・安定だけ差。優先度高い（低安定）方が多く貰う。
            var provinces = new List<Province> { Prov(1, 10f), Prov(2, 80f) };
            var alloc = DevelopmentAllocationRules.AllocateBudget(provinces, 200f);
            Assert.IsTrue(alloc.ContainsKey(1));
            Assert.IsTrue(alloc.ContainsKey(2));
            Assert.Greater(alloc[1], alloc[2]);
        }

        [Test]
        public void Allocate_RespectsPerProvinceCap_WaterFills()
        {
            // 1惑星だけ優先度を持つ → cap (40%) で頭打ち、残りは行き場が無く配分されない。
            var provinces = new List<Province> { Prov(1, 10f, 100f) };
            float budget = 1000f;
            var alloc = DevelopmentAllocationRules.AllocateBudget(provinces, budget);
            float cap = DevelopmentAllocationRules.PerProvinceCapRatio * budget;
            Assert.IsTrue(alloc.ContainsKey(1));
            Assert.LessOrEqual(alloc[1], cap + 1e-2f);
            Assert.AreEqual(cap, alloc[1], 1e-1f); // cap まで届く
        }

        [Test]
        public void Allocate_WaterFill_RedistributesToUnsaturated()
        {
            // 2惑星。cap=40% なので両方上限に達してもΣ=80%<budget。余りは全員 cap で配り切れず残る。
            var provinces = new List<Province> { Prov(1, 10f), Prov(2, 12f) };
            float budget = 1000f;
            var alloc = DevelopmentAllocationRules.AllocateBudget(provinces, budget);
            float cap = DevelopmentAllocationRules.PerProvinceCapRatio * budget;
            Assert.LessOrEqual(alloc[1], cap + 1e-2f);
            Assert.LessOrEqual(alloc[2], cap + 1e-2f);
        }

        [Test]
        public void Allocate_ZeroBudget_Empty()
        {
            var provinces = new List<Province> { Prov(1, 10f), Prov(2, 30f) };
            var alloc = DevelopmentAllocationRules.AllocateBudget(provinces, 0f);
            Assert.AreEqual(0, alloc.Count);
        }

        [Test]
        public void Allocate_NullList_Empty()
        {
            var alloc = DevelopmentAllocationRules.AllocateBudget(null, 1000f);
            Assert.IsNotNull(alloc);
            Assert.AreEqual(0, alloc.Count);
        }

        [Test]
        public void Allocate_EmptyList_Empty()
        {
            var alloc = DevelopmentAllocationRules.AllocateBudget(new List<Province>(), 1000f);
            Assert.AreEqual(0, alloc.Count);
        }

        [Test]
        public void Allocate_HandlesNullEntries()
        {
            var provinces = new List<Province> { null, Prov(2, 20f), null };
            var alloc = DevelopmentAllocationRules.AllocateBudget(provinces, 500f);
            Assert.IsTrue(alloc.ContainsKey(2));
            Assert.AreEqual(1, alloc.Count);
        }

        [Test]
        public void Allocate_Deterministic()
        {
            var a = new List<Province> { Prov(1, 10f), Prov(2, 30f), Prov(3, 55f) };
            var b = new List<Province> { Prov(1, 10f), Prov(2, 30f), Prov(3, 55f) };
            var r1 = DevelopmentAllocationRules.AllocateBudget(a, 750f);
            var r2 = DevelopmentAllocationRules.AllocateBudget(b, 750f);
            Assert.AreEqual(r1.Count, r2.Count);
            foreach (var kv in r1)
            {
                Assert.IsTrue(r2.ContainsKey(kv.Key));
                Assert.AreEqual(kv.Value, r2[kv.Key], 1e-4f);
            }
        }

        // --- ProjectedStabilityGain ---

        [Test]
        public void Gain_ZeroInvest_IsZero()
        {
            Assert.AreEqual(0f, DevelopmentAllocationRules.ProjectedStabilityGain(Prov(1, 20f), 0f), 1e-6f);
        }

        [Test]
        public void Gain_DiminishingReturns_PerUnitDecreases()
        {
            var p = Prov(1, 0f); // 伸びしろ最大でクランプの影響を排除
            float g100 = DevelopmentAllocationRules.ProjectedStabilityGain(p, 100f);
            float g200 = DevelopmentAllocationRules.ProjectedStabilityGain(p, 200f);
            // 総量は増えるが…
            Assert.Greater(g200, g100);
            // …1単位あたりは減る（収穫逓減）。
            float perUnitFirst = g100 / 100f;
            float perUnitSecond = (g200 - g100) / 100f;
            Assert.Greater(perUnitFirst, perUnitSecond);
        }

        [Test]
        public void Gain_ClampedByHeadroom()
        {
            // ほぼ満杯の惑星：伸びしろ僅少で gain がクランプされる。
            var p = Prov(1, GovernanceRules.MaxStability - 2f);
            float g = DevelopmentAllocationRules.ProjectedStabilityGain(p, 10000f);
            Assert.LessOrEqual(g, 2f + 1e-3f);
        }

        [Test]
        public void Gain_NullProvince_IsZero()
        {
            Assert.AreEqual(0f, DevelopmentAllocationRules.ProjectedStabilityGain(null, 100f), 1e-6f);
        }
    }
}
