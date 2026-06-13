using System.Collections.Generic;
using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 国全体の労働市場（#1957 LABM・<see cref="LaborRules"/>）を固定する：労働力人口(LAB-1)、就業と求人(LAB-2)、
    /// 失業率と種類(LAB-3)、オークンの法則(LAB-4)、フィリップス曲線と支持(LAB-5)。
    /// </summary>
    public class LaborTests
    {
        // ===== LAB-1 労働力人口 =====
        [Test]
        public void LaborForce_AndParticipation()
        {
            Assert.AreEqual(650f, LaborRules.LaborForce(1000f, 0.65f), 1e-3f);
            Assert.AreEqual(0.65f, LaborRules.ParticipationRate(600f, 50f, 1000f), 1e-4f); // (600+50)/1000
            Assert.AreEqual(0f, LaborRules.ParticipationRate(600f, 50f, 0f), 1e-4f);
            // 星系群から生産年齢人口を集計
            var p1 = new Province { demographics = new Population(100f, 600f, 100f) };
            var p2 = new Province { demographics = new Population(50f, 400f, 50f) };
            Assert.AreEqual(1000f, LaborRules.AggregateWorkingAge(new List<Province> { p1, p2 }), 1e-3f);
            Assert.AreEqual(0f, LaborRules.AggregateWorkingAge(null), 1e-4f);
        }

        // ===== LAB-2 就業と求人 =====
        [Test]
        public void Employed_Unemployed_FromJobs()
        {
            var e1 = new Enterprise();                     // 既定 employees=100
            var e2 = new Enterprise { employees = 200f };
            Assert.AreEqual(300f, LaborRules.AggregateJobs(new List<Enterprise> { e1, e2 }), 1e-3f);
            Assert.AreEqual(0f, LaborRules.AggregateJobs(null), 1e-4f);
            // 求人 < 労働力＝余りが失業
            Assert.AreEqual(300f, LaborRules.Employed(650f, 300f), 1e-3f);
            Assert.AreEqual(350f, LaborRules.Unemployed(650f, 300f), 1e-3f);
            // 求人 > 労働力＝全員就業（人手不足）
            Assert.AreEqual(650f, LaborRules.Employed(650f, 700f), 1e-3f);
            Assert.AreEqual(0f, LaborRules.Unemployed(650f, 650f), 1e-3f);
        }

        // ===== LAB-3 失業率と種類 =====
        [Test]
        public void UnemploymentRate_Cyclical_FullEmployment()
        {
            Assert.AreEqual(0.05f, LaborRules.UnemploymentRate(50f, 1000f), 1e-4f);
            Assert.AreEqual(0.95f, LaborRules.EmploymentRate(950f, 1000f), 1e-4f);
            Assert.AreEqual(0f, LaborRules.UnemploymentRate(50f, 0f), 1e-4f);
            Assert.AreEqual(0.01f, LaborRules.CyclicalUnemployment(0.05f, 0.04f), 1e-4f); // 不況ぶん
            Assert.IsTrue(LaborRules.IsFullEmployment(0.04f, 0.04f));   // 自然失業率以下＝完全雇用
            Assert.IsFalse(LaborRules.IsFullEmployment(0.06f, 0.04f));
        }

        // ===== LAB-4 オークンの法則 =====
        [Test]
        public void OkunsLaw_UnemploymentVsGap()
        {
            // 高失業（自然率超過）→GDPは潜在を下回る（不況＝負のギャップ）
            Assert.AreEqual(-0.04f, LaborRules.OutputGapFromUnemployment(0.06f, 0.04f, LaborRules.OkunCoefficient), 1e-4f);
            // 逆算：負のギャップ→失業は自然率を上回る
            Assert.AreEqual(0.06f, LaborRules.UnemploymentFromGap(-0.04f, 0.04f, LaborRules.OkunCoefficient), 1e-4f);
            // 正のギャップ（過熱）→失業は自然率を下回る
            Assert.AreEqual(0.02f, LaborRules.UnemploymentFromGap(0.04f, 0.04f, LaborRules.OkunCoefficient), 1e-4f);
            Assert.AreEqual(0.04f, LaborRules.UnemploymentFromGap(0.04f, 0.04f, 0f), 1e-4f); // okun0は自然率
        }

        // ===== LAB-5 フィリップス曲線・支持 =====
        [Test]
        public void PhillipsCurve_AndSupportPenalty()
        {
            // 自然失業率では期待インフレ通り
            Assert.AreEqual(0.02f, LaborRules.PhillipsInflation(0.04f, 0.04f, 0.02f, LaborRules.PhillipsSensitivity), 1e-4f);
            // 人手不足（低失業）→インフレ加速
            Assert.AreEqual(0.03f, LaborRules.PhillipsInflation(0.02f, 0.04f, 0.02f, LaborRules.PhillipsSensitivity), 1e-4f);
            // 高失業ほど支持ペナルティ（自然率では0、超過で増え、十分高いと満タン）
            Assert.AreEqual(0f, LaborRules.SupportPenalty(0.04f, 0.04f), 1e-4f);
            Assert.AreEqual(0.5f, LaborRules.SupportPenalty(0.14f, 0.04f), 1e-3f); // 超過0.10/0.2
            Assert.AreEqual(1f, LaborRules.SupportPenalty(0.30f, 0.04f), 1e-4f);   // クランプ
        }
    }
}
