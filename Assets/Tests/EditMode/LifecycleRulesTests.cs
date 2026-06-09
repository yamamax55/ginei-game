using NUnit.Framework;
using Ginei;
using LP = Ginei.LifecycleRules.LifespanParams;

namespace Ginei.Tests
{
    /// <summary>
    /// 人物ライフサイクル（LIFE-1 年齢／LIFE-2 死亡 #151/#152）を固定する：生年からの年齢導出（未設定/暦進行）、
    /// 年齢に応じた死亡率カーブの単調増加、寿命判定（roll で決定論的）、死亡フラグ。
    /// </summary>
    public class LifecycleRulesTests
    {
        [Test]
        public void Age_DerivedFromBirthYear()
        {
            Assert.AreEqual(40, LifecycleRules.Age(760, 800));
            Assert.AreEqual(0, LifecycleRules.Age(0, 800));   // 生年未設定＝加齢しない
            Assert.AreEqual(0, LifecycleRules.Age(810, 800)); // 暦巻き戻りは負にならない
        }

        [Test]
        public void Age_AdvancesWithCalendar()
        {
            var p = new Person(1, "p", Faction.帝国, PersonRole.軍人) { birthYear = 770 };
            var cal = new Calendar(800);
            Assert.AreEqual(30, LifecycleRules.Age(p, cal.currentYear));
            cal.Advance();
            Assert.AreEqual(31, LifecycleRules.Age(p, cal.currentYear));
        }

        [Test]
        public void AnnualMortality_RisesWithAge()
        {
            var prm = LP.Default;
            float young = LifecycleRules.AnnualMortality(30, prm);
            float onset = LifecycleRules.AnnualMortality(60, prm);
            float old = LifecycleRules.AnnualMortality(80, prm);
            Assert.AreEqual(0.005f, young, 1e-4f); // 壮年は基礎
            Assert.AreEqual(0.005f, onset, 1e-4f); // 閾値ちょうどはまだ基礎
            Assert.AreEqual(0.405f, old, 1e-4f);   // 60+20歳 → 0.005 + 20*0.02
            Assert.Greater(old, young);
        }

        [Test]
        public void ShouldDieOfAge_DeterministicByRoll()
        {
            var prm = LP.Default; // 80歳=年間0.405
            Assert.IsTrue(LifecycleRules.ShouldDieOfAge(80, roll: 0.1f, yearsPerTurn: 1, prm));   // roll<0.405
            Assert.IsFalse(LifecycleRules.ShouldDieOfAge(80, roll: 0.5f, yearsPerTurn: 1, prm));  // roll>0.405
        }

        [Test]
        public void ShouldDieOfAge_ScalesWithYearsPerTurn()
        {
            var prm = LP.Default; // 30歳=年間0.005
            // 5年/ターンなら 0.025 まで死亡域が広がる
            Assert.IsTrue(LifecycleRules.ShouldDieOfAge(30, roll: 0.02f, yearsPerTurn: 5, prm));
            Assert.IsFalse(LifecycleRules.ShouldDieOfAge(30, roll: 0.02f, yearsPerTurn: 1, prm));
        }

        [Test]
        public void Kill_SetsDeathYear_AndDeceased()
        {
            var p = new Person(1, "p", Faction.帝国, PersonRole.軍人);
            Assert.IsTrue(p.IsAvailable);
            Assert.IsTrue(LifecycleRules.Kill(p, 801));
            Assert.IsTrue(p.IsDeceased);
            Assert.IsFalse(p.IsAvailable);
            Assert.IsFalse(LifecycleRules.Kill(p, 802)); // 二度は死なない
        }
    }
}
