using NUnit.Framework;
using UnityEngine;

namespace Ginei.Tests
{
    /// <summary>作戦適性ルール（#1063）の純ロジック検証。既定 Params の具体値で期待値を固定する。</summary>
    public class OperationalAptitudeRulesTests
    {
        /// <summary>S適性は能力を大幅に増す（既定1.5倍）＝得意な戦場では別人のように強い。</summary>
        [Test]
        public void SグレードはS倍率を返す()
        {
            Assert.AreEqual(1.5f, OperationalAptitudeRules.AptitudeMultiplier(AptitudeGrade.S), 1e-4f);
        }

        /// <summary>E適性は能力を大幅に削る（既定0.6倍）＝苦手な戦場では凡将に堕ちる。</summary>
        [Test]
        public void EグレードはE倍率を返す()
        {
            Assert.AreEqual(0.6f, OperationalAptitudeRules.AptitudeMultiplier(AptitudeGrade.E), 1e-4f);
        }

        /// <summary>実効能力＝基準×適性倍率。S適性は上限100でクランプ、E適性は素直に減る（基準非破壊）。</summary>
        [Test]
        public void 実効能力は適性で増減し上限でクランプ()
        {
            // 80×1.5=120 → 100 でクランプ。
            Assert.AreEqual(100f, OperationalAptitudeRules.EffectivePerformance(80f, AptitudeGrade.S), 1e-4f);
            // 50×0.6=30。
            Assert.AreEqual(30f, OperationalAptitudeRules.EffectivePerformance(50f, AptitudeGrade.E), 1e-4f);
        }

        /// <summary>適性スコアからS〜E等級へ：1.0→S・0.5→C・0.0→E（高いほど上位）。</summary>
        [Test]
        public void スコアから等級へ変換する()
        {
            Assert.AreEqual(AptitudeGrade.S, OperationalAptitudeRules.GradeFromScore(1.0f));
            Assert.AreEqual(AptitudeGrade.C, OperationalAptitudeRules.GradeFromScore(0.5f));
            Assert.AreEqual(AptitudeGrade.E, OperationalAptitudeRules.GradeFromScore(0.0f));
        }

        /// <summary>最も得意な戦闘類型を選ぶ＝この提督をどこで使うか（適材適所）。拠点侵攻がSなら侵攻向き。</summary>
        [Test]
        public void 最も得意な戦闘類型を選ぶ()
        {
            // [遭遇戦=B, 拠点侵攻=S, 拠点防衛=C] → 侵攻が最上位。
            var grades = new[] { AptitudeGrade.B, AptitudeGrade.S, AptitudeGrade.C };
            Assert.AreEqual(CombatType.拠点侵攻, OperationalAptitudeRules.BestCombatType(grades));
        }

        /// <summary>不適合の罰：得意分野(S)なら罰ゼロ、最苦手(E)で最大罰（既定0.5）＝守りの名将を攻めに使う愚。</summary>
        [Test]
        public void 不適合の罰は苦手なほど大きい()
        {
            Assert.AreEqual(0f, OperationalAptitudeRules.MismatchPenalty(CombatType.拠点侵攻, AptitudeGrade.S), 1e-4f);
            Assert.AreEqual(0.5f, OperationalAptitudeRules.MismatchPenalty(CombatType.拠点侵攻, AptitudeGrade.E), 1e-4f);
            // B(中庸)までは罰ゼロ、C以降で罰が立つ。
            Assert.AreEqual(0f, OperationalAptitudeRules.MismatchPenalty(CombatType.拠点防衛, AptitudeGrade.B), 1e-4f);
            Assert.Greater(OperationalAptitudeRules.MismatchPenalty(CombatType.拠点防衛, AptitudeGrade.C), 0f);
        }

        /// <summary>適性のぶつかり合い：攻め手S・守り手Eなら攻め圧倒（1.5/0.6=2.5）、逆なら守り有利。</summary>
        [Test]
        public void 攻防の適性差が戦闘を左右する()
        {
            // 攻め手S(1.5) ÷ 守り手E(0.6) = 2.5 ＝攻め手有利。
            Assert.AreEqual(2.5f, OperationalAptitudeRules.TerrainMatchBonus(AptitudeGrade.S, AptitudeGrade.E, CombatType.拠点侵攻), 1e-4f);
            // 攻め手E ÷ 守り手S = 0.4 ＝守り手有利（1未満）。
            Assert.Less(OperationalAptitudeRules.TerrainMatchBonus(AptitudeGrade.E, AptitudeGrade.S, CombatType.拠点侵攻), 1f);
        }

        /// <summary>C（中庸）適性はほぼ等倍（既定で約0.96倍）＝普通の戦場では普通の働き。</summary>
        [Test]
        public void 中庸の適性はほぼ等倍()
        {
            Assert.AreEqual(0.96f, OperationalAptitudeRules.AptitudeMultiplier(AptitudeGrade.C), 1e-4f);
        }
    }
}
