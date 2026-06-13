using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 人事の空席補充（VacancyRules・LIFE-2）と捕虜の処遇（CaptivityRules・LIFE-4）が実窓口で一通り回ることを固定する：
    /// 役職保持者の死亡/退役→後任補充（資格＝階級ゲート・適任不在は空席）／捕虜化→解放/登用(寝返り)/処断（死亡へ合流）。
    /// </summary>
    public class PersonnelTurnoverIntegrationTests
    {
        private const int Y = 800;

        private static Person Officer(int id, Faction f, int tier)
            => new Person(id, "将校" + id, f, PersonRole.軍人) { rankTier = tier, birthYear = Y - 40 };

        // ===== 後任補充（VacancyRules） =====

        [Test]
        public void Vacancy_FilledBySeniorEligibleSuccessor_OnDeath()
        {
            GovernmentRegistry.Clear();
            var office = new Office(1, "宇宙艦隊司令長官", OfficeScope.国家, OfficeDomain.軍事)
            { militaryOnly = true, requiredTier = 8 };

            var a = Officer(1, Faction.帝国, 9); // 現職（大将級以上）
            var b = Officer(2, Faction.帝国, 8); // 後任有資格
            var c = Officer(3, Faction.帝国, 5); // 階級不足＝資格なし
            Assert.IsTrue(GovernmentRegistry.TryAppoint(Faction.帝国, office, a));
            Assert.AreSame(a, GovernmentRegistry.GetHolder(office));

            a.deathYear = Y; // 死亡＝任に就けない
            bool filled = VacancyRules.FillVacancy(Faction.帝国, office, new ICharacter[] { a, b, c });
            Assert.IsTrue(filled);
            Assert.AreSame(b, GovernmentRegistry.GetHolder(office)); // 有資格の最先任が後任
        }

        [Test]
        public void Vacancy_StaysEmpty_WhenNoEligibleSuccessor()
        {
            GovernmentRegistry.Clear();
            var office = new Office(2, "司令長官", OfficeScope.国家, OfficeDomain.軍事)
            { militaryOnly = true, requiredTier = 8 };
            var a = Officer(1, Faction.同盟, 8);
            GovernmentRegistry.TryAppoint(Faction.同盟, office, a);
            a.deathYear = Y;

            var c = Officer(3, Faction.同盟, 5); // 階級不足のみ
            bool filled = VacancyRules.FillVacancy(Faction.同盟, office, new ICharacter[] { c });
            Assert.IsFalse(filled); // 適任不在＝空席のまま（無痛補充にしない）
            Assert.IsNull(GovernmentRegistry.GetHolder(office));
        }

        [Test]
        public void ClearDeparted_VacatesDeceasedHolders()
        {
            GovernmentRegistry.Clear();
            var office = new Office(3, "総参謀長", OfficeScope.国家, OfficeDomain.軍事) { militaryOnly = true };
            var a = Officer(1, Faction.帝国, 9);
            GovernmentRegistry.TryAppoint(Faction.帝国, office, a);
            a.deathYear = Y;
            var vacated = VacancyRules.ClearDeparted();
            Assert.AreEqual(1, vacated.Count);
            Assert.IsNull(GovernmentRegistry.GetHolder(office));
        }

        // ===== 捕虜の処遇（CaptivityRules） =====

        [Test]
        public void Captivity_Release_ReturnsToOriginalFaction()
        {
            var p = Officer(1, Faction.同盟, 7);
            Assert.IsTrue(CaptivityRules.Capture(p, Faction.帝国, Y));
            Assert.AreEqual(CaptiveStatus.捕虜, p.captiveStatus);
            Assert.AreEqual(Faction.帝国, p.heldBy);
            Assert.IsFalse(p.IsAvailable); // 捕虜は任に就けない

            Assert.IsTrue(CaptivityRules.Release(p));
            Assert.AreEqual(CaptiveStatus.自由, p.captiveStatus);
            Assert.AreEqual(Faction.同盟, p.faction); // 元勢力へ復帰
            Assert.IsTrue(p.IsAvailable);
        }

        [Test]
        public void Captivity_Recruit_DefectsToCaptor()
        {
            var p = Officer(1, Faction.同盟, 7);
            CaptivityRules.Capture(p, Faction.帝国, Y);
            Assert.IsTrue(CaptivityRules.Recruit(p, Faction.帝国));
            Assert.AreEqual(CaptiveStatus.自由, p.captiveStatus);
            Assert.AreEqual(Faction.帝国, p.faction); // 寝返り＝捕虜側へ
            Assert.IsTrue(p.IsAvailable);
        }

        [Test]
        public void Captivity_Execute_MergesIntoDeath()
        {
            var p = Officer(1, Faction.同盟, 7);
            CaptivityRules.Capture(p, Faction.帝国, Y);
            Assert.IsTrue(CaptivityRules.Execute(p, Y));
            Assert.AreEqual(CaptiveStatus.処断済, p.captiveStatus);
            Assert.IsTrue(p.IsDeceased);   // #152 死亡へ合流
            Assert.IsFalse(p.IsAvailable);
        }

        [Test]
        public void DefaultDisposition_VariesByRegime()
        {
            Assert.AreEqual(CaptiveDisposition.解放, CaptivityRules.DefaultDisposition(CivilianControlType.君主統帥));
            Assert.AreEqual(CaptiveDisposition.処断, CaptivityRules.DefaultDisposition(CivilianControlType.党軍));
        }
    }
}
