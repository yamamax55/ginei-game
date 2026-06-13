using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 縦深防御線を固定する：縦深倍率の逓減（1+0.5+0.25…）、一点突破vs広正面のトレードオフ
    /// （集中1で破れ・均等0では互角以下＝0）、浸透は狭い突破口なら予備隊が塞ぎ広い崩壊は止まらない
    /// （予備応答0なら狭くても進む＝防衛線の本体は予備隊）、反撃の時間価値（早く浅いほど効く）、
    /// 突出部の逆包囲機会（肩が持つ時のみ）、連鎖崩壊（低士気は割合以上に崩れる）。
    /// </summary>
    public class DefenseLineRulesTests
    {
        private static readonly DefenseLineParams P = DefenseLineParams.Default;
        // 縦深逓減0.5/突破比2.0/浸透速度1.0/閉塞速度1.0/応答スケール1.0/包囲化深度2.0/伝播1.0

        [Test]
        public void LineStrength_DepthDiminishingReturns()
        {
            // 守備100×4正面：1層=400、2層=400×1.5=600、3層=400×1.75=700（重ねるほど強いが逓減）
            Assert.AreEqual(400f, DefenseLineRules.LineStrength(100f, 4, 1, P), 1e-3f);
            Assert.AreEqual(600f, DefenseLineRules.LineStrength(100f, 4, 2, P), 1e-3f);
            Assert.AreEqual(700f, DefenseLineRules.LineStrength(100f, 4, 3, P), 1e-3f);
            // 縦深0・正面0＝線は無い
            Assert.AreEqual(0f, DefenseLineRules.LineStrength(100f, 4, 0, P), 1e-5f);
            Assert.AreEqual(0f, DefenseLineRules.LineStrength(100f, 0, 3, P), 1e-5f);
        }

        [Test]
        public void BreakthroughChance_ConcentrationTradeoff()
        {
            // 攻撃400 vs 陣地守備200×4正面
            // 一点集中＝焦点に400全力＝比2.0＝突破確率1（ただし他正面は手付かず）
            Assert.AreEqual(1f, DefenseLineRules.BreakthroughChance(400f, 1f, 200f, 4, P), 1e-5f);
            // 広正面＝各100ずつ＝比0.5＝どこも破れない
            Assert.AreEqual(0f, DefenseLineRules.BreakthroughChance(400f, 0f, 200f, 4, P), 1e-5f);
            // 中間＝焦点に400×0.625=250＝比1.25＝確率0.25
            Assert.AreEqual(0.25f, DefenseLineRules.BreakthroughChance(400f, 0.5f, 200f, 4, P), 1e-5f);
            // 互角（比1.0）では破れない
            Assert.AreEqual(0f, DefenseLineRules.BreakthroughChance(200f, 1f, 200f, 4, P), 1e-5f);
        }

        [Test]
        public void PenetrationDepthTick_NarrowBreachSealedWideUnstoppable()
        {
            // 狭い突破口（幅0.4）＋即応予備＝前進0.4−閉塞0.6＝浸透は0.5→0.3へ押し戻される
            Assert.AreEqual(0.3f, DefenseLineRules.PenetrationDepthTick(0.5f, 0.4f, 1f, 1f, P), 1e-5f);
            // 広い崩壊（幅1.0）＝塞ぎようがなく止まらない＝0.5→1.5
            Assert.AreEqual(1.5f, DefenseLineRules.PenetrationDepthTick(0.5f, 1f, 1f, 1f, P), 1e-5f);
            // 予備応答0＝狭い突破口でも進み続ける＝防衛線の本体は前縁でなく予備隊
            Assert.AreEqual(0.7f, DefenseLineRules.PenetrationDepthTick(0.5f, 0.2f, 0f, 1f, P), 1e-5f);
            // 浸透は負にならない
            Assert.AreEqual(0f, DefenseLineRules.PenetrationDepthTick(0.1f, 0.1f, 1f, 1f, P), 1e-5f);
        }

        [Test]
        public void ReserveCounterattack_EarlierAndShallowerIsStronger()
        {
            // 予備100・即応・浸透ゼロ＝全力100
            Assert.AreEqual(100f, DefenseLineRules.ReserveCounterattack(100f, 0f, 0f, P), 1e-3f);
            // 応答スケール（1.0）経過で半減＝早いほど効く
            Assert.AreEqual(50f, DefenseLineRules.ReserveCounterattack(100f, 0f, 1f, P), 1e-3f);
            // 浸透1で半減＝浅いうちに叩く
            Assert.AreEqual(50f, DefenseLineRules.ReserveCounterattack(100f, 1f, 0f, P), 1e-3f);
            // 遅くて深い＝四半分
            Assert.AreEqual(25f, DefenseLineRules.ReserveCounterattack(100f, 1f, 1f, P), 1e-3f);
        }

        [Test]
        public void EncirclementOpportunity_DeepSalientWithHoldingFlanks()
        {
            // 肩が持ちこたえ＋浸透2.0（包囲化深度）＝機会1.0＝深入りした攻撃側が袋に入る
            Assert.AreEqual(1f, DefenseLineRules.EncirclementOpportunity(2f, true, P), 1e-5f);
            Assert.AreEqual(0.5f, DefenseLineRules.EncirclementOpportunity(1f, true, P), 1e-5f);
            // 肩が崩れていれば袋にならない＝0
            Assert.AreEqual(0f, DefenseLineRules.EncirclementOpportunity(2f, false, P), 1e-5f);
        }

        [Test]
        public void LineCollapseRisk_LowMoraleAmplifiesContagion()
        {
            // 4正面中2つ破られた：高士気＝割合どおり0.5
            Assert.AreEqual(0.5f, DefenseLineRules.LineCollapseRisk(2, 4, 1f, P), 1e-5f);
            // 士気ゼロ＝隣の崩壊で浮き足立ち2倍＝1.0
            Assert.AreEqual(1f, DefenseLineRules.LineCollapseRisk(2, 4, 0f, P), 1e-5f);
            // 中間士気＝0.5×1.5=0.75
            Assert.AreEqual(0.75f, DefenseLineRules.LineCollapseRisk(2, 4, 0.5f, P), 1e-5f);
            // 破られた正面なし・総正面0＝リスクなし
            Assert.AreEqual(0f, DefenseLineRules.LineCollapseRisk(0, 4, 0f, P), 1e-5f);
            Assert.AreEqual(0f, DefenseLineRules.LineCollapseRisk(2, 0, 0f, P), 1e-5f);
        }

        [Test]
        public void DefaultOverloads_MatchExplicitParams()
        {
            Assert.AreEqual(DefenseLineRules.LineStrength(100f, 4, 2, P),
                            DefenseLineRules.LineStrength(100f, 4, 2), 1e-5f);
            Assert.AreEqual(DefenseLineRules.BreakthroughChance(400f, 0.5f, 200f, 4, P),
                            DefenseLineRules.BreakthroughChance(400f, 0.5f, 200f, 4), 1e-5f);
            Assert.AreEqual(DefenseLineRules.PenetrationDepthTick(0.5f, 0.4f, 1f, 1f, P),
                            DefenseLineRules.PenetrationDepthTick(0.5f, 0.4f, 1f, 1f), 1e-5f);
            Assert.AreEqual(DefenseLineRules.ReserveCounterattack(100f, 1f, 1f, P),
                            DefenseLineRules.ReserveCounterattack(100f, 1f, 1f), 1e-5f);
            Assert.AreEqual(DefenseLineRules.EncirclementOpportunity(1f, true, P),
                            DefenseLineRules.EncirclementOpportunity(1f, true), 1e-5f);
            Assert.AreEqual(DefenseLineRules.LineCollapseRisk(2, 4, 0.5f, P),
                            DefenseLineRules.LineCollapseRisk(2, 4, 0.5f), 1e-5f);
        }
    }
}
