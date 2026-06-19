using NUnit.Framework;
using UnityEngine;

namespace Ginei.Tests
{
    /// <summary>
    /// 同盟管理（多国間軍事同盟の内部力学）の純ロジックを既定Paramsで固定。期待値は手計算で検算。
    /// </summary>
    public class AllianceManagementRulesTests
    {
        const float Eps = 1e-4f;

        [Test]
        public void Cohesion_脅威と価値観を正規化重みで混ぜる()
        {
            // (0.8*0.6 + 0.2*0.4)/(0.6+0.4) = (0.48+0.08)/1.0 = 0.56
            Assert.AreEqual(0.56f, AllianceManagementRules.Cohesion(0.8f, 0.2f), Eps);
        }

        [Test]
        public void Cohesion_脅威の方が結束を生む()
        {
            // 脅威重み0.6 > 価値重み0.4 なので、同量なら脅威のみの方が高い
            float threatOnly = AllianceManagementRules.Cohesion(1f, 0f);
            float valuesOnly = AllianceManagementRules.Cohesion(0f, 1f);
            Assert.Greater(threatOnly, valuesOnly);
            Assert.AreEqual(0.6f, threatOnly, Eps);
            Assert.AreEqual(0.4f, valuesOnly, Eps);
        }

        [Test]
        public void BurdenShare_拠出を国力で割る_国力ゼロは測れず0()
        {
            Assert.AreEqual(0.3f, AllianceManagementRules.BurdenShare(0.3f, 1.0f), Eps);
            Assert.AreEqual(1.0f, AllianceManagementRules.BurdenShare(500f, 500f), Eps);
            Assert.AreEqual(0f, AllianceManagementRules.BurdenShare(10f, 0f), Eps);
        }

        [Test]
        public void FreeRiderPenalty_応分未満ほど不満_応分以上は不満ゼロ()
        {
            // (1-0.3)*0.5 = 0.35
            Assert.AreEqual(0.35f, AllianceManagementRules.FreeRiderPenalty(0.3f), Eps);
            // 応分(1.0)で不満0
            Assert.AreEqual(0f, AllianceManagementRules.FreeRiderPenalty(1.0f), Eps);
            // 過剰負担(1.2)でも不満0
            Assert.AreEqual(0f, AllianceManagementRules.FreeRiderPenalty(1.2f), Eps);
        }

        [Test]
        public void CommitmentToWar_結束と自国利害を混ぜる()
        {
            // 0.7*0.6 + 0.4*0.4 = 0.42+0.16 = 0.58
            Assert.AreEqual(0.58f, AllianceManagementRules.CommitmentToWar(0.7f, 0.4f), Eps);
        }

        [Test]
        public void HegemonLeadership_国力比と正統性を混ぜる()
        {
            // 0.5*0.6 + 1.0*0.4 = 0.3+0.4 = 0.7
            Assert.AreEqual(0.7f, AllianceManagementRules.HegemonLeadership(0.5f, 1.0f), Eps);
        }

        [Test]
        public void DefectionRisk_結束低_不公平_外部誘いで上がる()
        {
            // ((1-0.2)+0.6+0.4)/3 * 1.0 = 1.8/3 = 0.6
            Assert.AreEqual(0.6f, AllianceManagementRules.DefectionRisk(0.2f, 0.6f, 0.4f), Eps);
            // 結束満点・不公平/誘いゼロならリスク0
            Assert.AreEqual(0f, AllianceManagementRules.DefectionRisk(1f, 0f, 0f), Eps);
        }

        [Test]
        public void AllianceStrength_結束が低いと烏合の衆()
        {
            // lerp(1-0.5, 1, 0.5) = lerp(0.5,1,0.5)=0.75 → 1000*0.75 = 750
            Assert.AreEqual(750f, AllianceManagementRules.AllianceStrength(1000f, 0.5f), Eps);
            // 結束満点なら総国力そのまま
            Assert.AreEqual(1000f, AllianceManagementRules.AllianceStrength(1000f, 1f), Eps);
        }

        [Test]
        public void WillHold_結束がリスクを上回れば持つ()
        {
            Assert.IsTrue(AllianceManagementRules.WillHold(0.6f, 0.4f));
            Assert.IsFalse(AllianceManagementRules.WillHold(0.3f, 0.5f));
            // 拮抗（結束=リスク）は持たない（厳密な大なり）
            Assert.IsFalse(AllianceManagementRules.WillHold(0.5f, 0.5f));
            // 閾値を上げると余裕が要る
            Assert.IsFalse(AllianceManagementRules.WillHold(0.6f, 0.4f, 0.3f));
        }

        [Test]
        public void 物語_脅威の同盟は固く_脅威が去ると緩みただ乗りで崩れる()
        {
            // 共通の脅威が大きく価値観も近い間は結束が高く、ただ乗りも露見せず同盟は持つ
            float warCohesion = AllianceManagementRules.Cohesion(0.9f, 0.7f); // (0.54+0.28)=0.82
            Assert.AreEqual(0.82f, warCohesion, Eps);
            float fairShare = AllianceManagementRules.BurdenShare(450f, 500f); // 0.9
            float warRisk = AllianceManagementRules.DefectionRisk(
                warCohesion,
                AllianceManagementRules.FreeRiderPenalty(fairShare), // (1-0.9)*0.5=0.05
                0.1f);
            Assert.IsTrue(AllianceManagementRules.WillHold(warCohesion, warRisk),
                "脅威下では結束が高く同盟は持つ");

            // 脅威が去り価値観のみ・ある国が露骨にただ乗りし、外部から誘いが来ると離脱リスクが結束を上回る
            float peaceCohesion = AllianceManagementRules.Cohesion(0.1f, 0.4f); // (0.06+0.16)=0.22
            Assert.AreEqual(0.22f, peaceCohesion, Eps);
            float freeRider = AllianceManagementRules.BurdenShare(50f, 500f); // 0.1
            float unfair = AllianceManagementRules.FreeRiderPenalty(freeRider); // (1-0.1)*0.5=0.45
            float peaceRisk = AllianceManagementRules.DefectionRisk(peaceCohesion, unfair, 0.6f);
            Assert.IsFalse(AllianceManagementRules.WillHold(peaceCohesion, peaceRisk),
                "脅威が去り負担も不公平・外部の誘いがあれば同盟は崩れる");
        }
    }
}
