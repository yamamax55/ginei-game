using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>POP労働スキル（#2034）：技能ストック(SKILL-1)/難易度(SKILL-2)/教育(SKILL-3)/職業訓練校(SKILL-4)/リスキリング(SKILL-5)/OJT(SKILL-6)/需給接続(SKILL-7)。</summary>
    public class PopSkillSystemTests
    {
        // --- SKILL-1 技能ストック ---
        [Test]
        public void SkillStock_LevelsAndWeightedAverage()
        {
            var s = new SkillStock(new[] { 0.8f, 0.6f, 0f, 0f, 0f, 0f });
            Assert.AreEqual(0.8f, s.Level(Occupation.農民), 1e-4f);
            var w = new Workforce(new[] { 0.5f, 0.5f, 0f, 0f, 0f, 0f });
            Assert.AreEqual(0.7f, s.WeightedAverage(w), 1e-4f); // 0.8×0.5+0.6×0.5
        }

        // --- SKILL-2 習得難易度＝希少性（貴重なスキルほど高難度） ---
        [Test]
        public void Difficulty_RarerIsHarder()
        {
            Assert.AreEqual(0.10f, SkillDifficultyRules.DifficultyOf(OccupationCategory.運搬清掃包装), 1e-4f);
            Assert.AreEqual(0.80f, SkillDifficultyRules.DifficultyOf(OccupationCategory.専門技術), 1e-4f);
            Assert.AreEqual(0.95f, SkillDifficultyRules.DifficultyOf("623"), 1e-4f); // ワープ航法士＝最難関
            Assert.AreEqual(0.90f, SkillDifficultyRules.DifficultyOf("622"), 1e-4f); // 宇宙船操縦士
            Assert.AreEqual(0.30f, SkillDifficultyRules.DifficultyOf("531"), 1e-4f); // 製造＝大分類基準（生産工程）
            // 難易度の順序が直感に合う：清掃 < 製造 < 専門技術 < 航宙士
            Assert.Less(SkillDifficultyRules.DifficultyOf("701"), SkillDifficultyRules.DifficultyOf("531"));
            Assert.Less(SkillDifficultyRules.DifficultyOf("531"), SkillDifficultyRules.DifficultyOf("622"));
        }

        [Test]
        public void Difficulty_PrerequisiteAndPremium()
        {
            Assert.AreEqual(EducationLevel.専門高等, SkillDifficultyRules.Prerequisite(0.9f));
            Assert.AreEqual(EducationLevel.高等,     SkillDifficultyRules.Prerequisite(0.6f));
            Assert.AreEqual(EducationLevel.中等,     SkillDifficultyRules.Prerequisite(0.4f));
            Assert.AreEqual(EducationLevel.初等,     SkillDifficultyRules.Prerequisite(0.2f));
            Assert.AreEqual(60f, SkillDifficultyRules.AcquisitionMonths(1.0f, 60f), 1e-2f); // 最難関は長期
            Assert.AreEqual(1f,  SkillDifficultyRules.AcquisitionMonths(0.0f, 60f), 1e-2f);
            Assert.AreEqual(1.4f, SkillDifficultyRules.RarityPremium(0.8f, 0.5f), 1e-4f);   // 希少→賃金プレミアム
        }

        // --- SKILL-3 学校経路（前提教育ゲート） ---
        [Test]
        public void Education_BaselineAndCeiling()
        {
            Assert.AreEqual(0.72f, SkillEducationRules.BaselineSkill(0.9f, 0.8f), 1e-4f);
            Assert.IsTrue(SkillEducationRules.MeetsPrerequisite(EducationLevel.高等, EducationLevel.中等));
            Assert.IsFalse(SkillEducationRules.MeetsPrerequisite(EducationLevel.初等, EducationLevel.高等));
            // 前提を満たせばベースライン到達
            Assert.AreEqual(0.8f, SkillEducationRules.SkillCeiling(0.8f, EducationLevel.専門高等, 0.9f), 1e-4f);
            // 前提未達の高度スキルは頭打ち（教育格差が技能格差に）
            Assert.AreEqual(0.08f, SkillEducationRules.SkillCeiling(0.8f, EducationLevel.初等, 0.9f), 1e-4f);
        }

        // --- SKILL-4 職業訓練校の網羅的整備 ---
        [Test]
        public void VocationalTraining_TargetsAndYield()
        {
            Assert.AreEqual("622", VocationalTrainingRules.DefaultTargetMinor(TrainingInstitutionType.航宙士養成所));
            Assert.AreEqual("092", VocationalTrainingRules.DefaultTargetMinor(TrainingInstitutionType.テラフォーミング訓練所));
            Assert.AreEqual("431", VocationalTrainingRules.DefaultTargetMinor(TrainingInstitutionType.軍技能訓練));
            Assert.AreEqual(80f, VocationalTrainingRules.Intake(100, 80f), 1e-2f);
            Assert.AreEqual(80f, VocationalTrainingRules.TrainedSupply(100f, 0.8f), 1e-2f);
            Assert.AreEqual(0.68f, VocationalTrainingRules.SkillYield(0.8f, 0.3f), 1e-4f); // 易しい職は高い到達
            Assert.AreEqual(0.44f, VocationalTrainingRules.SkillYield(0.8f, 0.9f), 1e-4f); // 高難度は狭き門
            var pilotSchool = new VocationalTrainingSchool(1, TrainingInstitutionType.航宙士養成所, Faction.同盟, 50, 0.8f, "622");
            Assert.AreEqual(0.44f, VocationalTrainingRules.SkillYieldFor(pilotSchool), 1e-4f);
        }

        // --- SKILL-5 リスキリング ---
        [Test]
        public void Reskilling_CostSpeedGate()
        {
            // 同じ大分類内は安い・別大分類は高い
            Assert.AreEqual(40f, ReskillingRules.TransitionCost(OccupationCategory.生産工程, OccupationCategory.生産工程, 0.3f, 100f), 1e-2f);
            Assert.AreEqual(195f, ReskillingRules.TransitionCost(OccupationCategory.生産工程, OccupationCategory.専門技術, 0.8f, 100f), 1e-2f);
            // 年齢効率＝若いほど速い
            Assert.AreEqual(1f, ReskillingRules.AgeEfficiency(30, 35, 65), 1e-4f);
            Assert.AreEqual(0.5f, ReskillingRules.AgeEfficiency(50, 35, 65), 1e-4f);
            // 前提教育ゲート
            Assert.IsTrue(ReskillingRules.CanReskill(EducationLevel.専門高等, 0.9f));
            Assert.IsFalse(ReskillingRules.CanReskill(EducationLevel.初等, 0.9f));
            // 速度＝高難度は遅い／前提未達は0
            Assert.AreEqual(0.14f, ReskillingRules.TransitionSpeed(0.2f, 0.3f, 30, 35, 65, EducationLevel.高等), 1e-4f);
            Assert.AreEqual(0f, ReskillingRules.TransitionSpeed(0.2f, 0.9f, 30, 35, 65, EducationLevel.初等), 1e-4f);
        }

        // --- SKILL-6 OJT・現場習熟 ---
        [Test]
        public void Ojt_ExperienceCurveCappedByEducation()
        {
            Assert.AreEqual(0.3f, OnTheJobTrainingRules.OjtGain(0.2f, 0.8f, 0.5f, 0f), 1e-4f); // (0.8−0.2)×0.5
            Assert.AreEqual(0.36f, OnTheJobTrainingRules.OjtGain(0.2f, 0.8f, 0.5f, 0.2f), 1e-4f); // 企業内訓練で効率↑
            Assert.AreEqual(0f, OnTheJobTrainingRules.OjtGain(0.9f, 0.8f, 0.5f, 0f), 1e-4f);    // 上限超は0
            Assert.AreEqual(0.5f, OnTheJobTrainingRules.Advance(0.2f, 0.8f, 0.5f, 0f), 1e-4f);
            // 上限は教育/難易度で律速（前提未達の高度スキルは頭打ち）
            Assert.AreEqual(0.08f, OnTheJobTrainingRules.SkillCeiling(0.8f, EducationLevel.初等, 0.9f), 1e-4f);
        }

        // --- SKILL-7 技能の需給接続 ---
        [Test]
        public void SkillEffect_ProductivityWageMilitary()
        {
            Assert.AreEqual(1.3f, SkillEffectRules.ProductivityContribution(1f), 1e-4f); // 熟練で産出#93↑
            Assert.AreEqual(1.4f, SkillEffectRules.WagePremium(1f, 0.8f, 0.5f), 1e-4f);  // 希少スキル高熟練は高給#1969
            Assert.AreEqual(1f, SkillEffectRules.WagePremium(0f, 0.8f, 0.5f), 1e-4f);
            Assert.AreEqual(100f, SkillEffectRules.MilitaryQuality(1f, 100f), 1e-2f);    // 高度な航宙士で軍が強い#96
            Assert.AreEqual(60f, SkillEffectRules.MilitaryQuality(0f, 100f), 1e-2f);
            Assert.AreEqual(1.68f, SkillEffectRules.ScarcityWageFactor(1.2f, 1f, 0.8f, 0.5f), 1e-4f); // 人手不足×希少
        }
    }
}
