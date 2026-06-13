using NUnit.Framework;
using UnityEngine;

namespace Ginei.Tests
{
    /// <summary>戦友紐帯（一次集団の戦友愛・RMK-2 #1405）の純ロジック検証。</summary>
    public class KameradschaftRulesTests
    {
        const float Eps = 1e-4f;

        /// <summary>一次集団の凝集＝苦難×時間×相互依存の積。三つ揃って高く、一つ欠ければ崩れる。</summary>
        [Test]
        public void PrimaryGroupCohesion_三要素の積()
        {
            // 0.8*0.9*0.5 = 0.36
            Assert.AreEqual(0.36f, KameradschaftRules.PrimaryGroupCohesion(0.8f, 0.9f, 0.5f), Eps);
            // 相互依存が0なら凝集も0（一つでも欠ければ戦友愛は育たない）
            Assert.AreEqual(0f, KameradschaftRules.PrimaryGroupCohesion(1f, 1f, 0f), Eps);
            // 全部最大で1
            Assert.AreEqual(1f, KameradschaftRules.PrimaryGroupCohesion(1f, 1f, 1f), Eps);
        }

        /// <summary>戦友愛→戦闘ボーナスは凝集の二乗・実効値≥1.0。深い戦友愛で踏ん張る。</summary>
        [Test]
        public void CohesionCombatBonus_凝集の二乗で戦闘力を支える()
        {
            // 凝集0で基準（1.0）
            Assert.AreEqual(1f, KameradschaftRules.CohesionCombatBonus(0f), Eps);
            // 凝集1で 1+0.3 = 1.3
            Assert.AreEqual(1.3f, KameradschaftRules.CohesionCombatBonus(1f), Eps);
            // 凝集0.5で 1+0.25*0.3 = 1.075（二乗ゆえ浅い間柄はほぼ無益）
            Assert.AreEqual(1.075f, KameradschaftRules.CohesionCombatBonus(0.5f), Eps);
            // 常に1.0以上
            Assert.GreaterOrEqual(KameradschaftRules.CohesionCombatBonus(0.3f), 1f);
        }

        /// <summary>大義より戦友＝戦友愛が主・思想は従。凝集高なら大義薄でも戦える。</summary>
        [Test]
        public void FightForComradesNotCause_一次集団が二次集団に勝る()
        {
            // 戦友愛1・大義0 → 0.7（思想なしでも戦える）
            Assert.AreEqual(0.7f, KameradschaftRules.FightForComradesNotCause(1f, 0f), Eps);
            // 戦友愛0・大義1 → 0.3（大義だけでは戦意は薄い）
            Assert.AreEqual(0.3f, KameradschaftRules.FightForComradesNotCause(0f, 1f), Eps);
            // 同じ値でも戦友愛の方が重い
            Assert.Greater(KameradschaftRules.FightForComradesNotCause(0.6f, 0.2f),
                           KameradschaftRules.FightForComradesNotCause(0.2f, 0.6f));
        }

        /// <summary>凝集は共闘で速く育つ＝修羅場が絆を深める。</summary>
        [Test]
        public void CohesionBuildTick_共闘で速く育つ()
        {
            // 平時（共闘0）：0.5 + 0.02*1 = 0.52
            Assert.AreEqual(0.52f, KameradschaftRules.CohesionBuildTick(0.5f, 0f, 1f), Eps);
            // 共闘1：rate = 0.02*(1+4) = 0.1 → 0.5+0.1 = 0.6
            Assert.AreEqual(0.6f, KameradschaftRules.CohesionBuildTick(0.5f, 1f, 1f), Eps);
            // 共闘の方が平時より速く深まる
            Assert.Greater(KameradschaftRules.CohesionBuildTick(0.3f, 1f, 1f),
                           KameradschaftRules.CohesionBuildTick(0.3f, 0f, 1f));
        }

        /// <summary>戦友の死の悲嘆は凝集の二乗×喪失割合。強い戦友愛ほど痛い。</summary>
        [Test]
        public void ComradeLossGrief_凝集が強いほど深い喪失()
        {
            // 凝集1・全喪失：1*1*0.7 = 0.7
            Assert.AreEqual(0.7f, KameradschaftRules.ComradeLossGrief(1f, 1f), Eps);
            // 凝集0.5・全喪失：0.25*1*0.7 = 0.175
            Assert.AreEqual(0.175f, KameradschaftRules.ComradeLossGrief(0.5f, 1f), Eps);
            // 喪失なしなら悲嘆なし
            Assert.AreEqual(0f, KameradschaftRules.ComradeLossGrief(1f, 0f), Eps);
            // 強い戦友愛ほど同じ喪失で痛い
            Assert.Greater(KameradschaftRules.ComradeLossGrief(0.9f, 0.5f),
                           KameradschaftRules.ComradeLossGrief(0.4f, 0.5f));
        }

        /// <summary>集団崩壊は損耗×凝集で戦闘力を削る＝戦友がいなくなると戦えない。</summary>
        [Test]
        public void GroupDissolutionPenalty_崩壊で戦闘力が崩れる()
        {
            // 凝集1・損耗1：1*1*0.6 = 0.6
            Assert.AreEqual(0.6f, KameradschaftRules.GroupDissolutionPenalty(1f, 1f), Eps);
            // 凝集0.5・損耗0.5：0.5*0.5*0.6 = 0.15
            Assert.AreEqual(0.15f, KameradschaftRules.GroupDissolutionPenalty(0.5f, 0.5f), Eps);
            // 損耗なしならペナルティなし
            Assert.AreEqual(0f, KameradschaftRules.GroupDissolutionPenalty(1f, 0f), Eps);
        }

        /// <summary>新兵補充は凝集を希釈＝よそ者は輪に入れない。</summary>
        [Test]
        public void ReplacementIntegration_新兵で凝集が薄まる()
        {
            // 補充なし（新兵0）なら凝集そのまま
            Assert.AreEqual(0.8f, KameradschaftRules.ReplacementIntegration(0.8f, 0f), Eps);
            // 凝集0.8・新兵0.5：新顔の凝集=0.8*(1-0.5)=0.4 → 0.8*0.5+0.4*0.5 = 0.6
            Assert.AreEqual(0.6f, KameradschaftRules.ReplacementIntegration(0.8f, 0.5f), Eps);
            // 補充が多いほど凝集が下がる
            Assert.Less(KameradschaftRules.ReplacementIntegration(0.8f, 0.8f),
                        KameradschaftRules.ReplacementIntegration(0.8f, 0.2f));
        }

        /// <summary>強い戦友愛で結ばれた部隊の判定＝凝集が閾値以上。</summary>
        [Test]
        public void IsBondedUnit_閾値で結束部隊を判定()
        {
            Assert.IsTrue(KameradschaftRules.IsBondedUnit(0.8f, 0.7f));
            Assert.IsFalse(KameradschaftRules.IsBondedUnit(0.5f, 0.7f));
            // 境界はちょうど閾値で結束
            Assert.IsTrue(KameradschaftRules.IsBondedUnit(0.7f, 0.7f));
        }
    }
}
