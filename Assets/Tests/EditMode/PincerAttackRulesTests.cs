using NUnit.Framework;
using UnityEngine;

namespace Ginei.Tests
{
    /// <summary>挟撃（両翼同期攻撃）純ロジックのテスト。既定 Params で期待値固定。</summary>
    public class PincerAttackRulesTests
    {
        const float Eps = 1e-4f;

        [Test]
        public void Coordination_SyncedArms_IsFull_MistimedArms_IsZero()
        {
            // 同時到達＝完全同期、真逆のタイミング＝同期ゼロ。
            Assert.AreEqual(1f, PincerAttackRules.PincerCoordination(0.5f, 0.5f), Eps);
            Assert.AreEqual(0f, PincerAttackRules.PincerCoordination(0f, 1f), Eps);
            // 許容幅(0.15)以内のずれは完全同期扱い。
            Assert.AreEqual(1f, PincerAttackRules.PincerCoordination(0.2f, 0.3f), Eps);
            // gap=0.575 → over=0.425 / span=0.85 = 0.5 → 1-0.5=0.5。
            Assert.AreEqual(0.5f, PincerAttackRules.PincerCoordination(0f, 0.575f), Eps);
        }

        [Test]
        public void CrossfireBonus_ScalesWithCoordinationAndEncirclement()
        {
            // フル同期×全周包囲＝最大ボーナス crossfireScale=0.5。
            Assert.AreEqual(0.5f, PincerAttackRules.CrossfireBonus(1f, 1f), Eps);
            // 0.5×0.8=0.4 → ×0.5 = 0.2。
            Assert.AreEqual(0.2f, PincerAttackRules.CrossfireBonus(0.5f, 0.8f), Eps);
            // 同期ゼロならボーナス無し。
            Assert.AreEqual(0f, PincerAttackRules.CrossfireBonus(0f, 1f), Eps);
        }

        [Test]
        public void FirepowerSplit_LargerEnemyFirepower_SplitsMore()
        {
            // ef/(ef+1)×splitScale(0.6)。火力1 → 0.5×0.6=0.3、火力3 → 0.75×0.6=0.45。
            Assert.AreEqual(0.3f, PincerAttackRules.FirepowerSplit(1f), Eps);
            Assert.AreEqual(0.45f, PincerAttackRules.FirepowerSplit(3f), Eps);
            // 火力ゼロは分散ゼロ。
            Assert.AreEqual(0f, PincerAttackRules.FirepowerSplit(0f), Eps);
            // 大火力ほど分散は強いが上限 splitScale を超えない。
            Assert.Less(PincerAttackRules.FirepowerSplit(1000f), 0.6f + Eps);
        }

        [Test]
        public void RetreatDenial_ScalesWithCoordination()
        {
            // 同期度×denialScale(0.7)。
            Assert.AreEqual(0.7f, PincerAttackRules.RetreatDenial(1f), Eps);
            Assert.AreEqual(0.35f, PincerAttackRules.RetreatDenial(0.5f), Eps);
            Assert.AreEqual(0f, PincerAttackRules.RetreatDenial(0f), Eps);
        }

        [Test]
        public void MistimingPenalty_ZeroWithinTolerance_GrowsWithGap()
        {
            // 許容内(0.1<=0.15)はペナルティ無し。
            Assert.AreEqual(0f, PincerAttackRules.MistimingPenalty(0.1f), Eps);
            // gap=0.575 → 0.425/0.85=0.5。
            Assert.AreEqual(0.5f, PincerAttackRules.MistimingPenalty(0.575f), Eps);
            // 最大ずれで mistimingScale=1.0 上限。
            Assert.AreEqual(1f, PincerAttackRules.MistimingPenalty(1f), Eps);
        }

        [Test]
        public void ArmIsolationRisk_WeakArm_AndGap_IsHigh_EvenWhenInTime_IsZero()
        {
            // 互角(50:50)はリスクほぼ無し。
            Assert.AreEqual(0f, PincerAttackRules.ArmIsolationRisk(50f, 50f, 1f), Eps);
            // 劣勢(30:90)＝enemyShare0.75→disadvantage0.5、gap=1 → 0.5×1×0.6=0.3。
            Assert.AreEqual(0.3f, PincerAttackRules.ArmIsolationRisk(30f, 90f, 1f), Eps);
            // 劣勢でも同期(gap=0)なら孤立しない＝リスク0。
            Assert.AreEqual(0f, PincerAttackRules.ArmIsolationRisk(30f, 90f, 0f), Eps);
        }

        [Test]
        public void NetValue_PositiveWhenCrossfireWins_NegativeWhenIsolationWins()
        {
            // 利が害を上回れば正。
            Assert.AreEqual(0.5f, PincerAttackRules.PincerNetValue(0.5f, 0f), Eps);
            // 孤立リスクが勝てば負（仕掛けるべきでない）。
            Assert.AreEqual(-0.2f, PincerAttackRules.PincerNetValue(0.1f, 0.3f), Eps);
        }

        [Test]
        public void IsPincerClosed_AtOrAboveThreshold()
        {
            // 既定しきい値0.6：ちょうどで成立、未満で不成立。
            Assert.IsTrue(PincerAttackRules.IsPincerClosed(0.6f));
            Assert.IsFalse(PincerAttackRules.IsPincerClosed(0.5f));
        }

        [Test]
        public void Narrative_SyncedPincerCrushes_MistimedArmGetsDefeatedInDetail()
        {
            // 物語：両翼が同期して挟撃すれば、敵は十字砲火を浴び退路を断たれて崩れる。
            float synced = PincerAttackRules.PincerCoordination(0.5f, 0.55f); // ほぼ同時着
            Assert.IsTrue(PincerAttackRules.IsPincerClosed(synced), "同期した両翼は挟撃成立");
            float crossfire = PincerAttackRules.CrossfireBonus(synced, 0.9f); // 二方向から撃てている
            float denial = PincerAttackRules.RetreatDenial(synced);
            float syncedRisk = PincerAttackRules.ArmIsolationRisk(60f, 60f, 0.05f); // 互角・ほぼ同期
            Assert.Greater(crossfire, 0f, "十字砲火ボーナスが出る");
            Assert.Greater(denial, 0.5f, "退路を断てている");
            Assert.Greater(PincerAttackRules.PincerNetValue(crossfire, syncedRisk), 0f, "同期挟撃は正味で有利");

            // ところが片翼が遅れると挟撃は成立せず、孤立した先着の片翼が各個撃破される。
            float mistimed = PincerAttackRules.PincerCoordination(0.1f, 0.9f); // 大きく遅着
            Assert.IsFalse(PincerAttackRules.IsPincerClosed(mistimed), "ずれた両翼は挟撃不成立");
            float lateGap = Mathf.Abs(0.1f - 0.9f);
            float isolation = PincerAttackRules.ArmIsolationRisk(30f, 90f, lateGap); // 劣勢の片翼が単独で晒される
            float thinCrossfire = PincerAttackRules.CrossfireBonus(mistimed, 0.9f);
            Assert.Greater(isolation, 0f, "孤立した片翼に各個撃破リスク");
            Assert.Less(PincerAttackRules.PincerNetValue(thinCrossfire, isolation), 0f, "ずれた挟撃は正味で不利＝仕掛けるべきでない");
        }
    }
}
