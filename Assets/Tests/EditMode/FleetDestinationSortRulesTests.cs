using System.Collections.Generic;
using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 艦隊メニュー③「目的の星系」の並べ替え（<see cref="FleetDestinationSortRules"/>）。
    /// 到達不能を近距離扱いしないこと・同値が安定すること・昇降順が主キーだけを反転することを固定する。
    /// </summary>
    public class FleetDestinationSortRulesTests
    {
        private static DestinationRow Row(int id, string name, int ownerRank, int hops,
                                          DestinationVerdict v, int order)
            => new DestinationRow(id, name, ownerRank, hops, v, order);

        private static List<int> Ids(List<DestinationRow> rows)
        {
            var ids = new List<int>();
            for (int i = 0; i < rows.Count; i++) ids.Add(rows[i].systemId);
            return ids;
        }

        // ── 距離 ──

        [Test]
        public void Distance_Ascending_NearestFirst()
        {
            var rows = new List<DestinationRow>
            {
                Row(1, "C", 0, 3, DestinationVerdict.可, 0),
                Row(2, "A", 0, 1, DestinationVerdict.可, 1),
                Row(3, "B", 0, 2, DestinationVerdict.可, 2),
            };
            FleetDestinationSortRules.Sort(rows, DestinationSortKey.距離, true);
            CollectionAssert.AreEqual(new[] { 2, 3, 1 }, Ids(rows));
        }

        [Test]
        public void Distance_Descending_FarthestFirst()
        {
            var rows = new List<DestinationRow>
            {
                Row(1, "C", 0, 3, DestinationVerdict.可, 0),
                Row(2, "A", 0, 1, DestinationVerdict.可, 1),
                Row(3, "B", 0, 2, DestinationVerdict.可, 2),
            };
            FleetDestinationSortRules.Sort(rows, DestinationSortKey.距離, false);
            CollectionAssert.AreEqual(new[] { 1, 3, 2 }, Ids(rows));
        }

        /// <summary>
        /// ★到達不能（-1）を「0ホップ＝最も近い」として扱わない。昇順でも降順でも末尾に置く
        /// ＝距離で並べたときに行けない星系が先頭を占めない。
        /// </summary>
        [Test]
        public void Distance_Unreachable_IsAlwaysLast_BothDirections()
        {
            var rows = new List<DestinationRow>
            {
                Row(1, "遠", 0, 5, DestinationVerdict.可, 0),
                Row(2, "不能", 0, FleetDestinationSortRules.Unreachable, DestinationVerdict.不可, 1),
                Row(3, "近", 0, 1, DestinationVerdict.可, 2),
            };

            FleetDestinationSortRules.Sort(rows, DestinationSortKey.距離, true);
            Assert.AreEqual(2, rows[rows.Count - 1].systemId, "昇順で末尾でない");

            FleetDestinationSortRules.Sort(rows, DestinationSortKey.距離, false);
            Assert.AreEqual(2, rows[rows.Count - 1].systemId, "降順でも末尾でなければならない");
        }

        [Test]
        public void Distance_MultipleUnreachable_KeepOriginalOrder()
        {
            var rows = new List<DestinationRow>
            {
                Row(1, "x", 0, FleetDestinationSortRules.Unreachable, DestinationVerdict.不可, 0),
                Row(2, "y", 0, FleetDestinationSortRules.Unreachable, DestinationVerdict.不可, 1),
                Row(3, "z", 0, 2, DestinationVerdict.可, 2),
            };
            FleetDestinationSortRules.Sort(rows, DestinationSortKey.距離, true);
            CollectionAssert.AreEqual(new[] { 3, 1, 2 }, Ids(rows), "到達不能どうしは元の並びのまま");
        }

        // ── 星系名 ──

        [Test]
        public void Name_Ascending_IsOrdinal_AndReversible()
        {
            var rows = new List<DestinationRow>
            {
                Row(1, "B", 0, 1, DestinationVerdict.可, 0),
                Row(2, "A", 0, 1, DestinationVerdict.可, 1),
                Row(3, "C", 0, 1, DestinationVerdict.可, 2),
            };
            FleetDestinationSortRules.Sort(rows, DestinationSortKey.星系名, true);
            CollectionAssert.AreEqual(new[] { 2, 1, 3 }, Ids(rows));

            FleetDestinationSortRules.Sort(rows, DestinationSortKey.星系名, false);
            CollectionAssert.AreEqual(new[] { 3, 1, 2 }, Ids(rows));
        }

        /// <summary>名前順では到達不能でも下へ落とさない（名前で探しているときに一部だけ消えない）。</summary>
        [Test]
        public void Name_DoesNotPushUnreachableToTheEnd()
        {
            var rows = new List<DestinationRow>
            {
                Row(1, "B", 0, 1, DestinationVerdict.可, 0),
                Row(2, "A", 0, FleetDestinationSortRules.Unreachable, DestinationVerdict.不可, 1),
            };
            FleetDestinationSortRules.Sort(rows, DestinationSortKey.星系名, true);
            Assert.AreEqual(2, rows[0].systemId, "名前順なら到達不能でも A が先");
        }

        // ── 所属 ──

        [Test]
        public void Owner_GroupsByRank_ThenName()
        {
            var rows = new List<DestinationRow>
            {
                Row(1, "敵B", 2, 1, DestinationVerdict.可, 0),
                Row(2, "自A", 0, 1, DestinationVerdict.可, 1),
                Row(3, "敵A", 2, 1, DestinationVerdict.可, 2),
                Row(4, "友A", 1, 1, DestinationVerdict.可, 3),
            };
            FleetDestinationSortRules.Sort(rows, DestinationSortKey.所属, true);
            CollectionAssert.AreEqual(new[] { 2, 4, 3, 1 }, Ids(rows), "自勢力→非敵対→敵対、同勢力内は名前順");
        }

        // ── 可否 ──

        [Test]
        public void Verdict_Ascending_OrderableFirst()
        {
            var rows = new List<DestinationRow>
            {
                Row(1, "不可", 0, 1, DestinationVerdict.不可, 0),
                Row(2, "警告", 0, 1, DestinationVerdict.警告, 1),
                Row(3, "可", 0, 1, DestinationVerdict.可, 2),
            };
            FleetDestinationSortRules.Sort(rows, DestinationSortKey.可否, true);
            CollectionAssert.AreEqual(new[] { 3, 2, 1 }, Ids(rows));
        }

        // ── 安定性・決定論 ──

        [Test]
        public void SameKey_KeepsOriginalOrder_Stable()
        {
            var rows = new List<DestinationRow>();
            for (int i = 0; i < 12; i++) rows.Add(Row(100 + i, "同", 0, 3, DestinationVerdict.可, i));

            FleetDestinationSortRules.Sort(rows, DestinationSortKey.距離, true);
            for (int i = 0; i < rows.Count; i++)
                Assert.AreEqual(100 + i, rows[i].systemId, "同値の並びが崩れている（不安定ソート）");

            // 降順にしても、同値どうしの並びは反転しない（押すたびに順序が踊らない）。
            FleetDestinationSortRules.Sort(rows, DestinationSortKey.距離, false);
            for (int i = 0; i < rows.Count; i++)
                Assert.AreEqual(100 + i, rows[i].systemId, "同値の並びが降順で反転している");
        }

        [Test]
        public void Sort_IsDeterministic_AcrossRepeats()
        {
            List<int> first = null;
            for (int trial = 0; trial < 3; trial++)
            {
                var rows = new List<DestinationRow>
                {
                    Row(5, "E", 2, 4, DestinationVerdict.警告, 0),
                    Row(1, "A", 0, 4, DestinationVerdict.可, 1),
                    Row(3, "C", 1, 2, DestinationVerdict.可, 2),
                };
                FleetDestinationSortRules.Sort(rows, DestinationSortKey.距離, true);
                if (first == null) first = Ids(rows);
                else CollectionAssert.AreEqual(first, Ids(rows));
            }
        }

        // ── null/縮退 ──

        [Test]
        public void Sort_NullOrTiny_IsSafe()
        {
            Assert.DoesNotThrow(() => FleetDestinationSortRules.Sort(null, DestinationSortKey.距離, true));
            var one = new List<DestinationRow> { Row(1, "A", 0, 1, DestinationVerdict.可, 0) };
            Assert.DoesNotThrow(() => FleetDestinationSortRules.Sort(one, DestinationSortKey.所属, false));
            Assert.AreEqual(1, one[0].systemId);
        }

        [Test]
        public void Row_NullName_BecomesEmpty()
        {
            var r = new DestinationRow(1, null, 0, 1, DestinationVerdict.可, 0);
            Assert.AreEqual("", r.name);
        }

        // ── 可否の判定（要塞封鎖は発令できる＝警告） ──

        [Test]
        public void VerdictOf_FortressBlockade_IsWarningNotRefusal()
        {
            Assert.AreEqual(DestinationVerdict.警告,
                FleetDestinationSortRules.VerdictOf(MoveOrderRejection.要塞封鎖, false));
        }

        [Test]
        public void VerdictOf_StopsShort_IsWarning()
        {
            Assert.AreEqual(DestinationVerdict.警告,
                FleetDestinationSortRules.VerdictOf(MoveOrderRejection.なし, true));
            Assert.AreEqual(DestinationVerdict.可,
                FleetDestinationSortRules.VerdictOf(MoveOrderRejection.なし, false));
        }

        [Test]
        public void VerdictOf_OtherRejections_AreRefusals()
        {
            Assert.AreEqual(DestinationVerdict.不可,
                FleetDestinationSortRules.VerdictOf(MoveOrderRejection.到達不能, false));
            Assert.AreEqual(DestinationVerdict.不可,
                FleetDestinationSortRules.VerdictOf(MoveOrderRejection.交戦中, true));
        }

        // ── 表示 ──

        [Test]
        public void HeaderLabel_ShowsKeyAndDirection()
        {
            Assert.AreEqual("距離 昇順", FleetDestinationSortRules.HeaderLabel(DestinationSortKey.距離, true));
            Assert.AreEqual("所属 降順", FleetDestinationSortRules.HeaderLabel(DestinationSortKey.所属, false));
        }

        [Test]
        public void AllKeys_CoversEveryEnumValue()
        {
            var all = (DestinationSortKey[])System.Enum.GetValues(typeof(DestinationSortKey));
            Assert.AreEqual(all.Length, FleetDestinationSortRules.AllKeys.Length);
            for (int i = 0; i < all.Length; i++)
            {
                Assert.Contains(all[i], FleetDestinationSortRules.AllKeys);
                Assert.IsFalse(string.IsNullOrEmpty(FleetDestinationSortRules.AscendingMeaning(all[i])),
                               all[i].ToString());
            }
        }
    }
}
