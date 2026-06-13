using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// POP の引っ越し＝移住（#194・<see cref="PopulationMigrationRules"/>）を固定する：定住魅力度（安定/統合で上がる）、
    /// 住みよい惑星へ流れる向き／摩擦で小差は動かない／1tick上限／総量保存／コホート同期。
    /// </summary>
    public class PopulationMigrationTests
    {
        private static PopulationMigrationRules.MigrationParams Prm => PopulationMigrationRules.MigrationParams.Default;

        private static Province P(float stability, float integration = 1f, float pop = 100f)
            => new Province(1, "民主", pop) { stability = stability, integration = integration };

        [Test]
        public void Attractiveness_RisesWithStabilityAndIntegration()
        {
            var good = P(GovernanceRules.MaxStability, 1f);
            var bad = P(10f, 0.2f);
            Assert.Greater(PopulationMigrationRules.Attractiveness(good),
                           PopulationMigrationRules.Attractiveness(bad));
            Assert.That(PopulationMigrationRules.Attractiveness(good), Is.InRange(0f, 1f));
        }

        [Test]
        public void MigrationFlow_TowardAttractive_ZeroReverse()
        {
            var poor = P(10f, 0.3f, 100f);   // 荒れた惑星
            var rich = P(GovernanceRules.MaxStability, 1f, 100f); // 住みよい惑星
            Assert.Greater(PopulationMigrationRules.MigrationFlow(poor, rich, Prm, 1f), 0f); // 荒地→住みよい へ流れる
            Assert.AreEqual(0f, PopulationMigrationRules.MigrationFlow(rich, poor, Prm, 1f), 1e-6f); // 逆向きは流れない
        }

        [Test]
        public void MigrationFlow_SmallDifference_NoMove_AndCapped()
        {
            // 引力差が摩擦未満＝動かない
            var a = P(60f, 1f);
            var b = P(61f, 1f);
            Assert.AreEqual(0f, PopulationMigrationRules.MigrationFlow(a, b, Prm, 1f), 1e-6f);

            // 1tick上限割合でクランプ（大差でも一気に空洞化しない）
            var poor = P(0f, 0f, 1000f);
            var rich = P(GovernanceRules.MaxStability, 1f, 1000f);
            float flow = PopulationMigrationRules.MigrationFlow(poor, rich, Prm, 1f);
            Assert.LessOrEqual(flow, poor.population * Prm.maxFraction + 1e-3f);
        }

        [Test]
        public void TickPair_ConservesTotal_AndMovesPeople()
        {
            var poor = P(5f, 0.2f, 200f);
            var rich = P(GovernanceRules.MaxStability, 1f, 200f);
            float before = poor.population + rich.population;
            float moved = PopulationMigrationRules.TickPair(poor, rich, Prm, 1f);
            Assert.Greater(moved, 0f);
            Assert.Less(poor.population, 200f);   // 荒地は流出で減る
            Assert.Greater(rich.population, 200f); // 住みよい地は流入で増える
            Assert.AreEqual(before, poor.population + rich.population, 1e-2f); // 総量保存（湧かない・消えない）
        }

        [Test]
        public void Move_SyncsDemographics()
        {
            var from = P(5f, 0.2f, 100f);
            var to = P(GovernanceRules.MaxStability, 1f, 100f);
            PopulationDynamicsRules.EnsureDemographics(from);
            PopulationDynamicsRules.EnsureDemographics(to);
            float before = from.population + to.population;
            PopulationMigrationRules.Move(from, to, 20f);
            Assert.AreEqual(80f, from.population, 1e-2f);
            Assert.AreEqual(120f, to.population, 1e-2f);
            // population はコホート合計に同期
            Assert.AreEqual(from.demographics.Total, from.population, 1e-2f);
            Assert.AreEqual(to.demographics.Total, to.population, 1e-2f);
            Assert.AreEqual(before, from.population + to.population, 1e-2f);
        }
    }
}
