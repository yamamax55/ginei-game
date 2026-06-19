using NUnit.Framework;
using UnityEngine;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 占領統治を固定する：必要駐留は人口×敵意×地形で膨らみ、従順度は駐留×寛容/(駐留+文化距離)で
    /// 育ち、収益は収奪率×従順（強収奪は従順を頼れない）、怨嗟は収奪×弾圧×不服従で積み、正統性は
    /// 従順×協力×時間で飽和し、安定化は正統性＋駐留が怨嗟を上回ると正、コストは駐留維持＋怨嗟治安、
    /// 採算は収益≥コスト×閾値。クランプを担保。
    /// 既定＝駐留0.5/地形1/寛容1/収奪冪2/怨嗟率1/正統性率0.001/上限1/安定化0.5/維持0.1/治安2。
    /// </summary>
    public class OccupationGovernanceRulesTests
    {
        private static readonly OccupationGovernanceParams P = OccupationGovernanceParams.Default;

        [Test]
        public void GarrisonRequirement_ScalesWithPopulationHostilityTerrain()
        {
            // 100×0.5×0.8×(1+0.5×1)=40×1.5=60
            Assert.AreEqual(60f, OccupationGovernanceRules.GarrisonRequirement(100f, 0.8f, 0.5f, P), 1e-4f);
            // 敵意0＝歓迎されている＝駐留不要
            Assert.AreEqual(0f, OccupationGovernanceRules.GarrisonRequirement(100f, 0f, 0.5f, P), 1e-4f);
            // 地形が険しいほど兵が要る（単調増）
            Assert.Greater(OccupationGovernanceRules.GarrisonRequirement(100f, 0.8f, 1f, P),
                           OccupationGovernanceRules.GarrisonRequirement(100f, 0.8f, 0f, P));
            // 負入力・超過入力はクランプ
            Assert.AreEqual(0f, OccupationGovernanceRules.GarrisonRequirement(-100f, 2f, -1f, P), 1e-4f);
            // 既定委譲版が一致
            Assert.AreEqual(60f, OccupationGovernanceRules.GarrisonRequirement(100f, 0.8f, 0.5f), 1e-4f);
        }

        [Test]
        public void PopulationCompliance_RisesWithGarrisonAndLeniency()
        {
            // persuasion=10×(1+0.5×1)=15、15/(15+0.5)=0.967742
            Assert.AreEqual(0.967742f, OccupationGovernanceRules.PopulationCompliance(10f, 0.5f, 0.5f, P), 1e-4f);
            // 駐留も寛容もゼロ＝従順0（剥き出しの占領）
            Assert.AreEqual(0f, OccupationGovernanceRules.PopulationCompliance(0f, 0f, 0.5f, P), 1e-4f);
            // 文化的距離0＝完全に従う
            Assert.AreEqual(1f, OccupationGovernanceRules.PopulationCompliance(10f, 0.5f, 0f, P), 1e-4f);
            // 寛容が高いほど従順（単調増）
            Assert.Greater(OccupationGovernanceRules.PopulationCompliance(10f, 1f, 0.5f, P),
                           OccupationGovernanceRules.PopulationCompliance(10f, 0f, 0.5f, P));
        }

        [Test]
        public void ExtractionYield_StrongExtractionCannotRelyOnCompliance()
        {
            // complianceFactor=Lerp(1, 0.6^2=0.36, 0.5)=0.68、100×0.5×0.68=34
            Assert.AreEqual(34f, OccupationGovernanceRules.ExtractionYield(100f, 0.5f, 0.6f, P), 1e-3f);
            // 収奪0＝収益0
            Assert.AreEqual(0f, OccupationGovernanceRules.ExtractionYield(100f, 0f, 0.6f, P), 1e-4f);
            // 同じ収奪率なら従順が高いほど収益大
            Assert.Greater(OccupationGovernanceRules.ExtractionYield(100f, 0.5f, 1f, P),
                           OccupationGovernanceRules.ExtractionYield(100f, 0.5f, 0.2f, P));
            // 収奪率0なら従順は無関係（complianceFactor=1）＝econ×0=0
            Assert.AreEqual(0f, OccupationGovernanceRules.ExtractionYield(100f, 0f, 0f, P), 1e-4f);
        }

        [Test]
        public void ResentmentBuildup_RisesWithExploitationRepressionDefiance()
        {
            // 0.8×0.5×(1-0.2)×1=0.8×0.5×0.8=0.32
            Assert.AreEqual(0.32f, OccupationGovernanceRules.ResentmentBuildup(0.8f, 0.5f, 0.2f, P), 1e-4f);
            // 完全従順（comp=1）＝不服従0＝恨みなし
            Assert.AreEqual(0f, OccupationGovernanceRules.ResentmentBuildup(0.8f, 0.5f, 1f, P), 1e-4f);
            // 弾圧が強いほど恨みが積む（抑圧の代償・単調増）
            Assert.Greater(OccupationGovernanceRules.ResentmentBuildup(0.8f, 1f, 0.2f, P),
                           OccupationGovernanceRules.ResentmentBuildup(0.8f, 0.2f, 0.2f, P));
            // 出力は0..1にクランプ
            float r = OccupationGovernanceRules.ResentmentBuildup(2f, 2f, -1f, P);
            Assert.GreaterOrEqual(r, 0f);
            Assert.LessOrEqual(r, 1f);
        }

        [Test]
        public void Legitimacy_GrowsWithComplianceCollaborationTimeAndSaturates()
        {
            // t=1000: timeFactor=1-1/(1+0.001×1000)=1-1/2=0.5、0.8×0.5×0.5=0.2
            Assert.AreEqual(0.2f, OccupationGovernanceRules.Legitimacy(0.8f, 0.5f, 1000f, P), 1e-3f);
            // 協力ゼロ＝正統性は育たない
            Assert.AreEqual(0f, OccupationGovernanceRules.Legitimacy(0.8f, 0f, 1000f, P), 1e-4f);
            // 時間が経つほど育つ（飽和・単調増）
            Assert.Greater(OccupationGovernanceRules.Legitimacy(0.8f, 0.5f, 2000f, P),
                           OccupationGovernanceRules.Legitimacy(0.8f, 0.5f, 1000f, P));
            // 占領直後（t=0）は正統性ゼロ
            Assert.AreEqual(0f, OccupationGovernanceRules.Legitimacy(0.8f, 0.5f, 0f, P), 1e-4f);
        }

        [Test]
        public void StabilizationProgress_PositiveWhenLegitimacyAndGarrisonBeatResentment()
        {
            // garrisonControl=1/(1+1)=0.5、net=0.5+0.5-0.2=0.8、0.5×0.8=0.4
            Assert.AreEqual(0.4f, OccupationGovernanceRules.StabilizationProgress(0.5f, 1f, 0.2f, P), 1e-4f);
            // 怨嗟が勝てば不安定化（負＝反乱の芽）
            // net=0.1+0-0.9=-0.8、0.5×-0.8=-0.4
            Assert.AreEqual(-0.4f, OccupationGovernanceRules.StabilizationProgress(0.1f, 0f, 0.9f, P), 1e-4f);
            // 出力は-1..1にクランプ
            float s = OccupationGovernanceRules.StabilizationProgress(1f, 1000f, 0f, P);
            Assert.LessOrEqual(s, 1f);
            Assert.GreaterOrEqual(s, -1f);
        }

        [Test]
        public void OccupationCost_AddsUpkeepAndSecurity()
        {
            // 60×0.1 + 0.3×2 = 6 + 0.6 = 6.6
            Assert.AreEqual(6.6f, OccupationGovernanceRules.OccupationCost(60f, 0.3f, P), 1e-4f);
            // 怨嗟ゼロ＝駐留維持費のみ
            Assert.AreEqual(6f, OccupationGovernanceRules.OccupationCost(60f, 0f, P), 1e-4f);
            // 揉めるほど高くつく（単調増）
            Assert.Greater(OccupationGovernanceRules.OccupationCost(60f, 1f, P),
                           OccupationGovernanceRules.OccupationCost(60f, 0.3f, P));
            // 負入力クランプ
            Assert.AreEqual(0f, OccupationGovernanceRules.OccupationCost(-10f, -1f, P), 1e-4f);
        }

        [Test]
        public void IsOccupationSustainable_YieldMustMeetCostTimesThreshold()
        {
            // 収益40 ≥ コスト30×1.0＝持続可能
            Assert.IsTrue(OccupationGovernanceRules.IsOccupationSustainable(40f, 30f, 1f));
            // 収益20 < コスト30×1.0＝赤字＝持続不能
            Assert.IsFalse(OccupationGovernanceRules.IsOccupationSustainable(20f, 30f, 1f));
            // 閾値2.0＝コストの2倍稼げて初めて続ける価値あり（40<60）
            Assert.IsFalse(OccupationGovernanceRules.IsOccupationSustainable(40f, 30f, 2f));
            // コスト0で収益が正＝持続可能
            Assert.IsTrue(OccupationGovernanceRules.IsOccupationSustainable(10f, 0f, 1f));
            // 既定委譲版は閾値1.0
            Assert.IsTrue(OccupationGovernanceRules.IsOccupationSustainable(40f, 30f));
        }

        [Test]
        public void Story_BrutalVsBenevolentOccupation()
        {
            // 同じ占領地（人口100・敵意0.8・地形0.5・経済1000・文化的距離0.6）を二通りに治める。
            float population = 100f, hostility = 0.8f, terrain = 0.5f, economy = 1000f, dist = 0.6f;
            float garrison = OccupationGovernanceRules.GarrisonRequirement(population, hostility, terrain, P);

            // 苛烈な統治＝収奪9割・弾圧9割・寛容0・現地協力ほぼなし
            float harshLeniency = 0f, harshExtract = 0.9f, harshRepress = 0.9f, harshCollab = 0.05f;
            float harshComp = OccupationGovernanceRules.PopulationCompliance(garrison, harshLeniency, dist, P);
            float harshYield = OccupationGovernanceRules.ExtractionYield(economy, harshExtract, harshComp, P);
            float harshResent = OccupationGovernanceRules.ResentmentBuildup(harshExtract, harshRepress, harshComp, P);
            float harshLegit = OccupationGovernanceRules.Legitimacy(harshComp, harshCollab, 2000f, P);
            float harshStab = OccupationGovernanceRules.StabilizationProgress(harshLegit, garrison, harshResent, P);
            float harshCost = OccupationGovernanceRules.OccupationCost(garrison, harshResent, P);

            // 寛大な統治＝収奪3割・弾圧1割・寛容9割・現地協力厚い
            float kindLeniency = 0.9f, kindExtract = 0.3f, kindRepress = 0.1f, kindCollab = 0.8f;
            float kindComp = OccupationGovernanceRules.PopulationCompliance(garrison, kindLeniency, dist, P);
            float kindResent = OccupationGovernanceRules.ResentmentBuildup(kindExtract, kindRepress, kindComp, P);
            float kindLegit = OccupationGovernanceRules.Legitimacy(kindComp, kindCollab, 2000f, P);
            float kindStab = OccupationGovernanceRules.StabilizationProgress(kindLegit, garrison, kindResent, P);

            // 苛烈は短期に多く搾り取る（収益が大きい）が…
            Assert.Greater(harshYield, 0f);
            // …怨嗟が積もり治安コストが膨らむ
            Assert.Greater(harshResent, kindResent);
            // 寛大は正統性が育ち安定化が進む（苛烈は不安定化しがち）
            Assert.Greater(kindLegit, harshLegit);
            Assert.Greater(kindStab, harshStab);
            // 苛烈な統治はコストが高い（怨嗟の治安費）
            Assert.Greater(harshCost, OccupationGovernanceRules.OccupationCost(garrison, kindResent, P));
        }
    }
}
