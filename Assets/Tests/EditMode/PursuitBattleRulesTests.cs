using NUnit.Framework;
using UnityEngine;

namespace Ginei.Tests
{
    /// <summary>
    /// 追撃戦の損害収支（<see cref="PursuitBattleRules"/>）の EditMode テスト。
    /// 既定 Params で期待値を固定。Pow を通す箇所のみ許容誤差を緩める。
    /// </summary>
    public class PursuitBattleRulesTests
    {
        const float Eps = 1e-4f;
        const float PowEps = 1e-3f; // Pow を経由する損害計算用

        [Test]
        public void DisorderOnRout_WeightsMoraleHeavier()
        {
            // 0.6*1 + 0.4*0 = 0.6
            Assert.AreEqual(0.6f, PursuitBattleRules.DisorderOnRout(1f, 0f), Eps);
            // 0.6*0 + 0.4*1 = 0.4
            Assert.AreEqual(0.4f, PursuitBattleRules.DisorderOnRout(0f, 1f), Eps);
            // 0.6*0.5 + 0.4*0.5 = 0.5
            Assert.AreEqual(0.5f, PursuitBattleRules.DisorderOnRout(0.5f, 0.5f), Eps);
        }

        [Test]
        public void PursuitDamage_ScalesWithStrengthAndDisorder()
        {
            // 100 * pow(1,0.5) * 1 = 100
            Assert.AreEqual(100f, PursuitBattleRules.PursuitDamage(100f, 1f), PowEps);
            // 100 * pow(0.25,0.5) * 1 = 100 * 0.5 = 50
            Assert.AreEqual(50f, PursuitBattleRules.PursuitDamage(100f, 0.25f), PowEps);
            // 混乱0なら損害0
            Assert.AreEqual(0f, PursuitBattleRules.PursuitDamage(100f, 0f), PowEps);
        }

        [Test]
        public void EscapeFraction_EqualSpeedIsHalf()
        {
            Assert.AreEqual(0.5f, PursuitBattleRules.EscapeFraction(10f, 10f), Eps);
            // 速い敗走兵は多く逃げる: 30/(30+10)=0.75
            Assert.AreEqual(0.75f, PursuitBattleRules.EscapeFraction(30f, 10f), Eps);
            // 追撃側停止＝全逃走
            Assert.AreEqual(1f, PursuitBattleRules.EscapeFraction(10f, 0f), Eps);
        }

        [Test]
        public void OverpursuitRisk_RisesWithDepthThenCaps()
        {
            // 5 * 0.1 = 0.5
            Assert.AreEqual(0.5f, PursuitBattleRules.OverpursuitRisk(5f), Eps);
            // 20 * 0.1 = 2.0 → 上限0.9
            Assert.AreEqual(0.9f, PursuitBattleRules.OverpursuitRisk(20f), Eps);
        }

        [Test]
        public void CounterAmbushChance_NeedsBothRiskAndReserve()
        {
            // 0.5 * 1 = 0.5
            Assert.AreEqual(0.5f, PursuitBattleRules.CounterAmbushChance(0.5f, 1f), Eps);
            // 予備がなければ反撃成立せず
            Assert.AreEqual(0f, PursuitBattleRules.CounterAmbushChance(0.9f, 0f), Eps);
        }

        [Test]
        public void PursuitGain_DiscountedByRisk()
        {
            // 100 * (1 - 0.5) = 50
            Assert.AreEqual(50f, PursuitBattleRules.PursuitGain(100f, 0.5f), Eps);
            // 100 * (1 - 0.9) = 10
            Assert.AreEqual(10f, PursuitBattleRules.PursuitGain(100f, 0.9f), Eps);
        }

        [Test]
        public void CohesionLossFromFlight_ReducedByLeadership()
        {
            // 10 * 0.05 * (1 - 0/200=1) = 0.5
            Assert.AreEqual(0.5f, PursuitBattleRules.CohesionLossFromFlight(10f, 0f), Eps);
            // 10 * 0.05 * (1 - 100/200=0.5) = 0.25
            Assert.AreEqual(0.25f, PursuitBattleRules.CohesionLossFromFlight(10f, 100f), Eps);
            // 統率50: 10*0.05*(1-0.25=0.75) = 0.375
            Assert.AreEqual(0.375f, PursuitBattleRules.CohesionLossFromFlight(10f, 50f), Eps);
        }

        [Test]
        public void IsRoutPursuable_AboveThreshold()
        {
            // 既定閾値0.5
            Assert.IsTrue(PursuitBattleRules.IsRoutPursuable(0.6f));
            Assert.IsFalse(PursuitBattleRules.IsRoutPursuable(0.4f));
            Assert.IsFalse(PursuitBattleRules.IsRoutPursuable(0.5f)); // 閾値ちょうどは不可（> 判定）
        }

        [Test]
        public void Narrative_DisorderedRoutIsCrushed_ButDeepPursuitInvitesCounter()
        {
            // 物語: 士気崩壊し陣形も崩れた敗走兵は混乱が深い
            float disorder = PursuitBattleRules.DisorderOnRout(moraleCollapse: 0.9f, formationIntegrityLoss: 0.8f);
            // 0.6*0.9 + 0.4*0.8 = 0.54 + 0.32 = 0.86
            Assert.AreEqual(0.86f, disorder, Eps);
            Assert.IsTrue(PursuitBattleRules.IsRoutPursuable(disorder), "混乱が深い敗走は追撃可能");

            // 浅い追撃なら大損害をそのまま取れる
            float damage = PursuitBattleRules.PursuitDamage(200f, disorder);
            float shallowRisk = PursuitBattleRules.OverpursuitRisk(2f);    // 2*0.1=0.2
            float shallowGain = PursuitBattleRules.PursuitGain(damage, shallowRisk);

            // 深追いすると伸びきって反撃リスクが上がり、正味利得が削られる
            float deepRisk = PursuitBattleRules.OverpursuitRisk(8f);       // 8*0.1=0.8
            float deepGain = PursuitBattleRules.PursuitGain(damage, deepRisk);

            Assert.Less(deepRisk, 0.9f + Eps);
            Assert.Greater(deepRisk, shallowRisk, "深追いほど反撃リスクが高い");
            Assert.Greater(shallowGain, deepGain, "深追いは正味利得が割り引かれて損");

            // 敵に予備があれば深追いの反撃が成立する
            float ambush = PursuitBattleRules.CounterAmbushChance(deepRisk, enemyReserve: 0.5f);
            // 0.8 * 0.5 = 0.4
            Assert.AreEqual(0.4f, ambush, Eps);
            Assert.Greater(ambush, 0f, "予備があれば伏兵反撃が成立しうる");
        }
    }
}
