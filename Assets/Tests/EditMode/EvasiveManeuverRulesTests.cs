using NUnit.Framework;
using UnityEngine;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>回避機動：被弾低減と射撃精度のトレードオフ・敵追尾/予測の打ち消し・大型艦の鈍さ。</summary>
    public class EvasiveManeuverRulesTests
    {
        [Test]
        public void EvasionEffectiveness_AgilityTimesUnpredictability()
        {
            Assert.AreEqual(0.64f, EvasiveManeuverRules.EvasionEffectiveness(80f, 80f), 1e-4f); // 80*80*0.0001
            Assert.AreEqual(1f, EvasiveManeuverRules.EvasionEffectiveness(100f, 100f), 1e-4f);  // 上限クランプ
            Assert.AreEqual(0f, EvasiveManeuverRules.EvasionEffectiveness(0f, 100f), 1e-4f);    // 機動0＝回避不能
        }

        [Test]
        public void IncomingHitReduction_FallsWithEnemyTracking()
        {
            Assert.AreEqual(0.384f, EvasiveManeuverRules.IncomingHitReduction(0.64f, 0f), 1e-4f);  // 0.64*0.6*1
            Assert.AreEqual(0f, EvasiveManeuverRules.IncomingHitReduction(0.64f, 100f), 1e-4f);    // 追尾完璧＝効かない
            Assert.AreEqual(0.15f, EvasiveManeuverRules.IncomingHitReduction(0.5f, 50f), 1e-4f);   // 0.5*0.6*0.5
        }

        [Test]
        public void OwnAccuracyPenalty_RisesWithEvasion()
        {
            Assert.AreEqual(0.32f, EvasiveManeuverRules.OwnAccuracyPenalty(0.64f), 1e-4f); // 0.64*0.5
            Assert.AreEqual(0f, EvasiveManeuverRules.OwnAccuracyPenalty(0f), 1e-4f);       // 直進＝精度低下なし
        }

        [Test]
        public void EvasionEnergyCost_ScalesWithAgilityAndDt()
        {
            Assert.AreEqual(0.02f, EvasiveManeuverRules.EvasionEnergyCost(100f, 1f), 1e-4f); // 100*0.0002*1
            Assert.AreEqual(0.02f, EvasiveManeuverRules.EvasionEnergyCost(50f, 2f), 1e-4f);  // 50*0.0002*2
            Assert.AreEqual(0f, EvasiveManeuverRules.EvasionEnergyCost(100f, 0f), 1e-4f);    // dt0
        }

        [Test]
        public void AttackVsEvadeBalance_MapsAggression()
        {
            Assert.AreEqual(0f, EvasiveManeuverRules.AttackVsEvadeBalance(0.5f), 1e-4f);   // 中立
            Assert.AreEqual(-1f, EvasiveManeuverRules.AttackVsEvadeBalance(0f), 1e-4f);    // 全回避
            Assert.AreEqual(1f, EvasiveManeuverRules.AttackVsEvadeBalance(1f), 1e-4f);     // 全攻撃
            Assert.AreEqual(0.5f, EvasiveManeuverRules.AttackVsEvadeBalance(0.75f), 1e-4f);
        }

        [Test]
        public void JinkingPattern_DefeatedByPrediction()
        {
            Assert.AreEqual(1f, EvasiveManeuverRules.JinkingPattern(100f, 0f), 1e-4f);   // 不規則・予測なし＝完全に外す
            Assert.AreEqual(0f, EvasiveManeuverRules.JinkingPattern(100f, 100f), 1e-4f); // 予測完璧＝外せない
            Assert.AreEqual(0.4f, EvasiveManeuverRules.JinkingPattern(80f, 50f), 1e-4f); // 0.8*0.5
        }

        [Test]
        public void SizePenalty_LargerShipsEvadeWorse()
        {
            Assert.AreEqual(0.5f, EvasiveManeuverRules.SizePenalty(100f), 1e-4f);  // 基準質量
            Assert.AreEqual(0f, EvasiveManeuverRules.SizePenalty(0f), 1e-4f);      // 極小＝ペナルティなし
            Assert.AreEqual(0.75f, EvasiveManeuverRules.SizePenalty(300f), 1e-4f); // 300/400
            Assert.Less(EvasiveManeuverRules.SizePenalty(50f), EvasiveManeuverRules.SizePenalty(300f)); // 大きいほど重い
        }

        [Test]
        public void IsEvading_Threshold()
        {
            Assert.IsTrue(EvasiveManeuverRules.IsEvading(0.4f));   // 既定閾値0.3超
            Assert.IsFalse(EvasiveManeuverRules.IsEvading(0.2f));  // 未満
            Assert.IsTrue(EvasiveManeuverRules.IsEvading(0.3f));   // 境界＝成立
        }

        [Test]
        public void Narrative_JinkingCutsHitsButDullsAimAndLargeShipsCannotEvade()
        {
            // 機敏で不規則な小型艦は有効に回避し被弾を減らすが、自分の命中も落ちる（トレードオフ）。
            float eff = EvasiveManeuverRules.EvasionEffectiveness(90f, 90f);     // 0.81
            Assert.IsTrue(EvasiveManeuverRules.IsEvading(eff));
            float reduction = EvasiveManeuverRules.IncomingHitReduction(eff, 30f); // 被弾減
            float penalty = EvasiveManeuverRules.OwnAccuracyPenalty(eff);          // 自分の命中低下
            Assert.Greater(reduction, 0f);  // 被弾は減る
            Assert.Greater(penalty, 0f);    // だが自分も狙えない

            // 同じ機動・不規則さでも、大型艦は規模ペナルティで実効回避が削がれる＝回避しにくい。
            float smallNet = eff * (1f - EvasiveManeuverRules.SizePenalty(30f));
            float largeNet = eff * (1f - EvasiveManeuverRules.SizePenalty(400f));
            Assert.Greater(smallNet, largeNet); // 小型艦のほうがよく避ける
        }
    }
}
