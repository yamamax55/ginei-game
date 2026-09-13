using NUnit.Framework;
using UnityEngine;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 砲台の見た目の旋回と反動（<see cref="TurretAimRules"/>）。
    /// <b>要点</b>：旋回は見た目だけで、射界（固定基準）を広げないこと。
    /// </summary>
    public class TurretAimRulesTests
    {
        private static TurretAimParams P => TurretAimParams.Default;

        // ── 角度 ──

        [Test]
        public void SignedDelta_WrapsAcrossZero()
        {
            Assert.AreEqual(20f, TurretAimRules.SignedDelta(350f, 10f), 1e-3f);
            Assert.AreEqual(-20f, TurretAimRules.SignedDelta(10f, 350f), 1e-3f);
            Assert.AreEqual(0f, TurretAimRules.SignedDelta(45f, 45f), 1e-3f);
        }

        [Test]
        public void StepAngle_MovesAtMostTurnSpeedPerSecond()
        {
            // 既定 180度/秒。0.1 秒なら 18 度しか回らない。
            float a = TurretAimRules.StepAngle(0f, 90f, 0.1f, P);
            Assert.AreEqual(18f, a, 1e-3f);
        }

        [Test]
        public void StepAngle_SnapsWhenWithinStep()
        {
            float a = TurretAimRules.StepAngle(0f, 5f, 1f, P);   // 1秒で180度まで回れる
            Assert.AreEqual(5f, a, 1e-3f, "行き過ぎず目標でぴたりと止まる");
        }

        [Test]
        public void StepAngle_TakesShortWayAroundZero()
        {
            float a = TurretAimRules.StepAngle(350f, 10f, 0.05f, P);   // 9度ぶん
            Assert.AreEqual(359f, a, 1e-3f, "遠回りしている");
        }

        [Test]
        public void StepAngle_PausedDt_DoesNotMove()
        {
            Assert.AreEqual(30f, TurretAimRules.StepAngle(30f, 200f, 0f, P), 1e-3f);
        }

        [Test]
        public void Aligned_UsesTolerance()
        {
            Assert.IsTrue(TurretAimRules.Aligned(0f, 5f, P));      // 既定許容6度
            Assert.IsFalse(TurretAimRules.Aligned(0f, 20f, P));
            Assert.IsTrue(TurretAimRules.Aligned(358f, 2f, P), "0度またぎでも許容が効く");
        }

        // ── 射界を広げない（本モジュールの主旨） ──

        /// <summary>
        /// ★<b>固定基準の射界の外は、どれだけモデルを向けても撃てない</b>。
        /// ここが崩れると、追尾のたびに扇が付いて回り砲台を潰しても死角ができなくなる。
        /// </summary>
        [Test]
        public void CanFire_OutsideFixedArc_IsAlwaysFalse_EvenWhenPerfectlyAimed()
        {
            Assert.IsFalse(TurretAimRules.CanFire(hasTarget: true, inFixedArc: false, silenced: false,
                                                  paused: false, currentAngle: 90f, desiredAngle: 90f, P));
        }

        [Test]
        public void CanFire_InsideArcButNotAimedYet_IsFalse()
        {
            Assert.IsFalse(TurretAimRules.CanFire(true, true, false, false, 0f, 45f, P));
        }

        [Test]
        public void CanFire_InsideArcAndAimed_IsTrue()
        {
            Assert.IsTrue(TurretAimRules.CanFire(true, true, false, false, 0f, 3f, P));
        }

        [Test]
        public void CanFire_NoTargetSilencedOrPaused_IsFalse()
        {
            Assert.IsFalse(TurretAimRules.CanFire(false, true, false, false, 0f, 0f, P), "標的なし");
            Assert.IsFalse(TurretAimRules.CanFire(true, true, true, false, 0f, 0f, P), "沈黙");
            Assert.IsFalse(TurretAimRules.CanFire(true, true, false, true, 0f, 0f, P), "ポーズ中");
        }

        // ── 反動 ──

        [Test]
        public void RecoilOffset_PeaksAtFire_AndReturnsToZero()
        {
            Assert.AreEqual(P.recoilKick, TurretAimRules.RecoilOffset(0f, P), 1e-3f);
            Assert.AreEqual(0f, TurretAimRules.RecoilOffset(P.recoilRecover, P), 1e-3f);
            Assert.AreEqual(0f, TurretAimRules.RecoilOffset(P.recoilRecover * 5f, P), 1e-3f,
                            "戻り切ったあとに残ってはいけない");
            Assert.AreEqual(0f, TurretAimRules.RecoilOffset(-1f, P), 1e-3f, "撃つ前は 0");
        }

        [Test]
        public void RecoilOffset_DecreasesMonotonically()
        {
            float prev = float.MaxValue;
            for (int i = 0; i <= 10; i++)
            {
                float t = P.recoilRecover * i / 10f;
                float v = TurretAimRules.RecoilOffset(t, P);
                Assert.LessOrEqual(v, prev + 1e-4f, $"t={t} で戻りが逆行している");
                prev = v;
            }
        }

        // ── 方向 ──

        [Test]
        public void AngleOf_MatchesAtan2Convention()
        {
            Assert.AreEqual(0f, TurretAimRules.AngleOf(Vector2.right), 1e-3f);
            Assert.AreEqual(90f, TurretAimRules.AngleOf(Vector2.up), 1e-3f);
            Assert.AreEqual(180f, TurretAimRules.AngleOf(Vector2.left), 1e-3f);
            Assert.AreEqual(270f, TurretAimRules.AngleOf(Vector2.down), 1e-3f);
            Assert.AreEqual(0f, TurretAimRules.AngleOf(Vector2.zero), 1e-3f, "ゼロベクトルで暴れない");
        }

        // ── 調整値 ──

        [Test]
        public void Params_ClampToSaneRange()
        {
            var p = new TurretAimParams(-5f, 0f, -1f, 0f);
            Assert.GreaterOrEqual(p.turnSpeed, 1f);
            Assert.GreaterOrEqual(p.fireTolerance, 0.5f);
            Assert.GreaterOrEqual(p.recoilKick, 0f);
            Assert.Greater(p.recoilRecover, 0f);
        }

        [Test]
        public void Default_MatchesDeliveredModel()
        {
            // Blender の反動量 0.16（受渡し文書）に合わせてある。
            Assert.AreEqual(0.16f, TurretAimParams.Default.recoilKick, 1e-4f);
            Assert.AreEqual(180f, TurretAimParams.Default.turnSpeed, 1e-4f);
        }
    }
}
