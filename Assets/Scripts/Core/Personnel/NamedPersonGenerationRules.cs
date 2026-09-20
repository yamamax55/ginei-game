using System.Collections.Generic;

namespace Ginei
{
    /// <summary>ネームド人物が生まれた正規経路。既存人物は不明のまま復元し、架空の経歴を補わない。</summary>
    public enum PersonGenerationKind
    {
        不明,
        初期シナリオ,
        士官学校卒業,
        科挙登用,
        大学卒業,
        高専卒業,
        短大卒業,
        専門学校卒業,
        出生,
        その他
    }

    /// <summary>生成イベントの識別・再現seed・重複検査を一つに集約する。</summary>
    public static class NamedPersonGenerationRules
    {
        public static string EventId(PersonGenerationKind kind, Faction faction, int sourceId, int year)
            => $"person:{(int)kind}:{(int)faction}:{sourceId}:{year}";

        /// <summary>実行環境に依存する string.GetHashCode を使わない固定FNV-1a。</summary>
        public static int StableSeed(string eventId)
        {
            unchecked
            {
                uint hash = 2166136261u;
                string value = eventId ?? string.Empty;
                for (int i = 0; i < value.Length; i++)
                {
                    hash ^= value[i];
                    hash *= 16777619u;
                }
                return (int)(hash & 0x7fffffff);
            }
        }

        public static bool ContainsEvent(IEnumerable<Person> people, string eventId)
        {
            if (people == null || string.IsNullOrEmpty(eventId)) return false;
            foreach (Person person in people)
                if (person != null && person.generationEventId == eventId) return true;
            return false;
        }

        public static void Stamp(
            IEnumerable<Person> people, PersonGenerationKind kind, string eventId, int seed)
        {
            if (people == null) return;
            foreach (Person person in people)
            {
                if (person == null) continue;
                person.generationKind = kind;
                person.generationEventId = eventId ?? string.Empty;
                person.generationSeed = seed;
                ApplyGeneratedTraits(person, seed);
                ApplyGeneratedChronology(person, kind, seed);
            }
        }

        /// <summary>卒業・登用時点から無理のない生年を補い、年齢と経歴の順序を確定する。</summary>
        public static void ApplyGeneratedChronology(Person person, PersonGenerationKind kind, int seed)
        {
            if (person == null || person.birthYear != 0 || person.graduationYear <= 0) return;
            var random = new System.Random(seed ^ unchecked((int)0x45d9f3b));
            int age;
            switch (kind)
            {
                case PersonGenerationKind.士官学校卒業: age = 21 + random.Next(4); break; // 21..24
                case PersonGenerationKind.科挙登用: age = 23 + random.Next(10); break;    // 23..32
                case PersonGenerationKind.大学卒業: age = 22 + random.Next(4); break;    // 22..25
                case PersonGenerationKind.高専卒業: age = 20; break;
                case PersonGenerationKind.短大卒業:
                case PersonGenerationKind.専門学校卒業: age = 20 + random.Next(3); break; // 20..22
                default: return;
            }
            person.birthYear = person.graduationYear - age;
        }

        /// <summary>既存の人物特性フィールドへ、生成seedから一貫した信条・出自・個性を与える。</summary>
        public static void ApplyGeneratedTraits(Person person, int seed)
        {
            if (person == null) return;
            var random = new System.Random(seed ^ unchecked((int)0x6d2b79f5));
            double creedRoll = random.NextDouble();
            if (person.creed == Creed.無関心)
            {
                if (person.faction == Faction.帝国)
                    person.creed = creedRoll < 0.35 ? Creed.帝政擁護
                        : creedRoll < 0.60 ? Creed.門閥主義
                        : creedRoll < 0.85 ? Creed.能力主義 : Creed.中道;
                else
                    person.creed = creedRoll < 0.45 ? Creed.共和主義
                        : creedRoll < 0.75 ? Creed.能力主義 : Creed.中道;
            }

            double originRoll = random.NextDouble();
            if (person.socialOrigin == SocialOrigin.平民)
            {
                if (person.faction == Faction.帝国)
                    person.socialOrigin = originRoll < 0.25 ? SocialOrigin.門閥貴族
                        : originRoll < 0.45 ? SocialOrigin.下級貴族
                        : originRoll < 0.70 ? SocialOrigin.軍人家系 : SocialOrigin.平民;
                else
                    person.socialOrigin = originRoll < 0.15 ? SocialOrigin.植民星
                        : originRoll < 0.35 ? SocialOrigin.軍人家系 : SocialOrigin.平民;
            }

            if (person.charisma == 50) person.charisma = 35 + random.Next(46);       // 35..80
            if (person.constitution == 50) person.constitution = 35 + random.Next(51); // 35..85
            if (person.hobby == Hobby.なし)
                person.hobby = (Hobby)(1 + random.Next(System.Enum.GetValues(typeof(Hobby)).Length - 1));
            if (person.vice == Vice.なし && random.NextDouble() < 0.30)
                person.vice = (Vice)(1 + random.Next(System.Enum.GetValues(typeof(Vice)).Length - 1));
        }

        /// <summary>人物名鑑向けの短い生成証跡。旧人物には存在しない経歴を補わない。</summary>
        public static string Describe(Person person)
        {
            if (person == null) return "生成経歴なし";
            if (person.generationKind == PersonGenerationKind.不明 || string.IsNullOrEmpty(person.generationEventId))
                return "開始前の生成経歴は不明";

            string text = person.generationKind.ToString();
            if (person.graduationYear > 0) text += $" SE{person.graduationYear}";
            if (person.graduationYear > 0 && person.birthYear > 0)
                text += $"（{person.graduationYear - person.birthYear}歳）";
            if (person.schoolId > 0) text += $" 学校#{person.schoolId}";
            text += $"／証跡 {person.generationEventId}／seed {person.generationSeed}";
            return text;
        }

        public static string DescribeTraits(Person person)
        {
            if (person == null) return "人物特性なし";
            return $"信条 {person.creed}／出自 {person.socialOrigin}／人望 {person.charisma}／体質 {person.constitution}"
                 + $"／趣味 {person.hobby}／悪癖 {person.vice}";
        }
    }
}
