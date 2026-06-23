using NUnit.Framework;
using UnityEngine;

namespace Ginei.Tests
{
    /// <summary>
    /// 制宙戦略（マハン的制海権思想の宇宙版）の純ロジックのテスト。
    /// 既定 Params の具体値で期待値を固定（許容 1e-4f、Pow 箇所のみ 1e-3f）。
    /// </summary>
    public class MaritimeStrategyRulesTests
    {
        const float Eps = 1e-4f;
        const float PowEps = 1e-3f;

        [Test]
        public void SpaceLaneControl_FleetTimesPresence()
        {
            // 0.8 * 0.5 * 1.0(laneControlScale) = 0.4
            Assert.AreEqual(0.4f, MaritimeStrategyRules.SpaceLaneControl(0.8f, 0.5f), Eps);
            // 航路に居なければ支配なし
            Assert.AreEqual(0f, MaritimeStrategyRules.SpaceLaneControl(1f, 0f), Eps);
            // クランプ（過大入力でも0..1）
            Assert.AreEqual(1f, MaritimeStrategyRules.SpaceLaneControl(5f, 5f), Eps);
        }

        [Test]
        public void EconomicBenefit_ControlTimesTrade()
        {
            // 0.4 * 0.6 = 0.24
            Assert.AreEqual(0.24f, MaritimeStrategyRules.EconomicBenefit(0.4f, 0.6f), Eps);
            // 交易ゼロなら利益ゼロ
            Assert.AreEqual(0f, MaritimeStrategyRules.EconomicBenefit(1f, 0f), Eps);
        }

        [Test]
        public void CommandOfTheSpace_ShareOfFleets()
        {
            // 60/(60+40) = 0.6、pow(0.6,1)=0.6
            Assert.AreEqual(0.6f, MaritimeStrategyRules.CommandOfTheSpace(60f, 40f), PowEps);
            // 敵不在なら完全制宙
            Assert.AreEqual(1f, MaritimeStrategyRules.CommandOfTheSpace(50f, 0f), PowEps);
            // 双方不在＝拮抗0.5
            Assert.AreEqual(0.5f, MaritimeStrategyRules.CommandOfTheSpace(0f, 0f), Eps);
            // 拮抗
            Assert.AreEqual(0.5f, MaritimeStrategyRules.CommandOfTheSpace(50f, 50f), PowEps);
        }

        [Test]
        public void EnemyContainment_CommandTimesBlockade()
        {
            // 0.6 * 0.5 = 0.3
            Assert.AreEqual(0.3f, MaritimeStrategyRules.EnemyContainment(0.6f, 0.5f), Eps);
            // 封鎖に出なければ封じ込めゼロ
            Assert.AreEqual(0f, MaritimeStrategyRules.EnemyContainment(1f, 0f), Eps);
        }

        [Test]
        public void SeaPowerCost_FleetTimesMaintenance()
        {
            // 0.8 * 0.6 * 0.5(costScale) = 0.24
            Assert.AreEqual(0.24f, MaritimeStrategyRules.SeaPowerCost(0.8f, 0.6f), Eps);
            // 艦隊ゼロならコストゼロ
            Assert.AreEqual(0f, MaritimeStrategyRules.SeaPowerCost(0f, 1f), Eps);
        }

        [Test]
        public void NavalVsContinental_FleetShareOfInvestment()
        {
            // 70/(70+30) = 0.7（海洋寄り）
            Assert.AreEqual(0.7f, MaritimeStrategyRules.NavalVsContinental(70f, 30f), Eps);
            // 領域のみ＝大陸国家型0
            Assert.AreEqual(0f, MaritimeStrategyRules.NavalVsContinental(0f, 50f), Eps);
            // 双方無投資＝中立0.5
            Assert.AreEqual(0.5f, MaritimeStrategyRules.NavalVsContinental(0f, 0f), Eps);
        }

        [Test]
        public void StrategicReach_CommandTimesLane()
        {
            // 0.6 * 0.4 = 0.24
            Assert.AreEqual(0.24f, MaritimeStrategyRules.StrategicReach(0.6f, 0.4f), Eps);
            // 航路を握れなければ投射できない
            Assert.AreEqual(0f, MaritimeStrategyRules.StrategicReach(1f, 0f), Eps);
        }

        [Test]
        public void MaritimeStrategyValue_BenefitContainmentMinusCost()
        {
            // 0.24*0.3 - 0.24*0.5(costWeight) = 0.072 - 0.12 = -0.048（コスト勝ち＝割に合わない）
            Assert.AreEqual(-0.048f, MaritimeStrategyRules.MaritimeStrategyValue(0.24f, 0.3f, 0.24f), Eps);
            // 利益・封じ込め最大・コストゼロ＝価値1
            Assert.AreEqual(1f, MaritimeStrategyRules.MaritimeStrategyValue(1f, 1f, 0f), Eps);
            // 利益ゼロ・コスト最大＝負（-1へクランプはされない、-0.5）
            Assert.AreEqual(-0.5f, MaritimeStrategyRules.MaritimeStrategyValue(0f, 0f, 1f), Eps);
        }

        [Test]
        public void DefaultParams_HaveExpectedValues()
        {
            var p = MaritimeStrategyParams.Default;
            Assert.AreEqual(1.0f, p.laneControlScale, Eps);
            Assert.AreEqual(1.0f, p.commandExponent, Eps);
            Assert.AreEqual(0.5f, p.costScale, Eps);
            Assert.AreEqual(0.5f, p.costWeight, Eps);
        }

        // 物語テスト：海洋国家が制宙戦略で覇権を握る一連の流れ。
        [Test]
        public void Narrative_MaritimePowerSecuresLanesAndContainsEnemy()
        {
            var p = MaritimeStrategyParams.Default;

            // 海洋国家型へ大きく傾斜（艦隊80・領域20）
            float lean = MaritimeStrategyRules.NavalVsContinental(80f, 20f, p);
            Assert.Greater(lean, 0.5f, "艦隊重視＝海洋国家型へ傾く");

            // 強力な主力艦隊が航路に展開し航路を支配
            float lane = MaritimeStrategyRules.SpaceLaneControl(0.9f, 0.9f, p);
            Assert.Greater(lane, 0.7f, "主力が航路を握る");

            // 航路支配が交易で経済的利益を生む
            float benefit = MaritimeStrategyRules.EconomicBenefit(lane, 0.8f, p);
            Assert.Greater(benefit, 0.5f, "航路支配が富を生む");

            // 主力が敵を圧倒して制宙権を確立
            float command = MaritimeStrategyRules.CommandOfTheSpace(80f, 20f, p);
            Assert.Greater(command, 0.7f, "敵主力を圧倒し制宙権を握る");

            // 制宙権＋封鎖で敵艦隊を封じ込める
            float containment = MaritimeStrategyRules.EnemyContainment(command, 0.9f, p);
            Assert.Greater(containment, 0.6f, "敵艦隊を港に押し込める");

            // 制宙権と航路支配で遠方へ力を投射
            float reach = MaritimeStrategyRules.StrategicReach(command, lane, p);
            Assert.Greater(reach, 0.5f, "戦略的到達範囲が広がる");

            // コストは伴うが、利益と封じ込めの相乗が上回り戦略価値は正
            float cost = MaritimeStrategyRules.SeaPowerCost(0.8f, 0.5f, p);
            float value = MaritimeStrategyRules.MaritimeStrategyValue(benefit, containment, cost, p);
            Assert.Greater(value, 0f, "制宙戦略は割に合う＝海洋国家の覇権");
        }
    }
}
