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
            }
        }

        /// <summary>人物名鑑向けの短い生成証跡。旧人物には存在しない経歴を補わない。</summary>
        public static string Describe(Person person)
        {
            if (person == null) return "生成経歴なし";
            if (person.generationKind == PersonGenerationKind.不明 || string.IsNullOrEmpty(person.generationEventId))
                return "開始前の生成経歴は不明";

            string text = person.generationKind.ToString();
            if (person.graduationYear > 0) text += $" SE{person.graduationYear}";
            if (person.schoolId > 0) text += $" 学校#{person.schoolId}";
            text += $"／証跡 {person.generationEventId}／seed {person.generationSeed}";
            return text;
        }
    }
}
