using NUnit.Framework;
using UnityEngine;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 燃料兵站：燃料消費率（速度/規模）・行動半径・安全行動圏・燃料残量比・節約モード・補給艦延伸・立ち往生・燃料危機。
    /// 既定 Params で期待値固定（1e-4f）。
    /// </summary>
    public class FuelLogisticsRulesTests
    {
        [Test]
        public void FuelConsumption_RisesWithSpeedAndFleetSize()
        {
            // 1.0×(1+2×0.5)×(1+10×0.1) = 1.0×2.0×2.0
            Assert.AreEqual(4.0f, FuelLogisticsRules.FuelConsumption(2f, 10f), 1e-4f);
            // 速度0・規模0は基本消費そのもの
            Assert.AreEqual(1.0f, FuelLogisticsRules.FuelConsumption(0f, 0f), 1e-4f);
            // 負入力は0扱い
            Assert.AreEqual(1.0f, FuelLogisticsRules.FuelConsumption(-5f, -5f), 1e-4f);
        }

        [Test]
        public void OperatingRadius_IsReserveOverConsumption()
        {
            Assert.AreEqual(25f, FuelLogisticsRules.OperatingRadius(100f, 4f), 1e-4f);
            // 消費0はゼロ割回避（巨大半径＝事実上無制限）
            Assert.Greater(FuelLogisticsRules.OperatingRadius(100f, 0f), 1000f);
        }

        [Test]
        public void RangeFromBase_ReservesReturnMargin()
        {
            // 25×(1−0.4)
            Assert.AreEqual(15f, FuelLogisticsRules.RangeFromBase(25f, 0.4f), 1e-4f);
            // マージン0は全行動圏が使える
            Assert.AreEqual(25f, FuelLogisticsRules.RangeFromBase(25f, 0f), 1e-4f);
        }

        [Test]
        public void FuelState_IsReserveOverCapacity()
        {
            Assert.AreEqual(0.3f, FuelLogisticsRules.FuelState(30f, 100f), 1e-4f);
            // 容量0は満タン扱い
            Assert.AreEqual(1.0f, FuelLogisticsRules.FuelState(50f, 0f), 1e-4f);
            // 過積載はクランプ
            Assert.AreEqual(1.0f, FuelLogisticsRules.FuelState(200f, 100f), 1e-4f);
        }

        [Test]
        public void LowFuelPenalty_TightensAsFuelDrops()
        {
            // 閾値0.3以上は無抑制
            Assert.AreEqual(1.0f, FuelLogisticsRules.LowFuelPenalty(0.5f), 1e-4f);
            Assert.AreEqual(1.0f, FuelLogisticsRules.LowFuelPenalty(0.3f), 1e-4f);
            // 閾値の半分（0.15）→ Lerp(0.5,1,0.5)
            Assert.AreEqual(0.75f, FuelLogisticsRules.LowFuelPenalty(0.15f), 1e-4f);
            // 残量0は下限0.5
            Assert.AreEqual(0.5f, FuelLogisticsRules.LowFuelPenalty(0f), 1e-4f);
        }

        [Test]
        public void TankerSupport_ExtendsRangeUpToCap()
        {
            // 需要の1.0倍ぶんで上限（延伸2.0倍）に到達
            Assert.AreEqual(2.0f, FuelLogisticsRules.TankerSupport(150f, 100f), 1e-4f);
            // 需要の半分なら1.5倍
            Assert.AreEqual(1.5f, FuelLogisticsRules.TankerSupport(50f, 100f), 1e-4f);
            // 需要0は延伸なし
            Assert.AreEqual(1.0f, FuelLogisticsRules.TankerSupport(100f, 0f), 1e-4f);
        }

        [Test]
        public void Stranded_WhenFuelCannotReachSupply()
        {
            Assert.IsTrue(FuelLogisticsRules.Stranded(10f, 20f));   // 届かない
            Assert.IsFalse(FuelLogisticsRules.Stranded(30f, 20f));  // 届く
            Assert.IsFalse(FuelLogisticsRules.Stranded(20f, 20f));  // ちょうど届く
        }

        [Test]
        public void IsFuelCritical_AtOrBelowThreshold()
        {
            Assert.IsTrue(FuelLogisticsRules.IsFuelCritical(0.1f, 0.2f));
            Assert.IsTrue(FuelLogisticsRules.IsFuelCritical(0.2f, 0.2f));
            Assert.IsFalse(FuelLogisticsRules.IsFuelCritical(0.5f, 0.2f));
        }

        // ── 物語テスト ────────────────────────────────────────────

        [Test]
        public void Story_DeepStrikeRunsLowThenTankerSavesTheFleet()
        {
            var p = FuelLogisticsParams.Default;

            // 大艦隊が高速で敵中深く突入＝燃料を大量に食う
            float consumption = FuelLogisticsRules.FuelConsumption(3f, 20f, p); // 1×2.5×3 = 7.5
            Assert.AreEqual(7.5f, consumption, 1e-4f);

            // 満タン600の燃料で行ける行動半径と、帰還ぶんを残した安全行動圏
            float radius = FuelLogisticsRules.OperatingRadius(600f, consumption); // 80
            Assert.AreEqual(80f, radius, 1e-4f);
            float safeRange = FuelLogisticsRules.RangeFromBase(radius, p.returnMargin); // 80×0.6 = 48
            Assert.AreEqual(48f, safeRange, 1e-4f);

            // 進撃で燃料が乏しくなり節約モードに入る（残量比が閾値未満）
            float lowState = FuelLogisticsRules.FuelState(120f, 600f); // 0.2
            Assert.IsTrue(FuelLogisticsRules.IsFuelCritical(lowState, p.lowFuelThreshold));
            float penalty = FuelLogisticsRules.LowFuelPenalty(lowState, p); // 能力抑制
            Assert.Less(penalty, 1f);
            Assert.GreaterOrEqual(penalty, p.minPenaltyFactor);

            // このままでは補給拠点（距離150）に届かず立ち往生
            Assert.IsTrue(FuelLogisticsRules.Stranded(120f, 150f));

            // 補給艦が洋上補給＝行動圏が延びて届く（延伸倍率は1超）
            float extension = FuelLogisticsRules.TankerSupport(80f, 100f, p); // 1.8
            Assert.AreEqual(1.8f, extension, 1e-4f);
            float extendedReach = 120f * extension; // 216 ＞ 150
            Assert.IsFalse(FuelLogisticsRules.Stranded(extendedReach, 150f));
        }
    }
}
