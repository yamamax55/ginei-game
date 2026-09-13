using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 所属軍団と司令官の表示（<see cref="FleetCommandLabelRules"/>）と、そのセーブ往復・旧セーブ互換。
    ///
    /// 要点：①「旗艦」単独で出さない（全艦隊に旗艦はある）②司令官は実データだけ・無ければ「未任命」
    /// ③旧セーブが「0番の軍団／0番の人物」に化けない（実値+1 で保存する）。
    /// </summary>
    public class FleetCommandLabelSaveTests
    {
        // ── 表示文言 ──

        [Test]
        public void FleetTitle_DoesNotSayFlagship()
        {
            var f = new StrategicFleet(5, 0, Faction.同盟) { isCorpsFlagship = true };
            Assert.AreEqual("第5艦隊", FleetCommandLabelRules.FleetTitle(f));
            StringAssert.DoesNotContain("旗艦", FleetCommandLabelRules.FleetTitle(f),
                "艦隊名に旗艦を混ぜると『他の艦隊には旗艦が無い』と読まれる");
        }

        [Test]
        public void CorpsLabel_ShowsCorpsFlagshipInTheCorpsColumn()
        {
            var lead = new StrategicFleet(1, 0, Faction.同盟)
            { corpsId = 2, corpsName = "同盟第1軍団", isCorpsFlagship = true };
            var member = new StrategicFleet(2, 0, Faction.同盟)
            { corpsId = 2, corpsName = "同盟第1軍団" };

            Assert.AreEqual("同盟第1軍団（軍団旗艦）", FleetCommandLabelRules.CorpsLabel(lead));
            Assert.AreEqual("同盟第1軍団", FleetCommandLabelRules.CorpsLabel(member));
        }

        [Test]
        public void CorpsLabel_NoCorps_ShowsDash()
        {
            var f = new StrategicFleet(3, 0, Faction.同盟);
            Assert.AreEqual(FleetCommandLabelRules.NoCorps, FleetCommandLabelRules.CorpsLabel(f));
            Assert.AreEqual(FleetCommandLabelRules.NoCorps, FleetCommandLabelRules.CorpsLabel(null));
        }

        [Test]
        public void CorpsLabel_MissingName_FallsBackToId()
        {
            Assert.AreEqual("軍団#7", FleetCommandLabelRules.CorpsLabel(7, null, false));
        }

        /// <summary>軍団に属さないのに旗艦フラグだけ立つ＝不整合。名前を作らずフラグだけ見せる。</summary>
        [Test]
        public void CorpsLabel_FlagWithoutCorps_ShowsOnlyTheFlag()
        {
            Assert.AreEqual(FleetCommandLabelRules.CorpsFlagship,
                            FleetCommandLabelRules.CorpsLabel(-1, null, true));
        }

        [Test]
        public void CommanderLabel_UsesRealNameOrUnassigned()
        {
            Assert.AreEqual("未任命", FleetCommandLabelRules.CommanderLabel(null));
            Assert.AreEqual("未任命", FleetCommandLabelRules.CommanderLabel(""));
            Assert.AreEqual("ヤン", FleetCommandLabelRules.CommanderLabel("ヤン"));
            Assert.AreEqual("中将 ヤン", FleetCommandLabelRules.CommanderLabel("ヤン", "中将"));
        }

        // ── セーブ往復 ──

        private static GalaxyMap TwoSystems()
        {
            var map = new GalaxyMap();
            map.AddSystem(new StarSystem { id = 0, systemName = "A", owner = Faction.同盟 });
            map.AddSystem(new StarSystem { id = 1, systemName = "B", owner = Faction.同盟 });
            map.AddCorridor(new Corridor(0, 1, 5f, CorridorType.通商));
            return map;
        }

        [Test]
        public void RoundTrip_KeepsCorpsAndCommander()
        {
            GalaxyMap map = TwoSystems();
            var campaign = new CampaignState(map);
            var reg = new StrategicFleetRegistry(map);
            var f = new StrategicFleet(5, 0, Faction.同盟)
            {
                strength = 200,
                corpsId = 2,
                corpsName = "同盟第1軍団",
                isCorpsFlagship = true,
                armyGroupId = 1,
                armyGroupName = "同盟第1軍集団",
                commanderPersonId = 42,
            };
            reg.Add(f);

            CampaignSaveData save = CampaignSerializer.ToSaveData(campaign);
            CampaignSerializer.WriteFleets(save, reg);
            StrategicFleetRegistry restored = CampaignSerializer.ReadFleets(save, map);

            StrategicFleet r = restored.GetFleet(5);
            Assert.IsNotNull(r);
            Assert.AreEqual(2, r.corpsId);
            Assert.AreEqual("同盟第1軍団", r.corpsName);
            Assert.IsTrue(r.isCorpsFlagship);
            Assert.AreEqual(1, r.armyGroupId);
            Assert.AreEqual("同盟第1軍集団", r.armyGroupName);
            Assert.AreEqual(42, r.commanderPersonId, "司令官の割当が失われている");
        }

        [Test]
        public void RoundTrip_IndependentFleet_StaysIndependent()
        {
            GalaxyMap map = TwoSystems();
            var campaign = new CampaignState(map);
            var reg = new StrategicFleetRegistry(map);
            reg.Add(new StrategicFleet(6, 0, Faction.同盟) { strength = 100 });

            CampaignSaveData save = CampaignSerializer.ToSaveData(campaign);
            CampaignSerializer.WriteFleets(save, reg);
            StrategicFleet r = CampaignSerializer.ReadFleets(save, map).GetFleet(6);

            Assert.AreEqual(-1, r.corpsId);
            Assert.IsFalse(r.HasCorps);
            Assert.AreEqual(-1, r.commanderPersonId);
            Assert.IsFalse(r.HasCommander);
        }

        /// <summary>
        /// ★旧セーブ互換。JsonUtility は無いフィールドを 0 で埋めるので、生の id を保存していると
        /// 「0番の軍団に所属」「0番の人物が司令」と誤読される。実値+1 で持つことで 0＝無所属／未任命になる。
        /// </summary>
        [Test]
        public void LegacySave_WithoutTheseFields_ReadsAsIndependentAndUnassigned()
        {
            GalaxyMap map = TwoSystems();
            var save = new CampaignSaveData();
            save.fleets.Add(new StrategicFleetSave
            {
                id = 9, faction = (int)Faction.同盟, strength = 150,
                currentSystemId = 0, warpSpeed = 1.2f,
                // corpsIdPlus1 / commanderPersonIdPlus1 は旧セーブに無い＝0 のまま
            });

            StrategicFleet r = CampaignSerializer.ReadFleets(save, map).GetFleet(9);
            Assert.AreEqual(-1, r.corpsId, "0番の軍団に所属してはいけない");
            Assert.IsFalse(r.HasCorps);
            Assert.AreEqual(-1, r.commanderPersonId, "0番の人物が司令になってはいけない");
            Assert.IsFalse(r.HasCommander);
            Assert.IsFalse(r.isCorpsFlagship);
            Assert.AreEqual(FleetCommandLabelRules.NoCorps, FleetCommandLabelRules.CorpsLabel(r));
        }

        [Test]
        public void RoundTrip_CommanderIdZero_IsPreserved()
        {
            // 人物IDの 0 は有効な値になりうる＝+1 符号化が正しく往復すること。
            GalaxyMap map = TwoSystems();
            var campaign = new CampaignState(map);
            var reg = new StrategicFleetRegistry(map);
            reg.Add(new StrategicFleet(4, 0, Faction.同盟) { strength = 100, commanderPersonId = 0, corpsId = 0 });

            CampaignSaveData save = CampaignSerializer.ToSaveData(campaign);
            CampaignSerializer.WriteFleets(save, reg);
            StrategicFleet r = CampaignSerializer.ReadFleets(save, map).GetFleet(4);

            Assert.AreEqual(0, r.commanderPersonId);
            Assert.IsTrue(r.HasCommander);
            Assert.AreEqual(0, r.corpsId);
            Assert.IsTrue(r.HasCorps);
        }
    }
}
