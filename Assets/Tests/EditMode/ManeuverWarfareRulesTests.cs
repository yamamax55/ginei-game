using NUnit.Framework;
using UnityEngine;

namespace Ginei.Tests
{
    /// <summary>
    /// 機動戦 vs 消耗戦（<see cref="ManeuverWarfareRules"/>）の純ロジックテスト。既定
    /// <see cref="ManeuverWarfareParams.Default"/> で期待値を固定（機動寄与1.0・削り速度0.1・テンポ要求1.0・
    /// 損害下限0.05・費用対効果上限10）。
    /// </summary>
    public class ManeuverWarfareRulesTests
    {
        const float Eps = 1e-4f;

        [Test]
        public void ManeuverVsAttrition_PositiveWhenMobilityDominates()
        {
            // (60-40)/(60+40) = 0.2（機動寄り）
            Assert.AreEqual(0.2f, ManeuverWarfareRules.ManeuverVsAttrition(60f, 40f), Eps);
            // 拮抗は0
            Assert.AreEqual(0f, ManeuverWarfareRules.ManeuverVsAttrition(50f, 50f), Eps);
        }

        [Test]
        public void SystemDisruption_ZeroWhenNotAdvantaged()
        {
            // 機動優位0.5・敵結束0.4 → 0.5*0.6 = 0.3
            Assert.AreEqual(0.3f, ManeuverWarfareRules.SystemDisruption(0.5f, 0.4f), Eps);
            // 劣位（adv<0）では崩せない
            Assert.AreEqual(0f, ManeuverWarfareRules.SystemDisruption(-0.5f, 0.4f), Eps);
        }

        [Test]
        public void AttritionGrind_AccumulatesOverDuration()
        {
            // 1*5*0.1 = 0.5
            Assert.AreEqual(0.5f, ManeuverWarfareRules.AttritionGrind(1f, 5f), Eps);
            // 1*10*0.1 = 1.0（確実に削り切るが時間を要する）
            Assert.AreEqual(1.0f, ManeuverWarfareRules.AttritionGrind(1f, 10f), Eps);
        }

        [Test]
        public void TempoRequirement_OnlyManeuverDemandsSpeed()
        {
            // 機動寄り0.6 → テンポ要求0.6
            Assert.AreEqual(0.6f, ManeuverWarfareRules.TempoRequirement(0.6f), Eps);
            // 消耗寄り（≤0）はテンポを要求しない
            Assert.AreEqual(0f, ManeuverWarfareRules.TempoRequirement(-0.6f), Eps);
        }

        [Test]
        public void MismatchPenalty_GrowsWithEnemyFlexibility()
        {
            // 機動寄り0.8・敵柔軟性0.5 → 0.4（読まれて空振る）
            Assert.AreEqual(0.4f, ManeuverWarfareRules.MismatchPenalty(0.8f, 0.5f), Eps);
            // 消耗寄りは空振りしない
            Assert.AreEqual(0f, ManeuverWarfareRules.MismatchPenalty(-0.8f, 0.5f), Eps);
        }

        [Test]
        public void CostEffectiveness_HighWhenLossesSmall()
        {
            // 0.6/0.1 = 6.0
            Assert.AreEqual(6.0f, ManeuverWarfareRules.CostEffectiveness(0.6f, 0.1f), Eps);
            // 損害0.01は下限0.05に丸め → 0.5/0.05 = 10 → 上限10で頭打ち
            Assert.AreEqual(10f, ManeuverWarfareRules.CostEffectiveness(0.5f, 0.01f), Eps);
        }

        [Test]
        public void CertaintyVsEfficiency_PositiveWhenDisruptionLeads()
        {
            // (0.6-0.2)/(0.6+0.2) = 0.5（機動の効率へ傾く）
            Assert.AreEqual(0.5f, ManeuverWarfareRules.CertaintyVsEfficiency(0.2f, 0.6f), Eps);
            // 削りが勝れば消耗の確実へ（負）
            Assert.Less(ManeuverWarfareRules.CertaintyVsEfficiency(0.6f, 0.2f), 0f);
        }

        [Test]
        public void IsCollapseAchieved_TrueAboveThreshold()
        {
            Assert.IsTrue(ManeuverWarfareRules.IsCollapseAchieved(0.7f));
            Assert.IsFalse(ManeuverWarfareRules.IsCollapseAchieved(0.4f));
        }

        [Test]
        public void Narrative_ManeuverCollapsesCheaplyButWhiffsOnFlexibleFoe()
        {
            // 機動力に振った軍（機動70・火力30）＝機動戦寄り
            float approach = ManeuverWarfareRules.ManeuverVsAttrition(70f, 30f);
            Assert.Greater(approach, 0f);

            // 結束の緩い敵（0.2）は機動で大きく崩れ、わずかな損害（0.05）で高い費用対効果
            float disruption = ManeuverWarfareRules.SystemDisruption(approach, 0.2f);
            Assert.Greater(disruption, 0f);
            Assert.IsTrue(ManeuverWarfareRules.IsCollapseAchieved(disruption, 0.2f));
            float cheap = ManeuverWarfareRules.CostEffectiveness(disruption, 0.05f);
            Assert.Greater(cheap, 1f); // 少ない損害で崩す＝旨味

            // だが柔軟な敵（0.9）には読まれて空振る（噛み合わない）
            Assert.Greater(ManeuverWarfareRules.MismatchPenalty(approach, 0.9f), 0.3f);

            // 消耗戦は確実だが高コスト＝火力で押し続ければ着実に削り切る（時間と損害を払う）
            float grind = ManeuverWarfareRules.AttritionGrind(1f, 10f);
            Assert.AreEqual(1.0f, grind, Eps);
            // 崩壊より削りが勝つ局面では消耗の確実へ傾く
            Assert.Less(ManeuverWarfareRules.CertaintyVsEfficiency(grind, disruption), 0f);
        }
    }
}
