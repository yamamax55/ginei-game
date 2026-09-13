using System.Collections.Generic;
using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 援軍（ワープイン・#38 C-5）の台帳：所要時間どおりの到着／ポーズ・倍速・分割 dt 耐性／
    /// 二重出現しない／別戦場に混入しない／決着後の差し戻し。
    /// </summary>
    public class WarpReinforcementLedgerTests
    {
        private static readonly BattlefieldKey FieldA = BattlefieldKey.Corridor(1, 2);
        private static readonly BattlefieldKey FieldB = BattlefieldKey.Corridor(3, 4);

        private static WarpReinforcementLedger NewLedger() => new WarpReinforcementLedger();

        private static List<WarpReinforcement> Buf() => new List<WarpReinforcement>();

        // ===== 所要時間どおりに到着する =====

        [Test]
        public void 所要時間ちょうどで到着する()
        {
            var led = NewLedger();
            long id = led.Dispatch(FieldA, Faction.帝国, 7, 500, 60f);
            Assert.Greater(id, 0L);

            var got = Buf();
            led.Advance(59.9);
            Assert.AreEqual(0, led.TakeArrived(FieldA, got), "早すぎる到着はしない");

            led.Advance(0.1);   // 累計 60.0
            Assert.AreEqual(1, led.TakeArrived(FieldA, got));
            Assert.AreEqual(1, got.Count);
            Assert.AreEqual(7, got[0].fleetId);
            Assert.AreEqual(500, got[0].strength);
            Assert.AreEqual(Faction.帝国, got[0].faction);
            Assert.AreEqual(60.0, got[0].arrivalTime, 1e-6);
        }

        [Test]
        public void 遅れて到着しない_長く待っても取り出せる()
        {
            var led = NewLedger();
            led.Dispatch(FieldA, Faction.同盟, 1, 100, 10f);
            led.Advance(1000.0);
            var got = Buf();
            Assert.AreEqual(1, led.TakeArrived(FieldA, got));
        }

        [Test]
        public void 到着順に取り出される()
        {
            var led = NewLedger();
            led.Dispatch(FieldA, Faction.帝国, 1, 100, 90f);  // 後
            led.Dispatch(FieldA, Faction.帝国, 2, 100, 30f);  // 先
            led.Advance(100.0);

            var got = Buf();
            Assert.AreEqual(2, led.TakeArrived(FieldA, got));
            Assert.AreEqual(2, got[0].fleetId);
            Assert.AreEqual(1, got[1].fleetId);
        }

        // ===== ポーズ =====

        [Test]
        public void ポーズ中は進まず到着しない()
        {
            var led = NewLedger();
            led.Dispatch(FieldA, Faction.帝国, 1, 100, 30f);

            for (int i = 0; i < 100; i++) led.Advance(0.0);   // dt=0＝ポーズ
            Assert.AreEqual(0.0, led.Elapsed, 1e-9);
            Assert.AreEqual(0, led.TakeArrived(FieldA, Buf()));
            Assert.AreEqual(1, led.PendingCount);
        }

        [Test]
        public void 負のdtでは巻き戻らない()
        {
            var led = NewLedger();
            led.Advance(50.0);
            led.Advance(-20.0);
            Assert.AreEqual(50.0, led.Elapsed, 1e-9);
        }

        // ===== 倍速・分割 dt 耐性 =====

        [Test]
        public void 分割dtでも到着game時刻が一致する()
        {
            // 一括で 60 秒進める台帳と、6×10 秒で進める台帳（＝倍速で刻みが変わっても同じ）
            var bulk = NewLedger();
            var split = NewLedger();
            bulk.Dispatch(FieldA, Faction.帝国, 1, 100, 60f);
            split.Dispatch(FieldA, Faction.帝国, 1, 100, 60f);

            bulk.Advance(60.0);
            for (int i = 0; i < 6; i++) split.Advance(10.0);

            Assert.AreEqual(bulk.Elapsed, split.Elapsed, 1e-9);

            var a = Buf(); var b = Buf();
            Assert.AreEqual(1, bulk.TakeArrived(FieldA, a));
            Assert.AreEqual(1, split.TakeArrived(FieldA, b));
            Assert.AreEqual(a[0].arrivalTime, b[0].arrivalTime, 1e-9);
        }

        [Test]
        public void 倍速で刻んでも早着しない()
        {
            // 3倍速＝実時間 1 秒あたり game-3 秒（呼び手が game-秒に変換して渡す想定）
            var led = NewLedger();
            led.Dispatch(FieldA, Faction.帝国, 1, 100, 60f);
            for (int i = 0; i < 19; i++) led.Advance(3.0);       // 57 game-秒
            Assert.AreEqual(0, led.TakeArrived(FieldA, Buf()), "到着 game-時刻より前に出てこない");
            led.Advance(3.0);                                    // 60 game-秒
            Assert.AreEqual(1, led.TakeArrived(FieldA, Buf()));
        }

        [Test]
        public void SyncToはクロックの累積へ同期し過去へ戻らない()
        {
            var led = NewLedger();
            led.Dispatch(FieldA, Faction.帝国, 1, 100, 60f);
            led.SyncTo(59.0);
            Assert.AreEqual(0, led.TakeArrived(FieldA, Buf()));
            led.SyncTo(60.0);
            Assert.AreEqual(1, led.TakeArrived(FieldA, Buf()));

            led.SyncTo(10.0);                 // 過去への同期は無視
            Assert.AreEqual(60.0, led.Elapsed, 1e-9);
        }

        // ===== 二重出現しない =====

        [Test]
        public void 同じ派遣は二度取り出せない()
        {
            var led = NewLedger();
            led.Dispatch(FieldA, Faction.帝国, 1, 100, 10f);
            led.Advance(10.0);

            Assert.AreEqual(1, led.TakeArrived(FieldA, Buf()));
            Assert.AreEqual(0, led.PendingCount);
            Assert.AreEqual(0, led.TakeArrived(FieldA, Buf()), "二度目は出てこない＝二重出現しない");

            led.Advance(1000.0);
            Assert.AreEqual(0, led.TakeArrived(FieldA, Buf()));
        }

        // ===== 別戦場に混入しない =====

        [Test]
        public void 別戦場の派遣は混ざらない()
        {
            var led = NewLedger();
            led.Dispatch(FieldA, Faction.帝国, 11, 100, 10f);
            led.Dispatch(FieldB, Faction.帝国, 22, 100, 10f);
            led.Advance(10.0);

            var got = Buf();
            Assert.AreEqual(1, led.TakeArrived(FieldA, got));
            Assert.AreEqual(11, got[0].fleetId);
            Assert.AreEqual(1, led.PendingCount, "別戦場ぶんは残る");

            got.Clear();
            Assert.AreEqual(1, led.TakeArrived(FieldB, got));
            Assert.AreEqual(22, got[0].fleetId);
        }

        [Test]
        public void 端の順序が逆でも同じ戦場として取り出せる()
        {
            var led = NewLedger();
            led.Dispatch(BattlefieldKey.Corridor(2, 1), Faction.帝国, 1, 100, 10f);
            led.Advance(10.0);
            Assert.AreEqual(1, led.TakeArrived(BattlefieldKey.Corridor(1, 2), Buf()));
        }

        [Test]
        public void 無効な戦場キーは受理しない()
        {
            var led = NewLedger();
            Assert.AreEqual(0L, led.Dispatch(default, Faction.帝国, 1, 100, 10f));
            Assert.AreEqual(0, led.PendingCount);
            Assert.AreEqual(0, led.TakeArrived(default, Buf()));
        }

        [Test]
        public void 兵力0以下は受理しない()
        {
            var led = NewLedger();
            Assert.AreEqual(0L, led.Dispatch(FieldA, Faction.帝国, 1, 0, 10f));
            Assert.AreEqual(0L, led.Dispatch(FieldA, Faction.帝国, 1, -5, 10f));
            Assert.AreEqual(0, led.PendingCount);
        }

        [Test]
        public void 到達不能な所要時間は受理しない()
        {
            var led = NewLedger();
            Assert.AreEqual(0L, led.Dispatch(FieldA, Faction.帝国, 1, 100, WarpReinforcementRules.Unreachable));
            Assert.AreEqual(0, led.PendingCount);
        }

        // ===== 戦闘終了後（戦場が閉じている）=====

        [Test]
        public void 決着で未到着の援軍は差し戻される()
        {
            var led = NewLedger();
            led.Dispatch(FieldA, Faction.帝国, 1, 300, 60f);
            led.Dispatch(FieldA, Faction.同盟, 2, 400, 90f);
            led.Dispatch(FieldB, Faction.帝国, 3, 500, 60f);

            led.Advance(30.0);
            var diverted = Buf();
            Assert.AreEqual(2, led.CloseBattlefield(FieldA, diverted), "その戦場ぶんだけ差し戻す");
            Assert.AreEqual(1, led.PendingCount, "別戦場の派遣は残る");
            Assert.AreEqual(300, diverted[0].strength);
            Assert.AreEqual(400, diverted[1].strength);
            Assert.IsTrue(led.IsClosed(FieldA));
            Assert.IsFalse(led.IsClosed(FieldB));
        }

        [Test]
        public void 決着後に到着しても参戦しない()
        {
            var led = NewLedger();
            led.Dispatch(FieldA, Faction.帝国, 1, 100, 60f);
            led.CloseBattlefield(FieldA, null);   // 差し戻し先を受け取らない呼び方でも台帳からは消える

            led.Advance(120.0);
            Assert.AreEqual(0, led.TakeArrived(FieldA, Buf()), "閉じた戦場へは参戦させない");
            Assert.AreEqual(0, led.PendingCount);
        }

        [Test]
        public void 閉じた戦場への新規派遣は受理しない()
        {
            var led = NewLedger();
            led.CloseBattlefield(FieldA, null);
            Assert.AreEqual(0L, led.Dispatch(FieldA, Faction.帝国, 1, 100, 10f));
            Assert.AreEqual(0, led.PendingCount);

            // 同じ回廊で改めて会戦が起きたら開き直せる
            Assert.IsTrue(led.ReopenBattlefield(FieldA));
            Assert.IsFalse(led.ReopenBattlefield(FieldA));
            Assert.Greater(led.Dispatch(FieldA, Faction.帝国, 1, 100, 10f), 0L);
        }

        [Test]
        public void 決着直前に到着済みで未取り出しのものも差し戻される()
        {
            var led = NewLedger();
            led.Dispatch(FieldA, Faction.帝国, 1, 100, 10f);
            led.Advance(20.0);                       // 到着済みだが取り出していない

            var diverted = Buf();
            Assert.AreEqual(1, led.CloseBattlefield(FieldA, diverted));
            Assert.AreEqual(1, diverted.Count, "取りこぼしを戦略へ返せる＝艦隊が消えない");
        }

        // ===== 照会（UI の到着予定表示）=====

        [Test]
        public void 到着予定を照会できる()
        {
            var led = NewLedger();
            long a = led.Dispatch(FieldA, Faction.帝国, 1, 100, 60f);
            long b = led.Dispatch(FieldA, Faction.同盟, 2, 200, 30f);
            led.Dispatch(FieldB, Faction.帝国, 3, 300, 15f);
            led.Advance(10.0);

            var list = Buf();
            Assert.AreEqual(2, led.PeekPending(FieldA, list), "その戦場ぶんだけ");
            Assert.AreEqual(2, list[0].fleetId, "到着予定順");
            Assert.AreEqual(1, list[1].fleetId);
            Assert.AreEqual(3, led.PendingCount, "照会では消えない");

            Assert.AreEqual(50f, led.RemainingSeconds(a), 1e-4f);
            Assert.AreEqual(20f, led.RemainingSeconds(b), 1e-4f);
            Assert.AreEqual(-1f, led.RemainingSeconds(9999L), 1e-4f);
            Assert.AreEqual(30.0, led.NextArrivalTime(FieldA), 1e-6);
            Assert.AreEqual(-1.0, led.NextArrivalTime(BattlefieldKey.Corridor(80, 81)), 1e-6);
        }

        [Test]
        public void 勢力で絞って照会できる()
        {
            var led = NewLedger();
            led.Dispatch(FieldA, Faction.帝国, 1, 100, 60f);
            led.Dispatch(FieldA, Faction.同盟, 2, 200, 30f);

            var list = Buf();
            Assert.AreEqual(1, led.PeekPending(FieldA, Faction.帝国, list));
            Assert.AreEqual(1, list[0].fleetId);
        }

        [Test]
        public void IDで引ける_所要時間も持つ()
        {
            var led = NewLedger();
            long id = led.Dispatch(FieldA, Faction.帝国, 1, 100, 45f);
            Assert.IsTrue(led.TryGet(id, out WarpReinforcement o));
            Assert.IsTrue(o.IsValid);
            Assert.AreEqual(45f, o.TravelSeconds, 1e-4f);
            Assert.AreEqual(0.0, o.dispatchTime, 1e-9);
            Assert.IsFalse(led.TryGet(9999L, out _));
        }

        // ===== 取り消し・全消去 =====

        [Test]
        public void 派遣を取り消せる()
        {
            var led = NewLedger();
            long id = led.Dispatch(FieldA, Faction.帝国, 8, 100, 60f);
            Assert.IsTrue(led.Cancel(id, out WarpReinforcement o));
            Assert.AreEqual(8, o.fleetId);
            Assert.AreEqual(0, led.PendingCount);
            Assert.IsFalse(led.Cancel(id, out _), "二度は取り消せない");

            led.Advance(100.0);
            Assert.AreEqual(0, led.TakeArrived(FieldA, Buf()));
        }

        [Test]
        public void Clearで空になる()
        {
            var led = NewLedger();
            led.Dispatch(FieldA, Faction.帝国, 1, 100, 60f);
            led.CloseBattlefield(FieldB, null);
            led.Advance(10.0);

            led.Clear();
            Assert.AreEqual(0, led.PendingCount);
            Assert.AreEqual(0, led.ClosedCount);
            Assert.AreEqual(0.0, led.Elapsed, 1e-9);
            Assert.IsFalse(led.IsClosed(FieldB));
        }

        // ===== 到着時刻を直接指定する経路（セーブ復元）=====

        [Test]
        public void DispatchAtで絶対到着時刻を指定できる()
        {
            var led = NewLedger();
            led.Advance(100.0);
            long id = led.DispatchAt(FieldA, Faction.帝国, 1, 100, 160.0);
            Assert.IsTrue(led.TryGet(id, out WarpReinforcement o));
            Assert.AreEqual(160.0, o.arrivalTime, 1e-6);
            Assert.AreEqual(60f, o.TravelSeconds, 1e-4f);
        }

        [Test]
        public void 過去の到着時刻は現在へ丸められ即到着する()
        {
            var led = NewLedger();
            led.Advance(100.0);
            long id = led.DispatchAt(FieldA, Faction.帝国, 1, 100, 10.0);
            Assert.Greater(id, 0L);
            Assert.AreEqual(1, led.TakeArrived(FieldA, Buf()));
        }
    }
}
