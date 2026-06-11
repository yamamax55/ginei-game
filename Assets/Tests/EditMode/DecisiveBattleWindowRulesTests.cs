using NUnit.Framework;
using UnityEngine;

namespace Ginei.Tests
{
    /// <summary>
    /// DecisiveBattleWindowRules（決戦の機会窓口・SKUN-6 #1436）の純ロジック検証。
    /// 既定 Params（集結0.4/補給0.3/士気0.3・発火閾値0.6・フェード率0.25）で期待値を固定する。
    /// </summary>
    public class DecisiveBattleWindowRulesTests
    {
        const float Eps = 1e-4f;

        /// <summary>準備度＝戦力集結・補給・士気の重み付き平均（条件が揃うほど高い）。</summary>
        [Test]
        public void Readiness_重み付き平均で準備度を出す()
        {
            // 0.4*1 + 0.3*0.5 + 0.3*0 = 0.55、重み合計1.0
            float r = DecisiveBattleWindowRules.Readiness(1f, 0.5f, 0f);
            Assert.AreEqual(0.55f, r, Eps);
            // 全条件満点なら準備度1.0
            Assert.AreEqual(1f, DecisiveBattleWindowRules.Readiness(1f, 1f, 1f), Eps);
        }

        /// <summary>敵の脆弱性＝露出×疲弊（一方が0なら好機にならない）。</summary>
        [Test]
        public void EnemyVulnerability_露出と疲弊の積()
        {
            Assert.AreEqual(0.5f, DecisiveBattleWindowRules.EnemyVulnerability(1f, 0.5f), Eps);
            Assert.AreEqual(0f, DecisiveBattleWindowRules.EnemyVulnerability(0f, 1f), Eps); // 露出ゼロは脆弱でない
        }

        /// <summary>窓の開き＝準備度×敵脆弱性（両方揃ってはじめて開く）。</summary>
        [Test]
        public void WindowOpening_準備度と敵脆弱性の積()
        {
            Assert.AreEqual(0.48f, DecisiveBattleWindowRules.WindowOpening(0.8f, 0.6f), Eps);
            Assert.AreEqual(0f, DecisiveBattleWindowRules.WindowOpening(1f, 0f), Eps); // 敵が脆くないと開かない
        }

        /// <summary>窓の持続＝条件が保たれる間は維持・崩れると閉じる。</summary>
        [Test]
        public void WindowTick_条件が崩れると窓が閉じる()
        {
            // 条件完全保持なら不変
            Assert.AreEqual(0.8f, DecisiveBattleWindowRules.WindowTick(0.8f, 1f, 1f), Eps);
            // 保持0.5・dt1 → 0.8 - 0.25*(1-0.5)*1 = 0.8 - 0.125 = 0.675
            Assert.AreEqual(0.675f, DecisiveBattleWindowRules.WindowTick(0.8f, 0.5f, 1f), Eps);
        }

        /// <summary>決戦トリガー＝窓の開きが閾値0.6を超えたら発火（EventEngine への発火窓口）。</summary>
        [Test]
        public void DecisiveBattleTrigger_閾値超えで発火()
        {
            Assert.IsTrue(DecisiveBattleWindowRules.DecisiveBattleTrigger(0.6f));  // 境界＝発火
            Assert.IsTrue(DecisiveBattleWindowRules.DecisiveBattleTrigger(0.8f));
            Assert.IsFalse(DecisiveBattleWindowRules.DecisiveBattleTrigger(0.59f)); // 未熟＝不発
        }

        /// <summary>好機を逃すコスト＝窓の大きさ×ためらい（即断なら損失なし）。</summary>
        [Test]
        public void OpportunityCost_好機とためらいの積()
        {
            Assert.AreEqual(0.56f, DecisiveBattleWindowRules.OpportunityCost(0.7f, 0.8f), Eps);
            Assert.AreEqual(0f, DecisiveBattleWindowRules.OpportunityCost(0.9f, 0f), Eps); // 即断はコストゼロ
        }

        /// <summary>好機の消失＝時間で去る（条件に依らず単調に閉じる）。</summary>
        [Test]
        public void FleetingWindow_時間で窓が去る()
        {
            // 0.8 - 0.25*1 = 0.55
            Assert.AreEqual(0.55f, DecisiveBattleWindowRules.FleetingWindow(0.8f, 1f), Eps);
            // 十分な時間で完全に閉じる（クランプ0）
            Assert.AreEqual(0f, DecisiveBattleWindowRules.FleetingWindow(0.2f, 5f), Eps);
        }

        /// <summary>決戦戦果＝窓の開き×戦力比（機と力が揃うほど一挙に決まる）／決戦の瞬間判定。</summary>
        [Test]
        public void DecisiveOutcome_と_IsDecisiveMoment()
        {
            Assert.AreEqual(0.7f, DecisiveBattleWindowRules.DecisiveOutcomeMagnitude(1f, 0.7f), Eps);
            Assert.AreEqual(0.36f, DecisiveBattleWindowRules.DecisiveOutcomeMagnitude(0.6f, 0.6f), Eps);
            // 決戦すべき瞬間か（既定閾値0.6）
            Assert.IsTrue(DecisiveBattleWindowRules.IsDecisiveMoment(0.7f));
            Assert.IsFalse(DecisiveBattleWindowRules.IsDecisiveMoment(0.5f));
        }
    }
}
