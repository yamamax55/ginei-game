using NUnit.Framework;
using UnityEngine;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 戦列（陣列）の維持/突破/崩壊：密度で結束・相互支援・一点集中の突破圧力・崩壊カスケード・隙の脆弱性・立て直し。
    /// 既定 Params で期待値を固定。
    /// </summary>
    public class BattleLineRulesTests
    {
        [Test]
        public void LineCohesion_IdealSpacingIsFull_OutsideToleranceFallsToFloor()
        {
            // 適正間隔1.0ぴったり＝陣列満点なら結束1.0。
            Assert.AreEqual(1.0f, BattleLineRules.LineCohesion(1f, 1.0f), 1e-4f);
            // 許容幅0.5ぶん外れる（1.5）と結束下限係数0.5へ。
            Assert.AreEqual(0.5f, BattleLineRules.LineCohesion(1f, 1.5f), 1e-4f);
            // 半分外れ（1.25）＋陣列0.8 → 0.8 * lerp(1,0.5,0.5)=0.8*0.75=0.6。
            Assert.AreEqual(0.6f, BattleLineRules.LineCohesion(0.8f, 1.25f), 1e-4f);
        }

        [Test]
        public void MutualSupport_GrowsWithCohesionAndWidthButWidthDiminishes()
        {
            // 結束満点・幅4 → 1*√4=2.0（幅は平方根で逓減）。
            Assert.AreEqual(2.0f, BattleLineRules.MutualSupport(1f, 4f), 1e-4f);
            // 結束半分・幅1 → 0.5。
            Assert.AreEqual(0.5f, BattleLineRules.MutualSupport(0.5f, 1f), 1e-4f);
        }

        [Test]
        public void BreakthroughPressure_RisesWithEnemyConcentrationFallsWithCohesion()
        {
            // 拮抗（集中1・結束1）→ 1.0。
            Assert.AreEqual(1.0f, BattleLineRules.BreakthroughPressure(1f, 1f), 1e-4f);
            // 集中2・結束0.5 → 4.0（結束が薄いと同じ集中でも圧力が跳ねる）。
            Assert.AreEqual(4.0f, BattleLineRules.BreakthroughPressure(2f, 0.5f), 1e-4f);
        }

        [Test]
        public void LineBreach_TriggersAtOrAboveThreshold()
        {
            Assert.IsTrue(BattleLineRules.LineBreach(1.0f));   // 閾値1.0以上で突破
            Assert.IsTrue(BattleLineRules.LineBreach(2.0f));
            Assert.IsFalse(BattleLineRules.LineBreach(0.9f));  // 閾値未満は持ちこたえる
        }

        [Test]
        public void CollapseCascade_HighWhenSevereAndLowCohesion()
        {
            // 深刻度1・結束0 → 全崩壊1.0（連鎖）。
            Assert.AreEqual(1.0f, BattleLineRules.CollapseCascade(1f, 0f), 1e-4f);
            // 深刻度1・結束1 → 連鎖しない0。
            Assert.AreEqual(0.0f, BattleLineRules.CollapseCascade(1f, 1f), 1e-4f);
            // 深刻度0.8・結束0.5 → 0.8*0.5=0.4。
            Assert.AreEqual(0.4f, BattleLineRules.CollapseCascade(0.8f, 0.5f), 1e-4f);
        }

        [Test]
        public void GapVulnerability_HighWhenSpreadThin()
        {
            // 兵力2を幅10へ薄く展開 → 密度0.2 → 隙0.8。
            Assert.AreEqual(0.8f, BattleLineRules.GapVulnerability(10f, 2f), 1e-4f);
            // 兵力10を幅10＝基準密度1.0 → 隙なし0。
            Assert.AreEqual(0.0f, BattleLineRules.GapVulnerability(10f, 10f), 1e-4f);
        }

        [Test]
        public void LineReformChance_BaseRisesWithReservesAndLeadership()
        {
            // 予備なし・統率0 → 基底0.2。
            Assert.AreEqual(0.2f, BattleLineRules.LineReformChance(0f, 0f), 1e-4f);
            // 予備満タン・統率100 → 0.2+0.4+0.4=1.0。
            Assert.AreEqual(1.0f, BattleLineRules.LineReformChance(1f, 100f), 1e-4f);
            // 予備0.5・統率50 → 0.2+0.2+0.2=0.6。
            Assert.AreEqual(0.6f, BattleLineRules.LineReformChance(0.5f, 50f), 1e-4f);
        }

        [Test]
        public void IsLineHolding_TrueWhilePressureBelowThresholdAndCohesionRemains()
        {
            Assert.IsTrue(BattleLineRules.IsLineHolding(0.8f, 0.5f));   // 結束あり・圧力<1＝維持
            Assert.IsFalse(BattleLineRules.IsLineHolding(0.5f, 1.5f));  // 圧力が閾値超＝突破済み
            Assert.IsFalse(BattleLineRules.IsLineHolding(0f, 0.1f));    // 結束ゼロ＝もう戦列でない
        }

        /// <summary>
        /// 物語：戦列は相互支援で強いが、一点突破で崩壊が連鎖する。薄く広げた戦列は隙が多く突破されやすく、
        /// 突破されると結束の低さゆえ崩れが伝播する。密に組んだ戦列は同じ敵集中を受け止めて持ちこたえる。
        /// </summary>
        [Test]
        public void Narrative_ThinLineBreaksAndCascadesWhileTightLineHolds()
        {
            // 密な戦列（陣列満点・適正間隔）。
            float tightCohesion = BattleLineRules.LineCohesion(1f, 1.0f);
            // 薄く広げた戦列（陣列満点だが間隔が開き過ぎ）。
            float thinCohesion = BattleLineRules.LineCohesion(1f, 1.5f);
            Assert.Greater(tightCohesion, thinCohesion, "密に組んだ方が結束は高い");

            // 兵力を広く薄く展開すると隙が増える（突破口になる）。
            float thinGap = BattleLineRules.GapVulnerability(20f, 5f);   // 密度0.25
            float tightGap = BattleLineRules.GapVulnerability(5f, 5f);    // 密度1.0
            Assert.Greater(thinGap, tightGap, "薄く広げるほど隙だらけ");

            // 同じ一点集中の敵に対し、薄い戦列の方が突破圧力が高い。
            float enemy = 0.6f;
            float thinPressure = BattleLineRules.BreakthroughPressure(enemy, thinCohesion);   // 0.6/0.5=1.2
            float tightPressure = BattleLineRules.BreakthroughPressure(enemy, tightCohesion); // 0.6/1.0=0.6
            Assert.Greater(thinPressure, tightPressure);

            // 密な戦列はこの集中を受け止める（維持）が、薄い戦列は突破される。
            Assert.IsTrue(BattleLineRules.IsLineHolding(tightCohesion, tightPressure)); // 0.6<1 ＝維持
            Assert.IsTrue(BattleLineRules.LineBreach(thinPressure));                    // 1.2>=1 ＝突破
            // 薄い戦列に十分な集中が掛かれば突破が起きる。
            float heavyPressure = BattleLineRules.BreakthroughPressure(2f, thinCohesion); // 2/0.5=4
            Assert.IsTrue(BattleLineRules.LineBreach(heavyPressure));

            // 突破後、結束が低い薄い戦列は崩壊が連鎖し、密な戦列より大きく崩れる。
            float thinCascade = BattleLineRules.CollapseCascade(1f, thinCohesion);
            float tightCascade = BattleLineRules.CollapseCascade(1f, tightCohesion);
            Assert.Greater(thinCascade, tightCascade, "結束の低い戦列ほど崩れが伝播する");
        }
    }
}
