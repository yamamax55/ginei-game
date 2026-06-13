using NUnit.Framework;
using UnityEngine;

namespace Ginei.Tests
{
    /// <summary>会戦テンポ（勢いの振り戻し/減衰）の純ロジック検証。既定 Params で期待値を固定。</summary>
    public class BattleTempoRulesTests
    {
        const float Eps = 1e-4f;

        [Test]
        public void Momentum_RelativeGainDiff()
        {
            // 戦果なしは拮抗0。自軍3:敵1で (3-1)/4=0.5。
            Assert.AreEqual(0f, BattleTempoRules.Momentum(0f, 0f), Eps);
            Assert.AreEqual(0.5f, BattleTempoRules.Momentum(3f, 1f), Eps);
            Assert.AreEqual(-0.5f, BattleTempoRules.Momentum(1f, 3f), Eps);
        }

        [Test]
        public void MomentumDecay_CoolsTowardNeutral()
        {
            // 0.8 × (1 - 0.5×0.5) = 0.8 × 0.75 = 0.6（攻め疲れで中庸へ）。
            Assert.AreEqual(0.6f, BattleTempoRules.MomentumDecay(0.8f, 0.5f), Eps);
            // 大きな dt でも符号反転せず0で下げ止まる。
            Assert.AreEqual(0f, BattleTempoRules.MomentumDecay(0.8f, 100f), Eps);
        }

        [Test]
        public void SwingBack_UnderdogPushesTowardEven()
        {
            // pull = 1 - resolve×swingStrength。resolve0.5 → 1-0.5×0.6=0.7 → 0.8×0.7=0.56。
            Assert.AreEqual(0.56f, BattleTempoRules.SwingBack(0.8f, 0.5f), Eps);
            // 全力の踏ん張り resolve1.0 → 1-0.6=0.4 → 0.32（拮抗側へ大きく振り戻す）。
            Assert.AreEqual(0.32f, BattleTempoRules.SwingBack(0.8f, 1.0f), Eps);
            // resolve0 は不変。
            Assert.AreEqual(0.8f, BattleTempoRules.SwingBack(0.8f, 0f), Eps);
        }

        [Test]
        public void TempoPhase_PushDrawPressed()
        {
            Assert.AreEqual(1, BattleTempoRules.TempoPhase(0.5f));
            Assert.AreEqual(0, BattleTempoRules.TempoPhase(0.05f)); // 不感帯内＝拮抗
            Assert.AreEqual(-1, BattleTempoRules.TempoPhase(-0.5f));
        }

        [Test]
        public void AvalancheDamping_ExtremeRatioDampsHardest()
        {
            // 拮抗1:1 は等倍。4:1 は pow(1/4, 0.5)=0.5。
            Assert.AreEqual(1f, BattleTempoRules.AvalancheDamping(1f), Eps);
            Assert.AreEqual(0.5f, BattleTempoRules.AvalancheDamping(4f), Eps);
            // 比<1 は1へクランプ＝等倍。
            Assert.AreEqual(1f, BattleTempoRules.AvalancheDamping(0.25f), Eps);
        }

        [Test]
        public void ClimaxIntensity_VolatilityConcave()
        {
            // 0.5×(2-0.5)=0.75。揺れ最大1で1、なしで0。
            Assert.AreEqual(0.75f, BattleTempoRules.ClimaxIntensity(0.5f), Eps);
            Assert.AreEqual(1f, BattleTempoRules.ClimaxIntensity(1f), Eps);
            Assert.AreEqual(0f, BattleTempoRules.ClimaxIntensity(0f), Eps);
        }

        [Test]
        public void DecisiveWindow_AndStalemate()
        {
            // 既定 decisiveThreshold0.6：0.7で決定機・0.5で否。
            Assert.IsTrue(BattleTempoRules.DecisiveWindow(0.7f));
            Assert.IsFalse(BattleTempoRules.DecisiveWindow(0.5f));
            // 既定 stalemate閾値0.15/時間8：拮抗0.1が10秒で膠着・3秒では否。
            Assert.IsTrue(BattleTempoRules.IsStalemate(0.1f, 10f));
            Assert.IsFalse(BattleTempoRules.IsStalemate(0.1f, 3f));
        }

        [Test]
        public void Story_OverwhelmingPushDoesNotAvalanche()
        {
            // 圧倒的優勢(0.9)でも、攻め疲れ(減衰)＋劣勢側の踏ん張り(振り戻し)で勢いが冷め、戦況が往復する。
            float m0 = 0.9f;
            Assert.AreEqual(1, BattleTempoRules.TempoPhase(m0));          // 押している
            Assert.IsTrue(BattleTempoRules.DecisiveWindow(m0));           // 一度は決定機

            float decayed = BattleTempoRules.MomentumDecay(m0, 0.5f);     // 0.9×0.75=0.675（攻め疲れ）
            Assert.AreEqual(0.675f, decayed, Eps);

            float swung = BattleTempoRules.SwingBack(decayed, 0.7f);      // pull=1-0.7×0.6=0.58 → 0.3915
            Assert.AreEqual(0.3915f, swung, Eps);

            // 振り戻し後は決定機を割り込み、まだ押してはいるが緊張が続く局面に戻る。
            Assert.Less(swung, m0);
            Assert.IsFalse(BattleTempoRules.DecisiveWindow(swung));
            Assert.AreEqual(1, BattleTempoRules.TempoPhase(swung));
        }
    }
}
