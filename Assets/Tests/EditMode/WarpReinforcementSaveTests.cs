using System.Collections.Generic;
using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 航行中の援軍のセーブ往復（#38 C-5）。
    /// 到着は<b>絶対 game-秒</b>で持つので、クロックと一緒に往復すれば残り時間がそのまま保たれる
    /// ＝「ロードしたら援軍が消えた／即着した」を防ぐ。旧セーブは援軍なしで読める（前方互換）。
    /// </summary>
    public class WarpReinforcementSaveTests
    {
        private static readonly BattlefieldKey Choke = BattlefieldKey.Corridor(3, 7);
        private static readonly BattlefieldKey Other = BattlefieldKey.Corridor(1, 2);

        private static CampaignSaveData SaveWith(WarpReinforcementLedger ledger, GameClock clock)
        {
            var save = new CampaignSaveData();
            CampaignSerializer.WriteClock(save, clock);
            CampaignSerializer.WriteReinforcements(save, ledger);
            return save;
        }

        [Test]
        public void RoundTrip_KeepsRemainingTime()
        {
            var clock = new GameClock();
            clock.Advance(100f);                       // 現在 game-時刻 100 秒
            var ledger = new WarpReinforcementLedger();
            ledger.SyncTo(clock.elapsedSeconds);
            ledger.Dispatch(Choke, Faction.同盟, 42, 3000, 60f); // 160 秒に到着

            CampaignSaveData save = SaveWith(ledger, clock);
            GameClock rc = CampaignSerializer.ReadClock(save);
            WarpReinforcementLedger rl = CampaignSerializer.ReadReinforcements(save, rc);

            var buf = new List<WarpReinforcement>();
            Assert.AreEqual(1, rl.PeekPending(Choke, buf));
            Assert.AreEqual(160.0, buf[0].arrivalTime, 1e-3);
            Assert.AreEqual(60f, WarpReinforcementRules.RemainingSeconds(buf[0].arrivalTime, rl.Elapsed), 1e-3f);
            Assert.AreEqual(42, buf[0].fleetId);
            Assert.AreEqual(3000, buf[0].strength);
            Assert.AreEqual(Faction.同盟, buf[0].faction);
        }

        [Test]
        public void RoundTrip_DoesNotArriveImmediatelyAfterLoad()
        {
            var clock = new GameClock();
            clock.Advance(500f);
            var ledger = new WarpReinforcementLedger();
            ledger.SyncTo(clock.elapsedSeconds);
            ledger.Dispatch(Choke, Faction.帝国, 7, 1000, 120f);

            CampaignSaveData save = SaveWith(ledger, clock);
            GameClock rc = CampaignSerializer.ReadClock(save);
            WarpReinforcementLedger rl = CampaignSerializer.ReadReinforcements(save, rc);

            var arrived = new List<WarpReinforcement>();
            Assert.AreEqual(0, rl.TakeArrived(Choke, arrived), "ロード直後に到着してしまった");

            rl.SyncTo(rc.elapsedSeconds + 120.0);
            Assert.AreEqual(1, rl.TakeArrived(Choke, arrived));
        }

        [Test]
        public void RoundTrip_KeepsBattlefieldSeparation()
        {
            var clock = new GameClock();
            var ledger = new WarpReinforcementLedger();
            ledger.Dispatch(Choke, Faction.同盟, 1, 100, 10f);
            ledger.Dispatch(Other, Faction.同盟, 2, 200, 10f);

            CampaignSaveData save = SaveWith(ledger, clock);
            WarpReinforcementLedger rl = CampaignSerializer.ReadReinforcements(save, CampaignSerializer.ReadClock(save));

            var a = new List<WarpReinforcement>();
            var b = new List<WarpReinforcement>();
            Assert.AreEqual(1, rl.PeekPending(Choke, a));
            Assert.AreEqual(1, rl.PeekPending(Other, b));
            Assert.AreEqual(1, a[0].fleetId);
            Assert.AreEqual(2, b[0].fleetId);
        }

        [Test]
        public void RoundTrip_KeepsSystemBattlefield()
        {
            var clock = new GameClock();
            var ledger = new WarpReinforcementLedger();
            BattlefieldKey sys = BattlefieldKey.System(9);
            ledger.Dispatch(sys, Faction.帝国, 5, 500, 30f);

            CampaignSaveData save = SaveWith(ledger, clock);
            WarpReinforcementLedger rl = CampaignSerializer.ReadReinforcements(save, CampaignSerializer.ReadClock(save));

            var buf = new List<WarpReinforcement>();
            Assert.AreEqual(1, rl.PeekPending(sys, buf));
            Assert.IsTrue(buf[0].battlefield.IsSystemBattle);
        }

        [Test]
        public void LegacySave_WithoutReinforcements_LoadsEmpty()
        {
            // 旧セーブ（リストが空／欠落）＝援軍なしで読める。
            var save = new CampaignSaveData();
            WarpReinforcementLedger rl = CampaignSerializer.ReadReinforcements(save, new GameClock());
            Assert.AreEqual(0, rl.PendingCount);

            Assert.AreEqual(0, CampaignSerializer.ReadReinforcements(null, null).PendingCount);
        }

        [Test]
        public void Write_WithNullLedger_ClearsList()
        {
            var save = new CampaignSaveData();
            save.reinforcements.Add(new ReinforcementSave { fleetId = 1 });
            CampaignSerializer.WriteReinforcements(save, null);
            Assert.AreEqual(0, save.reinforcements.Count);
        }

        [Test]
        public void PeekAll_ReturnsEveryPendingOrder()
        {
            var ledger = new WarpReinforcementLedger();
            ledger.Dispatch(Choke, Faction.同盟, 1, 100, 30f);
            ledger.Dispatch(Other, Faction.帝国, 2, 200, 10f);

            var buf = new List<WarpReinforcement>();
            Assert.AreEqual(2, ledger.PeekAll(buf));
            Assert.AreEqual(2, buf.Count);
            Assert.AreEqual(2, buf[0].fleetId, "到着予定の早い順に並ぶこと");
        }
    }
}
