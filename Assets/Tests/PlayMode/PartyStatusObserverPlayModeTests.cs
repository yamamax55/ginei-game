using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 政治オブザーバの政党欄：与党/野党を組閣と確定議席から出し（選挙後に支持率が逆転しても与党は変わらない）、
    /// 支持率・確定議席・ネームド政治家・国政議員・一般党員（不明／出所つき）を分けて表示し、表示で状態を変えないこと。
    /// GalaxyView は置かない（人物名は ID 表示）。セーブは触らない。
    /// </summary>
    public class PartyStatusObserverPlayModeTests
    {
        private CampaignState savedCampaign;
        private GalaxyMap savedMap;

        [SetUp]
        public void SetUp()
        {
            savedCampaign = StrategySession.Campaign;
            savedMap = StrategySession.Map;
        }

        [TearDown]
        public void TearDown()
        {
            PoliticsObserverOverlay overlay = Object.FindAnyObjectByType<PoliticsObserverOverlay>();
            if (overlay != null) Object.DestroyImmediate(overlay.gameObject);
            StrategySession.Campaign = savedCampaign;
            StrategySession.Map = savedMap;
        }

        [UnityTest]
        public IEnumerator Overlay_PartyRoleFromGovernment_AndSeparatedCounts()
        {
            var map = new GalaxyMap();
            map.AddSystem(new StarSystem(0, "テスト首都星", Vector2.zero, Faction.同盟));
            var campaign = new CampaignState(map);

            var s = new FactionState(Faction.同盟) { governmentForm = GovernmentForm.共和制, politics = new PoliticsState() };
            s.politics.parties.Add(new Party(1, "テスト民政党", Faction.同盟) { support = 0.5f });
            s.politics.parties.Add(new Party(2, "テスト進歩党", Faction.同盟) { support = 0.5f });
            var roster = new List<Person>
            {
                new Person(10, "甲", Faction.同盟, PersonRole.文民) { isPolitician = true },
                new Person(11, "乙", Faction.同盟, PersonRole.文民) { isPolitician = true },
            };
            var tick = default(PoliticsTickRules.PoliticsTickResult);
            tick.upperClassUp = -1;
            NationalYearOutcome o = ElectionCycleRules.RunNationalYear(s, 800, tick, null, roster, ElectionCycleParams.Default);
            LegislatorRosterRules.AssignElection(s.politics, s.faction, o.lowerRecord, roster, null, LegislatorRosterParams.Default);
            PartyMembershipRules.SetGeneralMembership(s.politics.parties[0], PartyMembershipTally.NationalScope, 250000L, "党公表", 800);
            s.politics.parties[1].support = 0.9f; // 選挙後の支持率の逆転（議席・与党は変わらない）
            campaign.states.Add(s);

            StrategySession.Map = map;
            StrategySession.Campaign = campaign;
            int lower1 = ElectionCycleRules.SeatsOf(s.politics.lowerSeats, 1);
            var members0 = new List<int>(s.politics.parties[0].memberIds);

            var overlay = new GameObject("PoliticsObserverOverlay").AddComponent<PoliticsObserverOverlay>();
            yield return null;
            string text = overlay.DumpTextForTest;

            StringAssert.Contains("テスト民政党 <color=#ffd700>[与党]</color>", text, "組閣した党（議席同数は党ID小）が与党");
            StringAssert.Contains("テスト進歩党 <color=#a0c8ff>[野党]</color>", text, "支持率が高くても組閣していなければ野党");
            StringAssert.Contains("確定議席", text);
            StringAssert.Contains("ネームド政治家", text);
            StringAssert.Contains("国政議員(実在)", text);
            StringAssert.Contains("250,000人（出所 党公表・SE800）", text);
            StringAssert.Contains("不明（未設定）", text, "集計の無い党は不明と表示");

            Assert.AreEqual(lower1, ElectionCycleRules.SeatsOf(s.politics.lowerSeats, 1), "表示で議席を変えない");
            CollectionAssert.AreEqual(members0, s.politics.parties[0].memberIds, "表示で所属を変えない");
            Assert.AreEqual(0.9f, s.politics.parties[1].support, 1e-6f);
        }
    }
}
