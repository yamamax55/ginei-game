using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Ginei.Tests
{
    public class TalentDevelopmentIntegrationPlayModeTests
    {
        private CampaignState savedCampaign;
        private GalaxyMap savedMap;
        private Dictionary<int, Province> savedProvinces;
        private StrategicFleetRegistry savedRegistry;
        private Faction savedPlayerFaction;
        private GameObject viewObject;
        private GameObject panelObject;
        private GalaxyView previousActive;

        [SetUp]
        public void SetUp()
        {
            savedCampaign = StrategySession.Campaign;
            savedMap = StrategySession.Map;
            savedProvinces = StrategySession.Provinces;
            savedRegistry = StrategySession.Reg;
            savedPlayerFaction = GameSettings.Instance.playerFaction;
            GameSettings.Instance.playerFaction = Faction.同盟;
        }

        [TearDown]
        public void TearDown()
        {
            TalentDevelopmentPanel.Hide();
            GalaxyView.SwapActiveForQa(previousActive);
            if (panelObject != null) Object.DestroyImmediate(panelObject);
            if (viewObject != null) Object.DestroyImmediate(viewObject);
            StrategySession.Campaign = savedCampaign;
            StrategySession.Map = savedMap;
            StrategySession.Provinces = savedProvinces;
            StrategySession.Reg = savedRegistry;
            GameSettings.Instance.playerFaction = savedPlayerFaction;
        }

        [UnityTest]
        public IEnumerator RecommendedDevelopment_TrainingInterruptionCompletionAndSave_AreConnected()
        {
            GalaxyView view;
            FactionState state;
            StrategicFleet fleetA;
            StrategicFleet fleetB;
            BuildWorld(out view, out state, out fleetA, out fleetB);

            Assert.IsTrue(view.StartRecommendedDevelopment(1, out string personReason), personReason);
            Assert.AreEqual(DevelopmentProgramKind.師事, state.talentDevelopment.programs[0].kind);
            Assert.IsTrue(view.StartFleetTraining(fleetA.id, out string fleetReason), fleetReason);
            Assert.IsTrue(view.StartCorpsExercise(Faction.同盟, fleetA.corpsId, out string corpsReason), corpsReason);
            Assert.Less(TalentDevelopmentRules.EffectiveFleetReadiness(state.talentDevelopment, fleetA.id, 1f), 1f);

            Assert.IsTrue(view.OrderMove(fleetA, 2, out string moveReason), moveReason);
            Assert.AreEqual(2, CountStatus(state.talentDevelopment, DevelopmentProgramStatus.中断),
                "出撃した艦隊訓練と軍団共同演習が同時に中断される");
            Assert.AreEqual(1f, TalentDevelopmentRules.EffectiveFleetReadiness(state.talentDevelopment, fleetA.id, 1f), 1e-5f,
                "訓練中断後は即応性ペナルティが解除される");

            view.Registry.Tick(10f);
            ResumeInterrupted(view, state.talentDevelopment);
            view.RunTalentDevelopmentAnnualTickForQa(801);
            Assert.AreEqual(3, CountStatus(state.talentDevelopment, DevelopmentProgramStatus.修了));
            Assert.Greater(TalentDevelopmentRules.EffectiveFleetReadiness(state.talentDevelopment, fleetA.id, 1f), 0.99f);

            CampaignState loaded = CampaignSerializer.FromJson(CampaignSerializer.ToJson(StrategySession.Campaign));
            Assert.AreEqual(3, CountStatus(loaded.states[0].talentDevelopment, DevelopmentProgramStatus.修了));
            float experience = loaded.states[0].talentDevelopment.people[0].growth.experience;
            TalentDevelopmentRules.TickYear(loaded.states[0].talentDevelopment, 801);
            Assert.AreEqual(experience, loaded.states[0].talentDevelopment.people[0].growth.experience, 1e-5f,
                "保存復元後に同じ年の修了効果が重複している");
            yield return null;
        }

        [UnityTest]
        public IEnumerator Panel_ShowsGrowthInvestmentTrainingAndVisibleScrollbar()
        {
            GalaxyView view;
            FactionState state;
            StrategicFleet fleetA;
            StrategicFleet fleetB;
            BuildWorld(out view, out state, out fleetA, out fleetB);
            view.RefreshTalentDevelopmentProfiles(Faction.同盟);

            panelObject = new GameObject("TalentDevelopmentPanelQa");
            panelObject.AddComponent<TalentDevelopmentPanel>();
            TalentDevelopmentPanel.Show();
            yield return null;

            Assert.IsTrue(TalentDevelopmentPanel.IsOpen);
            Text[] legacyText = panelObject.GetComponentsInChildren<Text>(true);
            Assert.IsNotNull(panelObject.GetComponentInChildren<ScrollRect>(true));
            Assert.IsNotNull(panelObject.GetComponentInChildren<Scrollbar>(true), "スクロール可能画面に見えるスクロールバーがない");
            string combined = "";
            Component[] components = panelObject.GetComponentsInChildren<Component>(true);
            for (int i = 0; i < components.Length; i++)
            {
                Component component = components[i];
                if (component == null || component.GetType().Name != "TextMeshProUGUI") continue;
                System.Reflection.PropertyInfo property = component.GetType().GetProperty("text");
                if (property != null) combined += property.GetValue(component) as string + "\n";
            }
            StringAssert.Contains("個人育成", combined);
            StringAssert.Contains("成長", combined);
            StringAssert.Contains("累計投資", combined);
            StringAssert.Contains("艦隊訓練", combined);
            StringAssert.Contains("軍団共同演習", combined);
            Assert.AreEqual(0, legacyText.Length, "画面内の文字はTMPへ統一する");
        }

        private void BuildWorld(out GalaxyView view, out FactionState state, out StrategicFleet fleetA, out StrategicFleet fleetB)
        {
            var map = new GalaxyMap();
            map.AddSystem(new StarSystem(1, "訓練星", Vector2.zero, Faction.同盟));
            map.AddSystem(new StarSystem(2, "演習星", Vector2.right * 10f, Faction.同盟));
            map.AddCorridor(new Corridor(1, 2, 1f));
            var campaign = new CampaignState(map);
            state = new FactionState(Faction.同盟);
            campaign.states.Add(state);
            StrategySession.Campaign = campaign;
            StrategySession.Map = map;
            StrategySession.Provinces = new Dictionary<int, Province>();

            var candidate = new Person(1, "候補生", Faction.同盟, PersonRole.軍人)
            {
                leadership = 82, attack = 82, defense = 82, mobility = 82,
                merit = new OfficialMerit(1) { evaluations = 1, cumulativeScore = 2f }
            };
            var mentor = new Person(2, "教官", Faction.同盟, PersonRole.軍人)
            {
                leadership = 96, attack = 96, defense = 96, mobility = 96,
                merit = new OfficialMerit(2) { evaluations = 1, cumulativeScore = 8f }
            };
            var commanders = new List<Person> { candidate, mentor };
            var registry = new StrategicFleetRegistry(map);
            fleetA = new StrategicFleet(10, 1, Faction.同盟, 10f) { corpsId = 7, corpsName = "第七軍団", isCorpsFlagship = true };
            fleetB = new StrategicFleet(11, 1, Faction.同盟, 10f) { corpsId = 7, corpsName = "第七軍団" };
            registry.Add(fleetA);
            registry.Add(fleetB);
            StrategySession.Reg = registry;

            viewObject = new GameObject("TalentDevelopmentQa");
            viewObject.SetActive(false);
            view = viewObject.AddComponent<GalaxyView>();
            view.BindElectionQaWorld(map, StrategySession.Provinces, commanders, new List<Person>());
            view.BindStrategicFleetRegistryForQa(registry);
            previousActive = GalaxyView.SwapActiveForQa(view);
            view.RunTalentDevelopmentAnnualTickForQa(800);
        }

        private static int CountStatus(TalentDevelopmentState state, DevelopmentProgramStatus status)
        {
            int count = 0;
            for (int i = 0; i < state.programs.Count; i++)
                if (state.programs[i] != null && state.programs[i].status == status) count++;
            return count;
        }

        private static void ResumeInterrupted(GalaxyView view, TalentDevelopmentState state)
        {
            for (int i = 0; i < state.programs.Count; i++)
            {
                DevelopmentProgram program = state.programs[i];
                if (program != null && program.status == DevelopmentProgramStatus.中断)
                    Assert.IsTrue(view.ResumeDevelopment(Faction.同盟, program.id, out string reason), reason);
            }
        }
    }
}
