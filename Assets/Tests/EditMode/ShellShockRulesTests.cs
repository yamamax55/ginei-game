using NUnit.Framework;
using UnityEngine;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// シェルショック（戦闘消耗＝即時的な戦闘不能）：砲爆撃の衝撃・限界突破・機能停止・
    /// パニック伝染・ベテラン耐性・短期回復・慢性化リスク・部隊戦闘継続力。既定Paramsで期待値固定。
    /// </summary>
    public class ShellShockRulesTests
    {
        [Test]
        public void BombardmentShock_WeightedSum()
        {
            // (0.8*0.6 + 0.5*0.4)/1.0 = 0.68
            Assert.AreEqual(0.68f, ShellShockRules.BombardmentShock(0.8f, 0.5f), 1e-4f);
            // 砲爆撃が激しく長いほど大きい。
            Assert.AreEqual(0f, ShellShockRules.BombardmentShock(0f, 0f), 1e-4f);
            Assert.AreEqual(1f, ShellShockRules.BombardmentShock(1f, 1f), 1e-4f);
        }

        [Test]
        public void BreakingPoint_ResilienceDividesLoad()
        {
            // 耐性0：0.8/1.0=0.8、0.8^1.5=sqrt(0.512)=0.715542
            Assert.AreEqual(0.715542f, ShellShockRules.BreakingPoint(0.8f, 0f), 1e-3f);
            // 耐性1.0：0.8/2.0=0.4、0.4^1.5=sqrt(0.064)=0.252982
            Assert.AreEqual(0.252982f, ShellShockRules.BreakingPoint(0.8f, 1f), 1e-3f);
            // 耐性が高いほど限界突破は小さい。
            Assert.Greater(ShellShockRules.BreakingPoint(0.8f, 0f),
                           ShellShockRules.BreakingPoint(0.8f, 1f));
        }

        [Test]
        public void FunctionalShutdown_ShockTimesBreakingCappedByMax()
        {
            // 0.8*(0.68*0.715542) = 0.389255
            Assert.AreEqual(0.389255f, ShellShockRules.FunctionalShutdown(0.68f, 0.715542f), 1e-3f);
            // 上限 maxShutdown=0.8 を超えない。
            Assert.AreEqual(0.8f, ShellShockRules.FunctionalShutdown(1f, 1f), 1e-4f);
            Assert.AreEqual(0f, ShellShockRules.FunctionalShutdown(0f, 1f), 1e-4f);
        }

        [Test]
        public void PanicContagion_ScalesWithProximity()
        {
            // 0.4*0.5*0.5 = 0.1
            Assert.AreEqual(0.1f, ShellShockRules.PanicContagion(0.4f, 0.5f), 1e-4f);
            // 密集が無ければ伝染しない。
            Assert.AreEqual(0f, ShellShockRules.PanicContagion(0.9f, 0f), 1e-4f);
        }

        [Test]
        public void VeteranImmunity_WeightedSum()
        {
            // (0.9*0.6 + 0.5*0.4)/1.0 = 0.74
            Assert.AreEqual(0.74f, ShellShockRules.VeteranImmunity(0.9f, 0.5f), 1e-4f);
            Assert.AreEqual(1f, ShellShockRules.VeteranImmunity(1f, 1f), 1e-4f);
        }

        [Test]
        public void ShortTermRecovery_WeightedSum()
        {
            // (0.8*0.6 + 0.6*0.4)/1.0 = 0.72
            Assert.AreEqual(0.72f, ShellShockRules.ShortTermRecovery(0.8f, 0.6f), 1e-4f);
            // 離脱も休息も無ければ回復しない。
            Assert.AreEqual(0f, ShellShockRules.ShortTermRecovery(0f, 0f), 1e-4f);
        }

        [Test]
        public void ChronificationRisk_ScalesWithRepeatedExposure()
        {
            // 0.5*0.6*0.5 = 0.15
            Assert.AreEqual(0.15f, ShellShockRules.ChronificationRisk(0.5f, 0.6f), 1e-4f);
            // 反復曝露が無ければ慢性化しない。
            Assert.AreEqual(0f, ShellShockRules.ChronificationRisk(0.9f, 0f), 1e-4f);
        }

        [Test]
        public void UnitCombatCapacity_ImmunityRestoresLostMargin()
        {
            // base=1-0.3-0.1=0.6、restored=0.6+0.5*(1-0.6)=0.8
            Assert.AreEqual(0.8f, ShellShockRules.UnitCombatCapacity(0.3f, 0.1f, 0.5f), 1e-4f);
            // 完全崩壊・耐性なしで0。
            Assert.AreEqual(0f, ShellShockRules.UnitCombatCapacity(0.8f, 0.5f, 0f), 1e-4f);
            // 崩壊もパニックも無ければ満タン。
            Assert.AreEqual(1f, ShellShockRules.UnitCombatCapacity(0f, 0f, 0f), 1e-4f);
        }

        [Test]
        public void ClampInputs_NoOutOfRange()
        {
            // 範囲外入力でも出力は0..1に収まる。
            Assert.AreEqual(1f, ShellShockRules.BombardmentShock(5f, 5f), 1e-4f);
            Assert.AreEqual(0f, ShellShockRules.BreakingPoint(-1f, 0.5f), 1e-4f);
            Assert.AreEqual(0f, ShellShockRules.PanicContagion(-2f, 1f), 1e-4f);
            Assert.AreEqual(1f, ShellShockRules.UnitCombatCapacity(-1f, -1f, 5f), 1e-4f);
        }

        /// <summary>
        /// 物語テスト：新兵の小隊が激しい砲爆撃を浴び、限界を超えて機能停止し、
        /// 密集していたためパニックが伝染し戦闘継続力を失う。一方、同じ砲火を浴びても
        /// 歴戦のベテラン部隊は耐性で崩れず戦い続け、前線を離脱して休息すれば短期で立ち直る。
        /// </summary>
        [Test]
        public void Story_GreenTroopsBreakWhileVeteransEndure()
        {
            var p = ShellShockParams.Default;

            // 激しく長い砲爆撃。
            float shock = ShellShockRules.BombardmentShock(0.9f, 0.8f, p);

            // 新兵：耐性ほぼ無し → 大きく限界突破。
            float greenImmunity = ShellShockRules.VeteranImmunity(0.1f, 0.1f, p);
            float greenBreaking = ShellShockRules.BreakingPoint(0.9f, greenImmunity, p);
            float greenShutdown = ShellShockRules.FunctionalShutdown(shock, greenBreaking, p);

            // ベテラン：戦闘経験＋鍛錬で高い耐性 → 限界突破は小さい。
            float vetImmunity = ShellShockRules.VeteranImmunity(0.9f, 0.8f, p);
            float vetBreaking = ShellShockRules.BreakingPoint(0.9f, vetImmunity, p);
            float vetShutdown = ShellShockRules.FunctionalShutdown(shock, vetBreaking, p);

            Assert.Greater(vetImmunity, greenImmunity);
            Assert.Greater(greenShutdown, vetShutdown, "新兵の方が深く機能停止する");

            // 密集した新兵小隊はパニックが伝染し戦闘継続力を失う。
            float greenPanic = ShellShockRules.PanicContagion(greenShutdown, 0.9f, p);
            float greenCapacity = ShellShockRules.UnitCombatCapacity(greenShutdown, greenPanic, greenImmunity, p);

            // ベテランは耐性で底上げされ高い戦闘継続力を保つ。
            float vetPanic = ShellShockRules.PanicContagion(vetShutdown, 0.9f, p);
            float vetCapacity = ShellShockRules.UnitCombatCapacity(vetShutdown, vetPanic, vetImmunity, p);

            Assert.Greater(vetCapacity, greenCapacity, "ベテランの方が戦い続けられる");

            // 前線離脱＋休息で短期回復が大きく進む。
            float recovery = ShellShockRules.ShortTermRecovery(1f, 1f, p);
            Assert.Greater(recovery, 0.9f);

            // 反復して機能停止を受けた新兵は慢性化（長期トラウマ）リスクを抱える。
            float chronic = ShellShockRules.ChronificationRisk(greenShutdown, 0.8f, p);
            Assert.Greater(chronic, 0f);
        }
    }
}
