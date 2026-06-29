using NUnit.Framework;
using UnityEngine;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 指揮所＝司令部：幕僚と処理能力で状況を把握し決断力で素早く決め、放射兆候で狙われ斬首され、
    /// 代替指揮所と委譲で継承して麻痺を避ける。既定パラメータで期待値固定（線形/多項式ゆえ厳密一致）。
    /// </summary>
    public class CommandPostRulesTests
    {
        const float Tol = 1e-4f;

        [Test]
        public void StaffProcessing_HeadcountTimesCompetence()
        {
            // headcount=clamp01(8/10)=0.8 ; *0.75 = 0.6
            Assert.AreEqual(0.6f, CommandPostRules.StaffProcessing(8f, 0.75f), Tol);
            // 標準人数で頭数飽和（超過は1.0でクランプ）＝練度そのまま。
            Assert.AreEqual(0.9f, CommandPostRules.StaffProcessing(50f, 0.9f), Tol);
            // 幕僚ゼロは処理能力ゼロ。
            Assert.AreEqual(0f, CommandPostRules.StaffProcessing(0f, 1f), Tol);
        }

        [Test]
        public void SituationalAwareness_InflowTimesProcessing()
        {
            // 0.9 * 0.6 * 1.0 = 0.54
            Assert.AreEqual(0.54f, CommandPostRules.SituationalAwareness(0.9f, 0.6f), Tol);
            // 情報が来ても捌けねば（処理0）把握できない。
            Assert.AreEqual(0f, CommandPostRules.SituationalAwareness(1f, 0f), Tol);
        }

        [Test]
        public void DecisionTempo_AwarenessTimesDecisiveness()
        {
            // 0.54 * 0.8 = 0.432
            Assert.AreEqual(0.432f, CommandPostRules.DecisionTempo(0.54f, 0.8f), Tol);
            // 把握していても決断できねば（決断力0）動けない。
            Assert.AreEqual(0f, CommandPostRules.DecisionTempo(0.54f, 0f), Tol);
        }

        [Test]
        public void CommandPostVulnerability_EmissionTimesExposure()
        {
            // 0.8 * (1 - 0.5) = 0.4
            Assert.AreEqual(0.4f, CommandPostRules.CommandPostVulnerability(0.8f, 0.5f), Tol);
            // 防御態勢が万全（1.0）なら狙われない。
            Assert.AreEqual(0f, CommandPostRules.CommandPostVulnerability(1f, 1f), Tol);
        }

        [Test]
        public void DecapitationRisk_VulnerabilityTimesTargeting()
        {
            // 0.4 * 0.75 = 0.3
            Assert.AreEqual(0.3f, CommandPostRules.DecapitationRisk(0.4f, 0.75f), Tol);
            // 敵が指揮中枢を狙っていなければ（照準0）リスクなし。
            Assert.AreEqual(0f, CommandPostRules.DecapitationRisk(0.4f, 0f), Tol);
        }

        [Test]
        public void SuccessionContinuity_AlternateTimesDelegation()
        {
            // 0.8 * 0.5 = 0.4
            Assert.AreEqual(0.4f, CommandPostRules.SuccessionContinuity(0.8f, 0.5f), Tol);
            // 代替指揮所があっても委譲の段取りが無ければ（委譲0）繋がらない。
            Assert.AreEqual(0f, CommandPostRules.SuccessionContinuity(1f, 0f), Tol);
        }

        [Test]
        public void ParalysisOnLoss_RiskDampenedByContinuity()
        {
            // 0.3 * (1 - 0.4) = 0.18
            Assert.AreEqual(0.18f, CommandPostRules.ParalysisOnLoss(0.3f, 0.4f), Tol);
            // 継承が万全（1.0）なら斬首を受けても麻痺しない。
            Assert.AreEqual(0f, CommandPostRules.ParalysisOnLoss(0.3f, 1f), Tol);
        }

        [Test]
        public void CommandPostEffectiveness_TempoMinusParalysis()
        {
            // 0.432 - 1.0*0.18 = 0.252
            Assert.AreEqual(0.252f, CommandPostRules.CommandPostEffectiveness(0.432f, 0.18f), Tol);
            // 麻痺が無ければ意思決定の速さがそのまま実効。
            Assert.AreEqual(0.432f, CommandPostRules.CommandPostEffectiveness(0.432f, 0f), Tol);
            // 麻痺が速さを上回れば実効は0でクランプ（司令部停止）。
            Assert.AreEqual(0f, CommandPostRules.CommandPostEffectiveness(0.2f, 0.5f), Tol);
        }

        [Test]
        public void Story_DecapitatedCommandPostParalyzesUnlessSuccessionPlanned()
        {
            // 充実した司令部：幕僚8名・練度0.75・情報流入0.9・決断力0.8。
            float proc = CommandPostRules.StaffProcessing(8f, 0.75f);            // 0.6
            float aware = CommandPostRules.SituationalAwareness(0.9f, proc);     // 0.54
            float tempo = CommandPostRules.DecisionTempo(aware, 0.8f);           // 0.432
            Assert.AreEqual(0.432f, tempo, Tol);

            // だが指揮所は活発な通信で晒され（放射0.8・防御0.5）、敵が中枢を狙う（照準0.75）。
            float vuln = CommandPostRules.CommandPostVulnerability(0.8f, 0.5f);  // 0.4
            float risk = CommandPostRules.DecapitationRisk(vuln, 0.75f);         // 0.3

            // 継承の段取りが乏しい（代替0.2・委譲0.25）＝斬首で麻痺。
            float weakSucc = CommandPostRules.SuccessionContinuity(0.2f, 0.25f); // 0.05
            float weakPar = CommandPostRules.ParalysisOnLoss(risk, weakSucc);    // 0.3*0.95 = 0.285
            float weakEff = CommandPostRules.CommandPostEffectiveness(tempo, weakPar); // 0.432-0.285 = 0.147
            Assert.AreEqual(0.147f, weakEff, Tol);

            // 代替指揮所と権限委譲を整えれば（代替0.8・委譲0.5）麻痺が和らぎ実効が上がる。
            float strongSucc = CommandPostRules.SuccessionContinuity(0.8f, 0.5f); // 0.4
            float strongPar = CommandPostRules.ParalysisOnLoss(risk, strongSucc); // 0.3*0.6 = 0.18
            float strongEff = CommandPostRules.CommandPostEffectiveness(tempo, strongPar); // 0.432-0.18 = 0.252
            Assert.AreEqual(0.252f, strongEff, Tol);

            // 継承を整えた方が中枢喪失の麻痺が小さく、司令部の実効が高い。
            Assert.Less(strongPar, weakPar);
            Assert.Greater(strongEff, weakEff);
        }
    }
}
