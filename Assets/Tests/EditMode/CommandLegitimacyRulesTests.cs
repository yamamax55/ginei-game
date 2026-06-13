using NUnit.Framework;
using UnityEngine;

namespace Ginei.Tests
{
    /// <summary>会戦指揮の正統性ロジック（#898）の純ロジックテスト。</summary>
    public class CommandLegitimacyRulesTests
    {
        /// <summary>正統性＝三要素の積。どれか一つでも欠ければ正統性は崩れる。</summary>
        [Test]
        public void CommandLegitimacy_三要素の積()
        {
            // 0.8 × 0.5 × 0.5 = 0.2
            Assert.AreEqual(0.2f, CommandLegitimacyRules.CommandLegitimacy(0.8f, 0.5f, 0.5f), 1e-5f);
            // 任命だけあっても本人の実績が0なら従わない
            Assert.AreEqual(0f, CommandLegitimacyRules.CommandLegitimacy(1f, 0f, 1f), 1e-5f);
            // 三拍子揃えば満額
            Assert.AreEqual(1f, CommandLegitimacyRules.CommandLegitimacy(1f, 1f, 1f), 1e-5f);
        }

        /// <summary>正統性が高いほど完全服従、低いほど不服従へ段階的に下がる（既定閾値）。</summary>
        [Test]
        public void ObedienceLevel_正統性で服従段階が決まる()
        {
            Assert.AreEqual(ObedienceLevel.完全服従, CommandLegitimacyRules.ObedienceLevel(0.9f));
            Assert.AreEqual(ObedienceLevel.渋々服従, CommandLegitimacyRules.ObedienceLevel(0.6f));
            Assert.AreEqual(ObedienceLevel.部分的不服従, CommandLegitimacyRules.ObedienceLevel(0.3f));
            Assert.AreEqual(ObedienceLevel.不服従, CommandLegitimacyRules.ObedienceLevel(0.1f));
            // 境界値はその段階に含む
            Assert.AreEqual(ObedienceLevel.完全服従, CommandLegitimacyRules.ObedienceLevel(0.75f));
        }

        /// <summary>命令実行倍率＝正統性が低いと鈍る（下限0.4..1）。基準非破壊の係数。</summary>
        [Test]
        public void OrderComplianceFactor_正統性が低いと命令が鈍る()
        {
            Assert.AreEqual(1f, CommandLegitimacyRules.OrderComplianceFactor(1f), 1e-5f);
            Assert.AreEqual(0.4f, CommandLegitimacyRules.OrderComplianceFactor(0f), 1e-5f);
            // 中間は線形：0.4 + 0.6×0.5 = 0.7
            Assert.AreEqual(0.7f, CommandLegitimacyRules.OrderComplianceFactor(0.5f), 1e-5f);
        }

        /// <summary>部分的不服従＝危険な命令ほど正統性が低いと拒まれる（不足分×危険度）。</summary>
        [Test]
        public void PartialDisobedienceRisk_危険な命令ほど拒まれる()
        {
            // 正統性0.2・危険度1.0 → (1-0.2)×1 = 0.8
            Assert.AreEqual(0.8f, CommandLegitimacyRules.PartialDisobedienceRisk(0.2f, 1f), 1e-5f);
            // 安全な命令（危険度0）は正統性が低くても拒まれない
            Assert.AreEqual(0f, CommandLegitimacyRules.PartialDisobedienceRisk(0.2f, 0f), 1e-5f);
            // 正統性が満点なら危険な命令でも通る
            Assert.AreEqual(0f, CommandLegitimacyRules.PartialDisobedienceRisk(1f, 1f), 1e-5f);
        }

        /// <summary>部分的不服従の判定は roll で決定論。</summary>
        [Test]
        public void PartialDisobedienceOccurs_rollで決定論()
        {
            // リスク0.8：roll=0.5 は拒否、roll=0.9 は従う
            Assert.IsTrue(CommandLegitimacyRules.PartialDisobedienceOccurs(0.2f, 1f, 0.5f));
            Assert.IsFalse(CommandLegitimacyRules.PartialDisobedienceOccurs(0.2f, 1f, 0.9f));
        }

        /// <summary>正統性欠如の士気ペナルティ＝(1−正統性)に比例（最大0.3）。</summary>
        [Test]
        public void MoralePenaltyFromIllegitimacy_納得なき指揮は士気を削る()
        {
            Assert.AreEqual(0f, CommandLegitimacyRules.MoralePenaltyFromIllegitimacy(1f), 1e-5f);
            Assert.AreEqual(0.3f, CommandLegitimacyRules.MoralePenaltyFromIllegitimacy(0f), 1e-5f);
            // 正統性0.5 → 0.5×0.3 = 0.15
            Assert.AreEqual(0.15f, CommandLegitimacyRules.MoralePenaltyFromIllegitimacy(0.5f), 1e-5f);
        }

        /// <summary>勝利は正統性を強化＝残り余地の15%を上乗せ。敗北は据え置き。</summary>
        [Test]
        public void AuthorityFromVictory_勝てば求心力が増す()
        {
            // 勝利：0.4 + (1-0.4)×0.15 = 0.49
            Assert.AreEqual(0.49f, CommandLegitimacyRules.AuthorityFromVictory(0.4f, true), 1e-5f);
            // 敗北：据え置き
            Assert.AreEqual(0.4f, CommandLegitimacyRules.AuthorityFromVictory(0.4f, false), 1e-5f);
            // 満点はこれ以上増えない
            Assert.AreEqual(1f, CommandLegitimacyRules.AuthorityFromVictory(1f, true), 1e-5f);
        }
    }
}
