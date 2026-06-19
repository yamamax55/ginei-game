using NUnit.Framework;
using UnityEngine;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 軍艦設計（攻防走のトレードオフ・WarshipDesignRules）を固定する。排水量の配分正規化、技術底上げ、
    /// 大型ほど鈍る速力、特化型と汎用型の対比（特化ボーナス↔設計均整）、過積載ペナルティ、総合戦闘力。
    /// 既定 Params の期待値を手計算で検算して凍結する。
    /// </summary>
    public class WarshipDesignRulesTests
    {
        [Test]
        public void TonnageAllocation_NormalizesToFirepowerShare()
        {
            // 合計1ちょうど→そのまま火力ぶん
            Assert.AreEqual(0.5f, WarshipDesignRules.TonnageAllocation(0.5f, 0.3f, 0.2f), 1e-4f);
            // 過配分（合計1.8）→按分（0.6/1.8）
            Assert.AreEqual(1f / 3f, WarshipDesignRules.TonnageAllocation(0.6f, 0.6f, 0.6f), 1e-4f);
            // 全て0→配分なし
            Assert.AreEqual(0f, WarshipDesignRules.TonnageAllocation(0f, 0f, 0f), 1e-4f);
        }

        [Test]
        public void FirepowerRating_TechLevelLiftsRating()
        {
            // tech1.0→満額（0.8）、tech0→下駄0.5倍（0.4）
            Assert.AreEqual(0.8f, WarshipDesignRules.FirepowerRating(0.8f, 1f), 1e-4f);
            Assert.AreEqual(0.4f, WarshipDesignRules.FirepowerRating(0.8f, 0f), 1e-4f);
            Assert.Greater(WarshipDesignRules.FirepowerRating(0.8f, 1f), WarshipDesignRules.FirepowerRating(0.8f, 0f));
        }

        [Test]
        public void ArmorRating_TechLevelLiftsRating()
        {
            // tech0.5→倍率0.75（0.6×0.75=0.45）
            Assert.AreEqual(0.45f, WarshipDesignRules.ArmorRating(0.6f, 0.5f), 1e-4f);
        }

        [Test]
        public void SpeedRating_LargerHullIsSlower()
        {
            // トン数0→0.6/1、トン数4→0.6/5＝大型ほど鈍い
            Assert.AreEqual(0.6f, WarshipDesignRules.SpeedRating(0.6f, 0f), 1e-4f);
            Assert.AreEqual(0.12f, WarshipDesignRules.SpeedRating(0.6f, 4f), 1e-4f);
            Assert.Greater(WarshipDesignRules.SpeedRating(0.6f, 0f), WarshipDesignRules.SpeedRating(0.6f, 4f));
        }

        [Test]
        public void DesignBalance_BalancedBeatsLopsided()
        {
            // 均整（0.8,0.8,0.8）→幾何平均0.8
            Assert.AreEqual(0.8f, WarshipDesignRules.DesignBalance(0.8f, 0.8f, 0.8f), 1e-3f);
            // 偏り（0.9,0.9,0.1）→pow(0.081,1/3)≈0.4327
            Assert.AreEqual(Mathf.Pow(0.081f, 1f / 3f), WarshipDesignRules.DesignBalance(0.9f, 0.9f, 0.1f), 1e-3f);
            Assert.Greater(WarshipDesignRules.DesignBalance(0.8f, 0.8f, 0.8f),
                WarshipDesignRules.DesignBalance(0.9f, 0.9f, 0.1f));
        }

        [Test]
        public void SpecializationBonus_ProminenceRaisesBonus()
        {
            // 完全均等（1/3）→0、単独突出（1.0）→満額0.5
            Assert.AreEqual(0f, WarshipDesignRules.SpecializationBonus(1f / 3f), 1e-4f);
            Assert.AreEqual(0.5f, WarshipDesignRules.SpecializationBonus(1f), 1e-4f);
            Assert.Greater(WarshipDesignRules.SpecializationBonus(0.7f), WarshipDesignRules.SpecializationBonus(0.4f));
        }

        [Test]
        public void OverloadPenalty_AppliesOnlyAboveCapacity()
        {
            // 積載内（0.9）→0、過積載（1.3）→0.3
            Assert.AreEqual(0f, WarshipDesignRules.OverloadPenalty(0.9f), 1e-4f);
            Assert.AreEqual(0.3f, WarshipDesignRules.OverloadPenalty(1.3f), 1e-4f);
        }

        [Test]
        public void CombatEffectiveness_OverloadCutsEffectiveness()
        {
            // 過積載なし→三定格平均0.8、過積載0.3→0.5
            Assert.AreEqual(0.8f, WarshipDesignRules.CombatEffectiveness(0.8f, 0.8f, 0.8f, 0f), 1e-4f);
            Assert.AreEqual(0.5f, WarshipDesignRules.CombatEffectiveness(0.8f, 0.8f, 0.8f, 0.3f), 1e-4f);
        }

        [Test]
        public void InputsAreClampedAndSafe()
        {
            // 範囲外入力でも 0..1 に収まり例外なし
            Assert.AreEqual(1f, WarshipDesignRules.FirepowerRating(5f, 5f), 1e-4f);   // 配分>1・tech>1
            Assert.AreEqual(0f, WarshipDesignRules.SpeedRating(-1f, -5f), 1e-4f);     // 負入力
            float eff = WarshipDesignRules.CombatEffectiveness(2f, 2f, 2f, 2f);       // 全て過大
            Assert.GreaterOrEqual(eff, 0f);
            Assert.LessOrEqual(eff, 1f);
        }

        // ─── 物語テスト：特化型の高速戦艦 vs 汎用型の標準艦 ───────────────────
        [Test]
        public void Narrative_FastSpecialistVersusGeneralist()
        {
            var p = WarshipDesignParams.Default;
            float tech = 1f;

            // 高速特化型：機動へ大きく配分（火力0.2/装甲0.2/機動0.6）の小型艦
            float fastF = WarshipDesignRules.FirepowerRating(0.2f, tech, p);
            float fastA = WarshipDesignRules.ArmorRating(0.2f, tech, p);
            float fastS = WarshipDesignRules.SpeedRating(0.6f, 0f, p);
            float fastSpec = WarshipDesignRules.SpecializationBonus(0.6f, p);
            float fastBalance = WarshipDesignRules.DesignBalance(fastF, fastA, fastS, p);

            // 汎用型：均等配分（0.34/0.33/0.33）の標準艦
            float genF = WarshipDesignRules.FirepowerRating(0.34f, tech, p);
            float genA = WarshipDesignRules.ArmorRating(0.33f, tech, p);
            float genS = WarshipDesignRules.SpeedRating(0.33f, 0f, p);
            float genSpec = WarshipDesignRules.SpecializationBonus(0.34f, p);
            float genBalance = WarshipDesignRules.DesignBalance(genF, genA, genS, p);

            // 特化型は機動定格と特化ボーナスで勝るが、設計均整は汎用型が勝る（偏りで下がる）
            Assert.Greater(fastS, genS);              // 速い
            Assert.Greater(fastSpec, genSpec);        // 特化が効く
            Assert.Greater(genBalance, fastBalance);  // 汎用は均整が取れている

            // 過積載した特化型（合計1.4）は戦闘力を削られる
            float overload = WarshipDesignRules.OverloadPenalty(1.4f, p);
            float trim = WarshipDesignRules.CombatEffectiveness(fastF, fastA, fastS, 0f, p);
            float bloated = WarshipDesignRules.CombatEffectiveness(fastF, fastA, fastS, overload, p);
            Assert.Greater(trim, bloated);
        }
    }
}
