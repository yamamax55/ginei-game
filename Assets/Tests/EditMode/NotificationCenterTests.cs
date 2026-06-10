using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>通知の単一窓口（NotificationCenter・#964 NOTIF-1）の EditMode テスト。</summary>
    public class NotificationCenterTests
    {
        [SetUp]
        public void Reset() => NotificationCenter.Clear();

        [Test]
        public void Push_AssignsIncrementingSeq()
        {
            Assert.AreEqual(1, NotificationCenter.Push(NotificationCategory.建艦, "A"));
            Assert.AreEqual(2, NotificationCenter.Push(NotificationCategory.占領, NotificationSeverity.注意, "B"));
            Assert.AreEqual(2, NotificationCenter.LastSeq);
            Assert.AreEqual(2, NotificationCenter.All.Count);
        }

        [Test]
        public void Since_ReturnsOnlyNewerThanGiven()
        {
            NotificationCenter.Push(NotificationCategory.戦闘, "1");
            long s2 = NotificationCenter.Push(NotificationCategory.戦闘, "2");
            NotificationCenter.Push(NotificationCategory.戦闘, "3");
            var since = NotificationCenter.Since(s2);
            Assert.AreEqual(1, since.Count);
            Assert.AreEqual("3", since[0].message);
            // 既読の先頭からは全件
            Assert.AreEqual(3, NotificationCenter.Since(0).Count);
        }

        [Test]
        public void Recent_ReturnsNewestFirst()
        {
            NotificationCenter.Push(NotificationCategory.政治, "old");
            NotificationCenter.Push(NotificationCategory.政治, "new");
            var recent = NotificationCenter.Recent(2);
            Assert.AreEqual("new", recent[0].message);
            Assert.AreEqual("old", recent[1].message);
            Assert.AreEqual(0, NotificationCenter.Recent(0).Count);
        }

        [Test]
        public void Capacity_DropsOldest()
        {
            for (int i = 0; i < NotificationCenter.Capacity + 10; i++)
                NotificationCenter.Push(NotificationCategory.システム, "n" + i);
            Assert.AreEqual(NotificationCenter.Capacity, NotificationCenter.All.Count);
            // 最古の seq=1..10 は捨てられ、最新の seq が残る
            Assert.AreEqual(NotificationCenter.Capacity + 10, NotificationCenter.LastSeq);
            Assert.AreEqual(11, NotificationCenter.All[0].seq); // 先頭は11番目
        }

        [Test]
        public void Push_NullMessage_BecomesEmpty()
        {
            NotificationCenter.Push(NotificationCategory.システム, null);
            Assert.AreEqual("", NotificationCenter.All[0].message);
        }

        [Test]
        public void Clear_EmptiesAndResetsSeq()
        {
            NotificationCenter.Push(NotificationCategory.建艦, "x");
            NotificationCenter.Clear();
            Assert.AreEqual(0, NotificationCenter.All.Count);
            Assert.AreEqual(0, NotificationCenter.LastSeq);
            Assert.AreEqual(1, NotificationCenter.Push(NotificationCategory.建艦, "y")); // 採番リセット
        }
    }
}
