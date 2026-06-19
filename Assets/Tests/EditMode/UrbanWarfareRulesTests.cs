using NUnit.Framework;
using UnityEngine;

namespace Ginei.Tests
{
    public class UrbanWarfareRulesTests
    {
        const float Eps = 1e-4f;

        [Test]
        public void DefenderAdvantage_DenseCityAndEntrenchmentsFavorDefense()
        {
            var p = UrbanWarfareParams.Default;
            // 0.5*0.6 + 0.5*0.4 = 0.5、最大密度×完全築城 -> 1.0、空地 -> 0。
            Assert.AreEqual(0.5f, UrbanWarfareRules.DefenderAdvantage(0.5f, 0.5f, p), Eps);
            Assert.AreEqual(1f, UrbanWarfareRules.DefenderAdvantage(1f, 1f, p), Eps);
            Assert.AreEqual(0f, UrbanWarfareRules.DefenderAdvantage(0f, 0f, p), Eps);
        }

        [Test]
        public void AttackerCostMultiplier_ThreeToOneRule()
        {
            // 攻者三倍：完全防御優位で 1+1*2 = 3.0、半々で 2.0、無優位で 1.0。
            Assert.AreEqual(3f, UrbanWarfareRules.AttackerCostMultiplier(1f), Eps);
            Assert.AreEqual(2f, UrbanWarfareRules.AttackerCostMultiplier(0.5f), Eps);
            Assert.AreEqual(1f, UrbanWarfareRules.AttackerCostMultiplier(0f), Eps);
        }

        [Test]
        public void CloseQuartersChaos_BlindBrawlBreedsConfusion()
        {
            // 密集×無視界 -> 1.0、半密度×半視界 0.8*0.5 -> 0.4、視界良好 -> 0。
            Assert.AreEqual(1f, UrbanWarfareRules.CloseQuartersChaos(1f, 0f), Eps);
            Assert.AreEqual(0.4f, UrbanWarfareRules.CloseQuartersChaos(0.8f, 0.5f), Eps);
            Assert.AreEqual(0f, UrbanWarfareRules.CloseQuartersChaos(1f, 1f), Eps);
        }

        [Test]
        public void CivilianImpedance_StrictRoEConstrainsAttack()
        {
            // 民間人満載×厳格ROE -> 0.8(上限)、半々 0.5*0.5*0.8 -> 0.2、民間人無し -> 0。
            Assert.AreEqual(0.8f, UrbanWarfareRules.CivilianImpedance(1f, 1f), Eps);
            Assert.AreEqual(0.2f, UrbanWarfareRules.CivilianImpedance(0.5f, 0.5f), Eps);
            Assert.AreEqual(0f, UrbanWarfareRules.CivilianImpedance(0f, 1f), Eps);
        }

        [Test]
        public void RubbleMobility_DebrisAndVehiclesBogDown()
        {
            // 瓦礫無し徒歩 -> 1.0、半破壊×半車両依存 (1-0.5)*(1-0.5) -> 0.25、全瓦礫 -> 0。
            Assert.AreEqual(1f, UrbanWarfareRules.RubbleMobility(0f, 0f), Eps);
            Assert.AreEqual(0.25f, UrbanWarfareRules.RubbleMobility(0.5f, 0.5f), Eps);
            Assert.AreEqual(0f, UrbanWarfareRules.RubbleMobility(1f, 0f), Eps);
        }

        [Test]
        public void BuildingClearing_CostSlowsTheClear()
        {
            // 全力歩兵÷コスト2 -> 0.5、0.6÷コスト3 -> 0.2、無コスト下限1.0で等倍。
            Assert.AreEqual(0.5f, UrbanWarfareRules.BuildingClearing(1f, 2f), Eps);
            Assert.AreEqual(0.2f, UrbanWarfareRules.BuildingClearing(0.6f, 3f), Eps);
            Assert.AreEqual(1f, UrbanWarfareRules.BuildingClearing(1f, 1f), Eps);
        }

        [Test]
        public void VerticalComplexity_HighAndLowMultiply()
        {
            // 高層×地下の積 -> 1.0、0.5*0.4 -> 0.2、片方ゼロ -> 0。
            Assert.AreEqual(1f, UrbanWarfareRules.VerticalComplexity(1f, 1f), Eps);
            Assert.AreEqual(0.2f, UrbanWarfareRules.VerticalComplexity(0.5f, 0.4f), Eps);
            Assert.AreEqual(0f, UrbanWarfareRules.VerticalComplexity(1f, 0f), Eps);
        }

        [Test]
        public void UrbanCombatProgress_ClearingMinusDragAndConstraint()
        {
            // 制圧0.6 − 制約0.1 − コスト超過(2-1)/3=0.3333 -> 0.16667。
            Assert.AreEqual(0.16667f, UrbanWarfareRules.UrbanCombatProgress(0.6f, 0.1f, 2f), Eps);
            // 制圧無し・コスト最大 -> 0 - 0 - (3-1)/3 = -0.66667（押し戻される）。
            Assert.AreEqual(-0.66667f, UrbanWarfareRules.UrbanCombatProgress(0f, 0f, 3f), Eps);
        }

        [Test]
        public void Narrative_GrindingStreetFightStallsTheAttacker()
        {
            var p = UrbanWarfareParams.Default;

            // 密集市街(密度1)に半ば築城された陣地(0.5)。防御側が強く優位。
            float adv = UrbanWarfareRules.DefenderAdvantage(1f, 0.5f, p);
            Assert.AreEqual(0.8f, adv, Eps);            // 0.6 + 0.2

            // 攻者三倍に近い損耗を強いられる。
            float cost = UrbanWarfareRules.AttackerCostMultiplier(adv, p);
            Assert.AreEqual(2.6f, cost, Eps);           // 1 + 0.8*2

            // 至近・無視界で指揮が乱れ、民間人と交戦規則が手を縛る。
            float chaos = UrbanWarfareRules.CloseQuartersChaos(0.9f, 0.2f, p);
            float impede = UrbanWarfareRules.CivilianImpedance(0.5f, 0.5f, p);
            Assert.AreEqual(0.72f, chaos, Eps);         // 0.9*0.8*1
            Assert.AreEqual(0.2f, impede, Eps);         // 0.25*0.8

            // 高い損耗の下では一棟の制圧も遅々として進まない。
            float clearing = UrbanWarfareRules.BuildingClearing(0.9f, cost, p);
            Assert.AreEqual(0.34615f, clearing, Eps);   // 0.9/2.6

            // 進捗は制圧 − 制約 − コスト超過で押し戻される（攻撃側は泥沼に沈む）。
            float progress = UrbanWarfareRules.UrbanCombatProgress(clearing, impede, cost, p);
            // 0.34615 - 0.2 - (2.6-1)/3=0.53333 -> -0.38718
            Assert.AreEqual(-0.38718f, progress, Eps);

            Assert.Greater(cost, 1f);
            Assert.Greater(chaos, 0.5f);
            Assert.Less(progress, 0f);
        }

        [Test]
        public void Params_AreClampedInConstructor()
        {
            var p = new UrbanWarfareParams(
                densityWeight: 9f,        // -> 1
                preparedWeight: -9f,      // -> 0
                attackerCostScale: 9f,    // -> 4
                chaosMax: 9f,             // -> 1
                impedanceMax: -9f,        // -> 0
                rubbleWeight: 9f,         // -> 1
                clearingScale: -9f,       // -> 0.01
                verticalWeight: 9f);      // -> 1
            Assert.AreEqual(1f, p.densityWeight, Eps);
            Assert.AreEqual(0f, p.preparedWeight, Eps);
            Assert.AreEqual(4f, p.attackerCostScale, Eps);
            Assert.AreEqual(1f, p.chaosMax, Eps);
            Assert.AreEqual(0f, p.impedanceMax, Eps);
            Assert.AreEqual(1f, p.rubbleWeight, Eps);
            Assert.AreEqual(0.01f, p.clearingScale, Eps);
            Assert.AreEqual(1f, p.verticalWeight, Eps);
        }
    }
}
