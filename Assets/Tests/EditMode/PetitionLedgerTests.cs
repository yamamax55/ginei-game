using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 進行中の陳情の在庫（稟議基盤整備）：採番・箱/勢力/決裁待ちの照会・有界化（古い決着済みから落とす）を固定する。
    /// 状態遷移は <see cref="WorkflowRules"/> へ委譲する（ここは store の振る舞い）。
    /// </summary>
    public class PetitionLedgerTests
    {
        private static Petition New(int id, Faction f, BoxKind box, string region = "")
            => new Petition(id, "案", f, box, PetitionOrigin.建白, "tax.cut", region);

        [Test]
        public void Add_AssignsId_WhenZero_AndIsMonotonic()
        {
            var led = new PetitionLedger();
            var a = New(0, Faction.同盟, BoxKind.政治家);
            var b = New(0, Faction.同盟, BoxKind.政治家);
            Assert.IsTrue(led.Add(a));
            Assert.IsTrue(led.Add(b));
            Assert.AreEqual(1, a.id);
            Assert.AreEqual(2, b.id);
            Assert.AreEqual(2, led.Count);
        }

        [Test]
        public void Add_RejectsDuplicateId()
        {
            var led = new PetitionLedger();
            Assert.IsTrue(led.Add(New(5, Faction.帝国, BoxKind.国王)));
            Assert.IsFalse(led.Add(New(5, Faction.帝国, BoxKind.国王))); // 同 id は二重投入しない
            Assert.AreEqual(1, led.Count);
        }

        [Test]
        public void Add_ExternalId_AdvancesSeq_NoCollision()
        {
            var led = new PetitionLedger();
            Assert.IsTrue(led.Add(New(10, Faction.同盟, BoxKind.政治家))); // 外部採番
            var auto = New(0, Faction.同盟, BoxKind.政治家);
            Assert.IsTrue(led.Add(auto));
            Assert.AreEqual(11, auto.id); // 採番は外部 id を超えて続く
        }

        [Test]
        public void Get_Remove_Work()
        {
            var led = new PetitionLedger();
            led.Add(New(1, Faction.同盟, BoxKind.政治家));
            Assert.IsNotNull(led.Get(1));
            Assert.IsNull(led.Get(99));
            Assert.IsTrue(led.Remove(1));
            Assert.IsFalse(led.Remove(1));
            Assert.AreEqual(0, led.Count);
        }

        [Test]
        public void Queries_ByFaction_ByBox_AwaitingDecision()
        {
            var led = new PetitionLedger();
            var imp = New(0, Faction.帝国, BoxKind.国王);
            var alli = New(0, Faction.同盟, BoxKind.政治家);
            var local = New(0, Faction.同盟, BoxKind.地方, "辺境");
            led.Add(imp); led.Add(alli); led.Add(local);

            Assert.AreEqual(2, led.ByFaction(Faction.同盟).Count);
            Assert.AreEqual(1, led.ByBox(BoxKind.国王).Count);
            Assert.AreEqual(1, led.ByBox(BoxKind.地方, "辺境").Count);
            Assert.AreEqual(0, led.ByBox(BoxKind.地方, "首都").Count);

            // 決裁待ちに上げたものだけ拾う
            alli.status = PetitionStatus.決裁待ち;
            local.status = PetitionStatus.決裁待ち;
            Assert.AreEqual(2, led.AwaitingDecision().Count);
            Assert.AreEqual(1, led.AwaitingDecision(BoxKind.政治家).Count);
            Assert.AreEqual(1, led.AwaitingDecision(BoxKind.地方, "辺境").Count);
        }

        [Test]
        public void Active_Excludes_Resolved()
        {
            var led = new PetitionLedger();
            var a = New(1, Faction.同盟, BoxKind.政治家); // 起案=活性
            var b = New(2, Faction.同盟, BoxKind.政治家); b.status = PetitionStatus.執行済; // 決着
            var c = New(3, Faction.同盟, BoxKind.政治家); c.status = PetitionStatus.黙殺; // 非活性だが未決着
            led.Add(a); led.Add(b); led.Add(c);
            Assert.AreEqual(1, led.ActiveCount());
            Assert.AreEqual(1, led.Active().Count);
        }

        [Test]
        public void Prune_DropsOldestResolved_KeepsActive_OverCapacity()
        {
            var led = new PetitionLedger { capacity = 2 };
            var done1 = New(1, Faction.同盟, BoxKind.政治家); done1.status = PetitionStatus.執行済;
            var done2 = New(2, Faction.同盟, BoxKind.政治家); done2.status = PetitionStatus.却下;
            var live = New(3, Faction.同盟, BoxKind.政治家); // 活性
            led.Add(done1); led.Add(done2); // 容量2＝まだ落ちない
            Assert.AreEqual(2, led.Count);
            led.Add(live); // 3件目で超過＝古い決着済みを1件落とす
            Assert.AreEqual(2, led.Count);
            Assert.IsNull(led.Get(1));      // 最古の決着済みが落ちた
            Assert.IsNotNull(led.Get(2));
            Assert.IsNotNull(led.Get(3));   // 活性は残る
            Assert.AreEqual(1, led.droppedCount); // 打ち切りは可視化される
        }

        [Test]
        public void Prune_NeverDropsActive_EvenOverCapacity()
        {
            var led = new PetitionLedger { capacity = 1 };
            led.Add(New(1, Faction.同盟, BoxKind.政治家)); // 活性
            led.Add(New(2, Faction.同盟, BoxKind.政治家)); // 活性＝落とせない
            Assert.AreEqual(2, led.Count); // 活性は容量を超えても残る
            Assert.AreEqual(0, led.droppedCount);
        }

        [Test]
        public void Clear_ResetsEverything()
        {
            var led = new PetitionLedger();
            led.Add(New(0, Faction.同盟, BoxKind.政治家));
            led.droppedCount = 5;
            led.Clear();
            Assert.AreEqual(0, led.Count);
            Assert.AreEqual(0, led.droppedCount);
            var after = New(0, Faction.同盟, BoxKind.政治家);
            led.Add(after);
            Assert.AreEqual(1, after.id); // 採番もリセット
        }
    }
}
