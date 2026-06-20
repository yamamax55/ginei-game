using NUnit.Framework;
using UnityEngine;

namespace Ginei.Tests
{
    /// <summary>労働争議（ストライキ）の純ロジック検証。既定 Params 期待値を手計算で固定。</summary>
    public class LaborStrikeRulesTests
    {
        const float Tol = 1e-4f;

        [Test]
        public void WorkerGrievance_WeightedAverageOfWageAndConditions()
        {
            // (0.8*0.6 + 0.5*0.4)/1.0 = 0.68
            Assert.AreEqual(0.68f, LaborStrikeRules.WorkerGrievance(0.8f, 0.5f), Tol);
        }

        [Test]
        public void Unionization_GrievanceTimesFreedomTimesGain()
        {
            // 0.68 * 0.5 * 1.2 = 0.408
            Assert.AreEqual(0.408f, LaborStrikeRules.Unionization(0.68f, 0.5f), Tol);
        }

        [Test]
        public void Unionization_NoFreedomKillsOrganizing()
        {
            // 組織化の自由ゼロ＝不満があっても団結しない
            Assert.AreEqual(0f, LaborStrikeRules.Unionization(1f, 0f), Tol);
        }

        [Test]
        public void StrikeParticipation_UnionTimesSolidarity()
        {
            // 0.5 * 0.6 * 1.1 = 0.33
            Assert.AreEqual(0.33f, LaborStrikeRules.StrikeParticipation(0.5f, 0.6f), Tol);
        }

        [Test]
        public void ProductionLoss_CriticalityAmplifiesPainPerParticipation()
        {
            // crit*gain = 0.6*1.3 = 0.78 ; 0.5*0.78 = 0.39
            Assert.AreEqual(0.39f, LaborStrikeRules.ProductionLoss(0.5f, 0.6f), Tol);
        }

        [Test]
        public void EmployerResponse_NegativeWhenRepressionDominates()
        {
            // 0.3*0.5 - 0.8*0.6 = 0.15 - 0.48 = -0.33（強硬）
            Assert.AreEqual(-0.33f, LaborStrikeRules.EmployerResponse(0.8f, 0.3f), Tol);
        }

        [Test]
        public void EmployerResponse_PositiveWhenConcessionDominates()
        {
            // 0.8*0.5 - 0.1*0.6 = 0.40 - 0.06 = 0.34（懐柔）
            Assert.AreEqual(0.34f, LaborStrikeRules.EmployerResponse(0.1f, 0.8f), Tol);
        }

        [Test]
        public void StrikerHardship_GrowsWithDurationShrinksWithFund()
        {
            // 2/(1+1) = 1.0 ; *1 = 1.0
            Assert.AreEqual(1f, LaborStrikeRules.StrikerHardship(2f, 1f), Tol);
            // 1/(1+3) = 0.25 ; 資金が多いと困窮は和らぐ
            Assert.AreEqual(0.25f, LaborStrikeRules.StrikerHardship(1f, 3f), Tol);
        }

        [Test]
        public void Settlement_SignedBetweenLaborWinAndCave()
        {
            // 0.68*-0.33 - 0.25*0.5 = -0.2244 - 0.125 = -0.3494（労働側が折れる）
            Assert.AreEqual(-0.3494f, LaborStrikeRules.Settlement(0.68f, -0.33f, 0.25f), Tol);
        }

        [Test]
        public void WarProductionImpact_ScalesWithMilitaryShare()
        {
            // 0.39 * 0.5 * 1.2 = 0.234
            Assert.AreEqual(0.234f, LaborStrikeRules.WarProductionImpact(0.39f, 0.5f), Tol);
        }

        [Test]
        public void Inputs_AreClampedAndArraySafeAtExtremes()
        {
            // clamp の確認：過大入力でも 0..1 / -1..1 に収まる
            Assert.AreEqual(1f, LaborStrikeRules.WorkerGrievance(5f, 5f), Tol);
            Assert.AreEqual(0f, LaborStrikeRules.WorkerGrievance(-5f, -5f), Tol);
            Assert.GreaterOrEqual(LaborStrikeRules.EmployerResponse(-9f, 9f), -1f);
            Assert.LessOrEqual(LaborStrikeRules.EmployerResponse(9f, -9f), 1f);
            Assert.AreEqual(0f, LaborStrikeRules.StrikerHardship(-5f, -5f), Tol);
            Assert.GreaterOrEqual(LaborStrikeRules.Settlement(2f, 2f, -2f), -1f);
            Assert.LessOrEqual(LaborStrikeRules.Settlement(2f, 2f, -2f), 1f);
        }

        [Test]
        public void Narrative_ShortStrikeWinsLongStrikeCaves()
        {
            var p = LaborStrikeParams.Default;
            // 賃金が長く停滞し条件も悪い職場＝高い不満
            float grievance = LaborStrikeRules.WorkerGrievance(0.9f, 0.8f, p);
            // 組織化の自由があり団結する
            float union = LaborStrikeRules.Unionization(grievance, 0.8f, p);
            // 強い連帯で多数が参加
            float part = LaborStrikeRules.StrikeParticipation(union, 0.9f, p);
            // 枢要産業＝止まると痛い
            float loss = LaborStrikeRules.ProductionLoss(part, 0.9f, p);
            Assert.Greater(loss, 0f);

            // 経営は弾圧意志が強く譲歩余力に乏しい＝強硬
            float response = LaborStrikeRules.EmployerResponse(0.8f, 0.2f, p);
            Assert.Less(response, 0f);

            // 短期＋潤沢なスト資金＝困窮は浅い → 妥結は深い困窮版より労働側に有利
            float earlyHardship = LaborStrikeRules.StrikerHardship(1f, 5f, p);
            // 長期＋枯れたスト資金＝困窮は深い
            float lateHardship = LaborStrikeRules.StrikerHardship(8f, 0f, p);
            Assert.Greater(lateHardship, earlyHardship);

            float earlySettlement = LaborStrikeRules.Settlement(grievance, response, earlyHardship, p);
            float lateSettlement = LaborStrikeRules.Settlement(grievance, response, lateHardship, p);
            // 長引くほど労働側はより不利な妥結へ折れる（困窮が効く）
            Assert.Less(lateSettlement, earlySettlement);

            // 軍需産業なら戦力供給へ波及
            float warHit = LaborStrikeRules.WarProductionImpact(loss, 0.8f, p);
            float civHit = LaborStrikeRules.WarProductionImpact(loss, 0.1f, p);
            Assert.Greater(warHit, civHit);
        }
    }
}
