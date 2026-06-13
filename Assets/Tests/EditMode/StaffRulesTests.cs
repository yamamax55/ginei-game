using System.Collections.Generic;
using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>参謀本部基盤：部隊参謀の対象梯団・セクション能力写像・練度/協調・実効ボーナス。</summary>
    public class StaffRulesTests
    {
        [Test]
        public void RequiresFieldStaff_FleetToCorpsOnly()
        {
            Assert.IsTrue(StaffRules.RequiresFieldStaff(EchelonType.艦隊));   // 艦隊長
            Assert.IsTrue(StaffRules.RequiresFieldStaff(EchelonType.軍団));   // 軍団長
            Assert.IsFalse(StaffRules.RequiresFieldStaff(EchelonType.分艦隊)); // 未満＝副官止まり
            Assert.IsFalse(StaffRules.RequiresFieldStaff(EchelonType.戦隊));
            Assert.IsFalse(StaffRules.RequiresFieldStaff(EchelonType.軍));     // 超＝大本営の領分
            Assert.IsFalse(StaffRules.RequiresFieldStaff(EchelonType.軍集団));
        }

        [Test]
        public void RelevantStat_AndSectionScore()
        {
            Assert.AreEqual(StaffStat.統率, StaffRules.RelevantStat(StaffSection.作戦));
            Assert.AreEqual(StaffStat.情報, StaffRules.RelevantStat(StaffSection.情報));
            Assert.AreEqual(StaffStat.情報, StaffRules.RelevantStat(StaffSection.計画));
            Assert.AreEqual(StaffStat.運営, StaffRules.RelevantStat(StaffSection.人事));
            Assert.AreEqual(StaffStat.運営, StaffRules.RelevantStat(StaffSection.兵站));
            Assert.AreEqual(StaffStat.運営, StaffRules.RelevantStat(StaffSection.通信));

            // SectionScore は relevant な能力値を拾う（作戦＝統率80）。
            Assert.AreEqual(80f, StaffRules.SectionScore(StaffSection.作戦, 80f, 60f, 40f), 1e-4f);
            Assert.AreEqual(40f, StaffRules.SectionScore(StaffSection.情報, 80f, 60f, 40f), 1e-4f);
            Assert.AreEqual(60f, StaffRules.SectionScore(StaffSection.兵站, 80f, 60f, 40f), 1e-4f);
        }

        [Test]
        public void SectionEffectiveness_AndEmpty()
        {
            Assert.AreEqual(0.8f, StaffRules.SectionEffectiveness(80f), 1e-4f);
            Assert.AreEqual(StaffRules.EmptySectionEffectiveness, StaffRules.SectionEffectiveness(-1f), 1e-4f); // 空席
        }

        [Test]
        public void ChiefFactor_Bounds()
        {
            Assert.AreEqual(1.0f, StaffRules.ChiefFactor(50f), 1e-4f);
            Assert.AreEqual(1f + StaffRules.ChiefInfluence, StaffRules.ChiefFactor(100f), 1e-4f);
            Assert.AreEqual(1f - StaffRules.ChiefInfluence, StaffRules.ChiefFactor(0f), 1e-4f);
            Assert.AreEqual(1f - StaffRules.ChiefInfluence * 0.5f, StaffRules.ChiefFactor(-1f), 1e-4f); // 参謀長空席
        }

        [Test]
        public void OverallQuality_AverageTimesChief()
        {
            var effs = new List<float> { 0.8f, 0.6f, 0.4f, 0.6f, 0.5f, 0.5f }; // 平均0.566..
            float q = StaffRules.OverallQuality(effs, 50f); // chief 等倍
            Assert.AreEqual((0.8f + 0.6f + 0.4f + 0.6f + 0.5f + 0.5f) / 6f, q, 1e-4f);
            // 名参謀長で底上げ
            Assert.Greater(StaffRules.OverallQuality(effs, 100f), q);
            // 空入力は下限
            Assert.AreEqual(StaffRules.EmptySectionEffectiveness, StaffRules.OverallQuality(null, 50f), 1e-4f);
        }

        [Test]
        public void DomainFactors_RampWithEffectiveness()
        {
            Assert.AreEqual(1f, StaffRules.OperationsFactor(0f), 1e-4f);
            Assert.AreEqual(1f + StaffRules.OpsBonusMax, StaffRules.OperationsFactor(1f), 1e-4f);
            Assert.AreEqual(1f + StaffRules.IntelBonusMax, StaffRules.IntelligenceFactor(1f), 1e-4f);
            Assert.AreEqual(1f + StaffRules.LogiBonusMax, StaffRules.LogisticsFactor(1f), 1e-4f);
            Assert.AreEqual(1f + StaffRules.PersBonusMax, StaffRules.PersonnelFactor(1f), 1e-4f);
        }
    }
}
