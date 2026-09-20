using System.Collections.Generic;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Ginei.Tests
{
    public class GineiListPlayModeTests
    {
        [Test]
        public void SortPageSelectConfirmAndCancel_WorkThroughPublicApi()
        {
            var list = new GineiList<int>("艦隊一覧") { PageSize = 2 };
            list.SetColumns(new[]
            {
                new GineiListColumn<int>("番号", x => x.ToString(), (a, b) => a.CompareTo(b))
            });
            list.SetDetailFormatter(x => "詳細 " + x);
            list.SetItems(new List<int> { 3, 1, 2 });

            list.SortByColumn(0);
            Assert.AreEqual(new[] { 1, 2 }, list.VisibleItems);
            Assert.AreEqual(2, list.PageCount);
            list.NextPage();
            Assert.AreEqual(new[] { 3 }, list.VisibleItems);

            int confirmed = -1;
            bool cancelled = false;
            list.Confirmed += x => confirmed = x;
            list.Cancelled += () => cancelled = true;
            list.SelectVisibleRow(0);
            list.ConfirmSelection();
            list.Cancel();

            Assert.AreEqual(3, confirmed);
            Assert.IsTrue(cancelled);
        }

        [Test]
        public void SortSameColumnAgain_TogglesDescending()
        {
            var list = new GineiList<int>();
            list.SetColumns(new[] { new GineiListColumn<int>("値", x => x.ToString(), (a, b) => a.CompareTo(b)) });
            list.SetItems(new[] { 2, 1, 3 });
            list.SortByColumn(0);
            list.SortByColumn(0);
            Assert.AreEqual(new[] { 3, 2, 1 }, list.VisibleItems);
        }

        [UnityTest]
        public IEnumerator OrderOfBattlePanel_BuildsCommanderPilotList()
        {
            var go = new GameObject("order-of-battle-list-pilot-test");
            try
            {
                var panel = go.AddComponent<OrderOfBattlePanel>();
                yield return null;
                var field = typeof(OrderOfBattlePanel).GetField("commanderList",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.NotNull(field);
                Assert.NotNull(field.GetValue(panel), "編制画面が共通GineiListを候補選択へ接続する");
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }
    }
}
