using NUnit.Framework;
using UnityEngine;

namespace Ginei.Tests
{
    /// <summary>艦の推進系（核融合スラスター）の運動性能の純ロジックテスト。既定Paramsの具体値で期待値を固定。</summary>
    public class PropulsionRulesTests
    {
        private const float Eps = 1e-4f;

        /// <summary>推力重量比＝推力/質量（質量は床止め）。</summary>
        [Test]
        public void ThrustToWeight_推力を質量で割る()
        {
            // 1000/500 = 2.0
            Assert.AreEqual(2f, PropulsionRules.ThrustToWeight(1000f, 500f), Eps);

            // 質量0は床(1)で割る＝0除算しない
            Assert.AreEqual(1000f, PropulsionRules.ThrustToWeight(1000f, 0f), Eps);

            // 負の推力は0
            Assert.AreEqual(0f, PropulsionRules.ThrustToWeight(-50f, 500f), Eps);
        }

        /// <summary>加速度は推力重量比に比例（accelScale=1.0）。</summary>
        [Test]
        public void Acceleration_推力重量比に比例()
        {
            // t2w=2.0, accelScale=1.0 → 2.0
            Assert.AreEqual(2f, PropulsionRules.Acceleration(2f), Eps);
            Assert.AreEqual(0f, PropulsionRules.Acceleration(0f), Eps);
        }

        /// <summary>最大速度＝(t2w×10)/(1+抵抗)。抵抗が高いほど頭打ちが低い。</summary>
        [Test]
        public void MaxVelocity_抵抗が高いほど遅い()
        {
            // t2w=2.0, drag=1 → 2*10/(1+1) = 10.0
            Assert.AreEqual(10f, PropulsionRules.MaxVelocity(2f, 1f), Eps);

            // 抵抗0なら基準速度フル＝2*10/1 = 20.0
            Assert.AreEqual(20f, PropulsionRules.MaxVelocity(2f, 0f), Eps);

            // 抵抗増で単調減
            Assert.Less(PropulsionRules.MaxVelocity(2f, 3f), PropulsionRules.MaxVelocity(2f, 1f));
        }

        /// <summary>旋回性能＝(t2w×2)/(1+質量)。大型ほど鈍重。</summary>
        [Test]
        public void TurnRate_大型艦は鈍い()
        {
            // t2w=2, mass=3 → 2*2/(1+3) = 4/4 = 1.0
            Assert.AreEqual(1f, PropulsionRules.TurnRate(2f, 3f), Eps);

            // 同じ機関でも大質量(9)は鈍い → 4/(1+9) = 0.4
            float heavy = PropulsionRules.TurnRate(2f, 9f);
            Assert.AreEqual(0.4f, heavy, Eps);
            Assert.Less(heavy, PropulsionRules.TurnRate(2f, 3f));
        }

        /// <summary>燃料消費率＝推力×スロットル×0.5。全開ほど食う。</summary>
        [Test]
        public void FuelConsumption_全開ほど食う()
        {
            // 1000 * 0.5 * fuelScale(0.5) = 250
            Assert.AreEqual(250f, PropulsionRules.FuelConsumption(1000f, 0.5f), Eps);

            // スロットル0は消費0、スロットルは0..1にclamp
            Assert.AreEqual(0f, PropulsionRules.FuelConsumption(1000f, 0f), Eps);
            Assert.AreEqual(500f, PropulsionRules.FuelConsumption(1000f, 2f), Eps); // throttle clamp→1.0
        }

        /// <summary>巡航効率＝最適スロットルで1.0、外すほど悪化し床(0.2)で下支え。</summary>
        [Test]
        public void CruiseEfficiency_最適点で最良外すと悪化()
        {
            // 最適点(0.5,0.5) → 1.0
            Assert.AreEqual(1f, PropulsionRules.CruiseEfficiency(0.5f, 0.5f), Eps);

            // 0.9 vs 最適0.5: 1 - 0.4*slope(1.0) = 0.6
            Assert.AreEqual(0.6f, PropulsionRules.CruiseEfficiency(0.9f, 0.5f), Eps);

            // 大きく外す(0.0 vs 1.0): 1-1 = 0 → 床0.2で下支え
            Assert.AreEqual(0.2f, PropulsionRules.CruiseEfficiency(0f, 1f), Eps);
        }

        /// <summary>戦闘加速の応答＝t2w×機関健全性、上限(3.0)で頭打ち。被弾で鈍る。</summary>
        [Test]
        public void CombatThrustResponse_機関の傷みで鈍り上限で頭打ち()
        {
            // t2w=2, health=1 → 2.0
            Assert.AreEqual(2f, PropulsionRules.CombatThrustResponse(2f, 1f), Eps);

            // 機関半損(health=0.5) → 1.0
            Assert.AreEqual(1f, PropulsionRules.CombatThrustResponse(2f, 0.5f), Eps);

            // 高出力(t2w=5)でも上限3.0で頭打ち
            Assert.AreEqual(3f, PropulsionRules.CombatThrustResponse(5f, 1f), Eps);
        }

        /// <summary>機敏判定＝旋回率が閾値以上。</summary>
        [Test]
        public void IsAgile_閾値以上で機敏()
        {
            Assert.IsTrue(PropulsionRules.IsAgile(1f));   // 既定閾値1.0ちょうど＝機敏
            Assert.IsFalse(PropulsionRules.IsAgile(0.4f)); // 鈍い大型艦
            Assert.IsTrue(PropulsionRules.IsAgile(2f, 1.5f));
        }

        /// <summary>
        /// 物語＝高速駆逐艦と巨大戦艦の対比。
        /// 軽量高出力の駆逐艦は推力重量比が高く、加速・最大速度・旋回すべてで巨艦を上回り「機敏」、
        /// 巨大戦艦は質量で旋回が鈍重になり機敏でない。被弾で機関が傷むと駆逐艦の戦闘応答も鈍る。
        /// </summary>
        [Test]
        public void 物語_俊敏な駆逐艦と鈍重な戦艦()
        {
            // 駆逐艦: 推力800, 質量200 → t2w = 4.0
            float destT2w = PropulsionRules.ThrustToWeight(800f, 200f);
            Assert.AreEqual(4f, destT2w, Eps);

            // 戦艦: 推力2000, 質量2000 → t2w = 1.0
            float battleT2w = PropulsionRules.ThrustToWeight(2000f, 2000f);
            Assert.AreEqual(1f, battleT2w, Eps);

            // 駆逐艦の方が加速・最大速度ともに上
            Assert.Greater(PropulsionRules.Acceleration(destT2w), PropulsionRules.Acceleration(battleT2w));
            Assert.Greater(PropulsionRules.MaxVelocity(destT2w, 1f), PropulsionRules.MaxVelocity(battleT2w, 1f));

            // 旋回: 駆逐艦(質量2) 4*2/(1+2)=8/3≈2.667 は機敏／戦艦(質量20) 1*2/(1+20)=2/21≈0.095 は鈍重
            float destTurn = PropulsionRules.TurnRate(destT2w, 2f);
            float battleTurn = PropulsionRules.TurnRate(battleT2w, 20f);
            Assert.AreEqual(8f / 3f, destTurn, 1e-3f);
            Assert.AreEqual(2f / 21f, battleTurn, 1e-3f);
            Assert.IsTrue(PropulsionRules.IsAgile(destTurn));
            Assert.IsFalse(PropulsionRules.IsAgile(battleTurn));

            // 機関が傷むと駆逐艦の戦闘応答も落ちる（健全1.0→0.3）
            float healthy = PropulsionRules.CombatThrustResponse(destT2w, 1f);
            float damaged = PropulsionRules.CombatThrustResponse(destT2w, 0.3f);
            Assert.Less(damaged, healthy);
        }
    }
}
