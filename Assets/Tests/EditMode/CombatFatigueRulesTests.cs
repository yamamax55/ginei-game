using NUnit.Framework;
using UnityEngine;

namespace Ginei.Tests
{
    /// <summary>累積戦闘疲弊（#1403・レマルク西部戦線型）の純ロジックテスト。既定Paramsの具体値で期待値を固定。</summary>
    public class CombatFatigueRulesTests
    {
        private const float Eps = 1e-4f;

        /// <summary>激戦と損害が疲弊を蓄積する（会戦をまたいで溜まる）。</summary>
        [Test]
        public void FatigueAccumulation_激戦と損害で疲弊が積む()
        {
            // 既定: 激戦0.03 + 損害0.04, intensity=1, casualties=1, dt=1 → +0.07
            float next = CombatFatigueRules.FatigueAccumulation(0.2f, 1f, 1f, 1f);
            Assert.AreEqual(0.27f, next, Eps);

            // 蓄積は単調＝休まなければ積み続ける
            float later = CombatFatigueRules.FatigueAccumulation(next, 0.5f, 0f, 1f);
            Assert.Greater(later, next);
        }

        /// <summary>休息で部分回復するが、癒えぬ床までしか抜けない（戦争の傷）。</summary>
        [Test]
        public void RestRecovery_休息で部分回復するが床は残る()
        {
            // 既定: 回復0.025, rest=1, dt=1 → 0.5 - 0.025 = 0.475
            float rested = CombatFatigueRules.RestRecovery(0.5f, 1f, 1f);
            Assert.AreEqual(0.475f, rested, Eps);

            // 大量休息でも床(0.1)より下へは抜けない
            float floored = CombatFatigueRules.RestRecovery(0.5f, 1f, 100f);
            Assert.AreEqual(0.1f, floored, Eps);
        }

        /// <summary>蓄積と回復の差し引き＝連戦は回復が追いつかず疲弊が増える。</summary>
        [Test]
        public void NetFatigueTick_連戦は回復が追いつかない()
        {
            // 連戦: intensity=1(0.03), rest=0.2(0.025*0.2=0.005) → +0.025 で増える
            float warHeavy = CombatFatigueRules.NetFatigueTick(0.4f, 1f, 0.2f, 1f);
            Assert.Greater(warHeavy, 0.4f);
            Assert.AreEqual(0.4f + 0.025f, warHeavy, Eps);

            // 十分休めば回復: intensity=0, rest=1(-0.025) → 減る
            float peace = CombatFatigueRules.NetFatigueTick(0.4f, 0f, 1f, 1f);
            Assert.Less(peace, 0.4f);
            Assert.AreEqual(0.375f, peace, Eps);
        }

        /// <summary>累積疲弊が士気と戦闘効率を持続的に削る（実効値・1で無減衰）。</summary>
        [Test]
        public void Penalty_疲弊が士気と戦闘効率を削る()
        {
            // 既定 moralePenaltyScale=0.6, fatigue=0.5 → 1 - 0.5*0.6 = 0.7
            Assert.AreEqual(0.7f, CombatFatigueRules.MoralePenaltyFromFatigue(0.5f), Eps);
            Assert.AreEqual(0.7f, CombatFatigueRules.CombatEffectivenessDecay(0.5f), Eps);

            // 疲弊ゼロなら無減衰
            Assert.AreEqual(1f, CombatFatigueRules.MoralePenaltyFromFatigue(0f), Eps);
            Assert.AreEqual(1f, CombatFatigueRules.CombatEffectivenessDecay(0f), Eps);
        }

        /// <summary>シェルショックは極度の疲弊と突発的衝撃の積で生じる。</summary>
        [Test]
        public void ShellShockRisk_疲弊と衝撃の積()
        {
            Assert.AreEqual(0.72f, CombatFatigueRules.ShellShockRisk(0.9f, 0.8f), Eps);
            // どちらか欠ければ生じない
            Assert.AreEqual(0f, CombatFatigueRules.ShellShockRisk(0f, 1f), Eps);
            Assert.AreEqual(0f, CombatFatigueRules.ShellShockRisk(1f, 0f), Eps);
        }

        /// <summary>無感覚・厭戦は疲弊が onset を超えると脱走・自壊を上乗せする。</summary>
        [Test]
        public void NumbnessAndAttrition_疲弊が極まると脱走が増える()
        {
            // 既定 onset=0.7, attritionScale=0.05, fatigue=0.9 → (0.9-0.7)*0.05 = 0.01
            Assert.AreEqual(0.01f, CombatFatigueRules.NumbnessAndAttrition(0.9f, 1f), Eps);
            // onset 未満は上乗せなし
            Assert.AreEqual(0f, CombatFatigueRules.NumbnessAndAttrition(0.6f, 1f), Eps);
        }

        /// <summary>歴戦の兵は疲弊に耐性があるが限界はある（完全には消えない）。</summary>
        [Test]
        public void VeteranResilience_歴戦は耐えるが限界がある()
        {
            // 既定 veteranMitigation=0.3, exp=1 → fatigue*(1-0.3)
            Assert.AreEqual(0.56f, CombatFatigueRules.VeteranResilience(0.8f, 1f), Eps);
            // 新兵(exp=0)は軽減なし
            Assert.AreEqual(0.8f, CombatFatigueRules.VeteranResilience(0.8f, 0f), Eps);
            // ベテランでも疲弊は残る＝0にはならない
            Assert.Greater(CombatFatigueRules.VeteranResilience(0.8f, 1f), 0f);
        }

        /// <summary>疲弊が閾値に達すると燃え尽き＝戦力にならない。</summary>
        [Test]
        public void IsBurnedOut_閾値で燃え尽き判定()
        {
            // 既定 burnoutThreshold=0.85
            Assert.IsTrue(CombatFatigueRules.IsBurnedOut(0.85f));
            Assert.IsTrue(CombatFatigueRules.IsBurnedOut(0.9f));
            Assert.IsFalse(CombatFatigueRules.IsBurnedOut(0.84f));
            // 明示閾値オーバーロード
            Assert.IsTrue(CombatFatigueRules.IsBurnedOut(0.5f, 0.5f));
        }
    }
}
