using NUnit.Framework;
using UnityEngine;

namespace Ginei.Tests
{
    /// <summary>斬首戦法の純ロジック（#斬首戦法）EditMode テスト。既定 Params で期待値を固定。</summary>
    public class DecapitationStrikeRulesTests
    {
        const float Eps = 1e-4f;
        const float PowEps = 1e-3f; // Pow を含む箇所のみ緩める

        [Test]
        public void EscortPenetration_EqualForces_IsHalf()
        {
            // ratio = pow(100/100, 0.5) = 1 → 1/(1+1) = 0.5
            Assert.AreEqual(0.5f, DecapitationStrikeRules.EscortPenetration(100f, 100f), Eps);
        }

        [Test]
        public void EscortPenetration_EdgeCases_NoEscortFullNoStrikeZero()
        {
            Assert.AreEqual(1f, DecapitationStrikeRules.EscortPenetration(50f, 0f), Eps);   // 護衛皆無＝完全貫通
            Assert.AreEqual(0f, DecapitationStrikeRules.EscortPenetration(0f, 50f), Eps);   // 突撃なし＝届かない
            // ratio = pow(4, 0.5) = 2 → 2/3
            Assert.AreEqual(2f / 3f, DecapitationStrikeRules.EscortPenetration(400f, 100f), PowEps);
        }

        [Test]
        public void StrikeDamage_NakedFlagship_IsHalf()
        {
            // denom = 100 + 100*(1+0) = 200 → exposure(1) * 100/200 = 0.5
            Assert.AreEqual(0.5f, DecapitationStrikeRules.StrikeDamage(100f, 0f, 1f), Eps);
        }

        [Test]
        public void StrikeDamage_DefendedAndPartlyExposed_IsLower()
        {
            // denom = 100 + 100*(1+1) = 300 → 0.5 * 100/300 = 0.16666667
            Assert.AreEqual(0.5f * (100f / 300f), DecapitationStrikeRules.StrikeDamage(100f, 1f, 0.5f), Eps);
            Assert.AreEqual(0f, DecapitationStrikeRules.StrikeDamage(100f, 0f, 0f), Eps); // 露出ゼロ＝打撃ゼロ
        }

        [Test]
        public void CommandParalysis_Centralization_AmplifiesAndClamps()
        {
            // 集権1.0：0.5*(1+1*1)=1.0 で頭打ち
            Assert.AreEqual(1f, DecapitationStrikeRules.CommandParalysis(0.5f, 1f), Eps);
            // 分権0.0：打撃ぶんのみ
            Assert.AreEqual(0.4f, DecapitationStrikeRules.CommandParalysis(0.4f, 0f), Eps);
            // 中庸0.5：0.4*(1+0.5)=0.6
            Assert.AreEqual(0.6f, DecapitationStrikeRules.CommandParalysis(0.4f, 0.5f), Eps);
        }

        [Test]
        public void ChainOfCommandResilience_SuccessorAndDecentralization()
        {
            Assert.AreEqual(1f, DecapitationStrikeRules.ChainOfCommandResilience(1f, 1f), Eps);
            Assert.AreEqual(0.5f, DecapitationStrikeRules.ChainOfCommandResilience(0.5f, 0.5f), Eps);
            Assert.AreEqual(0f, DecapitationStrikeRules.ChainOfCommandResilience(0f, 0f), Eps);
        }

        [Test]
        public void StrikeForceRisk_ThickEscort_HigherRisk()
        {
            Assert.AreEqual(0.5f, DecapitationStrikeRules.StrikeForceRisk(100f, 100f), Eps);  // 100/200
            Assert.AreEqual(0.75f, DecapitationStrikeRules.StrikeForceRisk(100f, 300f), Eps); // 300/400
            Assert.AreEqual(0f, DecapitationStrikeRules.StrikeForceRisk(0f, 300f), Eps);      // 突撃なし
            Assert.AreEqual(0f, DecapitationStrikeRules.StrikeForceRisk(100f, 0f), Eps);      // 守りなし
        }

        [Test]
        public void DecapitationValueAndIsDecapitated()
        {
            Assert.AreEqual(0.5f, DecapitationStrikeRules.DecapitationValue(1f, 0.5f), Eps);    // 麻痺がリスクを上回る
            Assert.AreEqual(-0.45f, DecapitationStrikeRules.DecapitationValue(0.3f, 0.75f), Eps); // リスク超過
            Assert.IsTrue(DecapitationStrikeRules.IsCommandDecapitated(0.5f, 0.4f));
            Assert.IsFalse(DecapitationStrikeRules.IsCommandDecapitated(0.3f, 0.4f));
        }

        [Test]
        public void Story_PenetrateEscortAndStrikeFlagship_ParalyzesCentralizedFoeButEliteIsExposed()
        {
            // 精鋭400で護衛100を貫き、防御なしの旗艦を討つ。
            float pen = DecapitationStrikeRules.EscortPenetration(400f, 100f);   // pow(4,.5)=2 → 2/3
            Assert.AreEqual(2f / 3f, pen, PowEps);

            float exposure = DecapitationStrikeRules.FlagshipExposure(pen);
            Assert.AreEqual(pen, exposure, Eps);                                  // 護衛を抜くほど旗艦が裸

            float dmg = DecapitationStrikeRules.StrikeDamage(400f, 0f, exposure); // (2/3)*(400/500)=0.53333
            Assert.AreEqual((2f / 3f) * 0.8f, dmg, PowEps);

            // 集権的な敵は中枢喪失で全軍が麻痺（頭打ち1.0）。
            float paralysisCentral = DecapitationStrikeRules.CommandParalysis(dmg, 1f);
            // 分権的な敵は麻痺を免れる（打撃ぶんに留まる）。
            float paralysisDecentral = DecapitationStrikeRules.CommandParalysis(dmg, 0f);
            Assert.Greater(paralysisCentral, paralysisDecentral);
            Assert.AreEqual(dmg, paralysisDecentral, PowEps);

            // 突撃精鋭は護衛の中で孤立リスクを負う（護衛100/総500=0.2）。
            float risk = DecapitationStrikeRules.StrikeForceRisk(400f, 100f);
            Assert.AreEqual(0.2f, risk, Eps);

            // 集権的な敵に対しては斬首が割に合う（正味プラス）。
            float valueCentral = DecapitationStrikeRules.DecapitationValue(paralysisCentral, risk);
            float valueDecentral = DecapitationStrikeRules.DecapitationValue(paralysisDecentral, risk);
            Assert.Greater(valueCentral, 0f);
            Assert.Greater(valueCentral, valueDecentral); // 分権の敵相手は旨味が薄い
        }
    }
}
