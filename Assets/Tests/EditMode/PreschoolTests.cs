using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 幼稚園（<see cref="KindergartenRules"/>＝就学前教育・教育チェーンの最下根）と保育園（<see cref="NurseryRules"/>＝保育・出生/労働）を固定する：
    /// 幼稚園は最小の素質寄与で教育チェーンに乗る／保育園は教育でなく出生率・労働参加を上げる（役割の違い）。
    /// </summary>
    public class PreschoolTests
    {
        [Test]
        public void Kindergarten_IsEducationChainRoot_SmallestBonus()
        {
            Assert.AreEqual(0.7f, KindergartenRules.EducationFactor(0.7f), 1e-4f);
            Assert.AreEqual(1f, KindergartenRules.EducationFactor(2f), 1e-4f);
            Assert.AreEqual(KindergartenRules.MaxTalentBonus, KindergartenRules.TalentBonus(1f), 1e-4f);
            // 幼稚園の寄与は全段で最小（小学校0.05より小）
            Assert.Less(KindergartenRules.TalentBonus(1f), ElementarySchoolRules.TalentBonus(1f));
            // 教育チェーンに段階的に積める
            float q = KindergartenRules.EffectiveIntakeQuality(0.5f, 1f);
            Assert.AreEqual(0.5f + KindergartenRules.MaxTalentBonus, q, 1e-4f);
        }

        [Test]
        public void Nursery_BoostsFertilityAndLabor_NotEducation()
        {
            // 整備0＝中立（×1.0）、整備が上がるほど出生率・労働参加が上がる（単調・上限）
            Assert.AreEqual(1f, NurseryRules.FertilityFactor(0f), 1e-4f);
            Assert.AreEqual(1f + NurseryRules.MaxFertilityBoost, NurseryRules.FertilityFactor(1f), 1e-4f);
            Assert.Greater(NurseryRules.FertilityFactor(0.8f), NurseryRules.FertilityFactor(0.3f));

            Assert.AreEqual(1f, NurseryRules.LaborParticipationFactor(0f), 1e-4f);
            Assert.AreEqual(1f + NurseryRules.MaxLaborBoost, NurseryRules.LaborParticipationFactor(1f), 1e-4f);
            Assert.Greater(NurseryRules.LaborParticipationFactor(0.8f), NurseryRules.LaborParticipationFactor(0.3f));

            // 値域クランプ（負/超過）
            Assert.AreEqual(1f, NurseryRules.FertilityFactor(-1f), 1e-4f);
            Assert.AreEqual(1f + NurseryRules.MaxLaborBoost, NurseryRules.LaborParticipationFactor(3f), 1e-4f);
        }

        [Test]
        public void Nursery_FertilityScalesBirthRate()
        {
            // 出生率倍率を VitalRates の出生率へ掛けると出生が増える（GalaxyView 配線の核）
            var baseRates = DemographicsRules.VitalRates.Default;
            float boosted = baseRates.birthRate * NurseryRules.FertilityFactor(1f);
            Assert.Greater(boosted, baseRates.birthRate);
        }
    }
}
