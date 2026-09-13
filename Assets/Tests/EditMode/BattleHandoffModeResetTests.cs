using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 会戦の受け渡し（<see cref="BattleHandoff"/>）の<b>モードと戦場キーの後始末</b>（#40×#38 の回帰）。
    ///
    /// 受け渡しは static の単一スロットなので、片付け漏れがあると次の会戦へ漏れる。
    /// 監査で見つかった実害：要塞戦のあと <c>IsCorridorFortress</c> が残り、通常の艦隊戦まで
    /// 要塞マップとして組まれる／前の回廊の <c>battlefield</c> が残り、別の会戦が他所宛ての援軍を吸い込む。
    /// </summary>
    public class BattleHandoffModeResetTests
    {
        [SetUp]
        public void Reset()
        {
            BattleHandoff.Clear();
            BattleHandoff.fleets.Clear();
        }

        private static void MarkFortressBattle()
        {
            BattleHandoff.Pending = true;
            BattleHandoff.IsCorridorFortress = true;
            BattleHandoff.fortressCorridorA = 3;
            BattleHandoff.fortressCorridorB = 7;
            BattleHandoff.battlefield = BattlefieldKey.Corridor(3, 7);
            BattleHandoff.SetFortressResult(true, false, 0);
        }

        // ── Clear がモードと戦場キーを落とす ──

        [Test]
        public void Clear_ResetsFortressModeAndBattlefield()
        {
            MarkFortressBattle();
            BattleHandoff.Clear();

            Assert.IsFalse(BattleHandoff.IsCorridorFortress, "要塞モードが残ると次の会戦が要塞マップになる");
            Assert.IsFalse(BattleHandoff.fortressResolved);
            Assert.IsFalse(BattleHandoff.fortressBreached);
            Assert.IsFalse(BattleHandoff.fortressStillHolds);
            Assert.IsFalse(BattleHandoff.battlefield.IsValid, "戦場キーが残ると他所宛ての援軍を吸い込む");
        }

        [Test]
        public void Clear_AlsoResetsOtherModes()
        {
            BattleHandoff.IsPlanetSiege = true;
            BattleHandoff.IsSystemView = true;
            BattleHandoff.Clear();
            Assert.IsFalse(BattleHandoff.IsPlanetSiege);
            Assert.IsFalse(BattleHandoff.IsSystemView);
        }

        // ── 各 Queue が要塞モードを持ち込まない（モードは排他）──

        [Test]
        public void Queue_ClearsFortressMode()
        {
            MarkFortressBattle();
            var a = new StrategicFleet { id = 1, faction = Faction.同盟, currentSystemId = 0, strength = 100 };
            var b = new StrategicFleet { id = 2, faction = Faction.帝国, currentSystemId = 0, strength = 100 };

            BattleHandoff.Queue(a, b, "Strategy");

            Assert.IsFalse(BattleHandoff.IsCorridorFortress, "通常の艦隊戦が要塞マップとして組まれてしまう");
            Assert.IsFalse(BattleHandoff.IsPlanetSiege);
            Assert.IsFalse(BattleHandoff.IsSystemView);
        }

        [Test]
        public void QueueMulti_ClearsFortressMode()
        {
            MarkFortressBattle();
            BattleHandoff.QueueMulti(null, Faction.同盟, Faction.帝国, 1, 2, "Strategy");
            Assert.IsFalse(BattleHandoff.IsCorridorFortress);
        }

        [Test]
        public void QueueSystemView_ClearsFortressMode()
        {
            MarkFortressBattle();
            BattleHandoff.QueueSystemView(5, "テスト星系", Faction.同盟, "Strategy");
            Assert.IsFalse(BattleHandoff.IsCorridorFortress);
            Assert.IsTrue(BattleHandoff.IsSystemView);
            Assert.IsFalse(BattleHandoff.IsPlanetSiege);
        }

        // ── スナップショット往復（ウィンドウ化会戦）──

        [Test]
        public void CaptureRestore_CarriesFortressStateAndBattlefield()
        {
            MarkFortressBattle();
            BattleHandoff.fortressName = "イゼルローン要塞";
            BattleHandoff.fortressOwner = Faction.帝国;
            BattleHandoff.fortressAttacker = Faction.同盟;
            BattleHandoff.fortressGarrison = 1200f;

            BattleHandoff.State s = BattleHandoff.Capture();
            BattleHandoff.Clear();
            Assert.IsFalse(BattleHandoff.IsCorridorFortress, "前提：Clear で落ちている");

            BattleHandoff.Restore(s);

            Assert.IsTrue(BattleHandoff.IsCorridorFortress);
            Assert.AreEqual(BattlefieldKey.Corridor(3, 7), BattleHandoff.battlefield);
            Assert.AreEqual("イゼルローン要塞", BattleHandoff.fortressName);
            Assert.AreEqual(Faction.帝国, BattleHandoff.fortressOwner);
            Assert.AreEqual(Faction.同盟, BattleHandoff.fortressAttacker);
            Assert.AreEqual(1200f, BattleHandoff.fortressGarrison, 1e-3f);
            Assert.IsTrue(BattleHandoff.fortressResolved);
            Assert.IsTrue(BattleHandoff.fortressBreached);
        }

        [Test]
        public void ClearFortress_KeepsOtherHandoffFields()
        {
            // 要塞ぶんだけ片付ける窓口（結果の反映後に呼ぶ）。ほかの受け渡し内容は壊さない。
            BattleHandoff.Pending = true;
            BattleHandoff.strengthA = 4321;
            MarkFortressBattle();

            BattleHandoff.ClearFortress();

            Assert.IsFalse(BattleHandoff.IsCorridorFortress);
            Assert.IsFalse(BattleHandoff.fortressResolved);
            Assert.AreEqual(4321, BattleHandoff.strengthA, "無関係のフィールドまで消してはいけない");
        }

        // ── 結果の書き込み ──

        [Test]
        public void SetFortressResult_RecordsAllThreeOutcomes()
        {
            BattleHandoff.SetFortressResult(true, false, 0);   // 突破して制圧
            Assert.IsTrue(BattleHandoff.fortressResolved);
            Assert.IsTrue(BattleHandoff.fortressBreached);
            Assert.IsFalse(BattleHandoff.fortressStillHolds);

            BattleHandoff.SetFortressResult(false, false, 0);  // 撃破したが未突破
            Assert.IsFalse(BattleHandoff.fortressBreached);
            Assert.IsFalse(BattleHandoff.fortressStillHolds);

            BattleHandoff.SetFortressResult(false, true, 0);   // 持ちこたえた
            Assert.IsFalse(BattleHandoff.fortressBreached);
            Assert.IsTrue(BattleHandoff.fortressStillHolds);
        }
    }
}
