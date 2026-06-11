using NUnit.Framework;
using UnityEngine;

namespace Ginei.Tests
{
    /// <summary>安全保障のジレンマ純ロジック（#1461）の検証。</summary>
    public class SecurityDilemmaRulesTests
    {
        private const float Eps = 1e-4f;

        /// <summary>受け取る脅威＝隣国軍備×攻防の区別不能性。区別できれば脅威ゼロ。</summary>
        [Test]
        public void PerceivedThreat_軍備と区別不能性の積()
        {
            // 軍備0.8×区別不能性0.5＝0.4。
            Assert.AreEqual(0.4f, SecurityDilemmaRules.PerceivedThreat(0.8f, 0.5f), Eps);
            // 純粋に防御的と区別できる(ambiguity=0)なら軍備があっても脅威ゼロ。
            Assert.AreEqual(0f, SecurityDilemmaRules.PerceivedThreat(1f, 0f), Eps);
        }

        /// <summary>防衛的軍備＝脅威×（1−自国の安全感）。十分安全なら軍備しない。</summary>
        [Test]
        public void DefensiveBuildup_脅威と不安全の積()
        {
            // 脅威0.6×(1−0.25)＝0.45。
            Assert.AreEqual(0.45f, SecurityDilemmaRules.DefensiveBuildup(0.6f, 0.25f), Eps);
            // 既に十分安全なら軍備しない。
            Assert.AreEqual(0f, SecurityDilemmaRules.DefensiveBuildup(0.9f, 1f), Eps);
        }

        /// <summary>螺旋的増大＝互いの自衛軍備が相手を競り上げる正のフィードバック。</summary>
        [Test]
        public void SpiralEscalation_互いに競り上がる()
        {
            // a=0.4,b=0.2,dt=1,gain=0.5 → newA=0.4+0.2*0.5=0.5, newB=0.2+0.4*0.5=0.4。
            var (na, nb) = SecurityDilemmaRules.SpiralEscalation(0.4f, 0.2f, 1f);
            Assert.AreEqual(0.5f, na, Eps);
            Assert.AreEqual(0.4f, nb, Eps);
            // 双方とも前tickより増大している＝安全を求めて軍備が積み上がる。
            Assert.Greater(na, 0.4f);
            Assert.Greater(nb, 0.2f);
        }

        /// <summary>攻防バランス＝攻撃有利ほどジレンマ深刻。防御有利なら深刻度ゼロ。</summary>
        [Test]
        public void OffenseDefenseBalance_攻撃有利で深刻化()
        {
            // 攻撃有利0.7×増幅1＝0.7。
            Assert.AreEqual(0.7f, SecurityDilemmaRules.OffenseDefenseBalance(0.7f), Eps);
            // 防御有利(攻撃有利0)ならジレンマは生じない。
            Assert.AreEqual(0f, SecurityDilemmaRules.OffenseDefenseBalance(0f), Eps);
        }

        /// <summary>区別可能性＝防御的と明確なほど緩和。区別不能なら緩和なし。</summary>
        [Test]
        public void Distinguishability_防御的明確さで緩和()
        {
            // 防御的と完全に区別できる(1)なら緩和倍率0＝ジレンマ消失。
            Assert.AreEqual(0f, SecurityDilemmaRules.Distinguishability(1f), Eps);
            // 全く区別できない(0)なら緩和なし＝倍率1。
            Assert.AreEqual(1f, SecurityDilemmaRules.Distinguishability(0f), Eps);
            // 中間。
            Assert.AreEqual(0.3f, SecurityDilemmaRules.Distinguishability(0.7f), Eps);
        }

        /// <summary>誰も望まない戦争＝螺旋×(1−偶発重み)＋偶発×偶発重み。偶発だけでも残る。</summary>
        [Test]
        public void UnwantedWar_螺旋と偶発の合成()
        {
            // 螺旋0.8×0.7＋偶発0.5×0.3＝0.56+0.15＝0.71。
            Assert.AreEqual(0.71f, SecurityDilemmaRules.UnwantedWar(0.8f, 0.5f), Eps);
            // 螺旋ゼロでも偶発だけで確率が残る＝0.4×0.3＝0.12。
            Assert.AreEqual(0.12f, SecurityDilemmaRules.UnwantedWar(0f, 0.4f), Eps);
        }

        /// <summary>信頼醸成措置＝透明性と自制が螺旋を緩める。双方満点で螺旋停止。</summary>
        [Test]
        public void TrustBuildingMeasures_透明性と自制で緩和()
        {
            // 透明性1・自制1＝trust1、倍率1−1*1＝0＝螺旋を完全に止める。
            Assert.AreEqual(0f, SecurityDilemmaRules.TrustBuildingMeasures(1f, 1f), Eps);
            // 透明性0.6・自制0.4＝trust0.5、倍率1−0.5＝0.5。
            Assert.AreEqual(0.5f, SecurityDilemmaRules.TrustBuildingMeasures(0.6f, 0.4f), Eps);
            // 何もしなければ緩和なし＝倍率1。
            Assert.AreEqual(1f, SecurityDilemmaRules.TrustBuildingMeasures(0f, 0f), Eps);
        }

        /// <summary>安全保障の螺旋判定＝螺旋強度が閾値以上。</summary>
        [Test]
        public void IsSecuritySpiral_閾値で判定()
        {
            Assert.IsTrue(SecurityDilemmaRules.IsSecuritySpiral(0.7f, 0.5f));
            Assert.IsTrue(SecurityDilemmaRules.IsSecuritySpiral(0.5f, 0.5f));
            Assert.IsFalse(SecurityDilemmaRules.IsSecuritySpiral(0.3f, 0.5f));
        }
    }
}
