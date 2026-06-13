using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 下士官教育（#210 下士官団・米軍 NCOPDS モデル）の純ロジックを固定する：
    /// STEP（教育が昇進の前提）／PME ラダーの選抜（上ほど狭き門）／下士官団の質／
    /// 背骨効果（練度・結束・自律）／損耗の質的打撃と再建の遅さ（“経験は急造できない”）。
    /// </summary>
    public class NcoEducationRulesTests
    {
        // ===== STEP（教育が昇進の前提） =====

        [Test]
        public void GradeTier_MapsCourseToTier()
        {
            Assert.AreEqual(1, NcoEducationRules.GradeTierFor(NcoCourse.初級));
            Assert.AreEqual(4, NcoEducationRules.GradeTierFor(NcoCourse.最先任));
        }

        [Test]
        public void RequiredCourse_MapsTierToCourse_AndClamps()
        {
            Assert.AreEqual(NcoCourse.初級, NcoEducationRules.RequiredCourseForTier(1));
            Assert.AreEqual(NcoCourse.最先任, NcoEducationRules.RequiredCourseForTier(4));
            Assert.AreEqual(NcoCourse.初級, NcoEducationRules.RequiredCourseForTier(0));   // 下限クランプ
            Assert.AreEqual(NcoCourse.最先任, NcoEducationRules.RequiredCourseForTier(9)); // 上限クランプ
        }

        [Test]
        public void PromotionEligible_RequiresMatchingCourse()
        {
            // 中級(段位2)修了 → 段位1,2は可・段位3は不可（no school, no promotion）
            Assert.IsTrue(NcoEducationRules.PromotionEligible(NcoCourse.中級, 2));
            Assert.IsTrue(NcoEducationRules.PromotionEligible(NcoCourse.中級, 1));
            Assert.IsFalse(NcoEducationRules.PromotionEligible(NcoCourse.中級, 3));
            Assert.IsTrue(NcoEducationRules.PromotionEligible(NcoCourse.最先任, 4));
            Assert.IsFalse(NcoEducationRules.PromotionEligible(NcoCourse.初級, 0)); // 段位0は無効
        }

        // ===== PME ラダーの選抜 =====

        [Test]
        public void QuotaPassing_NarrowsUpTheLadder()
        {
            Assert.AreEqual(70, NcoEducationRules.QuotaPassing(100, NcoCourse.初級));
            Assert.AreEqual(25, NcoEducationRules.QuotaPassing(100, NcoCourse.最先任));
            Assert.Greater(NcoEducationRules.QuotaPassing(100, NcoCourse.初級),
                           NcoEducationRules.QuotaPassing(100, NcoCourse.最先任));
            Assert.AreEqual(0, NcoEducationRules.QuotaPassing(0, NcoCourse.初級));
        }

        [Test]
        public void Graduates_GatedByCapacityAndPool()
        {
            // sitters=min(100,40)=40 → floor(40×0.7)=28
            Assert.AreEqual(28, NcoEducationRules.Graduates(100, 40, NcoCourse.初級));
            // pool が枠より小さい：sitters=10 → 7
            Assert.AreEqual(7, NcoEducationRules.Graduates(10, 1000, NcoCourse.初級));
            Assert.AreEqual(0, NcoEducationRules.Graduates(-5, 40, NcoCourse.初級));
        }

        // ===== 下士官団の質 =====

        [Test]
        public void ProgramQuality_RisesWithLadderReach()
        {
            Assert.AreEqual(1f, NcoEducationRules.ProgramQuality(NcoCourse.最先任, 1f), 1e-4f);
            Assert.AreEqual(NcoEducationRules.ProgramQualityFloor,
                            NcoEducationRules.ProgramQuality(NcoCourse.初級, 1f), 1e-4f);
            Assert.Greater(NcoEducationRules.ProgramQuality(NcoCourse.最先任, 0.5f),
                           NcoEducationRules.ProgramQuality(NcoCourse.初級, 0.5f));
        }

        // ===== 背骨効果（厚み・練度・結束・自律） =====

        [Test]
        public void Thickness_NormalizedToIdealRatio()
        {
            Assert.AreEqual(1f, NcoEducationRules.Thickness(15f, 100f), 1e-4f);   // 0.15/0.15
            Assert.AreEqual(0.5f, NcoEducationRules.Thickness(7.5f, 100f), 1e-4f);
            Assert.AreEqual(1f, NcoEducationRules.Thickness(30f, 100f), 1e-4f);   // 過剰はクランプ
            Assert.AreEqual(0f, NcoEducationRules.Thickness(0f, 100f), 1e-4f);
        }

        [Test]
        public void Multipliers_RequireBothDensityAndQuality()
        {
            var strong = new NcoCorps(1f, 1f);
            var thinButElite = new NcoCorps(0f, 1f);
            Assert.AreEqual(1f + NcoEducationRules.MaxProficiencyBonus,
                            NcoEducationRules.ProficiencyMultiplier(strong), 1e-4f);
            Assert.AreEqual(1f + NcoEducationRules.MaxCohesionBonus,
                            NcoEducationRules.CohesionMultiplier(strong), 1e-4f);
            // 下士官枯渇（density 0）は背骨が効かない＝倍率1.0
            Assert.AreEqual(1f, NcoEducationRules.ProficiencyMultiplier(thinButElite), 1e-4f);
            Assert.AreEqual(1f, NcoEducationRules.ProficiencyMultiplier(null), 1e-4f);
        }

        [Test]
        public void Autonomy_HighOnlyWithThickQualityCorps()
        {
            Assert.AreEqual(1f, NcoEducationRules.AutonomyFactor(new NcoCorps(1f, 1f)), 1e-4f);
            // 兵だけ（下士官枯渇）→ 自律ほぼ0＝中央指揮頼みで麻痺
            Assert.AreEqual(0f, NcoEducationRules.AutonomyFactor(new NcoCorps(0f, 1f)), 1e-4f);
            Assert.AreEqual(0f, NcoEducationRules.AutonomyFactor(null), 1e-4f);
        }

        // ===== 損耗の質・再建（“経験は急造できない”） =====

        [Test]
        public void Attrition_VeteranCorpsLosesMoreExperience()
        {
            float veteran = NcoEducationRules.AttritionExperienceLoss(new NcoCorps(1f, 0.8f), 0.1f);
            float green = NcoEducationRules.AttritionExperienceLoss(new NcoCorps(1f, 0.2f), 0.1f);
            Assert.AreEqual(0.14f, veteran, 1e-4f); // 0.1×(1+0.5×0.8)
            Assert.Greater(veteran, green);          // ベテランの壊滅は痛恨
        }

        [Test]
        public void Attrition_CannotExceedCurrentQuality()
        {
            float loss = NcoEducationRules.AttritionExperienceLoss(new NcoCorps(1f, 0.1f), 1f);
            Assert.AreEqual(0.1f, loss, 1e-4f); // 現在質までで頭打ち
            Assert.AreEqual(0f, NcoEducationRules.AttritionExperienceLoss(null, 0.5f), 1e-4f);
        }

        [Test]
        public void Dilution_RapidExpansionThinsTheCorps()
        {
            Assert.AreEqual(1f, NcoEducationRules.DilutionFactor(0f), 1e-4f);
            Assert.AreEqual(1f - NcoEducationRules.MaxExpansionDilution,
                            NcoEducationRules.DilutionFactor(1f), 1e-4f);
            Assert.Greater(NcoEducationRules.DilutionFactor(0f), NcoEducationRules.DilutionFactor(1f));
        }

        [Test]
        public void RebuildYears_TakesYearsAndIsZeroWhenAlreadyMet()
        {
            Assert.AreEqual(3.6f, NcoEducationRules.RebuildYears(0.2f, 0.8f), 1e-4f); // 0.6×6
            Assert.AreEqual(NcoEducationRules.BaseRebuildYears,
                            NcoEducationRules.RebuildYears(0f, 1f), 1e-4f);
            Assert.AreEqual(0f, NcoEducationRules.RebuildYears(0.8f, 0.5f), 1e-4f);   // 目標が現在以下
        }
    }
}
