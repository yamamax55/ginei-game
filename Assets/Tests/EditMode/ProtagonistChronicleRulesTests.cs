using System.Collections.Generic;
using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 主人公の一代記（TKO-6・<see cref="ProtagonistChronicle"/>/<see cref="ProtagonistChronicleRules"/>・#2483）を固定する：
    /// 追記・発生順、上限超過で古い順に落とす有界化と打ち切り計、新しい順の取り出し、null安全。
    /// </summary>
    public class ProtagonistChronicleRulesTests
    {
        [Test]
        public void Record_AppendsInOrder()
        {
            var c = new ProtagonistChronicle();
            ProtagonistChronicleRules.Record(c, 1, ChronicleEventKind.入校, "幼年学校へ");
            ProtagonistChronicleRules.Record(c, 12, ChronicleEventKind.卒業任官, "首席任官");
            ProtagonistChronicleRules.Record(c, 20, ChronicleEventKind.昇進, "少将へ");

            Assert.AreEqual(3, c.Count);
            Assert.AreEqual(ChronicleEventKind.入校, c.entries[0].kind);
            Assert.AreEqual(ChronicleEventKind.昇進, c.entries[2].kind);
            Assert.AreEqual(20, c.entries[2].monthIndex);
        }

        [Test]
        public void Record_BoundedDropsOldestAndCounts()
        {
            var c = new ProtagonistChronicle { capacity = 3 };
            for (int i = 1; i <= 5; i++)
                ProtagonistChronicleRules.Record(c, i, ChronicleEventKind.武勲, "戦功" + i);

            Assert.AreEqual(3, c.Count, "上限3で頭打ち");
            Assert.AreEqual(2, c.droppedCount, "古い2件を落とした");
            Assert.AreEqual(3, c.entries[0].monthIndex, "先頭は3件目（1,2は落ちた）");
            Assert.AreEqual(5, c.entries[2].monthIndex);
        }

        [Test]
        public void Recent_ReturnsNewestFirst()
        {
            var c = new ProtagonistChronicle();
            for (int i = 1; i <= 5; i++)
                ProtagonistChronicleRules.Record(c, i, ChronicleEventKind.主命達成, "達成" + i);

            var recent = ProtagonistChronicleRules.Recent(c, 2);
            Assert.AreEqual(2, recent.Count);
            Assert.AreEqual(5, recent[0].monthIndex, "最新が先頭");
            Assert.AreEqual(4, recent[1].monthIndex);
        }

        [Test]
        public void NullAndZero_Safe()
        {
            ProtagonistChronicleRules.Record(null, 1, ChronicleEventKind.死去, "x"); // 例外を投げない
            Assert.AreEqual(0, ProtagonistChronicleRules.Recent(null, 3).Count);
            Assert.AreEqual(0, ProtagonistChronicleRules.Recent(new ProtagonistChronicle(), 0).Count);
        }
    }
}
