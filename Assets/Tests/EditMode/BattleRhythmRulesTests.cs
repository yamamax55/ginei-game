using NUnit.Framework;
using UnityEngine;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>BattleRhythmRules（拍子と戦機窓＝五輪書の拍子・#1376）の純ロジック検証。</summary>
    public class BattleRhythmRulesTests
    {
        private const float Eps = 1e-4f;

        /// <summary>敵の拍子を読む力＝知覚×敵の読みやすさの積。どちらか0なら読めない。</summary>
        [Test]
        public void RhythmReading_知覚と敵の読みやすさの積()
        {
            Assert.AreEqual(0.5f, BattleRhythmRules.RhythmReading(1f, 0.5f), Eps);
            Assert.AreEqual(0.48f, BattleRhythmRules.RhythmReading(0.8f, 0.6f), Eps);
            // 知覚が鈍れば読めない
            Assert.AreEqual(0f, BattleRhythmRules.RhythmReading(0f, 1f), Eps);
            // 敵が拍子を隠せば読めない
            Assert.AreEqual(0f, BattleRhythmRules.RhythmReading(1f, 0f), Eps);
        }

        /// <summary>乗りの拍子＝勢い×拍子を読む力。勢いに乗ると力少なく勝てる。</summary>
        [Test]
        public void RidingRhythm_勢いと拍子読みの積()
        {
            Assert.AreEqual(0.56f, BattleRhythmRules.RidingRhythm(0.8f, 0.7f), Eps);
            Assert.AreEqual(0f, BattleRhythmRules.RidingRhythm(0f, 1f), Eps);
            Assert.AreEqual(1f, BattleRhythmRules.RidingRhythm(1f, 1f), Eps);
        }

        /// <summary>崩しの拍子＝拍子読み×(1−敵の平静)。敵の調子が狂うほど崩せる。</summary>
        [Test]
        public void BreakingRhythm_敵の平静が乱れるほど崩せる()
        {
            // 拍子読み0.8・敵平静0.25 → 0.8*0.75 = 0.6
            Assert.AreEqual(0.6f, BattleRhythmRules.BreakingRhythm(0.8f, 0.25f), Eps);
            // 敵が落ち着いていれば崩せない
            Assert.AreEqual(0f, BattleRhythmRules.BreakingRhythm(0.8f, 1f), Eps);
            // 敵が完全に崩れていれば拍子読みぶん通る
            Assert.AreEqual(0.8f, BattleRhythmRules.BreakingRhythm(0.8f, 0f), Eps);
        }

        /// <summary>後の先＝拍子読み×敵の踏み込み×倍率(0.6)。敵が深く攻めるほど隙が大きい。</summary>
        [Test]
        public void GoNoSen_敵が踏み込んだ直後の隙を打つ()
        {
            // 拍子読み1.0・踏み込み1.0・倍率0.6 → 0.6
            Assert.AreEqual(0.6f, BattleRhythmRules.GoNoSen(1f, 1f), Eps);
            // 拍子読み0.8・踏み込み0.5・倍率0.6 → 0.24
            Assert.AreEqual(0.24f, BattleRhythmRules.GoNoSen(0.8f, 0.5f), Eps);
            // 敵が動かなければ後の先は成立しない
            Assert.AreEqual(0f, BattleRhythmRules.GoNoSen(1f, 0f), Eps);
        }

        /// <summary>拍子の一致ボーナス＝基準1.0+0.5×拍子×状況適合。拍子に合えば効率↑（≧1.0）。</summary>
        [Test]
        public void RhythmMatchBonus_拍子が合えば力少なく勝つ()
        {
            // 拍子1.0・適合1.0 → 1.0 + 0.5 = 1.5
            Assert.AreEqual(1.5f, BattleRhythmRules.RhythmMatchBonus(1f, 1f), Eps);
            // 拍子0.8・適合0.5 → 1.0 + 0.5*0.4 = 1.2
            Assert.AreEqual(1.2f, BattleRhythmRules.RhythmMatchBonus(0.8f, 0.5f), Eps);
            // 状況に合わなければボーナスなし
            Assert.AreEqual(1f, BattleRhythmRules.RhythmMatchBonus(1f, 0f), Eps);
        }

        /// <summary>拍子外しのペナルティ＝基準1.0−0.5×(1−拍子読み)×自分の動き。外せば力あっても負ける（≦1.0）。</summary>
        [Test]
        public void RhythmMismatchPenalty_拍子を外せば力あっても勝てない()
        {
            // 拍子読み0・自分の動き1.0 → 1.0 - 0.5*1*1 = 0.5（強く動くほど外しの損が大きい）
            Assert.AreEqual(0.5f, BattleRhythmRules.RhythmMismatchPenalty(0f, 1f), Eps);
            // 拍子をよく読めばずれない
            Assert.AreEqual(1f, BattleRhythmRules.RhythmMismatchPenalty(1f, 1f), Eps);
            // 拍子読み0.6・自分の動き0.5 → 1.0 - 0.5*0.4*0.5 = 0.9
            Assert.AreEqual(0.9f, BattleRhythmRules.RhythmMismatchPenalty(0.6f, 0.5f), Eps);
        }

        /// <summary>テンポ支配＝拍子読み×主導権。自分の拍子に引き込む（主導権）。</summary>
        [Test]
        public void TempoControl_拍子読みと主導権で戦いのテンポを支配()
        {
            Assert.AreEqual(0.63f, BattleRhythmRules.TempoControl(0.9f, 0.7f), Eps);
            // 受け身ならテンポは奪えない
            Assert.AreEqual(0f, BattleRhythmRules.TempoControl(1f, 0f), Eps);
            // 拍子を読めねば引き込めない
            Assert.AreEqual(0f, BattleRhythmRules.TempoControl(0f, 1f), Eps);
        }

        /// <summary>リズム掴み判定＝拍子読みが閾値(0.6)超かつ一致ボーナスが1.0超。</summary>
        [Test]
        public void IsInRhythm_拍子を読みリズムに乗ったか()
        {
            // 読み0.7(>0.6)・ボーナス1.3(>1.0) → 掴んでいる
            Assert.IsTrue(BattleRhythmRules.IsInRhythm(0.7f, 1.3f));
            // 読みが閾値以下ならリズムを掴めていない
            Assert.IsFalse(BattleRhythmRules.IsInRhythm(0.5f, 1.3f));
            // 拍子が合致せず効率が上がっていなければ（ボーナス=1.0）掴めていない
            Assert.IsFalse(BattleRhythmRules.IsInRhythm(0.8f, 1f));
            // ちょうど閾値は満たす
            Assert.IsTrue(BattleRhythmRules.IsInRhythm(0.6f, 1.1f));
        }
    }
}
