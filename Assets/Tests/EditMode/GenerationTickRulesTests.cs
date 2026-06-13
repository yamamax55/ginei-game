using System.Collections.Generic;
using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 世代交代の年次 Tick（結婚・出産の配線オーケストレータ）を固定する：成年の男女が縁組みし、夫婦が子をなして名簿に加わる。
    /// 名簿上限で出産が打ち切られる（終盤ラグ回避）。婚姻/受胎/遺伝の数値は委譲先（優生学NGはそちらで担保）。
    /// </summary>
    public class GenerationTickRulesTests
    {
        const int Year = 800;

        static Person Man(int id, Faction fac = Faction.同盟)
            => new Person(id, "男" + id, fac, PersonRole.軍人)
            { sex = Sex.男性, birthYear = Year - 30, leadership = 50, attack = 50, defense = 50, mobility = 50, operation = 50, intelligence = 50 };

        static Person Woman(int id, Faction fac = Faction.同盟)
            => new Person(id, "女" + id, fac, PersonRole.軍人)
            { sex = Sex.女性, birthYear = Year - 25, leadership = 50, attack = 50, defense = 50, mobility = 50, operation = 50, intelligence = 50 };

        [Test]
        public void TickYear_MarriesThenBirthsChildIntoRoster()
        {
            var roster = new List<Person> { Man(1), Woman(2) };
            int seq = 100;
            // roll 順：結婚確率(女2)→受胎確率→[Conceive: 性別(1)＋能力10＋特性2＋劣性2=15]
            var q = new Queue<float>();
            q.Enqueue(0.1f); // 結婚（<0.4 で成立）
            q.Enqueue(0.1f); // 受胎（<0.30 で成立）
            for (int k = 0; k < 16; k++) q.Enqueue(0.5f); // 性別 + Conceive 15
            System.Func<float> roll = () => q.Dequeue();

            var res = GenerationTickRules.TickYear(roster, Year, () => seq++, roll,
                new GenerationTickRules.GenerationParams(0.4f, 50),
                ChildbirthRules.FertilityParams.Default, HeredityRules.HeredityParams.Default);

            Assert.AreEqual(1, res.marriages);
            Assert.AreEqual(1, res.births);
            Assert.AreEqual(3, roster.Count);

            // 夫婦が結ばれている
            Assert.AreEqual(2, roster[0].spouseId);
            Assert.AreEqual(1, roster[1].spouseId);

            // 子が名簿に加わり血縁が刻まれている（父系勢力）
            Person child = roster[2];
            Assert.AreEqual(100, child.id);
            Assert.AreEqual(1, child.fatherId);
            Assert.AreEqual(2, child.motherId);
            Assert.AreEqual(Faction.同盟, child.faction);
        }

        [Test]
        public void NoOppositeSex_NoMarriageNoBirth()
        {
            var roster = new List<Person> { Man(1), Man(2) }; // 男ばかり
            int seq = 100;
            var res = GenerationTickRules.TickYear(roster, Year, () => seq++, () => 0.1f,
                new GenerationTickRules.GenerationParams(1f, 50),
                ChildbirthRules.FertilityParams.Default, HeredityRules.HeredityParams.Default);
            Assert.AreEqual(0, res.marriages);
            Assert.AreEqual(0, res.births);
            Assert.AreEqual(2, roster.Count);
        }

        [Test]
        public void RosterCap_StopsBirths()
        {
            // 既婚夫婦だが名簿上限に達していれば出産しない（終盤ラグ回避）
            var m = Man(1); var w = Woman(2);
            PersonMarriageRules.Marry(m, w, Year);
            var roster = new List<Person> { m, w };
            int seq = 100;
            var res = GenerationTickRules.TickYear(roster, Year, () => seq++, () => 0.0f,
                new GenerationTickRules.GenerationParams(0.4f, 2), // 上限2＝既に満杯
                ChildbirthRules.FertilityParams.Default, HeredityRules.HeredityParams.Default);
            Assert.AreEqual(0, res.births);
            Assert.AreEqual(2, roster.Count);
        }

        [Test]
        public void DifferentFactions_DoNotMarry()
        {
            var roster = new List<Person> { Man(1, Faction.帝国), Woman(2, Faction.同盟) };
            int seq = 100;
            var res = GenerationTickRules.TickYear(roster, Year, () => seq++, () => 0.1f,
                new GenerationTickRules.GenerationParams(1f, 50),
                ChildbirthRules.FertilityParams.Default, HeredityRules.HeredityParams.Default);
            Assert.AreEqual(0, res.marriages); // 別勢力は縁組みしない（デモ方針）
        }

        [Test]
        public void NullSafe()
        {
            var res = GenerationTickRules.TickYear(null, Year, () => 1, () => 0.5f);
            Assert.AreEqual(0, res.marriages);
            Assert.AreEqual(0, res.births);
        }
    }
}
