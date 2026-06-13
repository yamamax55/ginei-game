using NUnit.Framework;
using UnityEngine;

namespace Ginei.Tests
{
    /// <summary>
    /// 夜戦／視界制限戦闘の純ロジック検証（既定 Params で期待値固定）。
    /// </summary>
    public class NightBattleRulesTests
    {
        const float Eps = 1e-4f;

        [Test]
        public void VisibilityPenalty_SensorCompensatesDarkness()
        {
            // 0.8*0.7*(1-0.5*0.6)=0.56*0.7=0.392
            float vp = NightBattleRules.VisibilityPenalty(0.8f, 0.5f);
            Assert.AreEqual(0.392f, vp, Eps);

            // 索敵が良いほど視界低下は小さい
            float better = NightBattleRules.VisibilityPenalty(0.8f, 1f);
            Assert.Less(better, vp);
        }

        [Test]
        public void AccuracyReduction_DropsWithVisibility()
        {
            // 0.392*0.8=0.3136
            Assert.AreEqual(0.3136f, NightBattleRules.AccuracyReduction(0.392f), Eps);
        }

        [Test]
        public void FriendlyFireRisk_ScalesWithDensity()
        {
            // 0.392*1.0*0.5=0.196
            Assert.AreEqual(0.196f, NightBattleRules.FriendlyFireRisk(0.392f, 1f), Eps);
            // 密度ゼロなら同士討ちなし
            Assert.AreEqual(0f, NightBattleRules.FriendlyFireRisk(0.392f, 0f), Eps);
        }

        [Test]
        public void CommandConfusion_LargerFleetMoreConfused()
        {
            // 0.392*0.6*(50/100)=0.392*0.6*0.5=0.1176
            float half = NightBattleRules.CommandConfusion(0.392f, 50f);
            Assert.AreEqual(0.1176f, half, Eps);
            // 大艦隊ほど混乱が大きい
            float full = NightBattleRules.CommandConfusion(0.392f, 100f);
            Assert.Greater(full, half);
        }

        [Test]
        public void NightSurpriseBonus_GrowsWithEnemyBlindness()
        {
            // 0.8*0.392*0.9=0.28224
            Assert.AreEqual(0.28224f, NightBattleRules.NightSurpriseBonus(0.8f, 0.392f), Eps);
            // 敵の視界が明瞭なら奇襲は効かない
            Assert.AreEqual(0f, NightBattleRules.NightSurpriseBonus(0.8f, 0f), Eps);
        }

        [Test]
        public void VeterancyCompensation_ReducesEffectivePenalty()
        {
            // 0.4*(1-0.7*0.7)=0.4*0.51=0.204
            float compensated = NightBattleRules.VeterancyCompensation(0.7f, 0.4f);
            Assert.AreEqual(0.204f, compensated, Eps);
            // 練度が補うので元の視界低下より小さい
            Assert.Less(compensated, 0.4f);
        }

        [Test]
        public void EngagementRangeShrink_ClosesDistance()
        {
            // 10*(1-0.4*0.7)=10*0.72=7.2
            Assert.AreEqual(7.2f, NightBattleRules.EngagementRangeShrink(0.4f, 10f), Eps);
        }

        [Test]
        public void IsNightChaos_AtThreshold()
        {
            Assert.IsTrue(NightBattleRules.IsNightChaos(0.5f));   // しきい値ちょうどで混乱
            Assert.IsTrue(NightBattleRules.IsNightChaos(0.6f));
            Assert.IsFalse(NightBattleRules.IsNightChaos(0.49f));
        }

        [Test]
        public void Narrative_DarknessHurtsButVeterancyAndStealthSave()
        {
            // 闇が深く索敵が乏しい星雲戦：視界が大きく落ちる
            float vp = NightBattleRules.VisibilityPenalty(1f, 0.1f);
            Assert.Greater(vp, 0.5f);
            Assert.IsTrue(NightBattleRules.IsNightChaos(vp)); // 夜戦混乱に陥る

            // 闇で命中が落ち、指揮が混乱し、同士討ちが増える
            float accuracyLoss = NightBattleRules.AccuracyReduction(vp);
            float confusion = NightBattleRules.CommandConfusion(vp, 90f);
            float friendlyFire = NightBattleRules.FriendlyFireRisk(vp, 0.9f);
            Assert.Greater(accuracyLoss, 0f);
            Assert.Greater(confusion, 0f);
            Assert.Greater(friendlyFire, 0f);

            // 交戦距離は縮み近接遭遇戦になる
            float shrunk = NightBattleRules.EngagementRangeShrink(vp, 20f);
            Assert.Less(shrunk, 20f);

            // だが練度が闇の不利を補う
            float greenPenalty = NightBattleRules.VeterancyCompensation(0.0f, vp);
            float veteranPenalty = NightBattleRules.VeterancyCompensation(0.9f, vp);
            Assert.Less(veteranPenalty, greenPenalty);

            // そして闇は奇襲の好機でもある（敵が盲目なら隠密接近が刺さる）
            float surprise = NightBattleRules.NightSurpriseBonus(0.9f, vp);
            Assert.Greater(surprise, 0f);

            // 一方こちらの索敵装備を上げれば闇の不利そのものが薄れる
            float withSensors = NightBattleRules.VisibilityPenalty(1f, 1f);
            Assert.Less(withSensors, vp);
        }
    }
}
