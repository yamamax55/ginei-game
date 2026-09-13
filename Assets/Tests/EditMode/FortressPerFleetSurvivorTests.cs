using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 手動で戦った要塞戦（#40）の損害は<b>艦隊ごとの実残存</b>で返す、という受け渡しの契約。
    ///
    /// レビュー指摘：合計だけを按分すると「無傷のA」と「全滅したB」を足して両方が半減する。
    /// 手動戦術では戦略艦隊IDごとの明細を返し、明細があるかぎり按分しない。
    /// （抽象的な自動解決だけが従来どおり合計の按分にフォールバックする。）
    /// </summary>
    public class FortressPerFleetSurvivorTests
    {
        [SetUp]
        public void Reset() => BattleHandoff.Clear();

        private static void Record(int fleetId, int survivor)
            => BattleHandoff.fortressSurvivors.Add(
                   new BattleHandoff.FleetSurvivor { fleetId = fleetId, survivor = survivor });

        [Test]
        public void PerFleetSurvivors_DoNotAverageAcrossFleets()
        {
            // A=200 は無傷、B=200 は全滅。合計残存は 200 だが、按分すると両方100になってしまう。
            Record(1, 200);
            Record(2, 0);

            Assert.AreEqual(2, BattleHandoff.fortressSurvivors.Count);
            Assert.AreEqual(200, BattleHandoff.fortressSurvivors[0].survivor, "無傷の隊は減らない");
            Assert.AreEqual(0, BattleHandoff.fortressSurvivors[1].survivor, "全滅した隊は0");

            // 参考：同じ状況を按分に掛けると両方100になる＝これを避けるための明細である。
            var before = new System.Collections.Generic.List<int> { 200, 200 };
            var after = new System.Collections.Generic.List<int>();
            FleetAttritionRules.Distribute(before, 200, after);
            Assert.AreEqual(100, after[0]);
            Assert.AreEqual(100, after[1]);
        }

        [Test]
        public void Clear_DropsSurvivorDetail()
        {
            Record(1, 50);
            BattleHandoff.Clear();
            Assert.AreEqual(0, BattleHandoff.fortressSurvivors.Count,
                            "明細が次の会戦へ漏れると無関係な艦隊の兵力を書き換える");
        }

        [Test]
        public void ClearFortress_DropsSurvivorDetail()
        {
            Record(1, 50);
            BattleHandoff.ClearFortress();
            Assert.AreEqual(0, BattleHandoff.fortressSurvivors.Count);
        }

        [Test]
        public void CaptureRestore_CarriesSurvivorDetail()
        {
            Record(5, 120);
            Record(6, 0);

            BattleHandoff.State s = BattleHandoff.Capture();
            BattleHandoff.Clear();
            Assert.AreEqual(0, BattleHandoff.fortressSurvivors.Count);

            BattleHandoff.Restore(s);

            Assert.AreEqual(2, BattleHandoff.fortressSurvivors.Count);
            Assert.AreEqual(5, BattleHandoff.fortressSurvivors[0].fleetId);
            Assert.AreEqual(120, BattleHandoff.fortressSurvivors[0].survivor);
            Assert.AreEqual(6, BattleHandoff.fortressSurvivors[1].fleetId);
            Assert.AreEqual(0, BattleHandoff.fortressSurvivors[1].survivor);
        }

        [Test]
        public void EmptyDetail_MeansFallBackToProportionalSplit()
        {
            // 自動解決は明細を持たない＝呼び手は合計の按分へ落ちる、という約束。
            BattleHandoff.SetFortressResult(false, true, 150);
            Assert.AreEqual(0, BattleHandoff.fortressSurvivors.Count);
            Assert.AreEqual(150, BattleHandoff.fortressAttackerSurvivor);
        }

        // ── 艦艇数も艦隊ごとに独立して減る ──

        [Test]
        public void ShipsFollowEachFleetsOwnSurvivor()
        {
            // A：200→200（無傷・8000隻のまま）／B：200→0（全滅・0隻）
            var a = new StrategicFleet { id = 1, strength = 200 };
            a.SetShips(8000);
            var b = new StrategicFleet { id = 2, strength = 200 };
            b.SetShips(8000);

            a.SetShips(FleetShipCountRules.AfterLosses(a.Ships, 200, 200));
            b.SetShips(FleetShipCountRules.AfterLosses(b.Ships, 200, 0));

            Assert.AreEqual(8000, a.Ships, "無傷の隊の艦艇が減ってはいけない");
            Assert.AreEqual(0, b.Ships, "全滅した隊は0隻");
            Assert.IsTrue(b.shipCountSet, "0隻が未初期化と誤解されないこと");
        }
    }
}
