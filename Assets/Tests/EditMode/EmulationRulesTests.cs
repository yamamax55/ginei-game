using NUnit.Framework;
using UnityEngine;

namespace Ginei.Tests
{
    /// <summary>
    /// 金銭的模倣カスケード（VEBL-2 #1597）の純ロジック検証。
    /// 模倣圧力・消費規範の底上げ・下方カスケード（滝の伝播）・見栄コスト・安定低下・需要押し上げを既定Paramsの具体値で固定。
    /// </summary>
    public class EmulationRulesTests
    {
        private const float Eps = 0.0001f;
        private static EmulationParams P => EmulationParams.Default;

        /// <summary>模倣圧力＝上位消費×地位願望×gain。上を見るほど・上に行きたいほど真似たくなる（積）。</summary>
        [Test]
        public void EmulationPressure_上位消費と地位願望の積()
        {
            // 0.8 × 0.5 × gain1 = 0.4
            Assert.AreEqual(0.4f, EmulationRules.EmulationPressure(0.8f, 0.5f, P), Eps);
            // どちらかが0なら模倣は起きない
            Assert.AreEqual(0f, EmulationRules.EmulationPressure(0f, 1f, P), Eps);
            Assert.AreEqual(0f, EmulationRules.EmulationPressure(1f, 0f, P), Eps);
        }

        /// <summary>消費規範は模倣圧（目標）へ時間で寄る＝下位が上位の水準へ底上げされる。</summary>
        [Test]
        public void ConsumptionNormShift_模倣圧へ底上げ()
        {
            // 現規範0.2→目標0.7、step=|0.7-0.2|×speed1×dt0.5=0.25 → 0.45
            Assert.AreEqual(0.45f, EmulationRules.ConsumptionNormShift(0.2f, 0.7f, 0.5f, P), Eps);
            // dt≤0 は不変
            Assert.AreEqual(0.2f, EmulationRules.ConsumptionNormShift(0.2f, 0.7f, 0f, P), Eps);
            // 模倣圧が低ければ寄り戻る（上を見なくなれば規範も下がる）
            Assert.Less(EmulationRules.ConsumptionNormShift(0.8f, 0.2f, 0.5f, P), 0.8f);
        }

        /// <summary>下方カスケード＝上位規範が階層を下る（rank低）ほど薄れる滝の伝播。</summary>
        [Test]
        public void CascadeDownward_下層ほど薄れる()
        {
            // 最上位 rank1 は減衰なし＝top のまま
            Assert.AreEqual(0.8f, EmulationRules.CascadeDownward(0.8f, 1f, 0.5f), Eps);
            // 最下位 rank0・decay0.5 → 0.8×(1-1×0.5)=0.4
            Assert.AreEqual(0.4f, EmulationRules.CascadeDownward(0.8f, 0f, 0.5f), Eps);
            // 中間 rank0.5・decay0.5 → 0.8×(1-0.5×0.5)=0.6
            Assert.AreEqual(0.6f, EmulationRules.CascadeDownward(0.8f, 0.5f, 0.5f), Eps);
            // decay0 は減衰なし（全階級が同じ規範）
            Assert.AreEqual(0.8f, EmulationRules.CascadeDownward(0.8f, 0f, 0f), Eps);
        }

        /// <summary>見栄コスト＝規範が所得（身の丈）を超えた分だけ重み付けして負担になる。</summary>
        [Test]
        public void KeepingUpCost_身の丈を超えた見栄だけ負担()
        {
            // 規範0.8・所得0.5、超過0.3×weight1.5=0.45
            Assert.AreEqual(0.45f, EmulationRules.KeepingUpCost(0.8f, 0.5f, P), Eps);
            // 規範≤所得なら背伸びしていない＝負担0
            Assert.AreEqual(0f, EmulationRules.KeepingUpCost(0.4f, 0.5f, P), Eps);
        }

        /// <summary>安定の侵食＝見栄の負担が安定を削る（背伸びの不満）。負担0なら侵食0。</summary>
        [Test]
        public void StabilityErosion_見栄負担が安定を削る()
        {
            // 負担0.45×erosionScale1=0.45
            Assert.AreEqual(0.45f, EmulationRules.StabilityErosion(0.45f, P), Eps);
            Assert.AreEqual(0f, EmulationRules.StabilityErosion(0f, P), Eps);
        }

        /// <summary>需要押し上げ＝消費規範の底上げが需要を押す（経済への正の面）。</summary>
        [Test]
        public void DemandBoost_規範が需要を押し上げる()
        {
            // 規範0.8×demandScale0.5=0.4
            Assert.AreEqual(0.4f, EmulationRules.DemandBoost(0.8f, P), Eps);
            Assert.AreEqual(0f, EmulationRules.DemandBoost(0f, P), Eps);
        }

        /// <summary>地位のランニングマシン＝水準は上がるが相対地位は変わらない徒労。圧が高いほど水準上昇量も大。</summary>
        [Test]
        public void StatusTreadmill_水準だけ上がる()
        {
            // 圧0.6×speed1×dt0.5=0.3（水準の上昇量・相対地位は不変）
            Assert.AreEqual(0.3f, EmulationRules.StatusTreadmill(0.6f, 0.5f, P), Eps);
            // dt≤0 は上昇なし
            Assert.AreEqual(0f, EmulationRules.StatusTreadmill(0.6f, 0f, P), Eps);
        }

        /// <summary>見栄の暴走判定＝規範が身の丈を threshold ぶん超えたら見栄スパイラル。</summary>
        [Test]
        public void IsConspicuousSpiral_身の丈超過の暴走()
        {
            // 規範0.9・所得0.4・閾0.3 → 超過0.5>0.3 ＝暴走
            Assert.IsTrue(EmulationRules.IsConspicuousSpiral(0.9f, 0.4f, 0.3f));
            // 規範0.6・所得0.5・閾0.3 → 超過0.1≤0.3 ＝暴走せず
            Assert.IsFalse(EmulationRules.IsConspicuousSpiral(0.6f, 0.5f, 0.3f));
        }
    }
}
