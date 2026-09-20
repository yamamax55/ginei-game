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

        [UnityTest]
        public IEnumerator UniversityCohort_GraduatesAfterFourYearsAndCanBeConsumedOnce()
        {
            var map = new GalaxyMap();
            map.AddSystem(new StarSystem(1, "大学試験星", Vector2.zero, Faction.同盟));
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

            viewObject = new GameObject("EducationGraduateSupplyQa");
            viewObject.SetActive(false);
            GalaxyView view = viewObject.AddComponent<GalaxyView>();
            view.BindElectionQaWorld(map, provinces, new List<Person>(), new List<Person>());
            view.BindEducationInstitutionsForQa(
                new List<University> { new University(1, Faction.同盟, "工科大学", CareerTrack.テクノクラート, 4) },
                new List<TechnicalCollege>(), new List<JuniorCollege>(), new List<VocationalSchool>());

            for (int year = 800; year <= 803; year++) view.RunEducationAnnualTickForQa(year);
            Assert.AreEqual(0f, EducationAnnualRules.AvailableGraduates(state.education, SchoolType.大学), 1e-5f);

            view.RunEducationAnnualTickForQa(804);
            Assert.AreEqual(4f, EducationAnnualRules.AvailableGraduates(state.education, SchoolType.大学), 1e-5f);
            Assert.AreEqual(3, view.ConsumeEducationGraduatesForQa(Faction.同盟, SchoolType.大学, 3));
            Assert.AreEqual(1f, EducationAnnualRules.AvailableGraduates(state.education, SchoolType.大学), 1e-5f);
            view.RunEducationAnnualTickForQa(804);
            Assert.AreEqual(1f, EducationAnnualRules.AvailableGraduates(state.education, SchoolType.大学), 1e-5f,
                "同年の再処理で卒業者が補充されている");
            Assert.AreEqual(1, view.ConsumeEducationGraduatesForQa(Faction.同盟, SchoolType.大学, 3));
            Assert.AreEqual(0, view.ConsumeEducationGraduatesForQa(Faction.同盟, SchoolType.大学, 3));
            yield return null;
        }

        [UnityTest]
        public IEnumerator EducationDump_ShowsEnrollmentNextGraduationAndGraduateSupply()
        {
            var map = new GalaxyMap();
            var campaign = new CampaignState(map);
            var state = new FactionState(Faction.同盟);
            state.education.schoolQuality = 0.7f;
            state.education.talentQuality = 0.6f;
            state.education.totalGraduates = 12f;
            state.education.activeCohorts.Add(new EducationCohort(1, SchoolType.大学, 800, 4f));
            state.education.graduateSupply.Add(new EducationGraduateSupply(SchoolType.大学, 2f));
            campaign.states.Add(state);
            StrategySession.Campaign = campaign;
            StrategySession.Map = map;
            StrategySession.Provinces = new Dictionary<int, Province>();

            viewObject = new GameObject("EducationObserverQa");
            viewObject.SetActive(false);
            GalaxyView view = viewObject.AddComponent<GalaxyView>();
            view.BindElectionQaWorld(map, StrategySession.Provinces, new List<Person>(), new List<Person>());

            string dump = view.BuildEducationDump();

            StringAssert.Contains("在学 4人/1組", dump);
            StringAssert.Contains("累計卒業 12人", dump);
            StringAssert.Contains("次回卒業 804年", dump);
            StringAssert.Contains("未登用卒業者:", dump);
            StringAssert.Contains("大学 2", dump);
            yield return null;
        }

        [UnityTest]
        public IEnumerator UniversityGraduates_AreGeneratedOnceWithStableEvidence()
        {
            var map = new GalaxyMap();
            map.AddSystem(new StarSystem(1, "人材試験星", Vector2.zero, Faction.同盟));
            var province = new Province(1, "", 1000f);
            var provinces = new Dictionary<int, Province> { { 1, province } };
            var campaign = new CampaignState(map);
            var state = new FactionState(Faction.同盟);
            state.education.graduateSupply.Add(new EducationGraduateSupply(SchoolType.大学, 3f));
            campaign.states.Add(state);
            var civilians = new List<Person>();
            StrategySession.Campaign = campaign;
            StrategySession.Map = map;
            StrategySession.Provinces = provinces;

            viewObject = new GameObject("NamedGraduateQa");
            viewObject.SetActive(false);
            GalaxyView view = viewObject.AddComponent<GalaxyView>();
            view.BindElectionQaWorld(map, provinces, new List<Person>(), civilians);
            view.BindEducationInstitutionsForQa(
                new List<University> { new University(7, Faction.同盟, "工科大学", CareerTrack.テクノクラート, 3) },
                new List<TechnicalCollege>(), new List<JuniorCollege>(), new List<VocationalSchool>());

            view.RunUniversityTickForQa(804);

            Assert.AreEqual(3, civilians.Count);
            string eventId = NamedPersonGenerationRules.EventId(
                PersonGenerationKind.大学卒業, Faction.同盟, 7, 804);
            for (int i = 0; i < civilians.Count; i++)
            {
                Assert.AreEqual(eventId, civilians[i].generationEventId);
                Assert.AreEqual(NamedPersonGenerationRules.StableSeed(eventId), civilians[i].generationSeed);
                StringAssert.Contains("・", civilians[i].name, "学校名と番号の仮名が固有名へ置換されていない");
            }
            Assert.AreEqual(civilians.Count, new HashSet<string>(civilians.ConvertAll(p => p.name)).Count,
                "同じ卒業イベント内で氏名が重複している");
            Assert.AreEqual(0f, EducationAnnualRules.AvailableGraduates(state.education, SchoolType.大学), 1e-5f);

            view.RunUniversityTickForQa(804);
            Assert.AreEqual(3, civilians.Count, "同じ卒業イベントから人物が重複生成された");
            yield return null;
        }

        [UnityTest]
        public IEnumerator UniversityGraduates_RespectFactionVacancies()
        {
            var map = new GalaxyMap();
            map.AddSystem(new StarSystem(1, "定員試験星", Vector2.zero, Faction.同盟));
            var provinces = new Dictionary<int, Province> { { 1, new Province(1, "", 1000f) } };
            var campaign = new CampaignState(map);
            var state = new FactionState(Faction.同盟);
            state.education.graduateSupply.Add(new EducationGraduateSupply(SchoolType.大学, 3f));
            campaign.states.Add(state);
            var civilians = new List<Person>();
            for (int i = 0; i < 39; i++)
                civilians.Add(new Person(i + 1, $"同盟文官{i}", Faction.同盟, PersonRole.文民));
            for (int i = 0; i < 40; i++)
                civilians.Add(new Person(100 + i, $"帝国文官{i}", Faction.帝国, PersonRole.文民));
            StrategySession.Campaign = campaign;
            StrategySession.Map = map;
            StrategySession.Provinces = provinces;

            viewObject = new GameObject("NamedGraduateCapacityQa");
            viewObject.SetActive(false);
            GalaxyView view = viewObject.AddComponent<GalaxyView>();
            view.BindElectionQaWorld(map, provinces, new List<Person>(), civilians);
            view.BindEducationInstitutionsForQa(
                new List<University> { new University(7, Faction.同盟, "工科大学", CareerTrack.テクノクラート, 3) },
                new List<TechnicalCollege>(), new List<JuniorCollege>(), new List<VocationalSchool>());

            view.RunUniversityTickForQa(804);

            Assert.AreEqual(80, civilians.Count, "同盟側の残り1枠だけを使用する");
            Assert.AreEqual(2f, EducationAnnualRules.AvailableGraduates(state.education, SchoolType.大学), 1e-5f,
                "名簿に入らない卒業者は供給プールへ残す");
            yield return null;
        }
    }
}
