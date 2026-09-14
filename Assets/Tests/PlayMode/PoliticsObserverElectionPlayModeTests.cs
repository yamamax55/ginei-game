using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 政治オブザーバが国政選挙・地方選挙を表示するか（見出し・議席・政府の状態と理由・次回日程・星系名と知事・不成立理由・非民主の対象外）。
    /// GalaxyView は置かない＝選挙結果は純ロジックで作り <see cref="StrategySession"/> に置く（人物名は ID 表示になる）。セーブは触らない。
    /// </summary>
    public class PoliticsObserverElectionPlayModeTests
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
        public IEnumerator Overlay_ShowsNationalAndLocalElections()
        {
            var map = new GalaxyMap();
            map.AddSystem(new StarSystem(0, "テスト首都星", Vector2.zero, Faction.同盟));
            map.AddSystem(new StarSystem(1, "テスト辺境星", Vector2.one, Faction.同盟));
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
            ElectionCycleRules.RunNationalYear(s, 800, tick, null, roster, ElectionCycleParams.Default);
            var owned = new List<LocalConstituency>
            {
                new LocalConstituency(0, 100f, 0.5f, ""),
                new LocalConstituency(1, 100f, 0.5f, ""),
            };
            LocalElectionRules.Reconcile(s.politics, owned, 800, LocalElectionParams.Default);
            LocalElectionRules.RunDue(s.politics, Faction.同盟, 800, owned, roster, LocalElectionParams.Default);
            campaign.states.Add(s);
            campaign.states.Add(new FactionState(Faction.帝国) { governmentForm = GovernmentForm.君主制 });

            StrategySession.Map = map;
            StrategySession.Campaign = campaign;

            var overlay = new GameObject("PoliticsObserverOverlay").AddComponent<PoliticsObserverOverlay>();
            yield return null;
            string text = overlay.DumpTextForTest;

            StringAssert.Contains("■ 国政選挙", text);
            StringAssert.Contains("■ 地方選挙", text);
            StringAssert.Contains("下院", text);
            StringAssert.Contains("次回 SE804", text);
            StringAssert.Contains("少数政権", text, "150/300 は過半数に届かない");
            StringAssert.Contains("直近開票", text);
            StringAssert.Contains("テスト首都星", text);
            StringAssert.Contains("人物#11", text, "首相10を除いた候補11が首都星の知事");
            StringAssert.Contains("テスト辺境星", text);
            StringAssert.Contains(LocalElectionRules.NoCandidateReason, text, "候補が尽きた星系は不成立理由を出す");
            StringAssert.Contains("対象外", text, "君主制の帝国は選挙の対象外");
        }
    }
}
