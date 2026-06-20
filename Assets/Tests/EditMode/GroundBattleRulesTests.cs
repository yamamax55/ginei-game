using System;
using NUnit.Framework;
using UnityEngine;

namespace Ginei.Tests
{
    /// <summary>
    /// 地上戦の戦闘解決（GroundBattleRules）の純ロジック検証。地形が守備有利／攻防比が勝敗と損害へ／
    /// 士気が実効戦力へ／占領進捗が攻撃側優勢に単調／境界・決定論・null安全。
    /// </summary>
    public class GroundBattleRulesTests
    {
        const float Eps = 1e-4f;

        // ---- 地形が守備に有利 ----
        [Test]
        public void DefenderAdvantage_TerrainOrder_FortressAndMountainFavorDefender()
        {
            float plains = GroundBattleRules.DefenderAdvantage(BattlefieldTerrain.平地);
            float urban = GroundBattleRules.DefenderAdvantage(BattlefieldTerrain.市街);
            float mountain = GroundBattleRules.DefenderAdvantage(BattlefieldTerrain.山岳);
            float fortress = GroundBattleRules.DefenderAdvantage(BattlefieldTerrain.要塞);

            Assert.AreEqual(1f, plains, Eps, "平地は守備有利なし");
            Assert.Greater(urban, plains);
            Assert.Greater(mountain, urban, "山岳は市街より守備有利");
            Assert.Greater(fortress, mountain, "要塞は最も守備有利");
        }

        [Test]
        public void DefenderAdvantage_FortificationRaisesAdvantage()
        {
            float bare = GroundBattleRules.DefenderAdvantage(BattlefieldTerrain.要塞, 0f);
            float built = GroundBattleRules.DefenderAdvantage(BattlefieldTerrain.要塞, 1f);
            Assert.Greater(built, bare, "築城度が守備有利を上乗せ");
            Assert.AreEqual(GroundBattleRules.FortressAdvantage * (1f + GroundBattleRules.FortificationBonus), built, Eps);
        }

        [Test]
        public void DefenderAdvantage_FortificationClampedAndNeverBelowOne()
        {
            // 負/過大の築城度はクランプ。守備有利は常に1以上。
            Assert.AreEqual(GroundBattleRules.DefenderAdvantage(BattlefieldTerrain.平地, 0f),
                            GroundBattleRules.DefenderAdvantage(BattlefieldTerrain.平地, -5f), Eps);
            Assert.AreEqual(GroundBattleRules.DefenderAdvantage(BattlefieldTerrain.平地, 1f),
                            GroundBattleRules.DefenderAdvantage(BattlefieldTerrain.平地, 9f), Eps);
            Assert.GreaterOrEqual(GroundBattleRules.DefenderAdvantage(BattlefieldTerrain.平地, 0f), 1f);
        }

        // ---- 地形が会戦の勝敗を左右 ----
        [Test]
        public void Resolve_TerrainHelpsDefenderHold()
        {
            // 同戦力・同士気。平地では拮抗で攻め勝つ寄り、要塞では守備が持ちこたえる。
            var plains = GroundBattleRules.Resolve(1200f, 1000f, BattlefieldTerrain.平地, 1f, 1f);
            var fortress = GroundBattleRules.Resolve(1200f, 1000f, BattlefieldTerrain.要塞, 1f, 1f);

            Assert.IsTrue(plains.attackerWon, "平地で1.2:1なら攻め勝つ");
            Assert.IsFalse(fortress.attackerWon, "同じ兵力でも要塞では守備が持つ");
            // 要塞では占領が進まない（停滞）。
            Assert.AreEqual(0f, fortress.captureProgressDelta, Eps);
            Assert.Greater(plains.captureProgressDelta, 0f);
        }

        // ---- 攻防比が大きいほど攻め勝ち、攻撃側損害は軽い ----
        [Test]
        public void Resolve_HigherAttackerRatio_WinsWithFewerLosses()
        {
            var even = GroundBattleRules.Resolve(10000f, 10000f, BattlefieldTerrain.平地, 1f, 1f);
            var overwhelm = GroundBattleRules.Resolve(40000f, 10000f, BattlefieldTerrain.平地, 1f, 1f);

            Assert.IsTrue(overwhelm.attackerWon, "圧倒的優勢で攻め勝つ");
            // 集中（二乗則）で優勢側ほど自軍損害が軽い（頭数あたり）。
            float evenRate = even.attackerLosses / 10000f;
            float overwhelmRate = overwhelm.attackerLosses / 40000f;
            Assert.Less(overwhelmRate, evenRate, "優勢側ほど頭数あたり損害が軽い");
            // 守備側は集中で各個撃破され損害率が増す。
            Assert.Greater(overwhelm.defenderLosses, even.defenderLosses);
        }

        // ---- 士気が実効戦力をスケール ----
        [Test]
        public void Resolve_DefenderMoraleScalesEffectiveness()
        {
            // 守備士気が高いと持ちこたえ、低いと崩れる（同じ頭数でも）。
            var highMorale = GroundBattleRules.Resolve(1000f, 1000f, BattlefieldTerrain.平地, 1f, 1f);
            var lowMorale = GroundBattleRules.Resolve(1000f, 1000f, BattlefieldTerrain.平地, 1f, 0.1f);

            Assert.IsFalse(highMorale.attackerWon, "守備満士気＝拮抗では攻め切れない");
            Assert.IsTrue(lowMorale.attackerWon, "守備士気が崩れれば攻め勝つ");
            Assert.Greater(lowMorale.captureProgressDelta, highMorale.captureProgressDelta);
        }

        [Test]
        public void MoraleFactor_BoundsAndWeight()
        {
            var p = GroundBattleParams.Default;
            Assert.AreEqual(1f, GroundBattleRules.MoraleFactor(1f, p), Eps, "満士気で1.0");
            Assert.AreEqual(1f - p.moraleWeight, GroundBattleRules.MoraleFactor(0f, p), Eps, "士気0で 1-weight");
            // クランプ（負/過大）。
            Assert.AreEqual(GroundBattleRules.MoraleFactor(0f, p), GroundBattleRules.MoraleFactor(-3f, p), Eps);
            Assert.AreEqual(GroundBattleRules.MoraleFactor(1f, p), GroundBattleRules.MoraleFactor(9f, p), Eps);
        }

        // ---- 複合兵科が攻撃側を底上げ ----
        [Test]
        public void CombinedArmsFactor_BalancedBeatsMonoculture()
        {
            float balanced = GroundBattleRules.CombinedArmsFactor(1f, 1f, 1f);
            float mono = GroundBattleRules.CombinedArmsFactor(1f, 0f, 0f);
            Assert.Greater(balanced, mono, "三兵科バランスは単一兵科より相乗が高い");
        }

        [Test]
        public void Resolve_CombinedArmsHelpsAttacker()
        {
            // バランス編成（armsFactor>1）の方が拮抗を破りやすい。
            float balanced = GroundBattleRules.CombinedArmsFactor(1f, 1f, 1f);
            float mono = GroundBattleRules.CombinedArmsFactor(1f, 0f, 0f);
            var p = GroundBattleParams.Default;

            var withArms = GroundBattleRules.Resolve(1000f, 1000f, BattlefieldTerrain.平地, 0f, 1f, 1f, balanced, p, null);
            var without = GroundBattleRules.Resolve(1000f, 1000f, BattlefieldTerrain.平地, 0f, 1f, 1f, mono, p, null);
            Assert.Greater(withArms.captureProgressDelta, without.captureProgressDelta);
        }

        // ---- 占領進捗が攻撃側優勢に単調 ----
        [Test]
        public void Resolve_CaptureProgressMonotonicInAttackerAdvantage()
        {
            float prev = -1f;
            foreach (float atk in new[] { 1100f, 2000f, 5000f, 20000f })
            {
                var r = GroundBattleRules.Resolve(atk, 1000f, BattlefieldTerrain.平地, 1f, 1f);
                Assert.GreaterOrEqual(r.captureProgressDelta, prev, "攻撃側兵力が増えるほど占領進捗は減らない");
                prev = r.captureProgressDelta;
            }
        }

        [Test]
        public void Resolve_CaptureProgressClampedToMax()
        {
            var p = GroundBattleParams.Default;
            var r = GroundBattleRules.Resolve(10_000_000f, 1f, BattlefieldTerrain.平地, 0f, 1f, 1f, 1f, p, null);
            Assert.LessOrEqual(r.captureProgressDelta, p.maxCaptureDelta + Eps, "占領進捗は上限でクランプ");
        }

        // ---- 境界（守備なし／攻撃なし） ----
        [Test]
        public void Resolve_NoGarrison_UncontestedCapture()
        {
            var r = GroundBattleRules.Resolve(1000f, 0f, BattlefieldTerrain.要塞, 1f, 1f);
            Assert.IsTrue(r.attackerWon);
            Assert.AreEqual(0f, r.attackerLosses, Eps, "無抵抗占領は攻撃側無傷");
            Assert.AreEqual(0f, r.defenderLosses, Eps);
            Assert.Greater(r.captureProgressDelta, 0f);
        }

        [Test]
        public void Resolve_NoAttacker_NoBattle()
        {
            var r = GroundBattleRules.Resolve(0f, 1000f, BattlefieldTerrain.平地, 1f, 1f);
            Assert.IsFalse(r.attackerWon);
            Assert.AreEqual(0f, r.attackerLosses, Eps);
            Assert.AreEqual(0f, r.defenderLosses, Eps);
            Assert.AreEqual(0f, r.captureProgressDelta, Eps);
        }

        [Test]
        public void Resolve_NegativeInputs_ClampSafe()
        {
            // 負の入力でも例外なく安全（クランプ）。
            var r = GroundBattleRules.Resolve(-500f, -500f, BattlefieldTerrain.平地, -1f, 1f, 1f, -2f, GroundBattleParams.Default, null);
            Assert.IsFalse(r.attackerWon);
            Assert.AreEqual(0f, r.attackerLosses, Eps);
            Assert.AreEqual(0f, r.defenderLosses, Eps);
            Assert.AreEqual(0f, r.captureProgressDelta, Eps);
        }

        // ---- 損害は実数を超えない（境界） ----
        [Test]
        public void Resolve_LossesNeverExceedHeadcount()
        {
            // 全 roll 値で損害が頭数を超えない。
            foreach (float rv in new[] { 0f, 0.5f, 1f })
            {
                var r = GroundBattleRules.Resolve(800f, 600f, BattlefieldTerrain.山岳, 0.5f, 0.8f, 0.7f, 1f,
                                                  GroundBattleParams.Default, () => rv);
                Assert.LessOrEqual(r.attackerLosses, 800f + Eps);
                Assert.LessOrEqual(r.defenderLosses, 600f + Eps);
                Assert.GreaterOrEqual(r.attackerLosses, 0f);
                Assert.GreaterOrEqual(r.defenderLosses, 0f);
            }
        }

        // ---- 決定論（同入力＝同結果） ----
        [Test]
        public void Resolve_Deterministic_SameInputsSameResult()
        {
            Func<float> roll = () => 0.42f;
            var a = GroundBattleRules.Resolve(1500f, 1000f, BattlefieldTerrain.市街, 0.3f, 0.9f, 0.85f, 1.2f, GroundBattleParams.Default, roll);
            var b = GroundBattleRules.Resolve(1500f, 1000f, BattlefieldTerrain.市街, 0.3f, 0.9f, 0.85f, 1.2f, GroundBattleParams.Default, () => 0.42f);
            Assert.AreEqual(a.attackerWon, b.attackerWon);
            Assert.AreEqual(a.attackerLosses, b.attackerLosses, Eps);
            Assert.AreEqual(a.defenderLosses, b.defenderLosses, Eps);
            Assert.AreEqual(a.captureProgressDelta, b.captureProgressDelta, Eps);
        }

        [Test]
        public void Resolve_NullRoll_UsesMidpoint_NoThrow()
        {
            // roll=null は 0.5（中央値）と同値＝決定論。
            var withNull = GroundBattleRules.Resolve(1500f, 1000f, BattlefieldTerrain.平地, 0f, 1f, 1f, 1f, GroundBattleParams.Default, null);
            var withHalf = GroundBattleRules.Resolve(1500f, 1000f, BattlefieldTerrain.平地, 0f, 1f, 1f, 1f, GroundBattleParams.Default, () => 0.5f);
            Assert.AreEqual(withHalf.attackerLosses, withNull.attackerLosses, Eps);
            Assert.AreEqual(withHalf.defenderLosses, withNull.defenderLosses, Eps);
        }

        [Test]
        public void Resolve_HigherRoll_RaisesLosses()
        {
            var low = GroundBattleRules.Resolve(1500f, 1000f, BattlefieldTerrain.平地, 0f, 1f, 1f, 1f, GroundBattleParams.Default, () => 0f);
            var high = GroundBattleRules.Resolve(1500f, 1000f, BattlefieldTerrain.平地, 0f, 1f, 1f, 1f, GroundBattleParams.Default, () => 1f);
            Assert.Greater(high.attackerLosses, low.attackerLosses, "roll が高いほど損害は重い（揺らぎ）");
            // 勝敗・占領進捗は roll に依らず安定（損害だけ揺れる）。
            Assert.AreEqual(low.attackerWon, high.attackerWon);
            Assert.AreEqual(low.captureProgressDelta, high.captureProgressDelta, Eps);
        }
    }
}
