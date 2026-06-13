using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 学校配属ゲート（#SCHOOL-AGE）の純ロジックを固定する：
    /// ネームドが在学中は「学校配属」で艦隊配属不可、卒業後に「艦隊配属可」。
    /// 年齢窓（SchoolAgeRules）版と卒業年版の双方／陸軍大学校に在学する現役将校も艦隊に出せない。
    /// </summary>
    public class SchoolPostingRulesTests
    {
        private static Person P(int birthYear, int graduationYear = 0, int deathYear = 0)
        {
            var p = new Person(1, "候補生", Faction.帝国, PersonRole.軍人);
            p.birthYear = birthYear;
            p.graduationYear = graduationYear;
            p.deathYear = deathYear;
            return p;
        }

        // ===== 年齢窓（SchoolAgeRules）版 =====

        [Test]
        public void Enrolled_DuringSchoolAgeWindow()
        {
            // 士官学校 [16,22)：18歳は在学中＝学校配属
            var cadet = P(birthYear: 782); // 800年に18歳
            Assert.IsTrue(SchoolPostingRules.IsEnrolled(cadet, 800, SchoolType.士官学校));
            Assert.AreEqual(MilitaryPosting.学校配属, SchoolPostingRules.PostingOf(cadet, 800, SchoolType.士官学校));
            Assert.IsFalse(SchoolPostingRules.CanAssignToFleet(cadet, 800, SchoolType.士官学校)); // 在学中は艦隊配属不可
        }

        [Test]
        public void Graduated_BecomesAssignable()
        {
            var grad = P(birthYear: 778); // 800年に22歳＝卒業
            Assert.IsFalse(SchoolPostingRules.IsEnrolled(grad, 800, SchoolType.士官学校));
            Assert.AreEqual(MilitaryPosting.艦隊配属可, SchoolPostingRules.PostingOf(grad, 800, SchoolType.士官学校));
            Assert.IsTrue(SchoolPostingRules.CanAssignToFleet(grad, 800, SchoolType.士官学校));
        }

        [Test]
        public void TooYoung_NotYetEnrolled()
        {
            var child = P(birthYear: 786); // 800年に14歳＝入学前
            Assert.IsFalse(SchoolPostingRules.IsEnrolled(child, 800, SchoolType.士官学校));
        }

        [Test]
        public void WarCollege_MidCareerOfficerIsLockedToSchool()
        {
            // 陸軍大学校 [28,31)：在学する現役将校（29歳）は艦隊に出せない
            var staffStudent = P(birthYear: 771); // 800年に29歳
            Assert.IsTrue(SchoolPostingRules.IsEnrolled(staffStudent, 800, SchoolType.陸軍大学校));
            Assert.IsFalse(SchoolPostingRules.CanAssignToFleet(staffStudent, 800, SchoolType.陸軍大学校));
            // まだ入校前（27歳）は在学でない
            Assert.IsFalse(SchoolPostingRules.IsEnrolled(P(birthYear: 773), 800, SchoolType.陸軍大学校));
        }

        [Test]
        public void UnknownBirthYear_IsAssignable_BackwardCompatible()
        {
            var p = P(birthYear: 0); // 生年未設定
            Assert.IsFalse(SchoolPostingRules.IsEnrolled(p, 800, SchoolType.士官学校));
        }

        // ===== 卒業年版 =====

        [Test]
        public void EnrolledByGraduationYear_FutureGraduationMeansStudent()
        {
            Assert.IsTrue(SchoolPostingRules.IsEnrolledByGraduationYear(805, 800));  // 卒業はまだ先＝在学
            Assert.IsFalse(SchoolPostingRules.IsEnrolledByGraduationYear(800, 800)); // 当年卒業＝もう学生でない
            Assert.IsFalse(SchoolPostingRules.IsEnrolledByGraduationYear(795, 800)); // 既卒
            Assert.IsFalse(SchoolPostingRules.IsEnrolledByGraduationYear(0, 800));   // 未設定
        }

        [Test]
        public void PersonGraduationYear_GatesAssignment()
        {
            var student = P(birthYear: 783, graduationYear: 805); // 800年時点で在学（卒業805）
            Assert.IsTrue(SchoolPostingRules.IsEnrolled(student, 800));
            Assert.AreEqual(MilitaryPosting.学校配属, SchoolPostingRules.PostingOf(student, 800));
            Assert.IsFalse(SchoolPostingRules.CanAssignToFleet(student, 800));

            var alumnus = P(birthYear: 778, graduationYear: 795); // 既卒
            Assert.IsTrue(SchoolPostingRules.CanAssignToFleet(alumnus, 800));
        }

        // ===== 就任可能性（生存・自由）と null =====

        [Test]
        public void DeceasedGraduate_CannotBeAssigned()
        {
            var dead = P(birthYear: 770, graduationYear: 792, deathYear: 799); // 既卒だが故人
            Assert.IsFalse(SchoolPostingRules.CanAssignToFleet(dead, 800));
        }

        [Test]
        public void Null_IsNotEnrolled_AndNotAssignable()
        {
            Assert.IsFalse(SchoolPostingRules.IsEnrolled((ICharacter)null, 800, SchoolType.士官学校));
            Assert.IsFalse(SchoolPostingRules.IsEnrolled((Person)null, 800));
            Assert.IsFalse(SchoolPostingRules.CanAssignToFleet((Person)null, 800));
            Assert.IsFalse(SchoolPostingRules.CanAssignToFleet((ICharacter)null, 800, SchoolType.士官学校));
        }
    }
}
