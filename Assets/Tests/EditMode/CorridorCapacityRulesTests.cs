using NUnit.Framework;
using UnityEngine;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 回廊補給スループット（#1367）の純ロジックテスト。回廊容量（通商＞要衝）・需要vs容量・補給の按分配分・
    /// 渋滞ペナルティ・優先配分・要衝のボトルネック・容量拡張・回廊飽和判定・空配列安全を既定 Params 具体値で担保する。
    /// </summary>
    public class CorridorCapacityRulesTests
    {
        const float Eps = 1e-4f;

        /// <summary>通商回廊は大容量・要衝回廊は小容量＝太い航路ほど運べる（基準0.8＞0.3、インフラで上乗せ）。</summary>
        [Test]
        public void CorridorThroughput_通商は要衝より大容量でインフラが上乗せ()
        {
            float commerce = CorridorCapacityRules.CorridorThroughput(true, 0f);
            float choke = CorridorCapacityRules.CorridorThroughput(false, 0f);
            Assert.AreEqual(0.8f, commerce, Eps);          // 通商基準容量
            Assert.AreEqual(0.3f, choke, Eps);             // 要衝基準容量
            Assert.Greater(commerce, choke);               // 通商＞要衝

            // 要衝にインフラ満額＝0.3＋0.2×1.0＝0.5。
            float chokeImproved = CorridorCapacityRules.CorridorThroughput(false, 1f);
            Assert.AreEqual(0.5f, chokeImproved, Eps);
        }

        /// <summary>需要／容量比＝容量超過でボトルネック。容量0は詰まり（大きな値）。</summary>
        [Test]
        public void DemandVsCapacity_需要が容量を超えるとボトルネック()
        {
            Assert.AreEqual(0.5f, CorridorCapacityRules.DemandVsCapacity(0.4f, 0.8f), Eps); // 0.4/0.8
            Assert.AreEqual(2f, CorridorCapacityRules.DemandVsCapacity(0.6f, 0.3f), Eps);   // 0.6/0.3＝超過
            Assert.Greater(CorridorCapacityRules.DemandVsCapacity(0.5f, 0f), 100f);          // 容量0＝パンク
            Assert.AreEqual(0f, CorridorCapacityRules.DemandVsCapacity(0f, 0f), Eps);        // 需要0＝詰まらない
        }

        /// <summary>補給の按分配分＝容量内は満額・超過なら全員が比例して不足する（兵站のボトルネック）。</summary>
        [Test]
        public void SupplyAllocation_容量超過で全員が比例配分される()
        {
            // 総需要0.6・容量0.8＝容量内＝満額。
            float[] within = CorridorCapacityRules.SupplyAllocation(new[] { 0.3f, 0.3f }, 0.8f);
            Assert.AreEqual(0.3f, within[0], Eps);
            Assert.AreEqual(0.3f, within[1], Eps);

            // 総需要0.8・容量0.4＝比率0.5＝全員半分に削られる。
            float[] over = CorridorCapacityRules.SupplyAllocation(new[] { 0.6f, 0.2f }, 0.4f);
            Assert.AreEqual(0.3f, over[0], Eps); // 0.6×0.5
            Assert.AreEqual(0.1f, over[1], Eps); // 0.2×0.5
            Assert.AreEqual(0.4f, over[0] + over[1], Eps); // 合計＝容量
        }

        /// <summary>渋滞ペナルティ＝容量内は1.0、超過で効率が落ちる。</summary>
        [Test]
        public void CongestionPenalty_容量超過で渋滞して効率が落ちる()
        {
            Assert.AreEqual(1f, CorridorCapacityRules.CongestionPenalty(1f), Eps);   // ちょうど満杯＝遅延なし
            Assert.AreEqual(1f, CorridorCapacityRules.CongestionPenalty(0.5f), Eps); // 余裕＝遅延なし
            // 比1.3＝超過0.3×傾き1.0＝1−0.3＝0.7。
            Assert.AreEqual(0.7f, CorridorCapacityRules.CongestionPenalty(1.3f), Eps);
        }

        /// <summary>優先配分＝高優先の補給を先に通し、容量が尽きたら低優先は欠乏する。</summary>
        [Test]
        public void PriorityRouting_高優先を先に通し低優先が欠乏する()
        {
            // 容量0.5・低優先0.2(需要0.4)＋高優先0.9(需要0.4)。高優先が先に満たされ残り0.1だけ低優先へ。
            float[] alloc = CorridorCapacityRules.PriorityRouting(
                new[] { 0.4f, 0.4f }, new[] { 0.2f, 0.9f }, 0.5f);
            Assert.AreEqual(0.4f, alloc[1], Eps); // 高優先＝満額
            Assert.AreEqual(0.1f, alloc[0], Eps); // 低優先＝残りだけ
        }

        /// <summary>優先度 null は等優先の按分にフォールバック。</summary>
        [Test]
        public void PriorityRouting_優先度なしは按分にフォールバック()
        {
            float[] alloc = CorridorCapacityRules.PriorityRouting(new[] { 0.6f, 0.2f }, null, 0.4f);
            float[] expected = CorridorCapacityRules.SupplyAllocation(new[] { 0.6f, 0.2f }, 0.4f);
            Assert.AreEqual(expected[0], alloc[0], Eps);
            Assert.AreEqual(expected[1], alloc[1], Eps);
        }

        /// <summary>要衝のボトルネック＝迂回路があるほど律速が緩む。最弱回廊が経路全体を決める。</summary>
        [Test]
        public void ChokepointBottleneck_迂回路で律速が緩む()
        {
            // 容量0.3・迂回路なし＝そのまま0.3（最弱で切れる）。
            Assert.AreEqual(0.3f, CorridorCapacityRules.ChokepointBottleneck(0.3f, 0f), Eps);
            // 迂回路満額＝余地0.7×1.0で1.0まで逃がせる。
            Assert.AreEqual(1f, CorridorCapacityRules.ChokepointBottleneck(0.3f, 1f), Eps);
            // 半分の迂回路＝0.3＋0.7×0.5＝0.65。
            Assert.AreEqual(0.65f, CorridorCapacityRules.ChokepointBottleneck(0.3f, 0.5f), Eps);
        }

        /// <summary>容量拡張＝インフラ投資で容量が育ち、飽和で逓減する。飽和判定＝需要過多で詰まる。</summary>
        [Test]
        public void CapacityExpansion_投資で容量が育ち飽和判定が効く()
        {
            // 0.3＋投資0.5×余地0.7×dt1.0＝0.3＋0.35＝0.65。
            Assert.AreEqual(0.65f, CorridorCapacityRules.CapacityExpansion(0.3f, 0.5f, 1f), Eps);
            // 満容量は伸びない。
            Assert.AreEqual(1f, CorridorCapacityRules.CapacityExpansion(1f, 1f, 1f), Eps);

            // 飽和判定（閾値1.0）：需要0.4・容量0.3＝比1.33＞1.0＝飽和。
            Assert.IsTrue(CorridorCapacityRules.IsCorridorSaturated(0.4f, 0.3f, 1f));
            // 需要0.2・容量0.8＝比0.25＜1.0＝飽和せず。
            Assert.IsFalse(CorridorCapacityRules.IsCorridorSaturated(0.2f, 0.8f, 1f));
        }

        /// <summary>空配列・null は安全（空配列を返す）。</summary>
        [Test]
        public void 空配列とnullは安全に空配列を返す()
        {
            Assert.AreEqual(0, CorridorCapacityRules.SupplyAllocation(null, 0.5f).Length);
            Assert.AreEqual(0, CorridorCapacityRules.SupplyAllocation(new float[0], 0.5f).Length);
            Assert.AreEqual(0, CorridorCapacityRules.PriorityRouting(null, null, 0.5f).Length);
            Assert.AreEqual(0, CorridorCapacityRules.PriorityRouting(new float[0], new float[0], 0.5f).Length);

            // 全需要0＝全員0配分。
            float[] zero = CorridorCapacityRules.SupplyAllocation(new[] { 0f, 0f }, 0.5f);
            Assert.AreEqual(0f, zero[0], Eps);
            Assert.AreEqual(0f, zero[1], Eps);
        }
    }
}
