using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// POP の出生・死亡を惑星内政へ接続する純ロジック（LIFE-3 #153 配線・<see cref="PopulationDynamicsRules"/>）を固定する：
    /// コホートの起こし（冪等）／安定度で出生死亡が増減／安定惑星は人口増・荒れた惑星は人口減／population 同期。
    /// </summary>
    public class PopulationDynamicsTests
    {
        private static DemographicsRules.VitalRates Base => DemographicsRules.VitalRates.Default;

        [Test]
        public void EnsureDemographics_SplitsPopulation_AndIsIdempotent()
        {
            var p = new Province(1, "民主", 200f);
            Assert.IsNull(p.demographics);
            Population pop = PopulationDynamicsRules.EnsureDemographics(p);
            Assert.IsNotNull(pop);
            Assert.AreEqual(200f, pop.Total, 1e-3f);            // 合計＝元の population
            Assert.Greater(pop.working, pop.youth);            // 生産年齢が最多（既定構成）
            // 冪等＝二度目は同じインスタンス（作り直さない）
            Assert.AreSame(pop, PopulationDynamicsRules.EnsureDemographics(p));
        }

        [Test]
        public void EffectiveVitalRates_StabilityRaisesBirths_LowersDeaths()
        {
            var stable = new Province(1, "民主", 100f) { stability = GovernanceRules.MaxStability };
            var failing = new Province(2, "民主", 100f) { stability = 0f };
            var rs = PopulationDynamicsRules.EffectiveVitalRates(stable, Base);
            var rf = PopulationDynamicsRules.EffectiveVitalRates(failing, Base);
            Assert.Greater(rs.birthRate, rf.birthRate);          // 安定＝出生多い
            Assert.Less(rs.elderMortality, rf.elderMortality);   // 安定＝死亡少ない
            // 加齢率は不変
            Assert.AreEqual(Base.youthAging, rs.youthAging, 1e-6f);
        }

        [Test]
        public void TickYear_StablePlanetGrows_SyncsPopulation()
        {
            var p = new Province(1, "民主", 100f) { stability = GovernanceRules.MaxStability };
            float delta = PopulationDynamicsRules.TickYear(p, Base);
            Assert.Greater(delta, 0f);                            // 安定惑星は1年で人口増
            Assert.AreEqual(p.demographics.Total, p.population, 1e-3f); // population はコホート合計に同期
        }

        [Test]
        public void TickYear_CollapsingPlanetDeclines()
        {
            var p = new Province(2, "専制", 100f) { stability = 0f };
            float delta = PopulationDynamicsRules.TickYear(p, Base);
            Assert.Less(delta, 0f);                               // 荒れた惑星は出生減・死亡増で人口減
        }

        [Test]
        public void ProjectedAnnualGrowth_NonDestructive()
        {
            var p = new Province(3, "民主", 100f) { stability = GovernanceRules.MaxStability };
            // 未設定でも非破壊で見積れる（demographics を作らない）
            float g = PopulationDynamicsRules.ProjectedAnnualGrowth(p, Base);
            Assert.Greater(g, 0f);
            Assert.IsNull(p.demographics); // 見積りは器を作らない（表示用）
        }
    }
}
