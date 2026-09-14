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
        public void ReserveId_AdvancesSeq_WithoutAddingItems_AndNeverGoesBack()
        {
            var led = new PetitionLedger();
            led.ReserveId(7);
            Assert.AreEqual(0, led.Count, "予約は在席を増やさない");

            var a = New(0, Faction.同盟, BoxKind.政治家);
            Assert.IsTrue(led.Add(a));
            Assert.AreEqual(8, a.id, "予約した id の次から採番する");

            led.ReserveId(3);   // 小さい id では戻らない
            led.ReserveId(0);
            led.ReserveId(-5);
            var b = New(0, Faction.同盟, BoxKind.政治家);
            Assert.IsTrue(led.Add(b));
            Assert.AreEqual(9, b.id);
        }

        [Test]
        public void Clear_ResetsReservedSeq()
        {
            var led = new PetitionLedger();
            led.ReserveId(20);
            led.Clear();
            var a = New(0, Faction.同盟, BoxKind.政治家);
            led.Add(a);
            Assert.AreEqual(1, a.id, "新規戦役では予約も持ち越さない");
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

        // ===== 黙殺の保持と履歴打切り（petition-dormant-retention-20260914） =====

        private static Petition Dormant(int id, bool vindicated = false)
        {
            var p = New(id, Faction.同盟, BoxKind.政治家);
            p.status = PetitionStatus.黙殺;
            p.vindicated = vindicated;
            return p;
        }

        [Test]
        public void IsPrunableDormant_OnlyUnvindicatedDormant_IsResolvedUnchanged()
        {
            Assert.IsTrue(WorkflowRules.IsPrunableDormant(Dormant(1)));
            Assert.IsFalse(WorkflowRules.IsPrunableDormant(Dormant(2, vindicated: true)), "正しさ判明の黙殺は再浮上候補");
            Assert.IsFalse(WorkflowRules.IsPrunableDormant(null));
            foreach (PetitionStatus s in new[] { PetitionStatus.起案, PetitionStatus.伝播中, PetitionStatus.決裁待ち,
                                                 PetitionStatus.承認, PetitionStatus.再浮上, PetitionStatus.執行済, PetitionStatus.却下 })
            {
                var p = New(3, Faction.同盟, BoxKind.政治家); p.status = s;
                Assert.IsFalse(WorkflowRules.IsPrunableDormant(p), s.ToString());
            }
            // 黙殺は終端ではない（意味を維持）
            Assert.IsFalse(WorkflowRules.IsResolved(Dormant(4)));
            Assert.IsFalse(WorkflowRules.IsActive(Dormant(5)));
        }

        [Test]
        public void Prune_KeepsDormant_WithinCapacity()
        {
            var led = new PetitionLedger { capacity = 3 };
            led.Add(Dormant(1)); led.Add(Dormant(2)); led.Add(Dormant(3));
            Assert.AreEqual(3, led.Count, "容量内の黙殺は再浮上に備えて保持");
            Assert.AreEqual(0, led.droppedCount);
        }

        [Test]
        public void Prune_ManyDormant_BoundedToCapacity_OldestFirst()
        {
            var led = new PetitionLedger { capacity = 4 };
            for (int i = 1; i <= 100; i++) led.Add(Dormant(i));
            Assert.AreEqual(4, led.Count, "黙殺が多数でも容量で有界");
            Assert.AreEqual(96, led.droppedCount, "落とした件数を正確に積む");
            Assert.AreEqual(97, led.items[0].id, "古い順に落とし新しい黙殺が残る");
            Assert.AreEqual(100, led.items[3].id);
            for (int i = 0; i < led.Count; i++)
                Assert.AreEqual(PetitionStatus.黙殺, led.items[i].status, "状態は変えない");
        }

        [Test]
        public void Prune_DropsResolvedBeforeDormant_EvenIfDormantIsOlder()
        {
            var led = new PetitionLedger { capacity = 2 };
            var oldDormant = Dormant(1);
            var done = New(2, Faction.同盟, BoxKind.政治家); done.status = PetitionStatus.執行済;
            led.Add(oldDormant); led.Add(done);
            led.Add(New(3, Faction.同盟, BoxKind.政治家)); // 活性で超過
            Assert.IsNull(led.Get(2), "決着済みを先に整理");
            Assert.IsNotNull(led.Get(1), "決着済みで足りれば黙殺は残す");
            Assert.AreEqual(1, led.droppedCount);

            var rejected = New(4, Faction.同盟, BoxKind.政治家); rejected.status = PetitionStatus.却下;
            led.Add(rejected); // 超過1＝却下(新しい)が黙殺(古い)より先に落ちる
            Assert.IsNull(led.Get(4));
            Assert.IsNotNull(led.Get(1));
            Assert.AreEqual(2, led.droppedCount);

            led.Add(Dormant(5)); // 決着済みが無い＝古い黙殺から
            Assert.IsNull(led.Get(1));
            Assert.IsNotNull(led.Get(5));
            Assert.IsNotNull(led.Get(3));
            Assert.AreEqual(3, led.droppedCount);
        }

        [Test]
        public void Prune_NeverDropsProtectedStatuses_AllowsOverCapacity()
        {
            var led = new PetitionLedger { capacity = 1 };
            var statuses = new[] { PetitionStatus.起案, PetitionStatus.伝播中, PetitionStatus.決裁待ち,
                                   PetitionStatus.承認, PetitionStatus.再浮上 };
            for (int i = 0; i < statuses.Length; i++)
            {
                var p = New(i + 1, Faction.同盟, BoxKind.政治家); p.status = statuses[i];
                led.Add(p);
            }
            led.Add(Dormant(6, vindicated: true));
            led.Add(Dormant(7)); // これだけ落とせる
            Assert.AreEqual(6, led.Count, "保護対象だけで超過する場合は超過を許す");
            Assert.IsNull(led.Get(7));
            Assert.AreEqual(1, led.droppedCount);
            for (int i = 0; i < statuses.Length; i++)
                Assert.AreEqual(statuses[i], led.Get(i + 1).status, "状態は変えない");
            Assert.AreEqual(PetitionStatus.黙殺, led.Get(6).status);
        }

        [Test]
        public void Prune_VindicatedDormantProtected_StillResurfaces()
        {
            var led = new PetitionLedger { capacity = 1 };
            var vind = Dormant(1, vindicated: true);
            led.Add(vind);
            for (int i = 2; i <= 10; i++) led.Add(Dormant(i));
            Assert.AreSame(vind, led.Get(1), "正しさ判明の黙殺は落ちない");
            Assert.AreEqual(9, led.droppedCount);

            // 再浮上経路：黙殺→再浮上→決裁待ち（台帳の同じ実体・同じ id）
            Assert.IsTrue(PetitionFlowRules.Resurface(vind, 0.1f));
            Assert.AreEqual(PetitionStatus.再浮上, vind.status);
            PetitionFlowRules.MarkAwaitingDecision(vind);
            Assert.AreEqual(1, led.AwaitingDecision().Count);
            Assert.AreEqual(1, led.AwaitingDecision()[0].id);
        }

        [Test]
        public void RetainedDormant_WithinCapacity_CanLaterBeVindicatedAndResurface()
        {
            var led = new PetitionLedger { capacity = 8 };
            var dormant = Dormant(0);
            led.Add(dormant);
            led.Add(New(0, Faction.同盟, BoxKind.政治家));
            Assert.AreSame(dormant, led.Get(dormant.id));
            dormant.vindicated = true; // 後に正しさが判明
            Assert.IsTrue(PetitionFlowRules.Resurface(dormant, 0f));
            Assert.AreEqual(1 + 1, led.ActiveCount(), "再浮上は活性として数える");
        }

        [Test]
        public void Prune_Repeated_IsIdempotent_AndDoesNotReuseIds()
        {
            var led = new PetitionLedger { capacity = 2 };
            for (int i = 0; i < 5; i++) led.Add(Dormant(0));
            Assert.AreEqual(2, led.Count);
            Assert.AreEqual(3, led.droppedCount);
            led.Prune(); led.Prune();
            Assert.AreEqual(2, led.Count, "重複 Prune で追加削除しない");
            Assert.AreEqual(3, led.droppedCount, "重複 Prune で件数を積まない");
            Assert.AreEqual(5, led.LastIssuedId);

            var fresh = New(0, Faction.同盟, BoxKind.政治家);
            led.Add(fresh);
            Assert.AreEqual(6, fresh.id, "落とした黙殺の id を再利用しない");
        }

        [Test]
        public void SaveRoundTrip_KeepsNextIdAndDroppedCount_WhenNewestWasDropped()
        {
            var led = new PetitionLedger { capacity = 2 };
            var vind1 = Dormant(0, vindicated: true);
            var vind2 = Dormant(0, vindicated: true);
            led.Add(vind1); led.Add(vind2);
            led.Add(Dormant(0));   // id3＝最新だが打切り対象
            Assert.IsNull(led.Get(3));
            Assert.AreEqual(1, led.droppedCount);
            Assert.AreEqual(3, led.LastIssuedId);

            var save = new CampaignSaveData();
            CampaignSerializer.WritePetitions(save.petitions, led);
            CampaignSerializer.WritePetitionLedgerMeta(led, out save.petitionsLastIssuedId, out save.petitionsDroppedCount);
            string json = UnityEngine.JsonUtility.ToJson(save);
            CampaignSaveData parsed = UnityEngine.JsonUtility.FromJson<CampaignSaveData>(json);

            var restored = new PetitionLedger { capacity = 2 };
            CampaignSerializer.ReadPetitions(parsed.petitions, restored);
            CampaignSerializer.ReadPetitionLedgerMeta(restored, parsed.petitionsLastIssuedId, parsed.petitionsDroppedCount);

            Assert.AreEqual(2, restored.Count);
            Assert.IsTrue(restored.Get(1).vindicated);
            Assert.AreEqual(PetitionStatus.黙殺, restored.Get(2).status);
            Assert.AreEqual(1, restored.droppedCount, "打切り累計を往復");
            var fresh = New(0, Faction.同盟, BoxKind.政治家);
            restored.Add(fresh);
            Assert.AreEqual(4, fresh.id, "落ちた最新 id(3) を再利用しない");
        }

        [Test]
        public void SaveRoundTrip_SmallerCapacityOnLoad_AddsLoadDrops_LegacyMetaNoop()
        {
            var led = new PetitionLedger { capacity = 4 };
            for (int i = 0; i < 4; i++) led.Add(Dormant(0));
            var save = new CampaignSaveData();
            CampaignSerializer.WritePetitions(save.petitions, led);
            CampaignSerializer.WritePetitionLedgerMeta(led, out save.petitionsLastIssuedId, out save.petitionsDroppedCount);

            var restored = new PetitionLedger { capacity = 2 }; // 容量設定は保存しない＝読み込み側の設定で整理
            CampaignSerializer.ReadPetitions(save.petitions, restored);
            CampaignSerializer.ReadPetitionLedgerMeta(restored, save.petitionsLastIssuedId, save.petitionsDroppedCount);
            Assert.AreEqual(2, restored.capacity);
            Assert.AreEqual(2, restored.Count);
            Assert.AreEqual(2, restored.droppedCount, "読み込み時の打切りも数える");
            Assert.AreEqual(3, restored.items[0].id);

            // 旧セーブ（メタ 0）は採番・件数を変えない
            var legacy = new PetitionLedger();
            CampaignSerializer.ReadPetitions(save.petitions, legacy);
            CampaignSerializer.ReadPetitionLedgerMeta(legacy, 0, 0);
            Assert.AreEqual(0, legacy.droppedCount);
            Assert.AreEqual(4, legacy.LastIssuedId);
            Assert.DoesNotThrow(() => CampaignSerializer.ReadPetitionLedgerMeta(null, 5, 5));
            CampaignSerializer.WritePetitionLedgerMeta(null, out int nid, out int nd);
            Assert.AreEqual(0, nid); Assert.AreEqual(0, nd);
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
