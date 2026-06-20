using NUnit.Framework;
using UnityEngine;

namespace Ginei.Tests
{
    /// <summary>戦争終結（講和の機運・引き際）の純ロジック検証。既定 Params で期待値固定。</summary>
    public class WarTerminationRulesTests
    {
        const float Eps = 1e-4f;

        [Test]
        public void WarWeariness_AggregatesWeightedSum()
        {
            // 0.8*0.45 + 0.6*0.3 + 0.4*0.25 = 0.36 + 0.18 + 0.10 = 0.64
            float w = WarTerminationRules.WarWeariness(0.8f, 0.6f, 0.4f);
            Assert.AreEqual(0.64f, w, Eps);
        }

        [Test]
        public void WarWeariness_ClampsInputsAndResult()
        {
            // 全要素 1 を超えても各重みの和(0.45+0.3+0.25=1.0)で上限 1.0。
            float w = WarTerminationRules.WarWeariness(2f, 2f, 2f);
            Assert.AreEqual(1f, w, Eps);
        }

        [Test]
        public void PeaceIncentive_RisesWithWearinessAndStalemate()
        {
            // 0.64 * (0.7 + 0.3*0.5) = 0.64 * 0.85 = 0.544
            float pi = WarTerminationRules.PeaceIncentive(0.64f, 0.5f);
            Assert.AreEqual(0.544f, pi, Eps);
            // 膠着なしでも厭戦の 7 割は動機になる。
            Assert.AreEqual(0.448f, WarTerminationRules.PeaceIncentive(0.64f, 0f), Eps);
        }

        [Test]
        public void NegotiationLeverage_BlendsPositionAndEnemyWeariness()
        {
            // 0.8*0.65 + 0.3*0.35 = 0.52 + 0.105 = 0.625
            float nl = WarTerminationRules.NegotiationLeverage(0.8f, 0.3f);
            Assert.AreEqual(0.625f, nl, Eps);
        }

        [Test]
        public void AcceptableTerms_FaceSavingRaisesFloor()
        {
            // 立場が強ければ leverage そのまま。
            Assert.AreEqual(0.625f, WarTerminationRules.AcceptableTerms(0.625f, 1f), Eps);
            // 立場が弱くても面子の必要が体面の下限(0.25)を確保する。
            Assert.AreEqual(0.25f, WarTerminationRules.AcceptableTerms(0.1f, 1f), Eps);
        }

        [Test]
        public void CulminationOfVictory_DeepAdvanceIsCullingPoint()
        {
            // 0.9*0.8*1.5 = 1.08 → clamp 1.0（勝ちすぎて攻勢限界）。
            Assert.AreEqual(1f, WarTerminationRules.CulminationOfVictory(0.9f, 0.8f), Eps);
            // 0.5*0.4*1.5 = 0.30
            Assert.AreEqual(0.3f, WarTerminationRules.CulminationOfVictory(0.5f, 0.4f), Eps);
        }

        [Test]
        public void SunkCostEscalation_ScalesWithInvestment()
        {
            // 0.6 * 0.5 = 0.30
            Assert.AreEqual(0.3f, WarTerminationRules.SunkCostEscalation(0.6f), Eps);
        }

        [Test]
        public void MutualExhaustionPeace_NeedsBothSidesTired()
        {
            // sqrt(0.64*0.81) = sqrt(0.5184) = 0.72
            Assert.AreEqual(0.72f, WarTerminationRules.MutualExhaustionPeace(0.64f, 0.81f), Eps);
            // 片方が元気(0)なら成立しない。
            Assert.AreEqual(0f, WarTerminationRules.MutualExhaustionPeace(0.9f, 0f), Eps);
        }

        [Test]
        public void ShouldSeekPeace_StrongPositionDelaysPeace()
        {
            // effective = 0.544 * (1 - 0.5*0.625) = 0.544 * 0.6875 = 0.374
            Assert.IsTrue(WarTerminationRules.ShouldSeekPeace(0.544f, 0.625f, 0.3f));
            Assert.IsFalse(WarTerminationRules.ShouldSeekPeace(0.544f, 0.625f, 0.4f));
        }

        [Test]
        public void Narrative_BothSidesBledWhiteSettleAtStalemate()
        {
            // 物語：双方が長期戦で血を流し尽くし、戦線は膠着。勝者なき手打ちへ。
            var p = WarTerminationParams.Default;

            // 自軍：重い損害・長期戦・経済疲弊。
            float own = WarTerminationRules.WarWeariness(0.85f, 0.9f, 0.8f, p);
            // 敵軍も同様に疲弊。
            float enemy = WarTerminationRules.WarWeariness(0.8f, 0.9f, 0.75f, p);

            Assert.Greater(own, 0.7f, "長期総力戦で自軍の厭戦は高い");
            Assert.Greater(enemy, 0.7f, "敵軍も厭戦は高い");

            // 戦線は膠着＝講和への動機が強く湧く。
            float incentive = WarTerminationRules.PeaceIncentive(own, 0.9f);
            Assert.Greater(incentive, 0.6f);

            // 戦況は互角＝交渉力は強くない。
            float leverage = WarTerminationRules.NegotiationLeverage(0.5f, enemy);
            Assert.Less(leverage, 0.7f);

            // 双方疲弊での講和が成立する水準に達する。
            float mutual = WarTerminationRules.MutualExhaustionPeace(own, enemy);
            Assert.Greater(mutual, 0.6f);

            // 立場が強くないので講和を急ぐべき＝手打ちに動く。
            Assert.IsTrue(WarTerminationRules.ShouldSeekPeace(incentive, leverage, 0.3f),
                "勝者なき消耗戦は引き際＝講和を求めるべき");
        }
    }
}
