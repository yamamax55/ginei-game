using NUnit.Framework;
using UnityEngine;

namespace Ginei.Tests
{
    public class GravityWellRulesTests
    {
        const float Eps = 1e-4f;
        const float EpsPow = 1e-3f; // Pow/Sqrt 箇所のみ緩める（本ルールは Pow2 のみ）

        [Test]
        public void GravityStrength_InverseSquareWithSoftening()
        {
            var p = GravityWellParams.Default;
            // G=1, soft=100 -> denom = d^2 + 10000。
            // mass=10000, d=0   -> 10000/10000 = 1.0
            // mass=10000, d=100 -> 10000/20000 = 0.5（距離で弱まる）
            // mass=100,   d=0   -> 100/10000   = 0.01
            Assert.AreEqual(1f, GravityWellRules.GravityStrength(10000f, 0f, p), EpsPow);
            Assert.AreEqual(0.5f, GravityWellRules.GravityStrength(10000f, 100f, p), EpsPow);
            Assert.AreEqual(0.01f, GravityWellRules.GravityStrength(100f, 0f, p), Eps);
        }

        [Test]
        public void GravityStrength_ClampsToMaxAndNonNegative()
        {
            var p = GravityWellParams.Default;
            // ブラックホール級は上限 maxStrength(100) で頭打ち。
            Assert.AreEqual(100f, GravityWellRules.GravityStrength(1e9f, 0f, p), Eps);
            // 質量0・負距離は安全に 0。
            Assert.AreEqual(0f, GravityWellRules.GravityStrength(0f, 50f, p), Eps);
            Assert.AreEqual(1f, GravityWellRules.GravityStrength(10000f, -100f, p), EpsPow);
        }

        [Test]
        public void OrbitalDeflection_SlowShipsGetPulledIn()
        {
            // 速度0は完全に曲げられる(1)、速いほど振り切る。
            Assert.AreEqual(1f, GravityWellRules.OrbitalDeflection(10f, 0f), Eps);
            Assert.AreEqual(0.5f, GravityWellRules.OrbitalDeflection(1f, 1f), Eps);
            Assert.AreEqual(0.25f, GravityWellRules.OrbitalDeflection(1f, 3f), Eps);
            // 重力0なら曲がらない。
            Assert.AreEqual(0f, GravityWellRules.OrbitalDeflection(0f, 5f), Eps);
        }

        [Test]
        public void ManeuverCost_HighThrustEasesGravity()
        {
            // 最強重力×推力なし -> 1.0、推力重量比1 -> 0.5、半重力 -> 0.5。
            Assert.AreEqual(1f, GravityWellRules.ManeuverCost(100f, 0f), Eps);
            Assert.AreEqual(0.5f, GravityWellRules.ManeuverCost(100f, 1f), Eps);
            Assert.AreEqual(0.5f, GravityWellRules.ManeuverCost(50f, 0f), Eps);
        }

        [Test]
        public void SlingshotGain_PeaksAtOptimalAngle()
        {
            // 最適角90度で最大(1.5)、45度ズレで半減(1.25)、真正面0度で利得なし(1.0)。
            Assert.AreEqual(1.5f, GravityWellRules.SlingshotGain(100f, 90f), Eps);
            Assert.AreEqual(1.25f, GravityWellRules.SlingshotGain(100f, 45f), Eps);
            Assert.AreEqual(1f, GravityWellRules.SlingshotGain(100f, 0f), Eps);
            // 重力が弱ければ利得も小さい。
            Assert.AreEqual(1.25f, GravityWellRules.SlingshotGain(50f, 90f), Eps);
        }

        [Test]
        public void TidalDamage_LongerHullsTornApart()
        {
            // 最強重力×基準艦長 -> 1.0、半長 -> 0.5、半重力 -> 0.5。
            Assert.AreEqual(1f, GravityWellRules.TidalDamage(100f, 1000f), Eps);
            Assert.AreEqual(0.5f, GravityWellRules.TidalDamage(100f, 500f), Eps);
            Assert.AreEqual(0.5f, GravityWellRules.TidalDamage(50f, 1000f), Eps);
        }

        [Test]
        public void EscapeDifficulty_StrongWellTrapsLowThrust()
        {
            // 最強重力×推力なし -> 1.0、推力重量比1 -> 0.5、半重力×推力0.5 -> 0.5。
            Assert.AreEqual(1f, GravityWellRules.EscapeDifficulty(100f, 0f), Eps);
            Assert.AreEqual(0.5f, GravityWellRules.EscapeDifficulty(100f, 1f), Eps);
            Assert.AreEqual(0.5f, GravityWellRules.EscapeDifficulty(50f, 0.5f), Eps);
        }

        [Test]
        public void WellTacticalValue_GainMinusDifficultySigned()
        {
            // 高利得(1.5)×低離脱難(0.5) -> +0.5（攻め時）。
            Assert.AreEqual(0.5f, GravityWellRules.WellTacticalValue(1.5f, 0.5f), Eps);
            // 利得なし×離脱不能 -> -1（罠）。
            Assert.AreEqual(-1f, GravityWellRules.WellTacticalValue(1f, 1f), Eps);
            // 中庸。
            Assert.AreEqual(0.25f, GravityWellRules.WellTacticalValue(1.25f, 0.25f), Eps);
        }

        [Test]
        public void IsCaptured_BothDeflectionAndEscape()
        {
            // 既定閾値0.5：偏向も離脱難も超えれば捕獲。
            Assert.IsTrue(GravityWellRules.IsCaptured(0.8f, 0.8f));
            Assert.IsFalse(GravityWellRules.IsCaptured(0.8f, 0.3f)); // 離脱できる
            Assert.IsFalse(GravityWellRules.IsCaptured(0.3f, 0.8f)); // 曲げられていない
            // 閾値明示版。
            Assert.IsTrue(GravityWellRules.IsCaptured(0.6f, 0.6f, 0.5f));
            Assert.IsFalse(GravityWellRules.IsCaptured(0.6f, 0.6f, 0.7f));
        }

        [Test]
        public void Narrative_BlackHoleSlingshotVersusTrappedDreadnought()
        {
            var p = GravityWellParams.Default;

            // ブラックホール近傍：強烈な重力(上限100)。
            float g = GravityWellRules.GravityStrength(1e9f, 0f, p);
            Assert.AreEqual(100f, g, Eps);

            // 高速の軽巡が最適角で突入＝スイングバイで加速して離脱する。
            float fastShipGain = GravityWellRules.SlingshotGain(g, 90f, p);
            float fastShipEscape = GravityWellRules.EscapeDifficulty(g, 4f, p); // 高推力重量比
            Assert.AreEqual(1.5f, fastShipGain, Eps);
            Assert.AreEqual(0.2f, fastShipEscape, Eps); // 1/(1+4)
            float fastValue = GravityWellRules.WellTacticalValue(fastShipGain, fastShipEscape, p);
            Assert.AreEqual(0.8f, fastValue, Eps); // 1.0 - 0.2（攻め時）
            float fastDeflect = GravityWellRules.OrbitalDeflection(g, 100f, p);
            Assert.IsFalse(GravityWellRules.IsCaptured(fastDeflect, fastShipEscape, 0.5f));

            // 鈍重な戦艦は同じ井戸に引き込まれ、長大な船体が潮汐で裂かれ、抜け出せない。
            float slowDeflect = GravityWellRules.OrbitalDeflection(g, 0f, p);
            float slowEscape = GravityWellRules.EscapeDifficulty(g, 0f, p);
            float tidal = GravityWellRules.TidalDamage(g, 1000f, p);
            Assert.AreEqual(1f, slowDeflect, Eps);
            Assert.AreEqual(1f, slowEscape, Eps);
            Assert.AreEqual(1f, tidal, Eps);
            Assert.IsTrue(GravityWellRules.IsCaptured(slowDeflect, slowEscape, 0.5f));

            // 同じ重力井戸が、機動力の差で「加速台」にも「墓場」にもなる。
            Assert.Greater(fastValue, 0f);
            Assert.Greater(tidal, 0.5f);
        }

        [Test]
        public void Params_AreClampedInConstructor()
        {
            var p = new GravityWellParams(
                gravConstant: -10f,     // -> 0
                softening: -10f,        // -> 0.01
                maxStrength: -10f,      // -> 0.01
                velocityScale: -10f,    // -> 0.01
                maneuverScale: 9f,      // -> 4
                slingshotMaxGain: 9f,   // -> 2
                optimalAngle: 999f,     // -> 180
                angleTolerance: -10f,   // -> 1
                tidalLengthScale: -10f, // -> 0.01
                tidalScale: 9f);        // -> 4
            Assert.AreEqual(0f, p.gravConstant, Eps);
            Assert.AreEqual(0.01f, p.softening, Eps);
            Assert.AreEqual(0.01f, p.maxStrength, Eps);
            Assert.AreEqual(0.01f, p.velocityScale, Eps);
            Assert.AreEqual(4f, p.maneuverScale, Eps);
            Assert.AreEqual(2f, p.slingshotMaxGain, Eps);
            Assert.AreEqual(180f, p.optimalAngle, Eps);
            Assert.AreEqual(1f, p.angleTolerance, Eps);
            Assert.AreEqual(0.01f, p.tidalLengthScale, Eps);
            Assert.AreEqual(4f, p.tidalScale, Eps);
        }
    }
}
