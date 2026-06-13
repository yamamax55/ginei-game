using System.Collections.Generic;
using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 陸軍大学校のエリート街道オーケストレータ（#SCHOOL-AGE 配線）の純ロジックを固定する：
    /// 入校適格／入校＝学校配属（艦隊配属不可）／卒業＝大学校卒+星の優遇+恩賜の軍刀組／
    /// 昇進優遇が doctrine で変わる（学閥主義＝恩賜が速く出世・実力主義＝俊英が追い越す）。
    /// </summary>
    public class WarCollegeCareerRulesTests
    {
        private const int Y = 800;

        private static Person Off(int id, Faction f, int apt, int birthYear,
            MilitaryDegree deg = MilitaryDegree.士官学校卒, int tier = 7, int spu = 0, int wcr = 0, int death = 0)
        {
            var p = new Person(id, "将校" + id, f, PersonRole.軍人);
            p.leadership = p.attack = p.defense = p.mobility = apt;
            p.birthYear = birthYear;
            p.militaryDegree = deg;
            p.rankTier = tier;
            p.schoolPostingUntilYear = spu;
            p.warCollegeRank = wcr;
            p.deathYear = death;
            return p;
        }

        // ===== 入校適格 =====

        [Test]
        public void CanEnroll_MidCareerOfficerOfRightAge()
        {
            Assert.IsTrue(WarCollegeCareerRules.CanEnroll(Off(1, Faction.帝国, 70, Y - 28), Y));   // 28歳・士官学校卒
            Assert.IsFalse(WarCollegeCareerRules.CanEnroll(Off(2, Faction.帝国, 70, Y - 28, deg: MilitaryDegree.大学校卒), Y)); // 既に参謀
            Assert.IsFalse(WarCollegeCareerRules.CanEnroll(Off(3, Faction.帝国, 70, Y - 40), Y));  // 40歳＝適齢外
            Assert.IsFalse(WarCollegeCareerRules.CanEnroll(Off(4, Faction.帝国, 70, Y - 22), Y));  // 22歳＝若すぎ
            Assert.IsFalse(WarCollegeCareerRules.CanEnroll(Off(5, Faction.帝国, 70, Y - 28, spu: Y + 2), Y)); // 在学中
            Assert.IsFalse(WarCollegeCareerRules.CanEnroll(Off(6, Faction.帝国, 70, Y - 28, death: Y - 1), Y)); // 故人
        }

        // ===== 入校＝学校配属（艦隊配属不可） =====

        [Test]
        public void Enroll_BecomesSchoolPosted_NotFleetAssignable()
        {
            var p = Off(1, Faction.帝国, 70, Y - 28);
            WarCollegeCareerRules.Enroll(p, Y);
            Assert.AreEqual(Y + WarCollegeCareerRules.WarCollegeDuration, p.schoolPostingUntilYear); // 修了は3年後
            Assert.IsFalse(SchoolPostingRules.CanAssignToFleet(p, Y));            // 在学中は艦隊配属不可
            Assert.IsTrue(WarCollegeCareerRules.IsGraduating(p, p.schoolPostingUntilYear));
            Assert.IsFalse(WarCollegeCareerRules.IsGraduating(p, p.schoolPostingUntilYear - 1));
        }

        // ===== 卒業＝大学校卒・星の優遇・恩賜の軍刀組 =====

        [Test]
        public void Graduate_BecomesStaffOfficer_WithTierBonusAndHonor()
        {
            var p = Off(1, Faction.帝国, 80, Y - 31, tier: 7, spu: Y);
            WarCollegeCareerRules.Graduate(p, 1); // 首席＝恩賜の軍刀組
            Assert.AreEqual(MilitaryDegree.大学校卒, p.militaryDegree);
            Assert.AreEqual(1, p.warCollegeRank);
            Assert.AreEqual(0, p.schoolPostingUntilYear);                         // 学校配属解除
            Assert.AreEqual(7 + MilitaryAcademyRules.WarCollegeTierBonus, p.rankTier); // 星の優遇 +2
            Assert.AreEqual(MilitaryHonor.恩賜の軍刀, WarCollegeCareerRules.HonorOf(p));
        }

        [Test]
        public void Graduate_TierBonusCappedAtEliteCeiling()
        {
            var p = Off(1, Faction.帝国, 80, Y - 31, tier: 8, spu: Y);
            WarCollegeCareerRules.Graduate(p, 1);
            Assert.AreEqual(WarCollegeCareerRules.EliteTierCeiling, p.rankTier); // 8+2=10 → 9 で頭打ち
        }

        // ===== 年次 Tick：卒業で軍才順に席次＝恩賜判定 =====

        [Test]
        public void TickYear_GraduatesAssignRankAndHonorByAptitude()
        {
            var top = Off(1, Faction.帝国, 90, Y - 31, spu: Y);
            var second = Off(2, Faction.帝国, 50, Y - 31, spu: Y);
            var roster = new List<Person> { second, top }; // 順不同
            WarCollegeCareerRules.TickYear(roster, Y, _ => PromotionDoctrine.学閥主義);

            Assert.AreEqual(MilitaryDegree.大学校卒, top.militaryDegree);
            Assert.AreEqual(1, top.warCollegeRank);                               // 軍才トップ＝首席
            Assert.AreEqual(2, second.warCollegeRank);
            Assert.AreEqual(MilitaryHonor.恩賜の軍刀, WarCollegeCareerRules.HonorOf(top));
            Assert.AreEqual(0, top.schoolPostingUntilYear);                      // 卒業＝学校配属解除
        }

        // ===== 年次 Tick：入校で学校配属＝艦隊配属不可（枠ぶんのみ） =====

        [Test]
        public void TickYear_EnrollsTopCandidate_LocksFromFleet()
        {
            var strong = Off(1, Faction.帝国, 80, Y - 28);
            var weak = Off(2, Faction.帝国, 60, Y - 28);
            var roster = new List<Person> { weak, strong };
            WarCollegeCareerRules.TickYear(roster, Y, _ => PromotionDoctrine.学閥主義);

            // 枠=1 なので軍才トップだけ入校＝学校配属＝艦隊配属不可
            Assert.IsTrue(SchoolPostingRules.IsEnrolled(strong, Y));
            Assert.IsFalse(SchoolPostingRules.CanAssignToFleet(strong, Y));
            Assert.AreEqual(0, weak.schoolPostingUntilYear); // 枠外は据え置き
        }

        // ===== 年次 Tick：昇進優遇が doctrine で反転 =====

        [Test]
        public void TickYear_Promotion_FactionalismFavorsSwordGroup()
        {
            // 低merit の恩賜(A) vs 高merit の隊付(B)。学閥主義では A が先に出世（史実の軍刀組）。
            var a = Off(1, Faction.帝国, 40, Y - 50, deg: MilitaryDegree.大学校卒, tier: 7, wcr: 1);
            var b = Off(2, Faction.帝国, 90, Y - 50, deg: MilitaryDegree.無資格, tier: 7);
            WarCollegeCareerRules.TickYear(new List<Person> { a, b }, Y, _ => PromotionDoctrine.学閥主義);
            Assert.AreEqual(8, a.rankTier); // 恩賜が昇進
            Assert.AreEqual(7, b.rankTier);
        }

        [Test]
        public void TickYear_Promotion_MeritocracyFavorsTalent()
        {
            var a = Off(1, Faction.帝国, 40, Y - 50, deg: MilitaryDegree.大学校卒, tier: 7, wcr: 1);
            var b = Off(2, Faction.帝国, 90, Y - 50, deg: MilitaryDegree.無資格, tier: 7);
            WarCollegeCareerRules.TickYear(new List<Person> { a, b }, Y, _ => PromotionDoctrine.実力主義);
            Assert.AreEqual(8, b.rankTier); // 俊英が昇進（米軍対比）
            Assert.AreEqual(7, a.rankTier);
        }
    }
}
