using NUnit.Framework;
using UnityEngine;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// ビーム兵器（荷電粒子砲）：出力・距離減衰・チャージ/連射・過熱/冷却・出力制限・着弾エネルギー。
    /// 既定パラメータの期待値を手計算で固定。
    /// </summary>
    public class BeamWeaponRulesTests
    {
        [Test]
        public void BeamPower_OutputTimesFocus_ClampedAtMax()
        {
            // 1000×0.8 = 800
            Assert.AreEqual(800f, BeamWeaponRules.BeamPower(1000f, 0.8f), 1e-4f);
            // 収束>1.0 は上限1.0でクランプ → 1000×1.0 = 1000
            Assert.AreEqual(1000f, BeamWeaponRules.BeamPower(1000f, 1.5f), 1e-4f);
            // 負の炉出力は0クランプ
            Assert.AreEqual(0f, BeamWeaponRules.BeamPower(-500f, 0.8f), 1e-4f);
        }

        [Test]
        public void RangeAttenuation_OneOverOnePlusKx()
        {
            // 距離0＝減衰なし
            Assert.AreEqual(800f, BeamWeaponRules.RangeAttenuation(800f, 0f), 1e-4f);
            // 800/(1+0.01×100) = 800/2 = 400
            Assert.AreEqual(400f, BeamWeaponRules.RangeAttenuation(800f, 100f), 1e-4f);
            // 800/(1+0.01×300) = 800/4 = 200
            Assert.AreEqual(200f, BeamWeaponRules.RangeAttenuation(800f, 300f), 1e-4f);
            // 負距離は0扱い＝減衰なし
            Assert.AreEqual(800f, BeamWeaponRules.RangeAttenuation(800f, -50f), 1e-4f);
        }

        [Test]
        public void ChargeTime_PowerOverCapacity_FloorAtMin()
        {
            // max(0.1, 0.5×800/400) = max(0.1, 1.0) = 1.0
            Assert.AreEqual(1.0f, BeamWeaponRules.ChargeTime(800f, 400f), 1e-4f);
            // 低出力/大容量は下限0.1へ：0.5×100/10000=0.005 → 0.1
            Assert.AreEqual(0.1f, BeamWeaponRules.ChargeTime(100f, 10000f), 1e-4f);
        }

        [Test]
        public void RateOfFire_InverseOfChargeTime_HighOutputFiresSlower()
        {
            // 1/1.0 = 1.0（高出力＝長チャージ＝低連射）
            Assert.AreEqual(1.0f, BeamWeaponRules.RateOfFire(1.0f), 1e-4f);
            // チャージ0.05は下限0.1へ → 1/0.1 = 10（連射上限）
            Assert.AreEqual(10f, BeamWeaponRules.RateOfFire(0.05f), 1e-4f);
        }

        [Test]
        public void HeatBuildup_PowerTimesFire_CappedPerStep()
        {
            // 0.5×0.5×0.5 = 0.125
            Assert.AreEqual(0.125f, BeamWeaponRules.HeatBuildup(0.5f, 0.5f), 1e-4f);
            // 0.5×1.0×2.0 = 1.0 だが1ステップ上限0.5でクランプ
            Assert.AreEqual(0.5f, BeamWeaponRules.HeatBuildup(1.0f, 2.0f), 1e-4f);
        }

        [Test]
        public void CoolingRecovery_RemainingHeatAfterCooling()
        {
            // 0.8×(1-clamp01(0.5×0.5)) = 0.8×0.75 = 0.6
            Assert.AreEqual(0.6f, BeamWeaponRules.CoolingRecovery(0.8f, 0.5f), 1e-4f);
            // 冷却能力2.0 → 冷却率clamp01(1.0)=1.0 → 残存0
            Assert.AreEqual(0f, BeamWeaponRules.CoolingRecovery(0.8f, 2.0f), 1e-4f);
        }

        [Test]
        public void ThermalThrottle_ZeroBelowThreshold_RampsAbove()
        {
            // 閾値0.7未満は無制限0
            Assert.AreEqual(0f, BeamWeaponRules.ThermalThrottle(0.5f), 1e-4f);
            // (0.85-0.7)/(1-0.7) × 1.0 = 0.15/0.3 = 0.5
            Assert.AreEqual(0.5f, BeamWeaponRules.ThermalThrottle(0.85f), 1e-4f);
            // 過熱最大＝最大制限1.0
            Assert.AreEqual(1.0f, BeamWeaponRules.ThermalThrottle(1.0f), 1e-4f);
        }

        [Test]
        public void DeliveredEnergy_ReachTimesOneMinusThrottle()
        {
            // 到達率 400/800=0.5、出力制限なし → 0.5
            Assert.AreEqual(0.5f, BeamWeaponRules.DeliveredEnergy(800f, 400f, 0f), 1e-4f);
            // 0.5×(1-0.5) = 0.25
            Assert.AreEqual(0.25f, BeamWeaponRules.DeliveredEnergy(800f, 400f, 0.5f), 1e-4f);
            // 出力0なら0
            Assert.AreEqual(0f, BeamWeaponRules.DeliveredEnergy(0f, 0f, 0f), 1e-4f);
        }

        /// <summary>
        /// 物語：イゼルローン要塞主砲（トゥール・ハンマー）の一射。
        /// 大出力炉を高収束で束ね、近距離なら着弾エネルギーはほぼ全開。
        /// 撃ち続ければ過熱が閾値を超えて出力制限が掛かり、着弾エネルギーが落ちる。
        /// </summary>
        [Test]
        public void Narrative_FortressMainGun_OverheatsAndThrottlesDown()
        {
            var p = BeamWeaponParams.Default;

            // 大出力炉（10000）を収束0.9で束ねる → 9000
            float power = BeamWeaponRules.BeamPower(10000f, 0.9f, p);
            Assert.AreEqual(9000f, power, 1e-4f);

            // 近距離（距離0）＝減衰なし、初弾は過熱なし＝制限なし → 着弾エネルギーは最大1.0
            float att0 = BeamWeaponRules.RangeAttenuation(power, 0f, p);
            float coldShot = BeamWeaponRules.DeliveredEnergy(power, att0, 0f, p);
            Assert.AreEqual(1.0f, coldShot, 1e-4f);

            // 高出力ゆえチャージは長く連射は遅い（蓄電容量9000）
            float charge = BeamWeaponRules.ChargeTime(power, 9000f, p); // 0.5×9000/9000=0.5
            Assert.AreEqual(0.5f, charge, 1e-4f);
            Assert.AreEqual(2.0f, BeamWeaponRules.RateOfFire(charge, p), 1e-4f); // 1/0.5

            // 連続射撃で過熱が溜まり閾値0.7を超える（蓄積0.5を冷却が追いつかず累積）
            float heat = Mathf.Clamp01(0.5f + 0.4f); // 0.9（説明用の累積過熱）
            float throttle = BeamWeaponRules.ThermalThrottle(heat, p); // (0.9-0.7)/0.3 = 0.6667
            Assert.Greater(throttle, 0f);

            // 過熱状態の同じ一射は制限で着弾エネルギーが下がる
            float hotShot = BeamWeaponRules.DeliveredEnergy(power, att0, throttle, p);
            Assert.Less(hotShot, coldShot);
            Assert.AreEqual(1f - throttle, hotShot, 1e-3f);

            // 冷却が回れば残存熱が下がる＝制限が緩む
            float cooled = BeamWeaponRules.CoolingRecovery(heat, 1.0f, p); // 0.9×(1-0.5)=0.45
            Assert.AreEqual(0.45f, cooled, 1e-4f);
            Assert.AreEqual(0f, BeamWeaponRules.ThermalThrottle(cooled, p), 1e-4f); // 閾値0.7未満＝制限解除
        }
    }
}
