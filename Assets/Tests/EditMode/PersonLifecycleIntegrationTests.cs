using System.Collections.Generic;
using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 人物ライフサイクルが「生まれてから死ぬまで」一通り回ることを統合的に固定する（純ロジックの結合）：
    /// 任官（士官学校卒・現役）→ 大学校入校（学校配属＝艦隊配属不可）→ 卒業（大学校卒=参謀・恩賜の軍刀組）
    /// → 昇進 → 停年退役 → 老衰死。実ルール（SchoolAgeRules/SchoolPostingRules/WarCollegeCareerRules/
    /// RetirementRules/LifecycleRules）を年単位で回し、各段階に到達することを確認する（GalaxyView 非依存・CI 検証可能）。
    /// </summary>
    public class PersonLifecycleIntegrationTests
    {
        [Test]
        public void FullCycle_BirthToDeath_PassesEveryStage()
        {
            const int startYear = 800;
            var p = new Person(1, "テスト将校", Faction.帝国, PersonRole.軍人)
            {
                birthYear = startYear - 22,            // 任官時22歳
                militaryDegree = MilitaryDegree.士官学校卒,
                rankTier = 6,
                serviceStatus = ServiceStatus.現役,
            };
            var roster = new List<Person> { p };
            var retire = RetirementRules.RetireParams.Default;
            var life = LifecycleRules.LifespanParams.Default;

            bool enrolled = false, schoolPostedNoFleet = false, staffOfficer = false,
                 swordHonor = false, retired = false, died = false;
            int peakTier = p.rankTier;

            for (int year = startYear; year <= startYear + 80 && !died; year++)
            {
                int age = LifecycleRules.Age(p, year);

                // 退役（停年）：現役で停年に達したら退役へ
                if (p.serviceStatus == ServiceStatus.現役 &&
                    RetirementRules.ShouldRetireByAge(age, p.rankTier, retire))
                    p.serviceStatus = ServiceStatus.退役;

                // キャリア（入校→学校配属→卒業→昇進）
                WarCollegeCareerRules.TickYear(roster, year, _ => PromotionDoctrine.学閥主義);

                // 各段階の到達を観測
                if (SchoolPostingRules.IsEnrolled(p, year))
                {
                    enrolled = true;
                    if (!SchoolPostingRules.CanAssignToFleet(p, year)) schoolPostedNoFleet = true; // 在学中は艦隊配属不可
                }
                if (p.militaryDegree == MilitaryDegree.大学校卒) staffOfficer = true;
                if (WarCollegeCareerRules.HonorOf(p) == MilitaryHonor.恩賜の軍刀) swordHonor = true;
                if (p.serviceStatus == ServiceStatus.退役) retired = true;
                if (p.rankTier > peakTier) peakTier = p.rankTier;

                // 老衰死（高齢で確実に＝roll を下げる）
                float roll = age >= 70 ? 0f : 1f;
                if (LifecycleRules.ShouldDieOfAge(age, roll, 1, life))
                    { p.deathYear = year; died = true; }
            }

            Assert.IsTrue(enrolled, "大学校入学（在学）に到達しなかった");
            Assert.IsTrue(schoolPostedNoFleet, "学校配属中の艦隊配属不可が成立しなかった");
            Assert.IsTrue(staffOfficer, "大学校卒（参謀）に到達しなかった");
            Assert.IsTrue(swordHonor, "恩賜の軍刀組に到達しなかった");
            Assert.Greater(peakTier, 6, "昇進が起きなかった");
            Assert.IsTrue(retired, "退役に到達しなかった");
            Assert.IsTrue(died, "老衰死に到達しなかった");
            Assert.IsFalse(p.IsAvailable, "死亡後も就任可能のまま");
        }

        [Test]
        public void RetiredOfficer_IsExcludedFromCareerProgression()
        {
            // 退役者は大学校入校・昇進の対象外（現役→退役の一方向）
            var p = new Person(1, "退役者", Faction.帝国, PersonRole.軍人)
            {
                birthYear = 800 - 28, militaryDegree = MilitaryDegree.士官学校卒,
                rankTier = 6, serviceStatus = ServiceStatus.退役,
            };
            Assert.IsFalse(WarCollegeCareerRules.CanEnroll(p, 800));
            WarCollegeCareerRules.TickYear(new List<Person> { p }, 800, _ => PromotionDoctrine.学閥主義);
            Assert.AreEqual(6, p.rankTier);                 // 昇進しない
            Assert.AreEqual(0, p.schoolPostingUntilYear);   // 入校もしない
        }
    }
}
