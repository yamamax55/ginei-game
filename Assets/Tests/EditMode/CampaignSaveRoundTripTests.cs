using NUnit.Framework;
using UnityEngine;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// L5（セーブ/ロード往復）：戦役世界状態が `CampaignSerializer` の往復で保存復元されるかを固定する。
    /// 保存される＝銀河（星系/回廊/惑星）＋勢力の社会・政治状態（regime/polity/community/inclusiveness/<b>governmentForm</b>）。
    /// 保存されない（在席状態＝設計上の境界）＝treasury/budget/fiscal（財政フロー）。＝何が永続し何が消えるかを pin。
    /// </summary>
    public class CampaignSaveRoundTripTests
    {
        private static CampaignState BuildSample()
        {
            var map = new GalaxyMap();
            map.AddSystem(new StarSystem(1, "A", new Vector2(1f, 2f), Faction.帝国));
            map.AddSystem(new StarSystem(2, "B", new Vector2(3f, 4f), Faction.同盟));
            map.AddCorridor(new Corridor(1, 2, 5f, CorridorType.要衝));
            var c = new CampaignState(map);

            var fs = new FactionState(Faction.帝国, 0.7f);
            fs.governmentForm = GovernmentForm.共産主義;   // 政体が進化/政変で変わった状態
            fs.regime.legitimacy = 0.42f;
            fs.regime.corruption = 0.6f;
            fs.polity.cooperation = 0.55f;
            fs.community.hope = 0.33f;
            fs.treasury = 999f;                            // 在席状態（保存されない想定）
            c.states.Add(fs);
            return c;
        }

        [Test]
        public void RoundTrip_PreservesGalaxyAndFactionSocialState()
        {
            var src = BuildSample();
            CampaignState round = CampaignSerializer.FromSaveData(CampaignSerializer.ToSaveData(src));

            // 銀河
            Assert.AreEqual(2, round.map.systems.Count);
            Assert.AreEqual("A", round.map.GetSystem(1).systemName);
            Assert.AreEqual(Faction.同盟, round.map.GetSystem(2).owner);
            Assert.AreEqual(1, round.map.corridors.Count);

            // 勢力の社会・政治状態
            FactionState r = round.states[0];
            Assert.AreEqual(Faction.帝国, r.faction);
            Assert.AreEqual(0.7f, r.inclusiveness, 1e-3f);
            Assert.AreEqual(0.42f, r.regime.legitimacy, 1e-3f);
            Assert.AreEqual(0.6f, r.regime.corruption, 1e-3f);
            Assert.AreEqual(0.55f, r.polity.cooperation, 1e-3f);
            Assert.AreEqual(0.33f, r.community.hope, 1e-3f);
        }

        [Test]
        public void RoundTrip_PreservesGovernmentForm()
        {
            // #117 政体形態が往復で保存される（旧：保存対象外でロード時に首長制へ戻るリグレッションの修正）。
            var src = BuildSample();
            CampaignState round = CampaignSerializer.FromSaveData(CampaignSerializer.ToSaveData(src));
            Assert.AreEqual(GovernmentForm.共産主義, round.states[0].governmentForm);
        }

        [Test]
        public void RoundTrip_ThroughJson_PreservesGovernmentForm()
        {
            var src = BuildSample();
            string json = CampaignSerializer.ToJson(src);
            CampaignState round = CampaignSerializer.FromJson(json);
            Assert.AreEqual(GovernmentForm.共産主義, round.states[0].governmentForm);
            Assert.AreEqual(0.42f, round.states[0].regime.legitimacy, 1e-3f);
        }

        [Test]
        public void RoundTrip_DropsInSessionFiscalState_ByDesign()
        {
            // 財政フロー（treasury/budget/fiscal）は在席状態＝保存対象外＝復元時は既定で再構築（設計境界を pin）。
            var src = BuildSample();
            CampaignState round = CampaignSerializer.FromSaveData(CampaignSerializer.ToSaveData(src));
            Assert.AreEqual(0f, round.states[0].treasury, 1e-3f); // 999 は保存されず既定0
        }

        [Test]
        public void OldSave_MissingGovernmentForm_DefaultsToChiefdom()
        {
            // 前方互換：governmentForm 欠落の旧JSONはロードで既定（首長制＝int 0）になる（JsonUtility 既定埋め）。
            string oldJson = "{\"schemaVersion\":1,\"systems\":[],\"corridors\":[],\"states\":[{\"faction\":0,\"inclusiveness\":0.5}]}";
            CampaignState round = CampaignSerializer.FromJson(oldJson);
            Assert.IsNotNull(round);
            Assert.AreEqual(GovernmentForm.首長制, round.states[0].governmentForm);
        }
    }
}
