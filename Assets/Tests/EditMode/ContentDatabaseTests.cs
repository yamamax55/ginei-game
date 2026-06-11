using NUnit.Framework;
using UnityEngine;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// データ索引（FND-1 #496・`ContentDatabase`）を固定する：名前→SO の引き、全列挙、未登録は null、Clear で破棄。
    /// 手動登録で Resources 走査を回避してロジックだけを担保する（SO は CreateInstance）。
    /// </summary>
    public class ContentDatabaseTests
    {
        [SetUp]
        public void Reset() => ContentDatabase.Clear();

        [TearDown]
        public void Cleanup() => ContentDatabase.Clear();

        private static FactionData Faction(string name)
        {
            var f = ScriptableObject.CreateInstance<FactionData>();
            f.factionName = name;
            return f;
        }

        private static ScenarioData Scenario(string name)
        {
            var s = ScriptableObject.CreateInstance<ScenarioData>();
            s.scenarioName = name;
            return s;
        }

        private static AdmiralData Admiral(string name)
        {
            var a = ScriptableObject.CreateInstance<AdmiralData>();
            a.admiralName = name;
            return a;
        }

        [Test]
        public void FactionByName_ResolvesRegistered()
        {
            var imperial = Faction("帝国軍");
            ContentDatabase.RegisterFaction(imperial);
            Assert.AreSame(imperial, ContentDatabase.FactionByName("帝国軍"));
            Assert.IsNull(ContentDatabase.FactionByName("存在しない"));
        }

        [Test]
        public void ScenarioByName_ResolvesRegistered()
        {
            var s = Scenario("アスターテ会戦");
            ContentDatabase.RegisterScenario(s);
            Assert.AreSame(s, ContentDatabase.ScenarioByName("アスターテ会戦"));
        }

        [Test]
        public void RegisterScenario_IndexesReferencedAdmirals_AndStaff()
        {
            // 提督は Resources 外にあり、シナリオの参照グラフ経由で索引化される（ベストプラクティス）
            var staff = Admiral("参謀キルヒアイス");
            var cmdr = Admiral("ラインハルト");
            cmdr.staffOfficers = new[] { staff };
            var s = Scenario("アスターテ会戦");
            s.fleets.Add(new ScenarioData.FleetEntry { admiral = cmdr });

            ContentDatabase.RegisterScenario(s);

            Assert.AreSame(cmdr, ContentDatabase.AdmiralByName("ラインハルト"));
            Assert.AreSame(staff, ContentDatabase.AdmiralByName("参謀キルヒアイス")); // 参謀も索引
            Assert.AreEqual(2, ContentDatabase.AllAdmirals().Count);
        }

        [Test]
        public void AllFactions_ListsRegistered()
        {
            ContentDatabase.RegisterFaction(Faction("A"));
            ContentDatabase.RegisterFaction(Faction("B"));
            Assert.AreEqual(2, ContentDatabase.AllFactions().Count);
        }

        [Test]
        public void Register_MarksBuilt_AvoidsResourceScan()
        {
            Assert.IsFalse(ContentDatabase.IsBuilt);
            ContentDatabase.RegisterFaction(Faction("X"));
            Assert.IsTrue(ContentDatabase.IsBuilt); // 手動登録済み＝EnsureBuilt は Resources を走査しない
        }

        [Test]
        public void Clear_ResetsIndex()
        {
            ContentDatabase.RegisterFaction(Faction("Y"));
            ContentDatabase.Clear();
            Assert.IsFalse(ContentDatabase.IsBuilt);
            // Clear 後は EnsureBuilt が走るが、テスト環境に Resources アセットは無い＝空索引
            Assert.IsNull(ContentDatabase.FactionByName("Y"));
        }

        [Test]
        public void NullAndEmptyNames_HandledSafely()
        {
            ContentDatabase.RegisterFaction(Faction("Z"));
            Assert.IsNull(ContentDatabase.FactionByName(null));
            ContentDatabase.RegisterFaction(null);                 // 無効登録は無視
            ContentDatabase.RegisterFaction(Faction(""));          // 空名は索引しない
            Assert.IsNull(ContentDatabase.FactionByName(""));
        }
    }
}
