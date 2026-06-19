using NUnit.Framework;
using UnityEngine;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 要塞主砲（トゥール・ハンマー型）を固定する：炉×収束の一撃、威力に比例する充填時間と隙、
    /// 旋回不能ゆえの射界、艦隊密集の増幅、過充填の暴発リスク、実証戦果の威圧、撃つ価値の判断。
    /// 既定 Params 期待値を手計算で検算し境界を担保。
    /// </summary>
    public class FortressArtilleryRulesTests
    {
        private static readonly FortressArtilleryParams P = FortressArtilleryParams.Default;
        // yieldScale=10/chargeScale=15/arcHalfAngle=30/clusterGain=1/missWindowScale=60/overchargeKnee=0.8/intimidationWeight=0.5

        [Test]
        public void BurstYield_ReactorTimesFocus()
        {
            // 1×1×10 = 10
            Assert.AreEqual(10f, FortressArtilleryRules.BurstYield(1f, 1f, P), 1e-4f);
            // 2×0.5×10 = 10
            Assert.AreEqual(10f, FortressArtilleryRules.BurstYield(2f, 0.5f, P), 1e-4f);
            // 収束は0..1にclamp（1.5→1）
            Assert.AreEqual(10f, FortressArtilleryRules.BurstYield(1f, 1.5f, P), 1e-4f);
            // 負入力は0
            Assert.AreEqual(0f, FortressArtilleryRules.BurstYield(-5f, 1f, P), 1e-4f);
        }

        [Test]
        public void ChargeTime_StrongerTakesLonger()
        {
            // 60×15/10 = 90
            Assert.AreEqual(90f, FortressArtilleryRules.ChargeTime(60f, 10f, P), 1e-4f);
            // 10×15/5 = 30（弱い・蓄電潤沢ほど速い）
            Assert.AreEqual(30f, FortressArtilleryRules.ChargeTime(10f, 5f, P), 1e-4f);
            // 蓄電0は撃てない＝無限大
            Assert.IsTrue(float.IsPositiveInfinity(FortressArtilleryRules.ChargeTime(10f, 0f, P)));
        }

        [Test]
        public void FiringArc_AlignmentWithinHalfAngle()
        {
            // 完全正対 = 1
            Assert.AreEqual(1f, FortressArtilleryRules.FiringArc(0f, 0f, P), 1e-4f);
            // 半角の半分ずれ（15度）= 0.5
            Assert.AreEqual(0.5f, FortressArtilleryRules.FiringArc(0f, 15f, P), 1e-4f);
            // 半角ちょうど（30度）= 0
            Assert.AreEqual(0f, FortressArtilleryRules.FiringArc(0f, 30f, P), 1e-4f);
            // 半角超は0（負へ行かずclamp）
            Assert.AreEqual(0f, FortressArtilleryRules.FiringArc(0f, 45f, P), 1e-4f);
            // 360度ラップ：差340→20度 → 1−20/30 = 0.3333...
            Assert.AreEqual(1f / 3f, FortressArtilleryRules.FiringArc(10f, 350f, P), 1e-4f);
        }

        [Test]
        public void ClusterEffect_AmplifiesByDensity()
        {
            // 10×(1+1×1) = 20（密集で倍）
            Assert.AreEqual(20f, FortressArtilleryRules.ClusterEffect(10f, 1f, P), 1e-4f);
            // 10×1.5 = 15
            Assert.AreEqual(15f, FortressArtilleryRules.ClusterEffect(10f, 0.5f, P), 1e-4f);
            // 密集0なら素の威力
            Assert.AreEqual(10f, FortressArtilleryRules.ClusterEffect(10f, 0f, P), 1e-4f);
        }

        [Test]
        public void MissedShotWindow_GrowsWithChargeTime()
        {
            // 90秒 → clamp(90/60) = 1（飽和）
            Assert.AreEqual(1f, FortressArtilleryRules.MissedShotWindow(90f, P), 1e-4f);
            // 30秒 → 0.5
            Assert.AreEqual(0.5f, FortressArtilleryRules.MissedShotWindow(30f, P), 1e-4f);
            // 0秒 → 隙なし
            Assert.AreEqual(0f, FortressArtilleryRules.MissedShotWindow(0f, P), 1e-4f);
        }

        [Test]
        public void OverchargeRisk_RisesPastKnee()
        {
            // 膝ちょうど(0.8) → 0
            Assert.AreEqual(0f, FortressArtilleryRules.OverchargeRisk(0.8f, P), 1e-4f);
            // 0.9 → (0.9−0.8)/0.2 = 0.5
            Assert.AreEqual(0.5f, FortressArtilleryRules.OverchargeRisk(0.9f, P), 1e-4f);
            // 満充填1 → 1
            Assert.AreEqual(1f, FortressArtilleryRules.OverchargeRisk(1f, P), 1e-4f);
            // 膝以下は安全0
            Assert.AreEqual(0f, FortressArtilleryRules.OverchargeRisk(0.5f, P), 1e-4f);
        }

        [Test]
        public void PsychologicalIntimidation_YieldAndDemonstration()
        {
            // 威力満（y=1）・実証なし：1×0.5 = 0.5
            Assert.AreEqual(0.5f, FortressArtilleryRules.PsychologicalIntimidation(100f, 0f, P), 1e-4f);
            // 威力満・実証満：0.5 + 0.5 + 0 = 1
            Assert.AreEqual(1f, FortressArtilleryRules.PsychologicalIntimidation(100f, 1f, P), 1e-4f);
            // 威力ゼロ・実証満：0 + 0 + 1×0.5×1 = 0.5
            Assert.AreEqual(0.5f, FortressArtilleryRules.PsychologicalIntimidation(0f, 1f, P), 1e-4f);
            // 双方ゼロ：0
            Assert.AreEqual(0f, FortressArtilleryRules.PsychologicalIntimidation(0f, 0f, P), 1e-4f);
        }

        [Test]
        public void IsWorthFiring_EffectVersusWindow()
        {
            // 20/(1+1) = 10 ≥ 5 → 撃つ
            Assert.IsTrue(FortressArtilleryRules.IsWorthFiring(20f, 1f, 5f));
            // 10/(1+1) = 5 ≥ 5 → 境界で撃つ
            Assert.IsTrue(FortressArtilleryRules.IsWorthFiring(10f, 1f, 5f));
            // 8/(1+1) = 4 < 5 → 隙に見合わず撃たない
            Assert.IsFalse(FortressArtilleryRules.IsWorthFiring(8f, 1f, 5f));
            // 既定閾値委譲版（=5）
            Assert.IsTrue(FortressArtilleryRules.IsWorthFiring(20f, 1f));
        }

        [Test]
        public void Saga_FortressTrumpHammerStandoff()
        {
            // 物語：要塞が炉を全開（出力6・収束1）にして主砲を満たす。
            float yield = FortressArtilleryRules.BurstYield(6f, 1f, P);   // 6×1×10 = 60
            Assert.AreEqual(60f, yield, 1e-4f);

            // 威力が大きいぶん充填は長い（蓄電10）。
            float charge = FortressArtilleryRules.ChargeTime(yield, 10f, P); // 60×15/10 = 90
            Assert.AreEqual(90f, charge, 1e-4f);

            // 長い充填はそのまま長い隙＝外せば致命的。
            float window = FortressArtilleryRules.MissedShotWindow(charge, P); // 90/60→clamp1
            Assert.AreEqual(1f, window, 1e-4f);

            // 敵艦隊が密集（密集1）して進んでくれば一撃が薙ぎ払う。
            float cluster = FortressArtilleryRules.ClusterEffect(yield, 1f, P); // 60×2 = 120
            Assert.AreEqual(120f, cluster, 1e-4f);

            // 隙(最大1)があっても殲滅効果が十分大きいので撃つ価値あり。
            Assert.IsTrue(FortressArtilleryRules.IsWorthFiring(cluster, window, P.missWindowScale > 0f ? 5f : 5f));

            // 撃って見せた実績（実証1）と威力で敵は竦む。
            float fear = FortressArtilleryRules.PsychologicalIntimidation(yield, 1f, P);
            Assert.That(fear, Is.GreaterThan(0.5f));

            // ただし安全圏(膝0.8)を超えて過充填すれば暴発リスクを背負う賭けになる。
            Assert.That(FortressArtilleryRules.OverchargeRisk(0.95f, P), Is.GreaterThan(0f));
        }
    }
}
