using NUnit.Framework;
using UnityEngine;

namespace Ginei.Tests
{
    /// <summary>回廊サボタージュ（占領せず回廊を一時遮断する破壊工作・#1390）の純ロジックテスト。</summary>
    public class CorridorSabotageRulesTests
    {
        const float Eps = 1e-4f;

        /// <summary>工作の効果＝技量×脆弱性。守りの薄い回廊ほど効き、両端は飽和。</summary>
        [Test]
        public void SabotageEffect_脆弱性が高いほど効く()
        {
            // 技量0.8×脆弱性0.5×gain1.0 = 0.4
            Assert.AreEqual(0.4f, CorridorSabotageRules.SabotageEffect(0.8f, 0.5f), Eps);
            // 脆弱性0は無効（堅牢な回廊は工作が効かない）
            Assert.AreEqual(0f, CorridorSabotageRules.SabotageEffect(1f, 0f), Eps);
            // 両端最大＝1にクランプ
            Assert.AreEqual(1f, CorridorSabotageRules.SabotageEffect(1f, 1f), Eps);
        }

        /// <summary>通行容量の低下＝遮断度×最大遮断率0.9。一時的に通せなくなる。</summary>
        [Test]
        public void ThroughputReduction_遮断度に比例して通行が下がる()
        {
            Assert.AreEqual(0f, CorridorSabotageRules.ThroughputReduction(0f), Eps);
            Assert.AreEqual(0.45f, CorridorSabotageRules.ThroughputReduction(0.5f), Eps);
            // 遮断度1でも最大0.9＝完全には消えない（工作は面の封鎖ではない）
            Assert.AreEqual(0.9f, CorridorSabotageRules.ThroughputReduction(1f), Eps);
        }

        /// <summary>遮断度のせめぎ合い＝工作で上がり復旧で下がる綱引き。</summary>
        [Test]
        public void DisruptionTick_工作と復旧の綱引き()
        {
            // 0.5 + gain1.0×effort1.0×dt0.2 − repair0.15×effort0×dt0.2 = 0.7
            Assert.AreEqual(0.7f, CorridorSabotageRules.DisruptionTick(0.5f, 1f, 0f, 0.2f), Eps);
            // 復旧が勝てば下がる：0.5 + 0 − 0.15×1.0×1.0 = 0.35
            Assert.AreEqual(0.35f, CorridorSabotageRules.DisruptionTick(0.5f, 0f, 1f, 1f), Eps);
        }

        /// <summary>復旧進捗＝掃海・修復で時間とともに通行が戻る。1で原状回復。</summary>
        [Test]
        public void RepairTick_時間で復旧し原状回復()
        {
            // 0.2 + repair0.15×capacity1.0×dt2.0 = 0.5
            Assert.AreEqual(0.5f, CorridorSabotageRules.RepairTick(0.2f, 1f, 2f), Eps);
            // 大きく進めても1にクランプ（復旧完了）
            Assert.AreEqual(1f, CorridorSabotageRules.RepairTick(0.9f, 1f, 100f), Eps);
        }

        /// <summary>発覚リスク＝露出×哨戒密度。哨戒が密だと工作員が捕まる。</summary>
        [Test]
        public void DetectionRisk_哨戒が密だと発覚しやすい()
        {
            Assert.AreEqual(0.5f, CorridorSabotageRules.DetectionRisk(1f, 0.5f), Eps);
            // 哨戒ゼロなら発覚しない
            Assert.AreEqual(0f, CorridorSabotageRules.DetectionRisk(1f, 0f), Eps);
        }

        /// <summary>否認可能な妨害＝帰属が上がるほど否認可能性が失われ実効が落ちる。</summary>
        [Test]
        public void DeniableInterdiction_帰属で否認可能性が失われる()
        {
            // 遮断度1→通行低下0.9、帰属0で否認可能性1：0.9
            Assert.AreEqual(0.9f, CorridorSabotageRules.DeniableInterdiction(1f, 0f), Eps);
            // 帰属0.5＝半分ばれる：0.9×0.5 = 0.45
            Assert.AreEqual(0.45f, CorridorSabotageRules.DeniableInterdiction(1f, 0.5f), Eps);
            // 完全に帰属＝否認不能で0
            Assert.AreEqual(0f, CorridorSabotageRules.DeniableInterdiction(1f, 1f), Eps);
        }

        /// <summary>補給遅延＝通行低下×敵の依存。敵が頼る回廊ほど迂回を強いる。</summary>
        [Test]
        public void SupplyDelayImposed_敵が頼る回廊ほど遅延が大きい()
        {
            Assert.AreEqual(0.4f, CorridorSabotageRules.SupplyDelayImposed(0.5f, 0.8f), Eps);
            // 敵が依存していなければ遅延を強いられない（迂回路がある）
            Assert.AreEqual(0f, CorridorSabotageRules.SupplyDelayImposed(0.9f, 0f), Eps);
        }

        /// <summary>回廊遮断判定＝遮断度が閾値0.6を超えれば一時通行不能（復旧で戻る）。</summary>
        [Test]
        public void IsCorridorBlocked_閾値超で一時通行不能()
        {
            Assert.IsFalse(CorridorSabotageRules.IsCorridorBlocked(0.6f)); // 閾値ちょうどは不成立
            Assert.IsTrue(CorridorSabotageRules.IsCorridorBlocked(0.7f));
            Assert.IsFalse(CorridorSabotageRules.IsCorridorBlocked(0.3f));
        }

        /// <summary>State 構築はクランプされる。</summary>
        [Test]
        public void State_クランプされる()
        {
            var s = new CorridorSabotageState(1.5f, -0.2f, 0.5f);
            Assert.AreEqual(1f, s.disruptionLevel, Eps);
            Assert.AreEqual(0f, s.detectionLevel, Eps);
            Assert.AreEqual(0.5f, s.repairProgress, Eps);
        }
    }
}
