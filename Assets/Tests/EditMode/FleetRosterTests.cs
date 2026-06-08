using NUnit.Framework;
using UnityEngine;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 艦隊編制台帳（#146）の純ロジックを固定する：
    /// 番号払い出し（勢力ごとに独立・解隊は再利用・永久欠番は不可）／提督配属（階級ゲート）／
    /// 解隊・永久欠番／表示名。
    /// </summary>
    public class FleetRosterTests
    {
        private static AdmiralData Admiral(int tier)
        {
            var a = ScriptableObject.CreateInstance<AdmiralData>();
            a.admiralName = "テスト提督"; a.rankTier = tier;
            return a;
        }

        [SetUp]
        public void Reset() => FleetRoster.Clear();

        [Test]
        public void NextAvailableNumber_StartsAtOne_ThenIncrements()
        {
            Assert.AreEqual(1, FleetRoster.NextAvailableNumber(Faction.帝国));
            FleetRoster.CreateFleet(Faction.帝国);            // 1
            FleetRoster.CreateFleet(Faction.帝国);            // 2
            FleetRoster.CreateFleet(Faction.帝国);            // 3
            Assert.AreEqual(4, FleetRoster.NextAvailableNumber(Faction.帝国));
        }

        [Test]
        public void NumberSpace_IsPerFaction()
        {
            FleetRoster.CreateFleet(Faction.帝国, 1);
            // 帝国の第1艦隊があっても、同盟の番号空間は独立
            Assert.AreEqual(1, FleetRoster.NextAvailableNumber(Faction.同盟));
            Assert.IsNotNull(FleetRoster.CreateFleet(Faction.同盟, 1));
            Assert.AreNotSame(FleetRoster.GetFleet(Faction.帝国, 1), FleetRoster.GetFleet(Faction.同盟, 1));
        }

        [Test]
        public void CreateFleet_SameActiveNumber_ReturnsExisting()
        {
            var u1 = FleetRoster.CreateFleet(Faction.帝国, 13);
            var u2 = FleetRoster.CreateFleet(Faction.帝国, 13);
            Assert.AreSame(u1, u2); // 現役の同番号は重複生成しない
        }

        [Test]
        public void RetireNumber_BlocksReuse()
        {
            FleetRoster.CreateFleet(Faction.同盟, 13);
            FleetRoster.RetireNumber(Faction.同盟, 13);
            Assert.IsTrue(FleetRoster.IsRetired(Faction.同盟, 13));
            Assert.AreEqual(FleetStatus.永久欠番, FleetRoster.GetFleet(Faction.同盟, 13).status);
            Assert.IsNull(FleetRoster.CreateFleet(Faction.同盟, 13)); // 永久欠番は払い出せない
        }

        [Test]
        public void Disband_AllowsReuse()
        {
            FleetRoster.CreateFleet(Faction.帝国); // 1
            FleetRoster.CreateFleet(Faction.帝国); // 2
            FleetRoster.CreateFleet(Faction.帝国); // 3
            Assert.AreEqual(4, FleetRoster.NextAvailableNumber(Faction.帝国));

            var f2 = FleetRoster.GetFleet(Faction.帝国, 2);
            Assert.IsTrue(FleetRoster.Disband(Faction.帝国, 2));
            Assert.AreEqual(FleetStatus.解隊, f2.status);
            // 解隊した番号は払い出しの最小候補に戻る（永久欠番と違い再利用可）
            Assert.AreEqual(2, FleetRoster.NextAvailableNumber(Faction.帝国));

            var reused = FleetRoster.CreateFleet(Faction.帝国, 2);
            Assert.AreEqual(FleetStatus.現役, reused.status);
            Assert.AreNotSame(f2, reused); // 新ユニットで再利用
        }

        [Test]
        public void AssignAdmiral_RankGate()
        {
            var unit = FleetRoster.CreateFleet(Faction.帝国, 1);

            // 階級ゲート無し（requiredTier=0）：非null提督なら配属可
            Assert.IsTrue(FleetRoster.AssignAdmiral(unit, Admiral(5)));
            Assert.IsTrue(unit.HasAdmiral);

            // ゲートあり：tier 不足は配属拒否（現状維持）
            var low = Admiral(5);
            Assert.IsFalse(FleetRoster.AssignAdmiral(unit, low, requiredTier: 7));

            // tier 充足は配属可
            var high = Admiral(7);
            Assert.IsTrue(FleetRoster.AssignAdmiral(unit, high, requiredTier: 7));
            Assert.AreSame(high, unit.assignedAdmiral);

            // null 提督は不可
            Assert.IsFalse(FleetRoster.AssignAdmiral(unit, null));
        }

        [Test]
        public void Unassign_And_Reassign()
        {
            var unit = FleetRoster.CreateFleet(Faction.帝国, 2);
            var a = Admiral(8);
            FleetRoster.AssignAdmiral(unit, a);
            FleetRoster.Unassign(unit);
            Assert.IsFalse(unit.HasAdmiral);

            var b = Admiral(8);
            Assert.IsTrue(FleetRoster.ReassignAdmiral(unit, b));
            Assert.AreSame(b, unit.assignedAdmiral);
        }

        [Test]
        public void DisplayName_FallsBackToNumber()
        {
            var plain = FleetRoster.CreateFleet(Faction.同盟, 13);
            Assert.AreEqual("第13艦隊", plain.DisplayName);

            var named = FleetRoster.CreateFleet(Faction.帝国, 1, "黒色槍騎兵艦隊");
            Assert.AreEqual("黒色槍騎兵艦隊", named.DisplayName);
        }

        [Test]
        public void RetiredNumber_IsSkippedByNextAvailable()
        {
            FleetRoster.CreateFleet(Faction.帝国, 1);
            FleetRoster.RetireNumber(Faction.帝国, 2); // 2 を欠番（ユニット未作成でも欠番化できる）
            // 1=現役, 2=永久欠番 → 次は 3
            Assert.AreEqual(3, FleetRoster.NextAvailableNumber(Faction.帝国));
        }
    }
}
