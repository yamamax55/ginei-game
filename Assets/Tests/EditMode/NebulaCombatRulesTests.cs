using NUnit.Framework;
using UnityEngine;

namespace Ginei.Tests
{
    /// <summary>
    /// 星雲戦（ガス星雲内の会戦）の純ロジック検証（既定 Params で期待値固定）。
    /// </summary>
    public class NebulaCombatRulesTests
    {
        const float Eps = 1e-4f;

        [Test]
        public void SensorAttenuation_DensityVsSensorPower()
        {
            // 0.8/(0.8+0.4)=0.8/1.2=0.6666667
            Assert.AreEqual(0.6666667f, NebulaCombatRules.SensorAttenuation(0.8f, 0.4f), Eps);
            // センサー出力が高いほど見通せる（減衰が小さい）
            Assert.Less(NebulaCombatRules.SensorAttenuation(0.8f, 2f),
                        NebulaCombatRules.SensorAttenuation(0.8f, 0.4f));
            // 濃度ゼロなら減衰なし
            Assert.AreEqual(0f, NebulaCombatRules.SensorAttenuation(0f, 0.5f), Eps);
        }

        [Test]
        public void BeamDispersion_GrowsWithDensityAndRange()
        {
            // 0.8*0.5*0.6=0.24
            Assert.AreEqual(0.24f, NebulaCombatRules.BeamDispersion(0.8f, 0.5f), Eps);
            // 至近距離なら拡散しない
            Assert.AreEqual(0f, NebulaCombatRules.BeamDispersion(0.8f, 0f), Eps);
            // 遠いほど拡散する
            Assert.Greater(NebulaCombatRules.BeamDispersion(0.8f, 1f),
                           NebulaCombatRules.BeamDispersion(0.8f, 0.5f));
        }

        [Test]
        public void StealthBonus_ProportionalToAttenuation()
        {
            // 0.5*0.8=0.4
            Assert.AreEqual(0.4f, NebulaCombatRules.StealthBonus(0.5f), Eps);
            // 敵センサーが万全なら隠密の利はない
            Assert.AreEqual(0f, NebulaCombatRules.StealthBonus(0f), Eps);
        }

        [Test]
        public void EngagementRangeReduction_ScalesWithDensity()
        {
            // 0.8*0.7=0.56
            Assert.AreEqual(0.56f, NebulaCombatRules.EngagementRangeReduction(0.8f), Eps);
            // 濃いほど近接戦を強いられる
            Assert.Greater(NebulaCombatRules.EngagementRangeReduction(1f),
                           NebulaCombatRules.EngagementRangeReduction(0.5f));
        }

        [Test]
        public void CommunicationDegradation_DensityVsSignal()
        {
            // 0.8/(0.8+0.4)=0.6666667
            Assert.AreEqual(0.6666667f, NebulaCombatRules.CommunicationDegradation(0.8f, 0.4f), Eps);
            // 信号強度が高いほど通信が貫通する（障害が小さい）
            Assert.Less(NebulaCombatRules.CommunicationDegradation(0.8f, 2f),
                        NebulaCombatRules.CommunicationDegradation(0.8f, 0.4f));
            // 濃度ゼロなら障害なし
            Assert.AreEqual(0f, NebulaCombatRules.CommunicationDegradation(0f, 0.5f), Eps);
        }

        [Test]
        public void CommandPenalty_ProportionalToDegradation()
        {
            // 0.5*0.8=0.4
            Assert.AreEqual(0.4f, NebulaCombatRules.CommandPenalty(0.5f), Eps);
            // 通信が乱れるほど指揮が落ちる
            Assert.Greater(NebulaCombatRules.CommandPenalty(0.8f),
                           NebulaCombatRules.CommandPenalty(0.3f));
        }

        [Test]
        public void AmbushOpportunity_StealthTimesCloseRange()
        {
            // 0.4*0.56*0.9=0.2016
            Assert.AreEqual(0.2016f, NebulaCombatRules.AmbushOpportunity(0.4f, 0.56f), Eps);
            // 隠密が利かなければ伏兵は成立しない
            Assert.AreEqual(0f, NebulaCombatRules.AmbushOpportunity(0f, 0.56f), Eps);
            // 近接が誘発されなければ伏兵は成立しない
            Assert.AreEqual(0f, NebulaCombatRules.AmbushOpportunity(0.4f, 0f), Eps);
        }

        [Test]
        public void IsNebulaFavorable_StealthMustBeatCommandLoss()
        {
            // 隠密0.6 > 指揮低下0.3 → 有利
            Assert.IsTrue(NebulaCombatRules.IsNebulaFavorable(0.6f, 0.3f));
            // 隠密0.3 < 指揮低下0.6 → 不利
            Assert.IsFalse(NebulaCombatRules.IsNebulaFavorable(0.3f, 0.6f));
            // 拮抗（差0）は既定しきい値0を上回らない＝有利ではない
            Assert.IsFalse(NebulaCombatRules.IsNebulaFavorable(0.5f, 0.5f));
            // しきい値を高く要求すれば僅差の有利は否定される
            Assert.IsFalse(NebulaCombatRules.IsNebulaFavorable(0.6f, 0.5f, 0.2f));
        }

        [Test]
        public void Narrative_NebulaBlindsBothButFavorsTheStalker()
        {
            // 濃い星雲・乏しいセンサー：敵の眼が大きく潰れる
            float atten = NebulaCombatRules.SensorAttenuation(1f, 0.2f); // 1/1.2=0.8333
            Assert.Greater(atten, 0.5f);

            // 眼が潰れるほど隠密接近が刺さる
            float stealth = NebulaCombatRules.StealthBonus(atten);
            Assert.Greater(stealth, 0f);

            // 濃いガスはビームを拡散させ遠射が鈍り、交戦距離が縮んで近接戦になる
            float dispersion = NebulaCombatRules.BeamDispersion(1f, 1f);
            float closeIn = NebulaCombatRules.EngagementRangeReduction(1f);
            Assert.Greater(dispersion, 0f);
            Assert.Greater(closeIn, 0f);

            // 隠密×近接が噛み合い伏兵の好機が生まれる
            float ambush = NebulaCombatRules.AmbushOpportunity(stealth, closeIn);
            Assert.Greater(ambush, 0f);

            // だが星雲は通信も妨げ、自軍の指揮も乱す
            float comms = NebulaCombatRules.CommunicationDegradation(1f, 0.3f);
            float command = NebulaCombatRules.CommandPenalty(comms);
            Assert.Greater(command, 0f);

            // 信号強度を上げれば指揮低下は和らぐ
            float betterComms = NebulaCombatRules.CommunicationDegradation(1f, 2f);
            Assert.Less(betterComms, comms);

            // 総合：隠密の利が指揮低下を上回れば星雲は伏兵側に味方する
            Assert.IsTrue(NebulaCombatRules.IsNebulaFavorable(stealth, command));
        }
    }
}
