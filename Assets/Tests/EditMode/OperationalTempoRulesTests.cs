using NUnit.Framework;
using UnityEngine;

namespace Ginei.Tests
{
    /// <summary>
    /// 作戦テンポ／OODAループ（<see cref="OperationalTempoRules"/>）の純ロジックテスト。既定
    /// <see cref="OperationalTempoParams.Default"/> で期待値を固定（基準サイクル2.0・複雑さ寄与1.0・効率下限0.1・
    /// 負担指数2.0・テンポ余裕1.2）。Pow/除算箇所のみ許容差を緩める。
    /// </summary>
    public class OperationalTempoRulesTests
    {
        const float Eps = 1e-4f;
        const float PowEps = 1e-3f;

        [Test]
        public void DecisionCycleTime_ScalesWithComplexityOverEfficiency()
        {
            // 2*(1+1*0.5)/1 = 3.0
            Assert.AreEqual(3.0f, OperationalTempoRules.DecisionCycleTime(1f, 0.5f), Eps);
        }

        [Test]
        public void DecisionCycleTime_ClampsEfficiencyToFloor()
        {
            // eff=max(0.1,0)=0.1 → 2*(1+0)/0.1 = 20.0（発散しない）
            Assert.AreEqual(20.0f, OperationalTempoRules.DecisionCycleTime(0f, 0f), Eps);
        }

        [Test]
        public void TempoAdvantage_PositiveWhenOwnCycleShorter()
        {
            // (4-2)/(4+2) = 0.3333…（自分が速い＝正）
            Assert.AreEqual(0.33333f, OperationalTempoRules.TempoAdvantage(2f, 4f), Eps);
            // 拮抗は0
            Assert.AreEqual(0f, OperationalTempoRules.TempoAdvantage(2f, 2f), Eps);
        }

        [Test]
        public void InitiativeSeizure_MapsAdvantageToZeroOne()
        {
            // (0.5+1)/2 = 0.75
            Assert.AreEqual(0.75f, OperationalTempoRules.InitiativeSeizure(0.5f), Eps);
            // 完全劣位は0
            Assert.AreEqual(0f, OperationalTempoRules.InitiativeSeizure(-1f), Eps);
        }

        [Test]
        public void EnemyDisorientation_ZeroWhenNotAhead()
        {
            // 優位0.5・適応力0.4 → 0.5*0.6 = 0.3
            Assert.AreEqual(0.3f, OperationalTempoRules.EnemyDisorientation(0.5f, 0.4f), Eps);
            // 劣位（adv<0）では敵は混乱しない
            Assert.AreEqual(0f, OperationalTempoRules.EnemyDisorientation(-0.5f, 0.4f), Eps);
        }

        [Test]
        public void SustainmentStrain_GrowsWithTempoOverSupply()
        {
            // pow(2/2,2)=1.0（補給能力と等速＝持続限界）
            Assert.AreEqual(1.0f, OperationalTempoRules.SustainmentStrain(2f, 2f), PowEps);
            // pow(1/2,2)=0.25（余裕あり）
            Assert.AreEqual(0.25f, OperationalTempoRules.SustainmentStrain(1f, 2f), PowEps);
        }

        [Test]
        public void OptimalTempo_BeatsEnemyButCappedBySupply()
        {
            // min(3, 2*1.2)=2.4（補給が足りれば敵より速く）
            Assert.AreEqual(2.4f, OperationalTempoRules.OptimalTempo(3f, 2f), Eps);
            // min(2, 2.4)=2.0（補給で頭打ち）
            Assert.AreEqual(2.0f, OperationalTempoRules.OptimalTempo(2f, 2f), Eps);
        }

        [Test]
        public void MomentumFromTempo_BuildsWithConsecutiveActions()
        {
            // 0.75 * (3/4) = 0.5625
            Assert.AreEqual(0.5625f, OperationalTempoRules.MomentumFromTempo(0.75f, 3), Eps);
            // 1手目は勢いゼロ
            Assert.AreEqual(0f, OperationalTempoRules.MomentumFromTempo(0.75f, 0), Eps);
        }

        [Test]
        public void IsOutpacingEnemy_TrueAboveThreshold()
        {
            Assert.IsTrue(OperationalTempoRules.IsOutpacingEnemy(0.3f));
            Assert.IsFalse(OperationalTempoRules.IsOutpacingEnemy(0.05f));
        }

        [Test]
        public void Narrative_FastOodaSeizesInitiativeButStrainsSupply()
        {
            // 速いOODA（自1.5サイクル vs 敵3サイクル）＝敵を後手に回し主導権を握る
            float adv = OperationalTempoRules.TempoAdvantage(1.5f, 3f);
            Assert.Greater(adv, 0f);
            Assert.IsTrue(OperationalTempoRules.IsOutpacingEnemy(adv));
            Assert.Greater(OperationalTempoRules.InitiativeSeizure(adv), 0.5f);
            Assert.Greater(OperationalTempoRules.EnemyDisorientation(adv, 0.3f), 0f);

            // だがテンポ4で補給能力2しかない＝速すぎて補給が追いつかない（負担が限界1.0）
            float strain = OperationalTempoRules.SustainmentStrain(4f, 2f);
            Assert.AreEqual(1.0f, strain, PowEps);

            // 持続可能な最適テンポは敵テンポ×余裕を狙いつつ補給で頭打ち（無理押ししない）
            float optimal = OperationalTempoRules.OptimalTempo(2f, 1f);
            Assert.AreEqual(1.2f, optimal, Eps);   // min(2, 1*1.2)=1.2
            Assert.Less(OperationalTempoRules.SustainmentStrain(optimal, 2f), 1.0f); // 持続可能
        }
    }
}
