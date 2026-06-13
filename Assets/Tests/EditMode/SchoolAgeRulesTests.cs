using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 学校の入学/卒業年齢（史実ベース・単一窓口 SchoolAgeRules）の純ロジックを固定する：
    /// 学制の年齢チェーン整合／陸軍大学校の史実（現役将校が約28→31）／科挙の年齢無制限／
    /// 既存 *Rules.GraduationAge の委譲一致／軍学歴別の生年精緻化。
    /// </summary>
    public class SchoolAgeRulesTests
    {
        // ===== 一般教育チェーンの年齢チェーン整合（卒業＝次校の入学） =====

        [Test]
        public void EducationChain_AgesLinkUp()
        {
            Assert.AreEqual(6, SchoolAgeRules.GraduationAge(SchoolType.幼稚園));
            Assert.AreEqual(SchoolAgeRules.GraduationAge(SchoolType.幼稚園), SchoolAgeRules.EntryAge(SchoolType.小学校));   // 6
            Assert.AreEqual(SchoolAgeRules.GraduationAge(SchoolType.小学校), SchoolAgeRules.EntryAge(SchoolType.中学校));   // 12
            Assert.AreEqual(SchoolAgeRules.GraduationAge(SchoolType.中学校), SchoolAgeRules.EntryAge(SchoolType.高校));     // 15
            Assert.AreEqual(SchoolAgeRules.GraduationAge(SchoolType.中学校), SchoolAgeRules.EntryAge(SchoolType.高専));     // 15（中学から高専）
            Assert.AreEqual(SchoolAgeRules.GraduationAge(SchoolType.高校), SchoolAgeRules.EntryAge(SchoolType.大学));       // 18
            Assert.AreEqual(SchoolAgeRules.GraduationAge(SchoolType.高校), SchoolAgeRules.EntryAge(SchoolType.短大));       // 18
            Assert.AreEqual(SchoolAgeRules.GraduationAge(SchoolType.高校), SchoolAgeRules.EntryAge(SchoolType.専門学校));   // 18
        }

        [Test]
        public void Duration_IsGraduationMinusEntry()
        {
            Assert.AreEqual(6, SchoolAgeRules.DurationYears(SchoolType.小学校));   // 6→12
            Assert.AreEqual(5, SchoolAgeRules.DurationYears(SchoolType.高専));     // 15→20（5年制）
            Assert.AreEqual(4, SchoolAgeRules.DurationYears(SchoolType.大学));     // 18→22（学部4年）
            Assert.AreEqual(3, SchoolAgeRules.DurationYears(SchoolType.陸軍大学校)); // 28→31
        }

        // ===== 軍学校（史実） =====

        [Test]
        public void WarCollege_IsForMidCareerOfficers_NotYouth()
        {
            // 陸軍大学校＝現役将校が約28歳で入校・約31歳で卒業（士官学校22より年長）
            Assert.AreEqual(28, SchoolAgeRules.EntryAge(SchoolType.陸軍大学校));
            Assert.AreEqual(31, SchoolAgeRules.GraduationAge(SchoolType.陸軍大学校));
            Assert.Greater(SchoolAgeRules.GraduationAge(SchoolType.陸軍大学校),
                           SchoolAgeRules.GraduationAge(SchoolType.士官学校));
        }

        [Test]
        public void CadetSchool_EntersYoung()
        {
            Assert.AreEqual(13, SchoolAgeRules.EntryAge(SchoolType.幼年学校));
            Assert.AreEqual(16, SchoolAgeRules.GraduationAge(SchoolType.幼年学校));
        }

        // ===== 科挙＝年齢制限なし =====

        [Test]
        public void ImperialExam_HasNoAgeCap()
        {
            Assert.IsFalse(SchoolAgeRules.IsAgeCapped(SchoolType.科挙));
            Assert.IsTrue(SchoolAgeRules.IsAgeCapped(SchoolType.大学));
            Assert.AreEqual(30, SchoolAgeRules.GraduationAge(SchoolType.科挙)); // 進士登用の典型年齢
        }

        // ===== 軍学歴別の生年精緻化 =====

        [Test]
        public void GraduationAgeForDegree_HigherDegreeIsOlder()
        {
            Assert.AreEqual(31, SchoolAgeRules.GraduationAgeForDegree(MilitaryDegree.大学校卒));
            Assert.AreEqual(22, SchoolAgeRules.GraduationAgeForDegree(MilitaryDegree.士官学校卒));
            Assert.AreEqual(16, SchoolAgeRules.GraduationAgeForDegree(MilitaryDegree.幼年学校卒));
            Assert.AreEqual(16, SchoolAgeRules.GraduationAgeForDegree(MilitaryDegree.無資格)); // 退校＝若くして去る
            Assert.Greater(SchoolAgeRules.GraduationAgeForDegree(MilitaryDegree.大学校卒),
                           SchoolAgeRules.GraduationAgeForDegree(MilitaryDegree.士官学校卒));
        }

        // ===== 既存 *Rules.GraduationAge の委譲一致（単一窓口） =====

        [Test]
        public void ExistingConstants_DelegateToSchoolAgeRules()
        {
            Assert.AreEqual(SchoolAgeRules.GraduationAge(SchoolType.士官学校), OfficerAcademyRules.GraduationAge);
            Assert.AreEqual(SchoolAgeRules.GraduationAge(SchoolType.大学), UniversityRules.GraduationAge);
            Assert.AreEqual(SchoolAgeRules.GraduationAge(SchoolType.高専), TechnicalCollegeRules.GraduationAge);
            Assert.AreEqual(SchoolAgeRules.GraduationAge(SchoolType.短大), JuniorCollegeRules.GraduationAge);
            Assert.AreEqual(SchoolAgeRules.GraduationAge(SchoolType.専門学校), VocationalSchoolRules.GraduationAge);
        }

        [Test]
        public void TechnicalCollege_YoungerThanUniversity()
        {
            // 高専(20) < 大学(22)＝5年制で早く現場へ
            Assert.Less(SchoolAgeRules.GraduationAge(SchoolType.高専),
                        SchoolAgeRules.GraduationAge(SchoolType.大学));
        }
    }
}
