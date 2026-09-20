using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Ginei;

namespace Ginei.Tests
{
    public class EducationAnnualIntegrationPlayModeTests
    {
        private CampaignState savedCampaign;
        private GalaxyMap savedMap;
        private Dictionary<int, Province> savedProvinces;
        private GameObject viewObject;

        [SetUp]
        public void SetUp()
        {
            savedCampaign = StrategySession.Campaign;
            savedMap = StrategySession.Map;
            savedProvinces = StrategySession.Provinces;
        }

        [TearDown]
        public void TearDown()
        {
            if (viewObject != null) Object.DestroyImmediate(viewObject);
            StrategySession.Campaign = savedCampaign;
            StrategySession.Map = savedMap;
            StrategySession.Provinces = savedProvinces;
        }

        [UnityTest]
        public IEnumerator BudgetAndPopulation_CreateEnrollmentOnceAndSurviveSave()
        {
            var map = new GalaxyMap();
            map.AddSystem(new StarSystem(1, "教育試験星", Vector2.zero, Faction.同盟));
            var province = new Province(1, "", 1000f)
            {
                demographics = new Population(youth: 150f, working: 750f, elderly: 100f)
            };
            var provinces = new Dictionary<int, Province> { { 1, province } };
            var campaign = new CampaignState(map);
            var state = new FactionState(Faction.同盟);
            state.budget.education = 1000f;
            campaign.states.Add(state);
            StrategySession.Campaign = campaign;
            StrategySession.Map = map;
            StrategySession.Provinces = provinces;

            viewObject = new GameObject("EducationAnnualQa");
            viewObject.SetActive(false);
            GalaxyView view = viewObject.AddComponent<GalaxyView>();
            view.BindElectionQaWorld(map, provinces, new List<Person>(), new List<Person>());

            view.RunEducationAnnualTickForQa(800);
            int cohortCount = state.education.activeCohorts.Count;
            float admitted = 0f;
            for (int i = 0; i < cohortCount; i++) admitted += state.education.activeCohorts[i].students;
            Assert.AreEqual(3, cohortCount, "大学設備なしなので小中高の3コホートだけ入学する");
            Assert.Greater(admitted, 0f);
            Assert.AreEqual(800, state.education.lastProcessedYear);

            view.RunEducationAnnualTickForQa(800);
            Assert.AreEqual(cohortCount, state.education.activeCohorts.Count, "同じ年の再実行で重複入学している");

            CampaignState loaded = CampaignSerializer.FromJson(CampaignSerializer.ToJson(campaign));
            Assert.AreEqual(cohortCount, loaded.states[0].education.activeCohorts.Count);
            Assert.AreEqual(800, loaded.states[0].education.lastProcessedYear);
            yield return null;
        }
    }
}
