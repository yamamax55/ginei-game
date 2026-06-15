using System.Collections.Generic;
using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 指導者・文民・官僚のサンプルデータを固定する：職分（<see cref="PersonVocationRules"/>）の分類が意図どおりで、
    /// id が一意（戦役人物と衝突しない高位帯）であること。人事画面の見本表示・人物稟議テストの土台。
    /// </summary>
    public class PersonSampleDataTests
    {
        [Test]
        public void Leaders_AreSovereignsOrPoliticians()
        {
            Assert.Greater(PersonSampleData.Leaders.Count, 0);
            bool hasSovereign = false, hasPolitician = false;
            foreach (var p in PersonSampleData.Leaders)
            {
                var v = PersonVocationRules.VocationOf(p);
                Assert.IsTrue(v == PersonVocation.君主 || v == PersonVocation.政治家, $"{p.name} は指導者でない（{v}）");
                if (v == PersonVocation.君主) hasSovereign = true;
                if (v == PersonVocation.政治家) hasPolitician = true;
            }
            Assert.IsTrue(hasSovereign, "君主が居ない");
            Assert.IsTrue(hasPolitician, "政治家が居ない");
        }

        [Test]
        public void Civilians_AreClerksOrTechnicians_IncludesTechnician()
        {
            Assert.Greater(PersonSampleData.Civilians.Count, 0);
            bool hasTechnician = false;
            foreach (var p in PersonSampleData.Civilians)
            {
                var v = PersonVocationRules.VocationOf(p);
                Assert.IsTrue(v == PersonVocation.文官 || v == PersonVocation.技術者, $"{p.name} は文民でない（{v}）");
                if (v == PersonVocation.技術者) hasTechnician = true;
            }
            Assert.IsTrue(hasTechnician, "技術者が居ない（才の比で技術者に分類されるはず）");
        }

        [Test]
        public void Bureaucrats_AreClerks()
        {
            Assert.Greater(PersonSampleData.Bureaucrats.Count, 0);
            foreach (var p in PersonSampleData.Bureaucrats)
                Assert.AreEqual(PersonVocation.文官, PersonVocationRules.VocationOf(p), $"{p.name} は官僚（文官）でない");
        }

        [Test]
        public void AllCivilian_HasUniqueIds_InHighRange()
        {
            var all = PersonSampleData.AllCivilian();
            Assert.AreEqual(PersonSampleData.Leaders.Count + PersonSampleData.Civilians.Count + PersonSampleData.Bureaucrats.Count, all.Count);
            var ids = new HashSet<int>();
            foreach (var p in all)
            {
                Assert.GreaterOrEqual(p.id, 90000, $"{p.name} の id が低位帯（戦役人物と衝突しうる）");
                Assert.IsTrue(ids.Add(p.id), $"id 重複: {p.id}");
                Assert.IsFalse(string.IsNullOrEmpty(p.name));
            }
        }

        [Test]
        public void SamplePerson_CanRaiseRingi_ToLeader()
        {
            // 官僚（事務官）が上司（指導者）へ稟議を挙げ、宛先の指導者だけが決裁できる
            ICharacter clerk = PersonSampleData.Bureaucrats[1];   // 民部事務官（同盟・tier4）
            ICharacter leader = PersonSampleData.Leaders[1];      // 評議会議長（同盟・tier8）
            var pet = PersonRingiRules.RaiseTo(0, "予算増額の上申", clerk, leader, "welfare.up");
            Assert.IsNotNull(pet, "文民の官僚は同勢力の上位者へ稟議を挙げられるはず");
            Assert.AreEqual(leader.Id, pet.addresseeId);
            pet.status = PetitionStatus.決裁待ち;
            Assert.IsTrue(PersonRingiRules.CanAdjudicate(leader, pet));
        }
    }
}
