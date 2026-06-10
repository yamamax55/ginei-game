using System.Collections.Generic;
using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>暦の年境界で回す加齢・老衰の統合（AnnualLifecycleRules・LIFE-2 #152 / TIME-6 #952）の EditMode テスト。</summary>
    public class AnnualLifecycleRulesTests
    {
        private static LifecycleRules.LifespanParams P => LifecycleRules.LifespanParams.Default; // 基礎0.5%/60歳から+2%

        private static Person Aged(int id, int birthYear)
            => new Person(id, "P" + id, Faction.帝国, PersonRole.軍人) { birthYear = birthYear };

        [Test]
        public void ProcessMortality_OldPerson_HighRoll_Survives()
        {
            // 90歳でも roll が死亡率より上なら生存（roll=1.0 は必ず生存）
            var roster = new List<Person> { Aged(1, 700) }; // SE790 で90歳
            var dead = AnnualLifecycleRules.ProcessMortality(roster, 790, 1, _ => 1f, P);
            Assert.AreEqual(0, dead.Count);
            Assert.IsFalse(roster[0].IsDeceased);
        }

        [Test]
        public void ProcessMortality_OldPerson_LowRoll_Dies_AndStampsDeathYear()
        {
            // 90歳・roll=0 は高死亡率を下回る＝死亡。没年が currentYear に立つ。
            var roster = new List<Person> { Aged(1, 700) };
            var dead = AnnualLifecycleRules.ProcessMortality(roster, 790, 1, _ => 0f, P);
            Assert.AreEqual(1, dead.Count);
            Assert.AreSame(roster[0], dead[0]);
            Assert.IsTrue(roster[0].IsDeceased);
            Assert.AreEqual(790, roster[0].deathYear);
        }

        [Test]
        public void ProcessMortality_YoungPerson_LowRoll_SurvivesUnlessExtreme()
        {
            // 30歳の基礎死亡率は0.5%。roll=0.004(<0.005) なら死ぬが、roll=0.01 なら生存。
            var young = new List<Person> { Aged(1, 760) }; // SE790 で30歳
            Assert.AreEqual(1, AnnualLifecycleRules.ProcessMortality(young, 790, 1, _ => 0.004f, P).Count);

            var young2 = new List<Person> { Aged(2, 760) };
            Assert.AreEqual(0, AnnualLifecycleRules.ProcessMortality(young2, 790, 1, _ => 0.01f, P).Count);
        }

        [Test]
        public void ProcessMortality_SkipsDeceasedAndUnsetBirthYear()
        {
            var alreadyDead = Aged(1, 700); alreadyDead.deathYear = 780; // 故人
            var noBirth = new Person(2, "X", Faction.同盟, PersonRole.軍人); // birthYear=0
            var roster = new List<Person> { alreadyDead, noBirth };
            var dead = AnnualLifecycleRules.ProcessMortality(roster, 790, 1, _ => 0f, P);
            Assert.AreEqual(0, dead.Count);                 // どちらも対象外
            Assert.AreEqual(780, alreadyDead.deathYear);    // 上書きされない
            Assert.IsFalse(noBirth.IsDeceased);             // 生年未設定は加齢しない
        }

        [Test]
        public void ProcessMortality_YearsPerTurn_RaisesMortality()
        {
            // 60歳の基礎死亡率は0.5%。yearsPerTurn=10 で約5%。roll=0.03 は1年では生存・10年換算では死亡。
            var oneYear = new List<Person> { Aged(1, 730) }; // SE790 で60歳
            Assert.AreEqual(0, AnnualLifecycleRules.ProcessMortality(oneYear, 790, 1, _ => 0.03f, P).Count);

            var tenYears = new List<Person> { Aged(2, 730) };
            Assert.AreEqual(1, AnnualLifecycleRules.ProcessMortality(tenYears, 790, 10, _ => 0.03f, P).Count);
        }

        [Test]
        public void ProcessMortality_NullRosterOrRoll_ReturnsEmpty_NoThrow()
        {
            Assert.AreEqual(0, AnnualLifecycleRules.ProcessMortality(null, 790, 1, _ => 0f, P).Count);
            var roster = new List<Person> { Aged(1, 700) };
            Assert.AreEqual(0, AnnualLifecycleRules.ProcessMortality(roster, 790, 1, null, P).Count);
            Assert.IsFalse(roster[0].IsDeceased);
        }

        [Test]
        public void ProcessMortality_DefaultParamsOverload_Works()
        {
            var roster = new List<Person> { Aged(1, 700) };
            var dead = AnnualLifecycleRules.ProcessMortality(roster, 790, 1, _ => 0f);
            Assert.AreEqual(1, dead.Count);
        }
    }
}
