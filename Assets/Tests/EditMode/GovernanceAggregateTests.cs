using NUnit.Framework;
using System.Collections.Generic;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 内政三層のハイブリッド（#767）の集約バックボーンを固定する：
    /// 星系は固有データを重複して持たず、配下の惑星 Province を人口加重でロールアップする。
    /// 安定度/統合度＝人口加重平均、支配思想＝人口最多、産出＝Σ(OutputFactor×pop)、反乱は最悪値。
    /// </summary>
    public class GovernanceAggregateTests
    {
        private static Province Planet(string ideology, float pop, float stability, float integration = 1f)
            => new Province { nativeIdeology = ideology, population = pop, stability = stability, integration = integration };

        [Test]
        public void Empty_Or_Null_ReturnsDefaultZeroPlanets()
        {
            Assert.AreEqual(0, GovernanceRules.AggregateSystem(null).planetCount);
            Assert.AreEqual(0, GovernanceRules.AggregateSystem(new List<Province>()).planetCount);
            // null 要素は数えない
            var withNull = new List<Province> { null, null };
            Assert.AreEqual(0, GovernanceRules.AggregateSystem(withNull).planetCount);
        }

        [Test]
        public void Stability_IsPopulationWeightedAverage()
        {
            // 人口100で安定80、人口300で安定40 → 加重平均 = (80*100 + 40*300)/400 = 50
            var planets = new List<Province>
            {
                Planet("民主", 100f, 80f),
                Planet("専制", 300f, 40f),
            };
            var g = GovernanceRules.AggregateSystem(planets);
            Assert.AreEqual(2, g.planetCount);
            Assert.AreEqual(400f, g.totalPopulation, 1e-3f);
            Assert.AreEqual(50f, g.weightedStability, 1e-3f); // 単純平均(60)ではなく人口加重(50)
        }

        [Test]
        public void DominantIdeology_IsByPopulation_NotByCount()
        {
            // 「民主」の惑星は2つだが人口は計150、「専制」は1つだが人口500 → 支配思想=専制
            var planets = new List<Province>
            {
                Planet("民主", 100f, 50f),
                Planet("民主", 50f, 50f),
                Planet("専制", 500f, 50f),
            };
            var g = GovernanceRules.AggregateSystem(planets);
            Assert.AreEqual("専制", g.dominantIdeology);
        }

        [Test]
        public void TotalOutput_SumsOutputFactorTimesPopulation()
        {
            // 安定100→OutputFactor=1.0、安定0→OutputFactor=MinOutputFactor(0.3)
            var hi = Planet("", 200f, GovernanceRules.MaxStability);   // 1.0 * 200 = 200
            var lo = Planet("", 100f, 0f);                              // 0.3 * 100 = 30
            var g = GovernanceRules.AggregateSystem(new List<Province> { hi, lo });
            float expected = GovernanceRules.OutputFactor(hi) * 200f + GovernanceRules.OutputFactor(lo) * 100f;
            Assert.AreEqual(expected, g.totalOutput, 1e-3f);
            Assert.AreEqual(230f, g.totalOutput, 1e-3f);
        }

        [Test]
        public void Unrest_And_MaxRebelPressure_ReflectWorstPlanet()
        {
            // 1つだけ反乱域（安定10 < RebelThreshold25）
            var calm = Planet("", 100f, 80f);
            var riot = Planet("", 100f, 10f);
            var g = GovernanceRules.AggregateSystem(new List<Province> { calm, riot });
            Assert.IsTrue(g.anyUnrest);
            Assert.AreEqual(GovernanceRules.RebelPressure(riot), g.maxRebelPressure, 1e-3f);
            Assert.Greater(g.maxRebelPressure, 0f);
        }

        [Test]
        public void ZeroPopulation_FallsBackToSimpleAverage()
        {
            // 全惑星 pop=0：ゼロ割せず単純平均（安定 (90+30)/2 = 60）
            var planets = new List<Province>
            {
                Planet("", 0f, 90f, 0.5f),
                Planet("", 0f, 30f, 1f),
            };
            var g = GovernanceRules.AggregateSystem(planets);
            Assert.AreEqual(0f, g.totalPopulation, 1e-3f);
            Assert.AreEqual(60f, g.weightedStability, 1e-3f);
            Assert.AreEqual(0.75f, g.weightedIntegration, 1e-3f); // (0.5+1)/2
        }
    }
}
