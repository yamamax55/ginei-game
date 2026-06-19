using NUnit.Framework;
using UnityEngine;

namespace Ginei.Tests
{
    /// <summary>指揮官個人の疲労・判断力低下の純ロジックテスト。既定Paramsの具体値で期待値を固定。</summary>
    public class CommanderFatigueRulesTests
    {
        private const float Eps = 1e-4f;
        private const float PowEps = 1e-3f;

        /// <summary>連戦（高強度×長期）で疲労が積み、体力が高いほど溜まりにくい。</summary>
        [Test]
        public void FatigueAccumulation_連戦で積み体力で緩む()
        {
            // 既定: accum0.08, staminaResistance1.0, intensity=1, days=5, sta=0.5
            //  divisor=1+0.5*1=1.5, add=1*5*0.08/1.5 = 0.4/1.5 = 0.26667
            Assert.AreEqual(0.4f / 1.5f, CommanderFatigueRules.FatigueAccumulation(1f, 5f, 50f), Eps);

            // 体力が高いほど蓄積は小さい
            float weak = CommanderFatigueRules.FatigueAccumulation(1f, 5f, 0f);
            float tough = CommanderFatigueRules.FatigueAccumulation(1f, 5f, 100f);
            Assert.Greater(weak, tough);
            // 体力0なら divisor=1 ＝ 0.4
            Assert.AreEqual(0.4f, weak, Eps);
        }

        /// <summary>休養日数×質で疲労を減らす＝休めば残存疲労が下がる（0未満には行かない）。</summary>
        [Test]
        public void RestRecovery_休養で疲労が減る()
        {
            // 既定: recovery0.1, fatigue=0.5, rest=3, quality=1 → 0.5 - 3*1*0.1 = 0.2
            Assert.AreEqual(0.2f, CommanderFatigueRules.RestRecovery(0.5f, 3f, 1f), Eps);

            // 質が低いと回復も鈍い
            Assert.AreEqual(0.35f, CommanderFatigueRules.RestRecovery(0.5f, 3f, 0.5f), Eps);

            // 過剰休養でも0未満には行かない
            Assert.AreEqual(0f, CommanderFatigueRules.RestRecovery(0.5f, 100f, 1f), Eps);
        }

        /// <summary>判断力低下は非線形＝浅いうちは粘り、終盤で急落する。</summary>
        [Test]
        public void JudgmentImpairment_非線形で終盤に急落()
        {
            // 既定 exponent=2, max=0.8: fatigue=0.5 → 0.25*0.8 = 0.2
            Assert.AreEqual(0.2f, CommanderFatigueRules.JudgmentImpairment(0.5f), PowEps);
            // fatigue=0.9 → 0.81*0.8 = 0.648
            Assert.AreEqual(0.648f, CommanderFatigueRules.JudgmentImpairment(0.9f), PowEps);
            // 疲労ゼロなら低下なし
            Assert.AreEqual(0f, CommanderFatigueRules.JudgmentImpairment(0f), Eps);

            // 0.5→0.9 の増分 > 0.1→0.5 の増分（凸＝終盤で急落）
            float low = CommanderFatigueRules.JudgmentImpairment(0.5f) - CommanderFatigueRules.JudgmentImpairment(0.1f);
            float high = CommanderFatigueRules.JudgmentImpairment(0.9f) - CommanderFatigueRules.JudgmentImpairment(0.5f);
            Assert.Greater(high, low);
        }

        /// <summary>集中の途切れは疲労×課題の複雑さ＝疲れた頭で複雑な局面ほど見落とす。</summary>
        [Test]
        public void ConcentrationLapse_疲労と複雑さの積()
        {
            // 既定 concentrationScale=0.6: fatigue=0.8, complexity=0.5 → 0.8*0.5*0.6 = 0.24
            Assert.AreEqual(0.24f, CommanderFatigueRules.ConcentrationLapse(0.8f, 0.5f), Eps);
            // 単純な課題なら途切れにくい
            Assert.Less(CommanderFatigueRules.ConcentrationLapse(0.8f, 0.1f),
                        CommanderFatigueRules.ConcentrationLapse(0.8f, 1f));
            // 疲労ゼロなら途切れなし
            Assert.AreEqual(0f, CommanderFatigueRules.ConcentrationLapse(0f, 1f), Eps);
        }

        /// <summary>誤判断リスクは判断低下×局面の重さ＝重い局面ほど一手の誤りが致命的。</summary>
        [Test]
        public void MisjudgmentRisk_判断低下と局面の重さ()
        {
            // 既定 misjudgmentScale=0.7: impair=0.5, stakes=0.8 → 0.5*0.8*0.7 = 0.28
            Assert.AreEqual(0.28f, CommanderFatigueRules.MisjudgmentRisk(0.5f, 0.8f), Eps);
            // 軽い局面なら誤判断の影響は小さい
            Assert.Less(CommanderFatigueRules.MisjudgmentRisk(0.5f, 0.1f),
                        CommanderFatigueRules.MisjudgmentRisk(0.5f, 1f));
            // 判断低下ゼロなら誤判断なし
            Assert.AreEqual(0f, CommanderFatigueRules.MisjudgmentRisk(0f, 1f), Eps);
        }

        /// <summary>実効能力＝基準×(1−判断低下)。基準は非破壊・低下ゼロで等倍。</summary>
        [Test]
        public void EffectiveCompetence_基準非破壊で低下を反映()
        {
            // base=80, impair=0.25 → 80*0.75 = 60
            Assert.AreEqual(60f, CommanderFatigueRules.EffectiveCompetence(80f, 0.25f), Eps);
            // 低下ゼロなら基準のまま
            Assert.AreEqual(80f, CommanderFatigueRules.EffectiveCompetence(80f, 0f), Eps);
            // 完全に判断不能なら0
            Assert.AreEqual(0f, CommanderFatigueRules.EffectiveCompetence(80f, 1f), Eps);
        }

        /// <summary>燃え尽き＝疲労が回復力を burnoutMargin 超える。</summary>
        [Test]
        public void BurnoutThreshold_回復力を超えると過労()
        {
            // 既定 margin=0.3: fatigue=0.6, resilience=20(=0.2) → 0.6-0.2=0.4 ≥ 0.3 → true
            Assert.IsTrue(CommanderFatigueRules.BurnoutThreshold(0.6f, 20f));
            // fatigue=0.4, resilience=20 → 0.4-0.2=0.2 < 0.3 → false
            Assert.IsFalse(CommanderFatigueRules.BurnoutThreshold(0.4f, 20f));
            // 高い回復力なら同じ疲労でも耐える
            Assert.IsFalse(CommanderFatigueRules.BurnoutThreshold(0.6f, 80f));
        }

        /// <summary>指揮適性＝実効能力が閾値以上。既定閾値40。</summary>
        [Test]
        public void IsFitForCommand_閾値で指揮可否()
        {
            // 既定 fitnessThreshold=40
            Assert.IsTrue(CommanderFatigueRules.IsFitForCommand(40f));
            Assert.IsTrue(CommanderFatigueRules.IsFitForCommand(60f));
            Assert.IsFalse(CommanderFatigueRules.IsFitForCommand(39f));
            // 明示閾値オーバーロード
            Assert.IsTrue(CommanderFatigueRules.IsFitForCommand(50f, 50f));
            Assert.IsFalse(CommanderFatigueRules.IsFitForCommand(49f, 50f));
        }

        /// <summary>物語：名将が連戦で消耗し誤判断の淵へ沈み、休養で立て直す一連の流れ。</summary>
        [Test]
        public void 物語_連戦の消耗から休養での立て直し()
        {
            // 統率80の名将。平時は判断低下ゼロ＝実効80で指揮適格。
            const float baseCompetence = 80f;
            float fresh = CommanderFatigueRules.EffectiveCompetence(baseCompetence, 0f);
            Assert.AreEqual(80f, fresh, Eps);
            Assert.IsTrue(CommanderFatigueRules.IsFitForCommand(fresh));

            // 高強度の作戦を体力50で20日連続＝疲労が積み上がる（clamp で上限）。
            float fatigue = CommanderFatigueRules.FatigueAccumulation(1f, 20f, 50f);
            Assert.Greater(fatigue, 0.9f);

            // 疲労が深く判断力が急落＝実効能力が落ちる。
            float impair = CommanderFatigueRules.JudgmentImpairment(fatigue);
            float worn = CommanderFatigueRules.EffectiveCompetence(baseCompetence, impair);
            Assert.Less(worn, fresh);

            // 重い局面では誤判断リスクが高い＋回復力を超えて燃え尽き。
            float risk = CommanderFatigueRules.MisjudgmentRisk(impair, 1f);
            Assert.Greater(risk, CommanderFatigueRules.MisjudgmentRisk(0f, 1f));
            Assert.IsTrue(CommanderFatigueRules.BurnoutThreshold(fatigue, 30f));

            // 後方で質の高い休養を10日＝疲労がほぼ抜ける。
            float rested = CommanderFatigueRules.RestRecovery(fatigue, 10f, 1f);
            Assert.Less(rested, fatigue);
            // 立て直し後は実効能力が回復し再び指揮適格。
            float recoveredImpair = CommanderFatigueRules.JudgmentImpairment(rested);
            float recoveredCompetence = CommanderFatigueRules.EffectiveCompetence(baseCompetence, recoveredImpair);
            Assert.Greater(recoveredCompetence, worn);
            Assert.IsTrue(CommanderFatigueRules.IsFitForCommand(recoveredCompetence));
        }
    }
}
