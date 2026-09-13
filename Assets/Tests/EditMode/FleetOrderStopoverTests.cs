using System.Collections.Generic;
using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 飛び石移動の禁止と、その<b>予告・通知・盤面表示の一致</b>（実機QAで見つかった食い違いの回帰）。
    ///
    /// 症状：「アイガーへ移動」と命じたのに、艦隊は途中の他勢力星系（セロトーレ）で止まり、
    /// MAP のラベルと経路線もそこまでしか出ない。原因は既存規則の飛び石禁止で、
    /// <b>経路配列そのものが切り詰められる</b>＝表示ではなく実際の到着地が変わる。
    /// 規則自体は正しいので、<b>先読み（PredictedStop）と実挙動（WarpTo 後の実航路）が一致すること</b>を固定する。
    /// </summary>
    public class FleetOrderStopoverTests
    {
        private const Faction Me = Faction.同盟;
        private const Faction Foe = Faction.帝国;

        /// <summary>0=自国 → 1=自国 → 2=敵国 → 3=敵国 の一本道。</summary>
        private static GalaxyMap Chain(params Faction[] owners)
        {
            var map = new GalaxyMap();
            for (int i = 0; i < owners.Length; i++)
                map.AddSystem(new StarSystem { id = i, systemName = "S" + i, owner = owners[i] });
            for (int i = 0; i + 1 < owners.Length; i++)
                map.AddCorridor(new Corridor(i, i + 1, 1f, CorridorType.通商));
            return map;
        }

        private static StrategicFleet Fleet(int at) =>
            new StrategicFleet { id = 1, faction = Me, currentSystemId = at, strength = 300 };

        // ── 先読みが実挙動と一致する ──

        [Test]
        public void PredictedStop_MatchesActualFinalDestination_WhenTruncated()
        {
            // 0(自) → 1(自) → 2(敵) → 3(敵)。3 を指しても 2 で止まる。
            GalaxyMap map = Chain(Me, Me, Foe, Foe);
            StrategicFleet f = Fleet(0);

            int predicted = FleetOrderRules.PredictedStop(map, f, Me, 3);
            Assert.AreEqual(2, predicted, "最初の非自勢力星系で止まるはず");

            Assert.IsTrue(f.WarpTo(map, 3));
            Assert.AreEqual(predicted, f.FinalDestinationId, "先読みと実際の最終目標が食い違っている");
            Assert.AreNotEqual(3, f.FinalDestinationId, "目的地まで行けるかのように保存してはいけない");
        }

        [Test]
        public void PredictedStop_IsGoal_WhenAllOwnTerritory()
        {
            GalaxyMap map = Chain(Me, Me, Me, Me);
            StrategicFleet f = Fleet(0);

            Assert.AreEqual(3, FleetOrderRules.PredictedStop(map, f, Me, 3));
            Assert.IsTrue(f.WarpTo(map, 3));
            Assert.AreEqual(3, f.FinalDestinationId);
        }

        [Test]
        public void PredictedStop_AdjacentEnemy_IsThatSystem()
        {
            GalaxyMap map = Chain(Me, Foe, Foe);
            StrategicFleet f = Fleet(0);
            Assert.AreEqual(1, FleetOrderRules.PredictedStop(map, f, Me, 2));
        }

        [Test]
        public void PredictedStop_SameSystem_ReturnsGoal()
        {
            GalaxyMap map = Chain(Me, Me);
            Assert.AreEqual(0, FleetOrderRules.PredictedStop(map, Fleet(0), Me, 0));
        }

        [Test]
        public void PredictedStop_NullSafe()
        {
            GalaxyMap map = Chain(Me, Me);
            Assert.AreEqual(5, FleetOrderRules.PredictedStop(null, Fleet(0), Me, 5));
            Assert.AreEqual(5, FleetOrderRules.PredictedStop(map, null, Me, 5));
        }

        // ── 予告文（発令前に出す一言）──

        [Test]
        public void StopoverNote_EmptyWhenReachable_TextWhenTruncated()
        {
            GalaxyMap map = Chain(Me, Me, Foe, Foe);
            StrategicFleet f = Fleet(0);

            Assert.AreEqual("", FleetOrderRules.StopoverNote(map, f, Me, 1, id => "S" + id));

            string note = FleetOrderRules.StopoverNote(map, f, Me, 3, id => "S" + id);
            Assert.IsNotEmpty(note);
            StringAssert.Contains("S2", note, "止まる星系の名前が入っていない");
        }

        [Test]
        public void StopoverNote_NullNameResolver_DoesNotThrow()
        {
            GalaxyMap map = Chain(Me, Me, Foe, Foe);
            Assert.DoesNotThrow(() => FleetOrderRules.StopoverNote(map, Fleet(0), Me, 3, null));
        }

        // ── 実航路をそのまま読める（盤面表示が再計算とずれない）──

        [Test]
        public void RemainingRoute_MatchesTruncatedPlan()
        {
            // 0(自) → 1(自) → 2(自) → 3(敵)。3 まで行けるので経路は 2,3 が残る。
            GalaxyMap map = Chain(Me, Me, Me, Foe);
            StrategicFleet f = Fleet(0);
            Assert.IsTrue(f.WarpTo(map, 3));

            IReadOnlyList<int> rest = f.RemainingRoute;
            Assert.AreEqual(1, f.destinationSystemId, "最初のホップ");
            CollectionAssert.AreEqual(new[] { 2, 3 }, new List<int>(rest));
            Assert.AreEqual(3, f.FinalDestinationId);
        }

        [Test]
        public void RemainingRoute_IsEmpty_WhenSingleHop()
        {
            GalaxyMap map = Chain(Me, Foe);
            StrategicFleet f = Fleet(0);
            Assert.IsTrue(f.WarpTo(map, 1));
            Assert.AreEqual(0, f.RemainingRoute.Count);
            Assert.IsFalse(FleetOrderRules.IsMultiHop(f));
        }

        [Test]
        public void RemainingRoute_IsEmpty_WhenDocked()
        {
            Assert.AreEqual(0, Fleet(0).RemainingRoute.Count);
        }

        // ── 移動中の再命令でも一致する ──

        [Test]
        public void Reorder_WhileMoving_PredictionStillMatches()
        {
            // 0(自) → 1(自) → 2(自) → 3(敵)。まず 2 へ動き、途中で 3 を指す。
            GalaxyMap map = Chain(Me, Me, Me, Foe);
            StrategicFleet f = Fleet(0);
            Assert.IsTrue(f.WarpTo(map, 2));
            Assert.IsTrue(f.IsOnCorridor);

            int predicted = FleetOrderRules.PredictedStop(map, f, Me, 3);
            Assert.IsTrue(f.WarpTo(map, 3));
            Assert.AreEqual(predicted, f.FinalDestinationId, "移動中の再命令でも先読みと一致すること");
        }

        [Test]
        public void Reorder_WhileMoving_TowardEnemy_StopsAtCurrentHop()
        {
            // 到達予定の星系が自勢力でなければ、そこで止まって占領する（既存規則）。
            GalaxyMap map = Chain(Me, Foe, Foe);
            StrategicFleet f = Fleet(0);
            Assert.IsTrue(f.WarpTo(map, 1));

            Assert.IsTrue(f.WarpTo(map, 2));
            Assert.AreEqual(1, f.FinalDestinationId, "敵星系へ入る途中で先へ飛び越えないこと");
            Assert.AreEqual(0, f.RemainingRoute.Count);
        }

        // ── 切り詰め規則そのもの ──

        [Test]
        public void FirstUnownedIndex_FindsFirstForeignSystem()
        {
            GalaxyMap map = Chain(Me, Me, Foe, Foe);
            var path = new List<int> { 0, 1, 2, 3 };
            Assert.AreEqual(2, FleetOrderRules.FirstUnownedIndex(map, path, Me));
        }

        [Test]
        public void FirstUnownedIndex_ReturnsLast_WhenAllOwn()
        {
            GalaxyMap map = Chain(Me, Me, Me);
            var path = new List<int> { 0, 1, 2 };
            Assert.AreEqual(2, FleetOrderRules.FirstUnownedIndex(map, path, Me));
        }

        [Test]
        public void FirstUnownedIndex_NullSafe()
        {
            Assert.AreEqual(0, FleetOrderRules.FirstUnownedIndex(null, new List<int> { 0, 1 }, Me));
            Assert.AreEqual(0, FleetOrderRules.FirstUnownedIndex(Chain(Me), null, Me));
            Assert.AreEqual(0, FleetOrderRules.FirstUnownedIndex(Chain(Me), new List<int>(), Me));
        }
    }
}
