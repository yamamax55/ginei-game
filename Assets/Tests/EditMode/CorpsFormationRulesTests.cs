using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>軍団陣形：横陣の幅制限・軍団長の後方配置・方陣の前列識別とローテーション。</summary>
    public class CorpsFormationRulesTests
    {
        [Test]
        public void SingleFleet_IsCommanderAtOrigin()
        {
            var slots = CorpsFormationRules.ComputeSlots(1, Formation.方陣, 6f);
            Assert.AreEqual(1, slots.Count);
            Assert.IsTrue(slots[0].commander);
            Assert.AreEqual(Vector2.zero, slots[0].localPos);
        }

        [Test]
        public void Line_WidthIsCapped()
        {
            // 戦闘艦隊20 → 横陣でも1列が MaxLineColumns(7) を超えない（広がり過ぎ防止）。
            var slots = CorpsFormationRules.ComputeSlots(21, Formation.横陣, 6f); // 軍団長込み21＝戦闘20
            int commanders = 0;
            var perRow = new Dictionary<int, int>();
            foreach (var s in slots)
            {
                if (s.commander) { commanders++; continue; }
                perRow.TryGetValue(s.rank, out int c); perRow[s.rank] = c + 1;
            }
            Assert.AreEqual(1, commanders);
            foreach (var kv in perRow)
                Assert.LessOrEqual(kv.Value, CorpsFormationRules.MaxLineColumns, "横陣の列幅が上限を超えた");
        }

        [Test]
        public void Commander_IsRearmostAndCentered()
        {
            var slots = CorpsFormationRules.ComputeSlots(10, Formation.方陣, 6f);
            CorpsSlot cmd = slots.Find(s => s.commander);
            Assert.IsTrue(cmd.commander);
            Assert.AreEqual(0f, cmd.localPos.x, 1e-4f); // 中央
            // 軍団長は全スロット中で最も後方（最小 y）＝前線に出過ぎない。
            float minY = float.MaxValue;
            foreach (var s in slots) minY = Mathf.Min(minY, s.localPos.y);
            Assert.AreEqual(minY, cmd.localPos.y, 1e-4f);
            // 前線（最前列）は軍団長より前方。
            float maxY = float.MinValue;
            foreach (var s in slots) maxY = Mathf.Max(maxY, s.localPos.y);
            Assert.Greater(maxY, cmd.localPos.y);
        }

        [Test]
        public void Square_FrontRankFlaggedOnFrontRow()
        {
            var slots = CorpsFormationRules.ComputeSlots(10, Formation.方陣, 6f);
            // 前列フラグの付くスロットは最前列(rank 0)だけ。
            foreach (var s in slots)
            {
                if (s.commander) continue;
                Assert.AreEqual(s.rank == 0, s.frontRank);
            }
            // 最前列が最大 y（最も前方）。
            float frontY = float.MinValue, otherY = float.MinValue;
            foreach (var s in slots)
            {
                if (s.commander) continue;
                if (s.frontRank) frontY = Mathf.Max(frontY, s.localPos.y);
                else otherY = Mathf.Max(otherY, s.localPos.y);
            }
            Assert.Greater(frontY, otherY);
        }

        [Test]
        public void RotateFrontToBack_MovesFrontFleetsToRear()
        {
            // 6艦隊・前列2 → 新並び [2,3,4,5,0,1]（前列0,1が末尾へ）
            int[] order = CorpsFormationRules.RotateFrontToBack(6, 2);
            Assert.AreEqual(new[] { 2, 3, 4, 5, 0, 1 }, order);
            // 全数=前列 → 一巡（恒等）
            Assert.AreEqual(new[] { 0, 1, 2, 3, 4 }, CorpsFormationRules.RotateFrontToBack(5, 5));
            // 0件は空
            Assert.AreEqual(0, CorpsFormationRules.RotateFrontToBack(0, 2).Length);
        }

        [Test]
        public void ColumnsFor_LineCappedSquareSqrt()
        {
            Assert.AreEqual(CorpsFormationRules.MaxLineColumns, CorpsFormationRules.ColumnsFor(Formation.横陣, 20));
            Assert.AreEqual(3, CorpsFormationRules.ColumnsFor(Formation.横陣, 3));
            Assert.LessOrEqual(CorpsFormationRules.ColumnsFor(Formation.方陣, 20), CorpsFormationRules.SquareMaxColumns);
        }
    }
}
