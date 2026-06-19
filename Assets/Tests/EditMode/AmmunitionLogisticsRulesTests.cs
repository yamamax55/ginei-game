using NUnit.Framework;
using UnityEngine;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 弾薬補給（継戦＝弾切れ）を固定する：消費＝発射レート×(1.0+1.0×交戦強度)、残量比＝残弾/定数、
    /// 継戦時間＝残弾/消費（消費0は無限）、火力維持＝崩壊閾値0.2以上で1.0・未満は残量比/0.2で激減、
    /// 撃ち惜しみ＝閾値0.3以上で1.0・未満は最低手数0.4→1.0へ線形、補給回復＝補給艦数×搬送能力、
    /// 弾切れ＝残弾0以下、残少＝残量比≤閾値。クランプを担保。
    /// </summary>
    public class AmmunitionLogisticsRulesTests
    {
        private static readonly AmmunitionLogisticsParams P = AmmunitionLogisticsParams.Default;
        // 基礎消費1.0/強度重み1.0/火力崩壊閾値0.2/撃ち惜しみ閾値0.3/最低手数0.4

        [Test]
        public void AmmoConsumption_RisesWithRateAndIntensity()
        {
            Assert.AreEqual(3.0f, AmmunitionLogisticsRules.AmmoConsumption(2f, 0.5f, P), 1e-4f); // 2*(1+0.5)
            Assert.AreEqual(1.0f, AmmunitionLogisticsRules.AmmoConsumption(1f, 0f, P), 1e-4f);   // 撃つだけ
            Assert.AreEqual(2.0f, AmmunitionLogisticsRules.AmmoConsumption(1f, 1f, P), 1e-4f);   // 激戦は2倍消費
            Assert.AreEqual(0f, AmmunitionLogisticsRules.AmmoConsumption(0f, 1f, P), 1e-4f);     // 撃たねば減らぬ
            Assert.AreEqual(2.0f, AmmunitionLogisticsRules.AmmoConsumption(1f, 5f, P), 1e-4f);   // 強度クランプ
        }

        [Test]
        public void AmmoState_ReserveOverCapacity()
        {
            Assert.AreEqual(0.5f, AmmunitionLogisticsRules.AmmoState(50f, 100f), 1e-4f);
            Assert.AreEqual(0f, AmmunitionLogisticsRules.AmmoState(0f, 100f), 1e-4f);
            Assert.AreEqual(1f, AmmunitionLogisticsRules.AmmoState(150f, 100f), 1e-4f); // 過積みはクランプ
            Assert.AreEqual(0f, AmmunitionLogisticsRules.AmmoState(50f, 0f), 1e-4f);    // 弾倉なし
        }

        [Test]
        public void SustainableFireTime_ReserveOverConsumption()
        {
            Assert.AreEqual(20f, AmmunitionLogisticsRules.SustainableFireTime(60f, 3f), 1e-4f);
            Assert.AreEqual(0f, AmmunitionLogisticsRules.SustainableFireTime(0f, 3f), 1e-4f);
            Assert.IsTrue(float.IsPositiveInfinity(AmmunitionLogisticsRules.SustainableFireTime(10f, 0f))); // 減らねば無限
        }

        [Test]
        public void FirepowerFromAmmo_SustainedThenCollapses()
        {
            Assert.AreEqual(1f, AmmunitionLogisticsRules.FirepowerFromAmmo(0.5f, P), 1e-4f);  // 潤沢
            Assert.AreEqual(1f, AmmunitionLogisticsRules.FirepowerFromAmmo(0.2f, P), 1e-4f);  // 閾値ちょうど
            Assert.AreEqual(0.5f, AmmunitionLogisticsRules.FirepowerFromAmmo(0.1f, P), 1e-4f); // 0.1/0.2＝激減
            Assert.AreEqual(0f, AmmunitionLogisticsRules.FirepowerFromAmmo(0f, P), 1e-4f);    // 弾切れで撃てぬ
        }

        [Test]
        public void FireDisciplinePenalty_HoldsFireWhenLow()
        {
            Assert.AreEqual(1f, AmmunitionLogisticsRules.FireDisciplinePenalty(0.5f, P), 1e-4f);  // 制限なし
            Assert.AreEqual(1f, AmmunitionLogisticsRules.FireDisciplinePenalty(0.3f, P), 1e-4f);  // 閾値ちょうど
            Assert.AreEqual(0.7f, AmmunitionLogisticsRules.FireDisciplinePenalty(0.15f, P), 1e-4f); // Lerp(0.4,1,0.5)
            Assert.AreEqual(0.4f, AmmunitionLogisticsRules.FireDisciplinePenalty(0f, P), 1e-4f);  // 最大限の撃ち惜しみ
        }

        [Test]
        public void ResupplyRate_ShipsTimesCapacity()
        {
            Assert.AreEqual(30f, AmmunitionLogisticsRules.ResupplyRate(3, 10f), 1e-4f);
            Assert.AreEqual(0f, AmmunitionLogisticsRules.ResupplyRate(-1, 10f), 1e-4f); // 負はクランプ
            Assert.AreEqual(0f, AmmunitionLogisticsRules.ResupplyRate(3, -5f), 1e-4f);  // 負の搬送はクランプ
        }

        [Test]
        public void WinchesterAndLow_Flags()
        {
            Assert.IsTrue(AmmunitionLogisticsRules.WinchesterState(0f));   // 完全な弾切れ
            Assert.IsFalse(AmmunitionLogisticsRules.WinchesterState(5f));
            Assert.IsTrue(AmmunitionLogisticsRules.IsAmmoLow(0.1f, 0.2f)); // 残少
            Assert.IsFalse(AmmunitionLogisticsRules.IsAmmoLow(0.5f, 0.2f));
        }

        [Test]
        public void Narrative_ProlongedBattleRunsDryAndFalters()
        {
            // 激戦が続いて備蓄が底をつき、火力が崩れて撃ち惜しむが、補給艦が来て継戦が戻る物語。
            var p = AmmunitionLogisticsParams.Default;
            float capacity = 1000f;

            // 高レート・激戦＝早く減る
            float consume = AmmunitionLogisticsRules.AmmoConsumption(2f, 1f, p); // 2*(1+1)=4/秒
            Assert.AreEqual(4f, consume, 1e-4f);

            // 残弾100＝あと25秒しか撃てない
            float left = AmmunitionLogisticsRules.SustainableFireTime(100f, consume);
            Assert.AreEqual(25f, left, 1e-4f);

            // 残量比0.1（=100/1000）＝崩壊閾値0.2未満で火力半減・撃ち惜しみも始まる
            float low = AmmunitionLogisticsRules.AmmoState(100f, capacity);
            Assert.AreEqual(0.1f, low, 1e-4f);
            Assert.IsTrue(AmmunitionLogisticsRules.IsAmmoLow(low, 0.2f));
            Assert.AreEqual(0.5f, AmmunitionLogisticsRules.FirepowerFromAmmo(low, p), 1e-4f);
            Assert.Less(AmmunitionLogisticsRules.FireDisciplinePenalty(low, p), 1f); // 手数が落ちる

            // 弾を撃ち尽くす＝Winchester＝もはや撃てない
            Assert.IsTrue(AmmunitionLogisticsRules.WinchesterState(0f));
            Assert.AreEqual(0f, AmmunitionLogisticsRules.FirepowerFromAmmo(AmmunitionLogisticsRules.AmmoState(0f, capacity), p), 1e-4f);

            // 補給艦2隻×搬送150が到着＝300回復＝残量比が崩壊閾値を超えて火力が戻る
            float resupply = AmmunitionLogisticsRules.ResupplyRate(2, 150f);
            Assert.AreEqual(300f, resupply, 1e-4f);
            float restored = AmmunitionLogisticsRules.AmmoState(resupply, capacity); // 0.3
            Assert.AreEqual(0.3f, restored, 1e-4f);
            Assert.AreEqual(1f, AmmunitionLogisticsRules.FirepowerFromAmmo(restored, p), 1e-4f); // 火力復活
        }
    }
}
