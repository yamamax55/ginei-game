using System.Collections.Generic;
using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>特殊作戦部隊：SEAL型多段選抜・選抜スコア・能力上昇・特殊作戦ボーナス。</summary>
    public class SpecialForcesRulesTests
    {
        [Test]
        public void SelectionScore_GritWeighted()
        {
            // 統率/機動重視（各0.4）＋攻撃0.2。統率100/機動100/攻撃0 → 80。
            Assert.AreEqual(80f, SpecialForcesRules.SelectionScore(100f, 100f, 0f), 1e-4f);
            Assert.AreEqual(50f, SpecialForcesRules.SelectionScore(50f, 50f, 50f), 1e-4f);
            Assert.Greater(SpecialForcesRules.SelectionScore(90f, 90f, 50f),
                           SpecialForcesRules.SelectionScore(50f, 50f, 90f)); // 不屈型が上
        }

        [Test]
        public void Funnel_AttritionLeavesFew()
        {
            // 16人 → 基礎0.5(8) → 地獄週0.5(4) → 卒業0.9(ceil 3.6=4)。上位スコアが残る。
            var cands = new List<SofCandidate>();
            for (int i = 0; i < 16; i++) cands.Add(new SofCandidate(i, i)); // score=i（id大ほど高得点）
            var passed = SpecialForcesRules.Funnel(cands);
            Assert.AreEqual(4, passed.Count);                 // 狭き門
            Assert.Contains(15, passed);                      // 最高得点は残る
            Assert.IsFalse(passed.Contains(0));               // 最低得点は脱落
            // 空・少人数の安全性
            Assert.AreEqual(0, SpecialForcesRules.Funnel(null).Count);
            Assert.AreEqual(1, SpecialForcesRules.Funnel(new List<SofCandidate> { new SofCandidate(7, 50f) }).Count);
        }

        [Test]
        public void QuotaPassing_PerStage()
        {
            Assert.AreEqual(8, SpecialForcesRules.QuotaPassing(16, SofStage.基礎課程));
            Assert.AreEqual(8, SpecialForcesRules.QuotaPassing(16, SofStage.地獄週));
            Assert.AreEqual(1, SpecialForcesRules.QuotaPassing(1, SofStage.地獄週)); // 1人以上は残す
            Assert.AreEqual(0, SpecialForcesRules.QuotaPassing(0, SofStage.基礎課程));
        }

        [Test]
        public void Factors_AdmiralUpliftAndSpecialOp()
        {
            Assert.AreEqual(1f, SpecialForcesRules.AdmiralCombatFactor(false), 1e-4f);
            Assert.AreEqual(1f + SpecialForcesRules.AdmiralCombatBonus, SpecialForcesRules.AdmiralCombatFactor(true), 1e-4f);

            // 特殊作戦（側背/包囲）時のみ追加。非SOF・通常攻撃では等倍。
            Assert.AreEqual(1f + SpecialForcesRules.SpecialOpBonus, SpecialForcesRules.SpecialOpFactor(true, true), 1e-4f);
            Assert.AreEqual(1f, SpecialForcesRules.SpecialOpFactor(true, false), 1e-4f);   // 通常攻撃
            Assert.AreEqual(1f, SpecialForcesRules.SpecialOpFactor(false, true), 1e-4f);   // 非SOF
        }
    }
}
