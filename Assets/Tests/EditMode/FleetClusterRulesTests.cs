using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 艦隊マーカーの集約（<see cref="FleetClusterRules"/>）を固定する。実機で「同じ場所の艦隊が
    /// 微小な駒とラベルで折り重なり、誰の何隊が居るのか読めない」問題への対処なので、
    /// <b>数を間違えないこと</b>が最優先＝欠落・二重計上・敵味方の混在を検証する。
    /// </summary>
    public class FleetClusterRulesTests
    {
        private const float R = FleetClusterRules.DefaultMergeRadius;

        // ships を省いたときは「兵力と同じ数字」を艦艇数として渡す＝ラベルの期待値を読みやすくするため。
        // 実際のゲームでは艦隊ごとの実隻数（StrategicFleet.Ships）を渡す。
        private static FleetMarkerInput F(int id, Faction fac, float x, float y, int strength = 100,
            bool moving = false, bool selected = false, int ships = -1)
            => new FleetMarkerInput(id, fac, new Vector2(x, y), strength, moving, selected,
                moving ? new Vector2(1f, 0f) : Vector2.zero, ships >= 0 ? ships : strength);

        private static List<FleetCluster> Build(params FleetMarkerInput[] fleets)
        {
            var outList = new List<FleetCluster>();
            FleetClusterRules.Build(fleets, R, outList);
            return outList;
        }

        [Test]
        public void NearbySameFaction_MergesIntoOneMarker()
        {
            var c = Build(
                F(1, Faction.同盟, 0f, 0f, 120),
                F(2, Faction.同盟, 0.2f, 0.1f, 80),
                F(3, Faction.同盟, -0.1f, 0.15f, 200));

            Assert.AreEqual(1, c.Count, "近接した同陣営が1つにまとまっていない");
            Assert.AreEqual(3, c[0].FleetCount);
            Assert.AreEqual(400, c[0].totalStrength);
            Assert.IsFalse(c[0].IsSingle);
        }

        [Test]
        public void DifferentFactions_AreNeverMixed()
        {
            var c = Build(
                F(1, Faction.同盟, 0f, 0f, 100),
                F(2, Faction.帝国, 0.05f, 0.05f, 100));  // ほぼ同じ位置でも混ぜない

            Assert.AreEqual(2, c.Count, "敵味方が同じマーカーにまとめられた");
            for (int i = 0; i < c.Count; i++) Assert.AreEqual(1, c[i].FleetCount);
        }

        [Test]
        public void MovingAndStationed_AreNotMixed()
        {
            var c = Build(
                F(1, Faction.同盟, 0f, 0f, 100),
                F(2, Faction.同盟, 0.05f, 0f, 100, moving: true));

            Assert.AreEqual(2, c.Count, "停泊中と航行中が同じマーカーになった（意味が違う）");
        }

        [Test]
        public void FarApart_StaySeparate()
        {
            var c = Build(
                F(1, Faction.同盟, 0f, 0f),
                F(2, Faction.同盟, 5f, 0f));
            Assert.AreEqual(2, c.Count);
        }

        [Test]
        public void EveryFleetAppearsExactlyOnce()
        {
            var fleets = new List<FleetMarkerInput>();
            int id = 1;
            // 3か所に固まりを作り、間にばらけた艦隊も混ぜる。
            for (int i = 0; i < 5; i++) fleets.Add(F(id++, Faction.同盟, 0f + i * 0.08f, 0f, 50));
            for (int i = 0; i < 4; i++) fleets.Add(F(id++, Faction.帝国, 3f, 1f + i * 0.05f, 70));
            for (int i = 0; i < 3; i++) fleets.Add(F(id++, Faction.同盟, -4f + i * 2f, -2f, 90));

            var outList = new List<FleetCluster>();
            FleetClusterRules.Build(fleets, R, outList);

            // 欠落なし・二重計上なし。
            Assert.AreEqual(fleets.Count, FleetClusterRules.TotalFleets(outList), "艦隊数の合計が入力と合わない");

            int expectedStrength = 0;
            for (int i = 0; i < fleets.Count; i++) expectedStrength += fleets[i].strength;
            Assert.AreEqual(expectedStrength, FleetClusterRules.TotalStrength(outList), "総兵力が入力と合わない");

            var seen = new HashSet<int>();
            for (int i = 0; i < outList.Count; i++)
                for (int j = 0; j < outList[i].fleetIds.Count; j++)
                    Assert.IsTrue(seen.Add(outList[i].fleetIds[j]), $"艦隊 {outList[i].fleetIds[j]} が2つのマーカーに含まれる");
            Assert.AreEqual(fleets.Count, seen.Count);
        }

        [Test]
        public void Result_IsDeterministic_RegardlessOfInputOrder()
        {
            var a = new List<FleetMarkerInput> { F(3, Faction.同盟, 0.1f, 0f), F(1, Faction.同盟, 0f, 0f), F(2, Faction.同盟, 0.2f, 0f) };
            var b = new List<FleetMarkerInput> { F(1, Faction.同盟, 0f, 0f), F(2, Faction.同盟, 0.2f, 0f), F(3, Faction.同盟, 0.1f, 0f) };

            var ca = new List<FleetCluster>(); FleetClusterRules.Build(a, R, ca);
            var cb = new List<FleetCluster>(); FleetClusterRules.Build(b, R, cb);

            Assert.AreEqual(ca.Count, cb.Count);
            for (int i = 0; i < ca.Count; i++)
            {
                CollectionAssert.AreEqual(ca[i].fleetIds, cb[i].fleetIds, "入力順で結果が変わる＝マーカーがちらつく");
                Assert.AreEqual(ca[i].center.x, cb[i].center.x, 1e-4f);
            }
        }

        [Test]
        public void FleetIds_AreSortedForAStableList()
        {
            var c = Build(F(9, Faction.同盟, 0f, 0f), F(2, Faction.同盟, 0.1f, 0f), F(5, Faction.同盟, 0.2f, 0f));
            Assert.AreEqual(1, c.Count);
            CollectionAssert.AreEqual(new[] { 2, 5, 9 }, c[0].fleetIds, "一覧の並びが不安定＝クリックのたびに順が変わる");
        }

        [Test]
        public void SelectionFlag_PropagatesToTheCluster()
        {
            var c = Build(F(1, Faction.同盟, 0f, 0f), F(2, Faction.同盟, 0.1f, 0f, selected: true));
            Assert.AreEqual(1, c.Count);
            Assert.IsTrue(c[0].anySelected, "選択中の艦隊を含むまとまりが強調されない＝選択を見失う");
        }

        [Test]
        public void MarkerLabel_ShowsCountOnlyWhenAggregated()
        {
            var single = Build(F(1, Faction.同盟, 0f, 0f, 1234));
            Assert.AreEqual("1,234隻", FleetClusterRules.MarkerLabel(single[0]), "単独なのに艦隊数を出している");

            var many = Build(F(1, Faction.同盟, 0f, 0f, 1000), F(2, Faction.同盟, 0.1f, 0f, 234));
            Assert.AreEqual("2艦隊 1,234隻", FleetClusterRules.MarkerLabel(many[0]));
        }

        [Test]
        public void MarkerLabel_ShowsShipsNotStrength()
        {
            // ★プレイヤー向けの艦隊規模は<b>艦艇数だけ</b>。抽象兵力は画面に出さない。
            // 数字は各艦隊の実隻数の合計で、兵力に「隻」を付け替えたものではないこと。
            var many = Build(F(1, Faction.同盟, 0f, 0f, strength: 500, ships: 20000),
                             F(2, Faction.同盟, 0.1f, 0f, strength: 100, ships: 4000));
            string s = FleetClusterRules.MarkerLabel(many[0]);

            StringAssert.Contains("24,000隻", s, "実隻数の合計になっていない");
            StringAssert.DoesNotContain("兵力", s, "兵力を画面に出してはいけない");
            StringAssert.DoesNotContain("600", s, "兵力の合計を隻として出してはいけない");
        }

        [Test]
        public void MarkerLabel_UsesNoSpecialGlyphs()
        {
            // フォントに無い記号（★◆⛨ 等）は豆腐になる。表示は数字と常用漢字だけで作る。
            var many = Build(F(1, Faction.同盟, 0f, 0f, 10), F(2, Faction.同盟, 0.1f, 0f, 20));
            string s = FleetClusterRules.MarkerLabel(many[0]);
            foreach (char ch in s)
                Assert.IsTrue(ch < 0x2000 || (ch >= 0x3000 && ch <= 0x9FFF),
                    $"記号 U+{(int)ch:X4} が混ざっている（フォント欠落の恐れ）");
        }

        [Test]
        public void HeadingLabel_GivesEightDirections()
        {
            Assert.AreEqual("東", FleetClusterRules.HeadingLabel(new Vector2(1f, 0f)));
            Assert.AreEqual("北", FleetClusterRules.HeadingLabel(new Vector2(0f, 1f)));
            Assert.AreEqual("西", FleetClusterRules.HeadingLabel(new Vector2(-1f, 0f)));
            Assert.AreEqual("南", FleetClusterRules.HeadingLabel(new Vector2(0f, -1f)));
            Assert.AreEqual("北東", FleetClusterRules.HeadingLabel(new Vector2(1f, 1f)));
            Assert.AreEqual("", FleetClusterRules.HeadingLabel(Vector2.zero));
        }

        [Test]
        public void NullAndEmpty_AreSafe()
        {
            var outList = new List<FleetCluster>();
            Assert.DoesNotThrow(() => FleetClusterRules.Build(null, R, outList));
            Assert.AreEqual(0, outList.Count);
            Assert.DoesNotThrow(() => FleetClusterRules.Build(new List<FleetMarkerInput>(), R, null));
            Assert.AreEqual(0, FleetClusterRules.TotalFleets(null));
            Assert.AreEqual("", FleetClusterRules.MarkerLabel(null));
        }

        [Test]
        public void ZeroRadius_KeepsEveryFleetSeparate()
        {
            var outList = new List<FleetCluster>();
            FleetClusterRules.Build(new List<FleetMarkerInput>
            {
                F(1, Faction.同盟, 0f, 0f), F(2, Faction.同盟, 0f, 0f),
            }, 0f, outList);
            // 半径0＝同一点だけ結合。中心が完全一致なので1つにまとまる（距離0は半径内）。
            Assert.AreEqual(1, outList.Count);
            Assert.AreEqual(2, outList[0].FleetCount);
        }
    }
}
