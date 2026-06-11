using NUnit.Framework;
using UnityEngine;

namespace Ginei.Tests
{
    /// <summary>長期遊撃戦の指揮官消耗（#1400）の純ロジックテスト。回復可能な運用疲弊・判断力低下を担保。</summary>
    public class CommanderBurdenRulesTests
    {
        private const float Tol = 1e-4f;

        /// <summary>負担の蓄積＝運用テンポ×責任の重さ×蓄積率ぶん疲労が積む（連日の判断が消耗させる）。</summary>
        [Test]
        public void BurdenAccumulation_AddsByTempoAndResponsibility()
        {
            // drive=0.8×0.5=0.4, +0.4×0.05×1=0.02
            float f = CommanderBurdenRules.BurdenAccumulation(0.3f, 0.8f, 0.5f, 1f);
            Assert.AreEqual(0.32f, f, Tol);
        }

        /// <summary>運用していなければ（テンポ0）疲労は積まない。</summary>
        [Test]
        public void BurdenAccumulation_NoTempo_NoGain()
        {
            float f = CommanderBurdenRules.BurdenAccumulation(0.4f, 0f, 1f, 5f);
            Assert.AreEqual(0.4f, f, Tol);
        }

        /// <summary>休息の回復＝恒久的な衰えと違い、休めば疲労が戻る（回復率0.08）。</summary>
        [Test]
        public void RestRecovery_ReducesFatigue()
        {
            // 0.6 - 1.0×0.08×1 = 0.52
            float f = CommanderBurdenRules.RestRecovery(0.6f, 1f, 1f);
            Assert.AreEqual(0.52f, f, Tol);
        }

        /// <summary>判断力低下＝疲労と睡眠不足が判断力を一時的に落とす（実効値＜1.0・疲れた指揮官は誤る）。</summary>
        [Test]
        public void JudgmentImpairment_FatigueAndSleepDebtLowerJudgment()
        {
            // load = clamp01(0.6 + 0.4×0.5)=0.8, 1 - 0.8×0.5 = 0.6
            float j = CommanderBurdenRules.JudgmentImpairment(0.6f, 0.4f);
            Assert.AreEqual(0.6f, j, Tol);
            // 万全（疲労・睡眠不足0）なら満額
            Assert.AreEqual(1f, CommanderBurdenRules.JudgmentImpairment(0f, 0f), Tol);
        }

        /// <summary>決断の質の劣化＝複雑な決断ほど判断力低下が響く（単純な決断は質を保てる）。</summary>
        [Test]
        public void DecisionQualityDecay_ComplexDecisionsSufferMore()
        {
            // 単純(0)は質満額、複雑(1)は判断力そのもの
            Assert.AreEqual(1f, CommanderBurdenRules.DecisionQualityDecay(0.6f, 0f), Tol);
            Assert.AreEqual(0.6f, CommanderBurdenRules.DecisionQualityDecay(0.6f, 1f), Tol);
            // 中間=Lerp(1,0.6,0.5)=0.8
            Assert.AreEqual(0.8f, CommanderBurdenRules.DecisionQualityDecay(0.6f, 0.5f), Tol);
        }

        /// <summary>抱え込みの罠＝委任できない判断負荷ほど疲労蓄積を加速する（倍率1以上）。</summary>
        [Test]
        public void MicromanagementTrap_LowDelegationAcceleratesBurden()
        {
            // unshed = 0.8×(1-0.2)=0.64, 1 + 0.64×0.5 = 1.32
            float m = CommanderBurdenRules.MicromanagementTrap(0.8f, 0.2f);
            Assert.AreEqual(1.32f, m, Tol);
            // 完全に委任できれば罠なし=1.0
            Assert.AreEqual(1f, CommanderBurdenRules.MicromanagementTrap(0.8f, 1f), Tol);
        }

        /// <summary>交代の回復＝交代要員がいれば休ませて回復できる／いなければ回復しない。</summary>
        [Test]
        public void RotationRelief_RequiresReplacement()
        {
            // 0.7 - 1.0×0.12×1 = 0.58
            Assert.AreEqual(0.58f, CommanderBurdenRules.RotationRelief(0.7f, 1f, 1f), Tol);
            // 交代要員なし=回復なし
            Assert.AreEqual(0.7f, CommanderBurdenRules.RotationRelief(0.7f, 0f, 1f), Tol);
        }

        /// <summary>燃え尽きリスク＝臨界(0.7)以下は0、超えて長く続くほど深刻な消耗に近づく。</summary>
        [Test]
        public void BurnoutRisk_RisesPastThresholdWithDuration()
        {
            // 臨界以下は0
            Assert.AreEqual(0f, CommanderBurdenRules.BurnoutRisk(0.6f, 1f), Tol);
            // overshoot=(0.85-0.7)/0.3=0.5, ×duration0.5 = 0.25
            Assert.AreEqual(0.25f, CommanderBurdenRules.BurnoutRisk(0.85f, 0.5f), Tol);
        }

        /// <summary>指揮官疲弊判定＝疲労が閾値超かつ判断力が閾値ぶん鈍ったとき真（休息・交代が要る）。</summary>
        [Test]
        public void IsCommandFatigued_FlagsHighFatigueAndLowJudgment()
        {
            // threshold 0.5: fatigue 0.7>0.5 かつ judgment 0.4<0.5 → 疲弊
            Assert.IsTrue(CommanderBurdenRules.IsCommandFatigued(0.7f, 0.4f, 0.5f));
            // 判断力がまだ十分(0.6)なら疲弊扱いしない
            Assert.IsFalse(CommanderBurdenRules.IsCommandFatigued(0.7f, 0.6f, 0.5f));
            // 疲労が低ければ疲弊しない
            Assert.IsFalse(CommanderBurdenRules.IsCommandFatigued(0.3f, 0.4f, 0.5f));
        }

        /// <summary>データ struct はコンストラクタで全フィールドを0..1にクランプする。</summary>
        [Test]
        public void CommanderBurden_ClampsFields()
        {
            var b = new CommanderBurden(1.5f, -0.2f, 0.5f);
            Assert.AreEqual(1f, b.fatigue, Tol);
            Assert.AreEqual(0f, b.decisionLoad, Tol);
            Assert.AreEqual(0.5f, b.sleepDebt, Tol);
        }
    }
}
