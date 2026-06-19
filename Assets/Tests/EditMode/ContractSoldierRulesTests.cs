using NUnit.Framework;
using UnityEngine;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 契約兵（任期制職業兵士の労働市場）を固定する：報酬で志願者が集まり、職務満足×再契約で定着し、戦闘リスク×死傷率で
    /// 危険手当が上がる、勤続×専門化で専門性が育つ、契約満了×不満で除隊流出、軍需要/適齢人口で市場逼迫＆賃金上昇、
    /// 定着＋供給−流出で兵力が安定する。既定 Params 期待値・境界・クランプを担保。
    /// </summary>
    public class ContractSoldierRulesTests
    {
        // 既定＝弾力1/再契約0.4/危険手当0.8/専門飽和20/除隊0.6/賃金1.5/供給重み0.5
        private static readonly ContractSoldierParams P = ContractSoldierParams.Default;

        [Test]
        public void RecruitmentSupply_ShareOfPay()
        {
            // 60/(60+40)=0.6
            Assert.AreEqual(0.6f, ContractSoldierRules.RecruitmentSupply(60f, 40f, P), 1e-4f);
            // 民間ゼロなら全供給
            Assert.AreEqual(1f, ContractSoldierRules.RecruitmentSupply(50f, 0f, P), 1e-4f);
            // 両方ゼロは安全に0
            Assert.AreEqual(0f, ContractSoldierRules.RecruitmentSupply(0f, 0f, P), 1e-4f);
        }

        [Test]
        public void RetentionRate_SatisfactionLiftedByReenlistment()
        {
            // 0.5 + (1-0.5)*0.4*1 = 0.7
            Assert.AreEqual(0.7f, ContractSoldierRules.RetentionRate(0.5f, 1f, P), 1e-4f);
            // ボーナス無しは満足そのまま
            Assert.AreEqual(0.5f, ContractSoldierRules.RetentionRate(0.5f, 0f, P), 1e-4f);
            // 満足満点は1
            Assert.AreEqual(1f, ContractSoldierRules.RetentionRate(1f, 1f, P), 1e-4f);
        }

        [Test]
        public void HazardPayDemand_RiskTimesCasualty()
        {
            // 0.8*1*0.5 = 0.4
            Assert.AreEqual(0.4f, ContractSoldierRules.HazardPayDemand(1f, 0.5f, P), 1e-4f);
            // どちらかゼロで手当ゼロ
            Assert.AreEqual(0f, ContractSoldierRules.HazardPayDemand(0f, 1f, P), 1e-4f);
            // 満ちれば上限
            Assert.AreEqual(0.8f, ContractSoldierRules.HazardPayDemand(1f, 1f, P), 1e-4f);
        }

        [Test]
        public void ProfessionalSkill_TenureAndSpecialization()
        {
            // 勤続20年で飽和(1)・専門1 → sqrt(1)=1
            Assert.AreEqual(1f, ContractSoldierRules.ProfessionalSkill(20f, 1f, P), 1e-3f);
            // 5年=tenure0.25・専門1 → sqrt(0.25)=0.5
            Assert.AreEqual(0.5f, ContractSoldierRules.ProfessionalSkill(5f, 1f, P), 1e-3f);
            // 飽和でも専門0.25 → sqrt(0.25)=0.5
            Assert.AreEqual(0.5f, ContractSoldierRules.ProfessionalSkill(20f, 0.25f, P), 1e-3f);
        }

        [Test]
        public void AttritionFromDischarge_ExpiryTimesDissatisfaction()
        {
            // 0.6*1*1 = 0.6（上限）
            Assert.AreEqual(0.6f, ContractSoldierRules.AttritionFromDischarge(1f, 1f, P), 1e-4f);
            // 0.6*0.5*0.5 = 0.15
            Assert.AreEqual(0.15f, ContractSoldierRules.AttritionFromDischarge(0.5f, 0.5f, P), 1e-4f);
            // 満了しても不満ゼロなら去らない
            Assert.AreEqual(0f, ContractSoldierRules.AttritionFromDischarge(1f, 0f, P), 1e-4f);
        }

        [Test]
        public void LaborMarketTightness_DemandOverPopulation()
        {
            // 30/100 = 0.3
            Assert.AreEqual(0.3f, ContractSoldierRules.LaborMarketTightness(30f, 100f, P), 1e-4f);
            // 需要が人口超過で逼迫1
            Assert.AreEqual(1f, ContractSoldierRules.LaborMarketTightness(150f, 100f, P), 1e-4f);
        }

        [Test]
        public void WageInflation_RisesWithTightness()
        {
            // 0.5*1.5 = 0.75
            Assert.AreEqual(0.75f, ContractSoldierRules.WageInflation(0.5f, P), 1e-4f);
            // 逼迫無しで上昇無し
            Assert.AreEqual(0f, ContractSoldierRules.WageInflation(0f, P), 1e-4f);
            // 高逼迫は1で頭打ち
            Assert.AreEqual(1f, ContractSoldierRules.WageInflation(1f, P), 1e-4f);
        }

        [Test]
        public void ForceStability_BlendsRetentionSupplyMinusAttrition()
        {
            // lerp(0.8,0.6,0.5)=0.7 − 0.1 = 0.6
            Assert.AreEqual(0.6f, ContractSoldierRules.ForceStability(0.8f, 0.6f, 0.1f, P), 1e-4f);
            // 流出が大きいと0へ
            Assert.AreEqual(0f, ContractSoldierRules.ForceStability(0.5f, 0.5f, 1f, P), 1e-4f);
        }

        [Test]
        public void Params_ClampAndDefault()
        {
            var d = ContractSoldierParams.Default;
            Assert.AreEqual(1f, d.payElasticity, 1e-4f);
            Assert.AreEqual(0.4f, d.reenlistmentWeight, 1e-4f);
            Assert.AreEqual(20f, d.skillSaturationYears, 1e-4f);
            // 異常値が clamp される（弾力は0.1..4、重みは0..1、飽和年は下限1）
            var c = new ContractSoldierParams(10f, 2f, -1f, 0f, 2f, -1f, 2f);
            Assert.AreEqual(4f, c.payElasticity, 1e-4f);
            Assert.AreEqual(1f, c.reenlistmentWeight, 1e-4f);
            Assert.AreEqual(0f, c.maxHazardPay, 1e-4f);
            Assert.AreEqual(1f, c.skillSaturationYears, 1e-4f);
            Assert.AreEqual(1f, c.maxDischargeAttrition, 1e-4f);
            Assert.AreEqual(0f, c.wageInflationGain, 1e-4f);
            Assert.AreEqual(1f, c.supplyWeight, 1e-4f);
        }

        [Test]
        public void Narrative_HighPayProfessionalArmyVsBleedingDraftMarket()
        {
            // 物語：好待遇の職業軍（高報酬・高満足・低リスク・長期勤続）は供給も定着も高く安定する。
            float supplyA = ContractSoldierRules.RecruitmentSupply(80f, 20f, P);   // 0.8
            float retainA = ContractSoldierRules.RetentionRate(0.8f, 1f, P);        // 0.8+0.2*0.4=0.88
            float attA = ContractSoldierRules.AttritionFromDischarge(0.2f, 0.1f, P);// 0.6*0.2*0.1=0.012
            float skillA = ContractSoldierRules.ProfessionalSkill(20f, 1f, P);      // 1
            float stabA = ContractSoldierRules.ForceStability(retainA, supplyA, attA, P);

            // 対照：低報酬・低満足・激戦・逼迫市場の軍は供給細り流出して崩れる。
            float supplyB = ContractSoldierRules.RecruitmentSupply(20f, 80f, P);    // 0.2
            float retainB = ContractSoldierRules.RetentionRate(0.2f, 0.1f, P);      // 0.2+0.8*0.4*0.1=0.232
            float attB = ContractSoldierRules.AttritionFromDischarge(0.9f, 0.9f, P);// 0.6*0.81=0.486
            float hazardB = ContractSoldierRules.HazardPayDemand(0.9f, 0.7f, P);    // 0.8*0.63=0.504
            float tightB = ContractSoldierRules.LaborMarketTightness(90f, 100f, P); // 0.9
            float wageB = ContractSoldierRules.WageInflation(tightB, P);            // 0.9*1.5→1
            float stabB = ContractSoldierRules.ForceStability(retainB, supplyB, attB, P);

            Assert.Greater(stabA, stabB);                 // 職業軍の方が安定
            Assert.Greater(skillA, 0.9f);                 // 専門性が育つ
            Assert.Greater(hazardB, 0.4f);                // 激戦で危険手当要求が高い
            Assert.AreEqual(1f, wageB, 1e-4f);            // 逼迫市場で賃金が天井
            Assert.Less(stabB, 0.3f);                     // 流出で兵力が崩れる
        }
    }
}
