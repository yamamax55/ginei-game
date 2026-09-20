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
    }
}
