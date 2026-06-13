using System.Collections.Generic;
using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>勲章ルール：戦功→等級・価値・恩給倍率・名誉点（上限クランプ）。</summary>
    public class MedalRulesTests
    {
        [Test]
        public void GradeForMerit_Thresholds()
        {
            Assert.AreEqual(MedalGrade.一級, MedalRules.GradeForMerit(95f));
            Assert.AreEqual(MedalGrade.二級, MedalRules.GradeForMerit(70f));
            Assert.AreEqual(MedalGrade.二級, MedalRules.GradeForMerit(89f));
            Assert.AreEqual(MedalGrade.三級, MedalRules.GradeForMerit(50f));
            Assert.AreEqual(MedalGrade.四級, MedalRules.GradeForMerit(30f));
            Assert.AreEqual(MedalGrade.五級, MedalRules.GradeForMerit(10f));
        }

        [Test]
        public void GradeAndKindFactors_Ordering()
        {
            Assert.AreEqual(1.0f, MedalRules.GradeFactor(MedalGrade.一級), 1e-4f);
            Assert.AreEqual(0.32f, MedalRules.GradeFactor(MedalGrade.五級), 1e-4f);
            Assert.Greater(MedalRules.GradeFactor(MedalGrade.一級), MedalRules.GradeFactor(MedalGrade.三級));

            Assert.AreEqual(1.0f, MedalRules.KindFactor(MedalKind.勲功章), 1e-4f);
            Assert.AreEqual(0.4f, MedalRules.KindFactor(MedalKind.従軍章), 1e-4f);
            Assert.Greater(MedalRules.KindFactor(MedalKind.武功章), MedalRules.KindFactor(MedalKind.従軍章));
        }

        [Test]
        public void Value_GradeTimesKind()
        {
            Assert.AreEqual(1.0f, MedalRules.Value(new Decoration(MedalKind.勲功章, MedalGrade.一級)), 1e-4f);
            Assert.AreEqual(0.32f * 0.4f, MedalRules.Value(new Decoration(MedalKind.従軍章, MedalGrade.五級)), 1e-4f);
        }

        [Test]
        public void PensionFactor_RaisesWithMedals_Capped()
        {
            Assert.AreEqual(1.0f, MedalRules.PensionFactor(null), 1e-4f);                 // 無勲章＝従来
            var one = new List<Decoration> { new Decoration(MedalKind.勲功章, MedalGrade.一級) }; // 価値1.0
            Assert.AreEqual(1.1f, MedalRules.PensionFactor(one), 1e-4f);                   // +10%
            var many = new List<Decoration>();
            for (int i = 0; i < 6; i++) many.Add(new Decoration(MedalKind.勲功章, MedalGrade.一級)); // 価値6.0
            Assert.AreEqual(1f + MedalRules.MaxPensionBonus, MedalRules.PensionFactor(many), 1e-4f); // 上限+50%
        }

        [Test]
        public void Prestige_RaisesWithMedals_Capped()
        {
            Assert.AreEqual(0f, MedalRules.Prestige(null), 1e-4f);
            var one = new List<Decoration> { new Decoration(MedalKind.勲功章, MedalGrade.一級) };
            Assert.AreEqual(10f, MedalRules.Prestige(one), 1e-4f);                          // 価値1.0×10
            var many = new List<Decoration>();
            for (int i = 0; i < 6; i++) many.Add(new Decoration(MedalKind.勲功章, MedalGrade.一級));
            Assert.AreEqual(MedalRules.MaxPrestige, MedalRules.Prestige(many), 1e-4f);      // 上限50
        }

        [Test]
        public void Award_SetsGradeFromMerit()
        {
            Decoration d = MedalRules.Award(MedalKind.武功章, 95f, 800, "敵旗艦撃破");
            Assert.AreEqual(MedalKind.武功章, d.kind);
            Assert.AreEqual(MedalGrade.一級, d.grade);
            Assert.AreEqual(800, d.awardedYear);
        }
    }
}
