using NUnit.Framework;
using UnityEngine;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 建国神話・国家叙事（国民統合の物語）を固定する：教育普及×文化反復で浸透し、浸透×帰属で統合、浸透×建国譚で正統性、
    /// 対抗叙事×硬直で修正主義の挑戦、浸透×矛盾事実で緊張、統合−緊張で頑健さ、頑健さ×教育継続で世代継承。
    /// 頑健さが挑戦を閾値ぶん上回る間だけ神話は持続する。既定 Params の具体値で担保。
    /// </summary>
    public class NationalMythRulesTests
    {
        private static readonly NationalMythParams P = NationalMythParams.Default;

        [Test]
        public void MythPenetration_教育を土台に文化反復が底上げ()
        {
            // 教育0.8×反復0.5＝0.8*Lerp(1,1.5,0.5)=0.8*1.25=1.0（クランプで満点）
            Assert.AreEqual(1f, NationalMythRules.MythPenetration(0.8f, 0.5f, P), 1e-4f);
            // 教育0.6×反復0.5＝0.6*1.25=0.75
            Assert.AreEqual(0.75f, NationalMythRules.MythPenetration(0.6f, 0.5f, P), 1e-4f);
            // 教育ゼロなら反復満点でも浸透ゼロ（反復だけでは根づかない）
            Assert.AreEqual(0f, NationalMythRules.MythPenetration(0f, 1f, P), 1e-4f);
        }

        [Test]
        public void NationalCohesion_浸透と共有帰属で統合()
        {
            // 浸透0.8×帰属0.5＝0.8*Lerp(0.4,1,0.5)=0.8*0.7=0.56
            Assert.AreEqual(0.56f, NationalMythRules.NationalCohesion(0.8f, 0.5f, P), 1e-4f);
            // 帰属満点なら浸透がそのまま統合へ
            Assert.AreEqual(0.75f, NationalMythRules.NationalCohesion(0.75f, 1f, P), 1e-4f);
            // 浸透ゼロなら統合ゼロ
            Assert.AreEqual(0f, NationalMythRules.NationalCohesion(0f, 1f, P), 1e-4f);
        }

        [Test]
        public void HistoricalLegitimacy_浸透と建国譚の幾何平均()
        {
            // Sqrt(0.64*1.0)=0.8
            Assert.AreEqual(0.8f, NationalMythRules.HistoricalLegitimacy(0.64f, 1f, P), 1e-3f);
            // 建国譚がゼロなら正統性は主張できない（一方だけでは立たない）
            Assert.AreEqual(0f, NationalMythRules.HistoricalLegitimacy(1f, 0f, P), 1e-3f);
            // 両方0.25＝Sqrt(0.0625)=0.25
            Assert.AreEqual(0.25f, NationalMythRules.HistoricalLegitimacy(0.25f, 0.25f, P), 1e-3f);
        }

        [Test]
        public void RevisionistChallenge_硬直が対抗叙事を増幅()
        {
            // 対抗0.5×硬直1.0＝0.5*Lerp(1,1.5,1)=0.5*1.5=0.75（硬直した神話は折れやすい）
            Assert.AreEqual(0.75f, NationalMythRules.RevisionistChallenge(0.5f, 1f, P), 1e-4f);
            // 硬直ゼロ（柔軟な神話）なら対抗叙事はそのまま
            Assert.AreEqual(0.4f, NationalMythRules.RevisionistChallenge(0.4f, 0f, P), 1e-4f);
        }

        [Test]
        public void MythRealityTension_浸透した神話ほど矛盾が痛む()
        {
            // 浸透0.8×矛盾0.5×0.7=0.28
            Assert.AreEqual(0.28f, NationalMythRules.MythRealityTension(0.8f, 0.5f, P), 1e-4f);
            // 浸透ゼロなら矛盾しても緊張ゼロ（誰も信じていない）
            Assert.AreEqual(0f, NationalMythRules.MythRealityTension(0f, 1f, P), 1e-4f);
        }

        [Test]
        public void NarrativeResilience_統合から緊張を引いた符号付き値()
        {
            // 統合0.56−緊張0.28=0.28（正＝叙事が国を束ねる）
            Assert.AreEqual(0.28f, NationalMythRules.NarrativeResilience(0.56f, 0.28f, P), 1e-4f);
            // 緊張が統合を上回れば負（破綻へ向かう）
            Assert.AreEqual(-0.3f, NationalMythRules.NarrativeResilience(0.2f, 0.5f, P), 1e-4f);
        }

        [Test]
        public void GenerationalTransmission_教育継続が継承を担う()
        {
            // 頑健さ0.28×継続1.0＝0.28*Lerp(0.2,1,1)=0.28
            Assert.AreEqual(0.28f, NationalMythRules.GenerationalTransmission(0.28f, 1f, P), 1e-4f);
            // 教育断絶（継続0）なら下限ぶんへ薄れる＝0.28*0.2=0.056
            Assert.AreEqual(0.056f, NationalMythRules.GenerationalTransmission(0.28f, 0f, P), 1e-4f);
            // 破綻した神話（負の頑健さ）は継承されない
            Assert.AreEqual(0f, NationalMythRules.GenerationalTransmission(-0.3f, 1f, P), 1e-4f);
        }

        [Test]
        public void IsMythEnduring_頑健さが挑戦を閾値ぶん上回る間だけ持続()
        {
            // 頑健さ0.8−挑戦0.1=0.7≥0.5＝持続
            Assert.IsTrue(NationalMythRules.IsMythEnduring(0.8f, 0.1f, 0.5f));
            // 頑健さ0.28−挑戦0=0.28<0.5＝持続しない
            Assert.IsFalse(NationalMythRules.IsMythEnduring(0.28f, 0f, 0.5f));
            // 頑健さが負（破綻）なら問答無用で false
            Assert.IsFalse(NationalMythRules.IsMythEnduring(-0.3f, 0f));
            // 既定閾値委譲版＝0.7-0.1=0.6≥0.5＝持続
            Assert.IsTrue(NationalMythRules.IsMythEnduring(0.7f, 0.1f));
        }

        [Test]
        public void Params_ctorクランプ()
        {
            var p = new NationalMythParams(2f, -1f, 2f, -0.5f, 5f, 9f);
            Assert.AreEqual(1f, p.reinforcementWeight, 1e-4f);
            Assert.AreEqual(0f, p.identityWeight, 1e-4f);
            Assert.AreEqual(1f, p.rigidityWeight, 1e-4f);
            Assert.AreEqual(0f, p.tensionWeight, 1e-4f);
            Assert.AreEqual(1f, p.continuityWeight, 1e-4f);
            Assert.AreEqual(1f, p.enduringThreshold, 1e-4f);
        }

        [Test]
        public void 物語_帝国ルドルフ神話は浸透し同盟は教育断絶で薄れる()
        {
            // 帝国：高い教育普及(0.9)＋強い文化反復(0.8)でルドルフ神話が深く浸透し、
            // 強固な帰属(0.9)で国民を束ねる。矛盾する事実(0.3)はあるが浸透が緊張を圧倒し、叙事は頑健。
            float imperialMyth = NationalMythRules.MythPenetration(0.9f, 0.8f, P);   // 0.9*1.4=1.0
            float imperialCohesion = NationalMythRules.NationalCohesion(imperialMyth, 0.9f, P);
            float imperialTension = NationalMythRules.MythRealityTension(imperialMyth, 0.3f, P);
            float imperialResilience = NationalMythRules.NarrativeResilience(imperialCohesion, imperialTension, P);
            Assert.Greater(imperialResilience, 0f);
            // 弱い修正主義の挑戦(対抗0.1×硬直0.4)を頑健さが上回り、神話は持続する
            // imperialResilience=0.73、challenge=0.1*1.2=0.12 → 0.73-0.12=0.61≥0.5＝持続
            float imperialChallenge = NationalMythRules.RevisionistChallenge(0.1f, 0.4f, P);
            Assert.IsTrue(NationalMythRules.IsMythEnduring(imperialResilience, imperialChallenge, P.enduringThreshold));

            // 同盟：自由への大逃避行の建国譚は強い(0.9)が、教育の継続が崩れる(0.2)と
            // 頑健な叙事も次世代へ伝わりにくい＝教育断絶では継承が痩せる。
            float allianceMyth = NationalMythRules.MythPenetration(0.7f, 0.6f, P);
            float allianceCohesion = NationalMythRules.NationalCohesion(allianceMyth, 0.7f, P);
            float allianceTension = NationalMythRules.MythRealityTension(allianceMyth, 0.4f, P);
            float allianceResilience = NationalMythRules.NarrativeResilience(allianceCohesion, allianceTension, P);
            float transmitContinuous = NationalMythRules.GenerationalTransmission(allianceResilience, 1f, P);
            float transmitBroken = NationalMythRules.GenerationalTransmission(allianceResilience, 0.2f, P);
            Assert.Less(transmitBroken, transmitContinuous); // 教育が断絶すると継承が薄れる
        }
    }
}
