using System.Collections.Generic;
using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    public class NamedPersonGenerationRulesTests
    {
        [Test]
        public void EventIdAndSeed_AreStableAndSeparateEvents()
        {
            string first = NamedPersonGenerationRules.EventId(PersonGenerationKind.大学卒業, Faction.同盟, 7, 804);
            string same = NamedPersonGenerationRules.EventId(PersonGenerationKind.大学卒業, Faction.同盟, 7, 804);
            string other = NamedPersonGenerationRules.EventId(PersonGenerationKind.大学卒業, Faction.同盟, 7, 805);

            Assert.AreEqual(first, same);
            Assert.AreEqual(NamedPersonGenerationRules.StableSeed(first), NamedPersonGenerationRules.StableSeed(same));
            Assert.AreNotEqual(first, other);
            Assert.AreNotEqual(NamedPersonGenerationRules.StableSeed(first), NamedPersonGenerationRules.StableSeed(other));
        }

        [Test]
        public void Stamp_RecordsProvenanceAndDetectsDuplicateEvent()
        {
            var people = new List<Person> { new Person(1, "卒業生", Faction.同盟, PersonRole.文民) };
            string eventId = NamedPersonGenerationRules.EventId(PersonGenerationKind.高専卒業, Faction.同盟, 3, 804);
            int seed = NamedPersonGenerationRules.StableSeed(eventId);

            NamedPersonGenerationRules.Stamp(people, PersonGenerationKind.高専卒業, eventId, seed);

            Assert.AreEqual(PersonGenerationKind.高専卒業, people[0].generationKind);
            Assert.AreEqual(eventId, people[0].generationEventId);
            Assert.AreEqual(seed, people[0].generationSeed);
            Assert.IsTrue(NamedPersonGenerationRules.ContainsEvent(people, eventId));
            Assert.IsFalse(NamedPersonGenerationRules.ContainsEvent(people, eventId + ":別"));
        }

        [Test]
        public void Stamp_SameCohortIsReproducibleButDoesNotCloneEveryPersonalityAndAge()
        {
            var first = new List<Person>();
            var replay = new List<Person>();
            for (int id = 10; id < 22; id++)
            {
                first.Add(new Person(id, "候補", Faction.同盟, PersonRole.文民)
                    { graduationYear = 804 });
                replay.Add(new Person(id, "候補", Faction.同盟, PersonRole.文民)
                    { graduationYear = 804 });
            }
            string eventId = NamedPersonGenerationRules.EventId(
                PersonGenerationKind.大学卒業, Faction.同盟, 7, 804);
            int seed = NamedPersonGenerationRules.StableSeed(eventId);

            NamedPersonGenerationRules.Stamp(first, PersonGenerationKind.大学卒業, eventId, seed);
            NamedPersonGenerationRules.Stamp(replay, PersonGenerationKind.大学卒業, eventId, seed);

            var personalities = new HashSet<string>();
            for (int i = 0; i < first.Count; i++)
            {
                Assert.AreEqual(first[i].creed, replay[i].creed);
                Assert.AreEqual(first[i].socialOrigin, replay[i].socialOrigin);
                Assert.AreEqual(first[i].charisma, replay[i].charisma);
                Assert.AreEqual(first[i].constitution, replay[i].constitution);
                Assert.AreEqual(first[i].hobby, replay[i].hobby);
                Assert.AreEqual(first[i].vice, replay[i].vice);
                Assert.AreEqual(first[i].birthYear, replay[i].birthYear);
                personalities.Add(first[i].creed + ":" + first[i].charisma + ":"
                    + first[i].constitution + ":" + first[i].hobby + ":" + first[i].vice);
            }
            Assert.Greater(personalities.Count, 1, "同じ卒業期の人物像が全員同一になっている");
        }

        [Test]
        public void CampaignPersonSave_PreservesGenerationEvidence_AndOldDataStaysUnknown()
        {
            var person = new Person(5, "技術者", Faction.同盟, PersonRole.文民)
            {
                generationKind = PersonGenerationKind.専門学校卒業,
                generationEventId = "person:7:1:9:804",
                generationSeed = 12345
            };

            Person restored = CampaignSerializer.PersonFromSave(CampaignSerializer.PersonToSave(person));
            Person old = CampaignSerializer.PersonFromSave(new PersonSave { id = 6, name = "旧人物" });

            Assert.AreEqual(person.generationKind, restored.generationKind);
            Assert.AreEqual(person.generationEventId, restored.generationEventId);
            Assert.AreEqual(person.generationSeed, restored.generationSeed);
            Assert.AreEqual(PersonGenerationKind.不明, old.generationKind);
            Assert.AreEqual(string.Empty, old.generationEventId);
            Assert.AreEqual(0, old.generationSeed);
            Assert.AreEqual(-1, old.spouseId);
            Assert.AreEqual(-1, old.motherId);
            Assert.AreEqual(-1, old.fatherId);
            Assert.AreEqual(-1, old.birthSystemId);
            Assert.AreEqual(-1, old.loyaltyTargetId);
            Assert.AreEqual(50, old.charisma);
            Assert.AreEqual(50, old.constitution);
            Assert.IsNotNull(old.hiddenTraits);
        }

        [Test]
        public void Describe_ShowsKnownEvidenceWithoutInventingOldHistory()
        {
            var generated = new Person(5, "技術者", Faction.同盟, PersonRole.文民)
            {
                schoolId = 7,
                graduationYear = 804,
                generationKind = PersonGenerationKind.大学卒業,
                generationEventId = "person:4:1:7:804",
                generationSeed = 123
            };

            string text = NamedPersonGenerationRules.Describe(generated);

            StringAssert.Contains("大学卒業", text);
            StringAssert.Contains("SE804", text);
            StringAssert.Contains("学校#7", text);
            StringAssert.Contains("seed 123", text);
            Assert.AreEqual("開始前の生成経歴は不明",
                NamedPersonGenerationRules.Describe(new Person(6, "旧人物", Faction.帝国, PersonRole.軍人)));
        }

        [Test]
        public void GeneratedTraits_AreStableBoundedAndUseExistingFields()
        {
            var first = new Person(1, "一人目", Faction.同盟, PersonRole.文民);
            var same = new Person(2, "二人目", Faction.同盟, PersonRole.文民);

            NamedPersonGenerationRules.ApplyGeneratedTraits(first, 12345);
            NamedPersonGenerationRules.ApplyGeneratedTraits(same, 12345);

            Assert.AreEqual(first.creed, same.creed);
            Assert.AreEqual(first.socialOrigin, same.socialOrigin);
            Assert.AreEqual(first.charisma, same.charisma);
            Assert.AreEqual(first.constitution, same.constitution);
            Assert.AreEqual(first.hobby, same.hobby);
            Assert.AreEqual(first.vice, same.vice);
            Assert.That(first.charisma, Is.InRange(35, 80));
            Assert.That(first.constitution, Is.InRange(35, 85));
            Assert.AreNotEqual(Hobby.なし, first.hobby);
            StringAssert.Contains("信条", NamedPersonGenerationRules.DescribeTraits(first));
        }

        [Test]
        public void GeneratedTraits_HaveVariationWithoutOverwritingExplicitValues()
        {
            var creeds = new HashSet<Creed>();
            bool hasVice = false, hasNoVice = false;
            for (int seed = 1; seed <= 100; seed++)
            {
                var person = new Person(seed, "候補", Faction.帝国, PersonRole.軍人);
                NamedPersonGenerationRules.ApplyGeneratedTraits(person, seed);
                creeds.Add(person.creed);
                hasVice |= person.vice != Vice.なし;
                hasNoVice |= person.vice == Vice.なし;
            }
            Assert.Greater(creeds.Count, 1);
            Assert.IsTrue(hasVice);
            Assert.IsTrue(hasNoVice);

            var explicitPerson = new Person(200, "明示人物", Faction.帝国, PersonRole.軍人)
            {
                creed = Creed.共和主義,
                socialOrigin = SocialOrigin.植民星,
                charisma = 91,
                constitution = 92,
                hobby = Hobby.歴史,
                vice = Vice.短気
            };
            NamedPersonGenerationRules.ApplyGeneratedTraits(explicitPerson, 9);
            Assert.AreEqual(Creed.共和主義, explicitPerson.creed);
            Assert.AreEqual(SocialOrigin.植民星, explicitPerson.socialOrigin);
            Assert.AreEqual(91, explicitPerson.charisma);
            Assert.AreEqual(92, explicitPerson.constitution);
            Assert.AreEqual(Hobby.歴史, explicitPerson.hobby);
            Assert.AreEqual(Vice.短気, explicitPerson.vice);
        }

        [TestCase(PersonGenerationKind.士官学校卒業, 21, 24)]
        [TestCase(PersonGenerationKind.科挙登用, 23, 32)]
        [TestCase(PersonGenerationKind.大学卒業, 22, 25)]
        [TestCase(PersonGenerationKind.高専卒業, 20, 20)]
        [TestCase(PersonGenerationKind.短大卒業, 20, 22)]
        [TestCase(PersonGenerationKind.専門学校卒業, 20, 22)]
        public void GeneratedChronology_UsesPlausibleGraduationAge(
            PersonGenerationKind kind, int minAge, int maxAge)
        {
            var person = new Person(1, "卒業者", Faction.同盟, PersonRole.文民)
            {
                graduationYear = 42
            };
            NamedPersonGenerationRules.ApplyGeneratedChronology(person, kind, 12345);
            int age = person.graduationYear - person.birthYear;

            Assert.That(age, Is.InRange(minAge, maxAge));
            StringAssert.Contains($"（{age}歳）", NamedPersonGenerationRules.Describe(new Person(2, "表示", Faction.同盟, PersonRole.文民)
            {
                generationKind = kind,
                generationEventId = "event",
                generationSeed = 1,
                graduationYear = person.graduationYear,
                birthYear = person.birthYear
            }));
        }

        [Test]
        public void GeneratedChronology_PreservesExplicitBirthYearAndIgnoresUnknownHistory()
        {
            var explicitPerson = new Person(1, "明示", Faction.帝国, PersonRole.軍人)
            {
                birthYear = 5,
                graduationYear = 30
            };
            NamedPersonGenerationRules.ApplyGeneratedChronology(
                explicitPerson, PersonGenerationKind.士官学校卒業, 9);
            Assert.AreEqual(5, explicitPerson.birthYear);

            var unknown = new Person(2, "不明", Faction.帝国, PersonRole.軍人)
            {
                graduationYear = 30
            };
            NamedPersonGenerationRules.ApplyGeneratedChronology(
                unknown, PersonGenerationKind.不明, 9);
            Assert.AreEqual(0, unknown.birthYear);
        }

        [Test]
        public void RemainingFactionSlots_IsIndependentAndReopensAfterDeath()
        {
            var people = new List<Person>
            {
                new Person(1, "帝国1", Faction.帝国, PersonRole.軍人),
                new Person(2, "帝国2", Faction.帝国, PersonRole.軍人),
                new Person(3, "同盟1", Faction.同盟, PersonRole.軍人),
                new Person(4, "同盟故人", Faction.同盟, PersonRole.軍人) { deathYear = 10 }
            };

            Assert.AreEqual(0, NamedPersonGenerationRules.RemainingFactionSlots(people, Faction.帝国, 4));
            Assert.AreEqual(1, NamedPersonGenerationRules.RemainingFactionSlots(people, Faction.同盟, 4));
            Assert.AreEqual(0, NamedPersonGenerationRules.RemainingFactionSlots(people, Faction.同盟, -1));
        }

        [Test]
        public void GeneratedNames_AreStableFactionSpecificAndPreserveScenarioNames()
        {
            string empire = NamedPersonGenerationRules.GenerateName(Faction.帝国, Sex.男性, 42, 123);
            string same = NamedPersonGenerationRules.GenerateName(Faction.帝国, Sex.男性, 42, 123);
            string alliance = NamedPersonGenerationRules.GenerateName(Faction.同盟, Sex.女性, 42, 123);
            Assert.AreEqual(empire, same);
            Assert.AreNotEqual(empire, alliance);
            StringAssert.Contains("・", empire);

            var generated = new Person(42, "大学804期1", Faction.同盟, PersonRole.文民) { sex = Sex.女性 };
            NamedPersonGenerationRules.Stamp(new[] { generated }, PersonGenerationKind.大学卒業, "event", 123);
            Assert.AreEqual(alliance, generated.name);

            var scenario = new Person(7, "既存名", Faction.帝国, PersonRole.軍人);
            NamedPersonGenerationRules.Stamp(new[] { scenario }, PersonGenerationKind.初期シナリオ, "scenario", 55);
            Assert.AreEqual("既存名", scenario.name);
        }

        [Test]
        public void FamilyNames_AreInheritedWithoutDuplicatingGivenName()
        {
            var parent = new Person(1, "アーデル・カール", Faction.帝国, PersonRole.軍人);
            string child = NamedPersonGenerationRules.GenerateFamilyName(
                parent, Faction.帝国, Sex.女性, 10, 99);
            StringAssert.StartsWith("アーデル・", child);
            Assert.AreEqual("アーデル", NamedPersonGenerationRules.FamilyNameOf(parent.name));
            Assert.AreEqual("ミッターマイアー", NamedPersonGenerationRules.FamilyNameOf("ミッターマイアー"));
        }

        [Test]
        public void FamilyDescription_ResolvesKnownPeopleAndKeepsMissingIdsVisible()
        {
            var father = new Person(1, "父名", Faction.同盟, PersonRole.軍人);
            var mother = new Person(2, "母名", Faction.同盟, PersonRole.軍人);
            var child = new Person(3, "子名", Faction.同盟, PersonRole.軍人)
            {
                fatherId = 1,
                motherId = 2,
                spouseId = 9
            };
            var people = new Dictionary<int, Person> { { 1, father }, { 2, mother } };
            string text = NamedPersonGenerationRules.DescribeFamily(
                child, id => people.TryGetValue(id, out Person found) ? found : null);

            StringAssert.Contains("父 父名", text);
            StringAssert.Contains("母 母名", text);
            StringAssert.Contains("配偶者 人物#9", text);
            Assert.AreEqual(string.Empty, NamedPersonGenerationRules.DescribeFamily(
                new Person(4, "単身", Faction.同盟, PersonRole.文民), null));
        }

        [Test]
        public void QualificationDescription_UsesEstablishedEducationAndSpecialty()
        {
            var officer = new Person(1, "士官", Faction.帝国, PersonRole.軍人)
            {
                militaryDegree = MilitaryDegree.大学校卒,
                hammockNumber = 2,
                warCollegeRank = 1
            };
            string military = NamedPersonGenerationRules.DescribeQualifications(officer);
            StringAssert.Contains("軍学歴 大学校卒（席次 2）", military);
            StringAssert.Contains("大学校席次 1", military);

            var official = new Person(2, "文官", Faction.帝国, PersonRole.文民)
            {
                examDegree = ExamDegree.進士,
                examRank = 3,
                operation = 80,
                intelligence = 80
            };
            StringAssert.Contains("科挙 進士（順位 3）",
                NamedPersonGenerationRules.DescribeQualifications(official));

            var scientist = new Person(3, "研究者", Faction.同盟, PersonRole.文民)
            {
                research = 90,
                planning = 80,
                engineering = 40,
                production = 30
            };
            StringAssert.Contains("専門 科学者",
                NamedPersonGenerationRules.DescribeQualifications(scientist));
            Assert.AreEqual(string.Empty, NamedPersonGenerationRules.DescribeQualifications(
                new Person(4, "無資格", Faction.同盟, PersonRole.文民)));
        }

        [Test]
        public void PoliticalCandidateSupply_PromotesBestEligibleCivilOfficialsOnly()
        {
            var existing = new Person(1, "現職", Faction.同盟, PersonRole.文民) { isPolitician = true };
            var best = new Person(2, "有望", Faction.同盟, PersonRole.文民)
                { charisma = 80, operation = 70, intelligence = 70 };
            var second = new Person(3, "次点", Faction.同盟, PersonRole.文民)
                { charisma = 60, operation = 60, intelligence = 60 };
            var engineer = new Person(4, "技術者", Faction.同盟, PersonRole.文民)
                { charisma = 99, research = 90, engineering = 90, planning = 90, production = 90 };
            var soldier = new Person(5, "軍人", Faction.同盟, PersonRole.軍人) { charisma = 99 };
            var foreign = new Person(6, "帝国文官", Faction.帝国, PersonRole.文民) { charisma = 99 };
            var dead = new Person(7, "故人", Faction.同盟, PersonRole.文民)
                { charisma = 99, deathYear = 10 };
            var people = new List<Person> { existing, second, engineer, soldier, foreign, dead, best };

            List<Person> promoted = NamedPersonGenerationRules.PromotePoliticalCandidates(
                people, Faction.同盟, 3);

            Assert.AreEqual(2, promoted.Count);
            Assert.AreSame(best, promoted[0]);
            Assert.AreSame(second, promoted[1]);
            Assert.IsTrue(best.isPolitician);
            Assert.IsTrue(second.isPolitician);
            Assert.IsFalse(engineer.isPolitician);
            Assert.IsFalse(soldier.isPolitician);
            Assert.IsFalse(foreign.isPolitician);
            Assert.IsFalse(dead.isPolitician);
            Assert.AreEqual(0, NamedPersonGenerationRules.PromotePoliticalCandidates(
                people, Faction.同盟, 3).Count, "充足後に同じ処理で重複転身している");
        }

        [Test]
        public void SupplyDescription_CountsLivingGenerationPathsAndTechnicalSpecialties()
        {
            var people = new List<Person>
            {
                new Person(1, "初期", Faction.同盟, PersonRole.軍人)
                    { generationKind = PersonGenerationKind.初期シナリオ },
                new Person(2, "研究", Faction.同盟, PersonRole.文民)
                {
                    generationKind = PersonGenerationKind.大学卒業,
                    research = 90, planning = 80, engineering = 40, production = 30
                },
                new Person(3, "技術", Faction.同盟, PersonRole.文民)
                {
                    generationKind = PersonGenerationKind.高専卒業,
                    research = 40, planning = 30, engineering = 90, production = 80
                },
                new Person(4, "故人", Faction.同盟, PersonRole.文民)
                    { generationKind = PersonGenerationKind.大学卒業, deathYear = 10 },
                new Person(5, "帝国", Faction.帝国, PersonRole.軍人)
                    { generationKind = PersonGenerationKind.初期シナリオ }
            };

            string text = NamedPersonGenerationRules.DescribeSupply(people, Faction.同盟);
            StringAssert.Contains("初期シナリオ 1", text);
            StringAssert.Contains("大学卒業 1", text);
            StringAssert.Contains("高専卒業 1", text);
            StringAssert.Contains("科学者 1", text);
            StringAssert.Contains("技術者 1", text);
        }

        [Test]
        public void IntegrityDescription_DetectsIdEventAndChronologyProblems_WithoutTreatingUnknownHistoryAsDuplicate()
        {
            var people = new List<Person>
            {
                new Person(1, "正常", Faction.同盟, PersonRole.軍人)
                {
                    birthYear = 780, graduationYear = 802,
                    generationEventId = "academy:1:802"
                },
                new Person(1, "ID重複", Faction.同盟, PersonRole.文民)
                {
                    birthYear = 790, graduationYear = 805,
                    generationEventId = "university:1:805"
                },
                new Person(3, "イベント重複", Faction.帝国, PersonRole.文民)
                {
                    birthYear = 780, graduationYear = 805,
                    generationEventId = "university:1:805"
                },
                new Person(0, "不正ID", Faction.帝国, PersonRole.文民),
                new Person(5, "年代逆転", Faction.帝国, PersonRole.文民)
                {
                    birthYear = 810, graduationYear = 805
                },
                new Person(6, "旧人物A", Faction.同盟, PersonRole.文民),
                new Person(7, "旧人物B", Faction.同盟, PersonRole.文民),
                null
            };
            people[0].generationSeed = NamedPersonGenerationRules.StableSeed(people[0].generationEventId);
            people[1].generationSeed = NamedPersonGenerationRules.StableSeed(people[1].generationEventId);

            string text = NamedPersonGenerationRules.DescribeIntegrity(people);

            StringAssert.Contains("ID重複 1", text);
            StringAssert.Contains("不正ID 1", text);
            StringAssert.Contains("生成イベント重複 1", text);
            StringAssert.Contains("年代逆転 1", text);
            StringAssert.Contains("seed不整合 1", text);
            StringAssert.DoesNotContain("生成イベント重複 3", text,
                "経歴不明の空イベントIDを重複として数えている");
            Assert.AreEqual("整合 OK", NamedPersonGenerationRules.DescribeIntegrity(null));
            Assert.AreEqual("整合 OK", NamedPersonGenerationRules.DescribeIntegrity(new[] { people[0] }));
        }

        [Test]
        public void NextAvailablePersonId_SurvivesLargeSaveRoundTrip_AndHandlesMaximumId()
        {
            var people = new List<Person>();
            for (int id = 1; id <= 10000; id++)
                people.Add(new Person(id, "人物" + id, Faction.同盟, PersonRole.文民));

            var save = new CampaignSaveData();
            CampaignSerializer.WritePeople(save, people);
            List<Person> restored = CampaignSerializer.ReadPeople(save);

            Assert.AreEqual(10001, NamedPersonGenerationRules.NextAvailablePersonId(restored));
            Assert.AreEqual(1, NamedPersonGenerationRules.NextAvailablePersonId(null));
            Assert.AreEqual(3, NamedPersonGenerationRules.NextAvailablePersonId(new[]
            {
                new Person(int.MaxValue, "上限", Faction.帝国, PersonRole.軍人),
                new Person(1, "一", Faction.帝国, PersonRole.軍人),
                new Person(2, "二", Faction.帝国, PersonRole.軍人)
            }));
        }
    }
}
