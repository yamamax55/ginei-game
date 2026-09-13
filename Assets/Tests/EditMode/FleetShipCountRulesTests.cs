using System.Collections.Generic;
using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 艦隊ごとの<b>艦艇数（隻）</b>と、会戦の損失按分。
    ///
    /// 要点：①兵力とは別の数として艦隊ごとに持ち歩く ②失った船はその艦隊からだけ減る
    /// ③全滅は0・無傷は据え置き ④按分の合計は必ず残存に一致（二重加算・目減りをしない）
    /// ⑤旧セーブは兵力から導出して埋める（後方互換）。
    /// </summary>
    public class FleetShipCountRulesTests
    {
        // ===== 後方互換の初期化 =====

        [Test]
        public void FromStrength_UsesTacticalScale()
        {
            Assert.AreEqual(300 * FleetShipCountRules.ShipsPerStrength, FleetShipCountRules.FromStrength(300));
            Assert.AreEqual(0, FleetShipCountRules.FromStrength(0));
            Assert.AreEqual(0, FleetShipCountRules.FromStrength(-5));
        }

        [Test]
        public void EnsureInitialized_FillsOnlyWhenMissing()
        {
            Assert.AreEqual(FleetShipCountRules.FromStrength(100),
                            FleetShipCountRules.EnsureInitialized(0, 100), "0＝未初期化なら兵力から導出");
            Assert.AreEqual(77, FleetShipCountRules.EnsureInitialized(77, 100), "既に入っていれば触らない");
        }

        [Test]
        public void Ships_OnFleet_FallsBackToStrength()
        {
            var f = new StrategicFleet { id = 1, strength = 50 };
            Assert.AreEqual(FleetShipCountRules.FromStrength(50), f.Ships);

            f.shipCount = 12;
            Assert.AreEqual(12, f.Ships);
        }

        // ===== 損失の反映（その艦隊の中だけで完結する）=====

        [Test]
        public void AfterLosses_ScalesWithOwnStrengthOnly()
        {
            // 兵力が半分になったら隻数も半分。
            Assert.AreEqual(500, FleetShipCountRules.AfterLosses(1000, 200, 100));
            // 4分の1なら4分の1。
            Assert.AreEqual(250, FleetShipCountRules.AfterLosses(1000, 200, 50));
        }

        [Test]
        public void AfterLosses_ZeroStrength_MeansAnnihilated()
        {
            Assert.AreEqual(0, FleetShipCountRules.AfterLosses(1000, 200, 0));
            Assert.AreEqual(0, FleetShipCountRules.AfterLosses(1000, 200, -3));
        }

        [Test]
        public void AfterLosses_Unharmed_KeepsCount()
        {
            Assert.AreEqual(1000, FleetShipCountRules.AfterLosses(1000, 200, 200));
            Assert.AreEqual(1000, FleetShipCountRules.AfterLosses(1000, 200, 999), "増やさない");
        }

        [Test]
        public void AfterLosses_KeepsAtLeastOneShipWhileAlive()
        {
            // 兵力がわずかに残っているのに0隻、にはしない。
            Assert.AreEqual(1, FleetShipCountRules.AfterLosses(10, 10000, 1));
        }

        [Test]
        public void AfterLosses_EdgeCases()
        {
            Assert.AreEqual(0, FleetShipCountRules.AfterLosses(0, 100, 50));
            Assert.AreEqual(500, FleetShipCountRules.AfterLosses(500, 0, 50), "元の兵力が0なら判断材料が無い＝据え置き");
        }

        [Test]
        public void Transferred_MovesProportionalShips()
        {
            Assert.AreEqual(400, FleetShipCountRules.Transferred(1000, 200, 80));
            Assert.AreEqual(0, FleetShipCountRules.Transferred(1000, 200, 0));
            Assert.AreEqual(1000, FleetShipCountRules.Transferred(1000, 200, 200));
            Assert.AreEqual(1000, FleetShipCountRules.Transferred(1000, 200, 9999), "元を超えて移さない");
        }

        [Test]
        public void Label_IsDistinctFromStrength()
        {
            StringAssert.Contains("隻", FleetShipCountRules.Label(12000));
            StringAssert.DoesNotContain("兵力", FleetShipCountRules.Label(12000));
        }

        [Test]
        public void Sum_AddsUpAndIgnoresNegatives()
        {
            Assert.AreEqual(300, FleetShipCountRules.Sum(new List<int> { 100, 200, -50 }));
            Assert.AreEqual(0, FleetShipCountRules.Sum(null));
        }

        // ===== 按分（合計が必ず残存に一致する）=====

        [Test]
        public void Distribute_SplitsByShareAndTotalsExactly()
        {
            var before = new List<int> { 100, 300 };
            var after = new List<int>();
            FleetAttritionRules.Distribute(before, 200, after);

            Assert.AreEqual(2, after.Count);
            Assert.AreEqual(200, FleetAttritionRules.Total(after), "合計が残存と一致しない＝兵力が湧く/消える");
            Assert.AreEqual(50, after[0]);
            Assert.AreEqual(150, after[1]);
        }

        [Test]
        public void Distribute_RemainderGoesToLargestFleet()
        {
            // 100:100:100 に 100 を配ると 33/33/33 で余り1。大きい隊（同値なら先頭）へ。
            var before = new List<int> { 100, 100, 100 };
            var after = new List<int>();
            FleetAttritionRules.Distribute(before, 100, after);

            Assert.AreEqual(100, FleetAttritionRules.Total(after));
            Assert.AreEqual(34, after[0]);
            Assert.AreEqual(33, after[1]);
            Assert.AreEqual(33, after[2]);
        }

        [Test]
        public void Distribute_ZeroSurvivor_AnnihilatesAll()
        {
            var after = new List<int>();
            FleetAttritionRules.Distribute(new List<int> { 100, 300 }, 0, after);
            CollectionAssert.AreEqual(new[] { 0, 0 }, after);
        }

        [Test]
        public void Distribute_SurvivorAtOrAboveTotal_KeepsEveryone()
        {
            var after = new List<int>();
            FleetAttritionRules.Distribute(new List<int> { 100, 300 }, 400, after);
            CollectionAssert.AreEqual(new[] { 100, 300 }, after);

            FleetAttritionRules.Distribute(new List<int> { 100, 300 }, 9999, after);
            CollectionAssert.AreEqual(new[] { 100, 300 }, after, "残存が過大でも増やさない");
        }

        [Test]
        public void Distribute_NeverExceedsEachFleetsOwnBefore()
        {
            // 余りの配布で、参戦時より多い隊が出ないこと。
            var before = new List<int> { 1, 1, 500 };
            var after = new List<int>();
            FleetAttritionRules.Distribute(before, 500, after);

            Assert.AreEqual(500, FleetAttritionRules.Total(after));
            for (int i = 0; i < before.Count; i++)
                Assert.LessOrEqual(after[i], before[i], $"{i}番目が参戦時より増えている");
        }

        [Test]
        public void Distribute_NullSafe()
        {
            var after = new List<int>();
            FleetAttritionRules.Distribute(null, 100, after);
            Assert.AreEqual(0, after.Count);
            Assert.DoesNotThrow(() => FleetAttritionRules.Distribute(new List<int> { 1 }, 1, null));
        }

        // ===== 艦隊どうしが混ざらない =====

        [Test]
        public void LossesStayWithinTheFleetThatTookThem()
        {
            // 本隊 200（8,000隻）と援軍 100（4,000隻）。残存 150＝本隊100・援軍50。
            var before = new List<int> { 200, 100 };
            var after = new List<int>();
            FleetAttritionRules.Distribute(before, 150, after);

            int mainShips = FleetShipCountRules.AfterLosses(8000, before[0], after[0]);
            int reinforcementShips = FleetShipCountRules.AfterLosses(4000, before[1], after[1]);

            Assert.AreEqual(100, after[0]);
            Assert.AreEqual(50, after[1]);
            Assert.AreEqual(4000, mainShips, "本隊は自分の減り方どおり");
            Assert.AreEqual(2000, reinforcementShips, "援軍も自分の減り方どおり（均等割りしない）");
        }

        // ===== 全滅（0隻）と未初期化を取り違えない =====

        [Test]
        public void AnnihilatedFleet_DoesNotRegenerateShips()
        {
            // 全滅して0隻になった艦隊が「未初期化」と誤解されて艦艇が復活しないこと。
            var f = new StrategicFleet { id = 1, strength = 10 };
            f.SetShips(0);

            Assert.AreEqual(0, f.Ships, "0隻が兵力から作り直されてはいけない");
            Assert.IsTrue(f.shipCountSet);
        }

        [Test]
        public void SetShips_MakesZeroMeaningful()
        {
            var f = new StrategicFleet { id = 1, strength = 100 };
            Assert.Greater(f.Ships, 0, "未設定なら兵力から導出");

            f.SetShips(0);
            Assert.AreEqual(0, f.Ships);

            f.SetShips(55);
            Assert.AreEqual(55, f.Ships);
        }
    }
}
