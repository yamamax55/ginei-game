using System.Collections.Generic;
using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>ミッションコマンド（任務戦術）：兵力比・必要兵力見積もり・統制上限・梯団・自動動員。</summary>
    public class MissionCommandTests
    {
        [Test]
        public void ForceRatio_AttackerThreeToOne()
        {
            Assert.AreEqual(3.0f, MissionCommandRules.ForceRatio(MissionType.星系攻略, true), 1e-4f);   // 防衛目標＝攻者三倍
            Assert.AreEqual(1.5f, MissionCommandRules.ForceRatio(MissionType.星系攻略, false), 1e-4f);  // 無防備＝軽い優越
            Assert.AreEqual(1.0f, MissionCommandRules.ForceRatio(MissionType.星系防衛, true), 1e-4f);
            Assert.AreEqual(0.5f, MissionCommandRules.ForceRatio(MissionType.哨戒, false), 1e-4f);
        }

        [Test]
        public void Estimate_CompetentStaffIsLean_IncompetentPads()
        {
            // 敵1万・攻略・防衛あり：基線=10000×3=30000。
            // 有能(comp=1)：安全率0 → 30000。無能(comp=0)：+50% → 45000。
            Assert.AreEqual(30000f, MissionCommandRules.EstimateRequiredStrength(10000f, MissionType.星系攻略, true, 1f), 1f);
            Assert.AreEqual(45000f, MissionCommandRules.EstimateRequiredStrength(10000f, MissionType.星系攻略, true, 0f), 1f);
            // 敵不在の無防備占領でも最小動員（駐留兵力）を下回らない。
            Assert.AreEqual(MissionCommandRules.MinMobilization,
                MissionCommandRules.EstimateRequiredStrength(0f, MissionType.星系攻略, false, 1f), 1f);
        }

        [Test]
        public void MaxCoordinable_ScalesWithCompetenceAndCircumstance()
        {
            Assert.AreEqual(15000f, MissionCommandRules.MaxCoordinableStrength(0f), 1f);  // 無能＝一個艦隊規模
            Assert.AreEqual(90000f, MissionCommandRules.MaxCoordinableStrength(1f), 1f);  // 有能＝軍集団規模
            Assert.AreEqual(52500f, MissionCommandRules.MaxCoordinableStrength(0.5f), 1f);
            // 諸般の事情で半減（補給難・予備拘束）。
            Assert.AreEqual(45000f, MissionCommandRules.MaxCoordinableStrength(1f, 0.5f), 1f);
        }

        [Test]
        public void RecommendEchelon_ByScale()
        {
            Assert.AreEqual(EchelonType.艦隊, MissionCommandRules.RecommendEchelon(15000f));
            Assert.AreEqual(EchelonType.軍団, MissionCommandRules.RecommendEchelon(45000f));
            Assert.AreEqual(EchelonType.軍, MissionCommandRules.RecommendEchelon(60000f));
            Assert.AreEqual(EchelonType.軍集団, MissionCommandRules.RecommendEchelon(90000f));
            Assert.AreEqual(EchelonType.宇宙艦隊, MissionCommandRules.RecommendEchelon(90001f));
        }

        [Test]
        public void PlanMission_CompetentStaff_MobilizesCorpsJustEnough()
        {
            // 有能な参謀本部(comp=1)：必要=30000、統制上限=90000、目標=30000。
            // 3個艦隊(各15000)から大きい順に2個を束ねた軍団を動員＝必要十分・feasible。
            var avail = new List<MissionForce>
            {
                new MissionForce(1, 15000), new MissionForce(2, 15000), new MissionForce(3, 15000),
            };
            var plan = MissionCommandRules.PlanMission(7, MissionType.星系攻略, Faction.帝国, 10000f, true, 1f, avail);
            Assert.AreEqual(30000f, plan.requiredStrength, 1f);
            Assert.AreEqual(30000f, plan.committedStrength, 1f);
            Assert.IsTrue(plan.feasible);
            Assert.AreEqual(2, plan.fleetIds.Count);
            Assert.Contains(1, plan.fleetIds);
            Assert.Contains(2, plan.fleetIds);
            Assert.AreEqual(EchelonType.軍団, plan.echelon); // 複数艦隊を束ねた軍団
        }

        [Test]
        public void PlanMission_IncompetentStaff_UnderMobilizes()
        {
            // 無能な参謀本部(comp=0)：必要=45000（過大に盛る）だが統制上限=15000＝過小動員のまま発動。
            var avail = new List<MissionForce>
            {
                new MissionForce(1, 15000), new MissionForce(2, 15000), new MissionForce(3, 15000),
            };
            var plan = MissionCommandRules.PlanMission(7, MissionType.星系攻略, Faction.帝国, 10000f, true, 0f, avail);
            Assert.AreEqual(45000f, plan.requiredStrength, 1f);
            Assert.AreEqual(15000f, plan.committedStrength, 1f); // 統制上限で頭打ち
            Assert.IsFalse(plan.feasible);                        // 必要兵力に届かない＝リスク
            Assert.AreEqual(1, plan.fleetIds.Count);
            Assert.AreEqual(EchelonType.艦隊, plan.echelon);
        }

        [Test]
        public void PlanMission_NoForces_NotFeasible()
        {
            var plan = MissionCommandRules.PlanMission(7, MissionType.星系攻略, Faction.同盟, 10000f, true, 1f, null);
            Assert.AreEqual(0f, plan.committedStrength, 1e-4f);
            Assert.IsFalse(plan.feasible);
            Assert.AreEqual(0, plan.fleetIds.Count);
        }
    }
}
