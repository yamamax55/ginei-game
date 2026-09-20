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
        private static readonly string[] ImperialFamilies =
            { "アーデル", "ファルク", "クライン", "ヴァイス", "ベルク", "ハルト", "ローゼン", "シュタイン", "ノルト", "ヴォルフ", "リヒター", "ブラウン" };
        private static readonly string[] ImperialMaleNames =
            { "エルンスト", "カール", "レオン", "オットー", "ユリウス", "マルク", "フリッツ", "アルノ", "テオ", "ルーカス", "コンラート", "ヴィクトル" };
        private static readonly string[] ImperialFemaleNames =
            { "エリーゼ", "クララ", "レナ", "マリア", "ゾフィー", "イリス", "アンナ", "ノラ", "テレーゼ", "ユリア", "フリーダ", "ヴィルマ" };
        private static readonly string[] AllianceFamilies =
            { "アレン", "チェン", "ハヤシ", "パーク", "シルバ", "カーン", "モリス", "リー", "サトウ", "ロペス", "クラーク", "キム" };
        private static readonly string[] AllianceMaleNames =
            { "アレックス", "ジュン", "ミン", "サミール", "レオ", "ケン", "ダニエル", "リュウ", "ノア", "ハル", "ミゲル", "イアン" };
        private static readonly string[] AllianceFemaleNames =
            { "ミラ", "ユナ", "リン", "サラ", "レイ", "マヤ", "エマ", "メイ", "ナオ", "ルナ", "ソフィア", "アイリ" };

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

        /// <summary>名簿上限を二勢力へ分け、死亡者を除いた現在人数から追加可能数を返す。</summary>
        public static int RemainingFactionSlots(IEnumerable<Person> people, Faction faction, int totalCapacity)
        {
            int capacity = faction == Faction.帝国
                ? System.Math.Max(0, totalCapacity) / 2
                : System.Math.Max(0, totalCapacity) - System.Math.Max(0, totalCapacity) / 2;
            int active = 0;
            if (people != null)
                foreach (Person person in people)
                    if (person != null && person.faction == faction && !person.IsDeceased) active++;
            return System.Math.Max(0, capacity - active);
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
                if (kind != PersonGenerationKind.初期シナリオ)
                    person.name = GenerateName(person.faction, person.sex, person.id, seed);
            }
        }

        /// <summary>勢力・性別・人物ID・固定seedから再現可能な固有名を作る。</summary>
        public static string GenerateName(Faction faction, Sex sex, int personId, int seed)
        {
            string[] families = faction == Faction.帝国 ? ImperialFamilies : AllianceFamilies;
            string[] given = faction == Faction.帝国
                ? (sex == Sex.女性 ? ImperialFemaleNames : ImperialMaleNames)
                : (sex == Sex.女性 ? AllianceFemaleNames : AllianceMaleNames);
            int stableId = System.Math.Max(0, personId);
            int familyIndex = PositiveMod(stableId + seed, families.Length);
            int givenIndex = PositiveMod(stableId / families.Length + seed / families.Length, given.Length);
            return families[familyIndex] + "・" + given[givenIndex];
        }

        /// <summary>親の姓を受け継ぎ、固定seedから名を付ける。姓が取れない場合は通常の勢力名簿へ戻す。</summary>
        public static string GenerateFamilyName(Person parent, Faction faction, Sex sex, int personId, int seed)
        {
            string family = FamilyNameOf(parent != null ? parent.name : null);
            if (string.IsNullOrEmpty(family)) return GenerateName(faction, sex, personId, seed);
            string[] given = faction == Faction.帝国
                ? (sex == Sex.女性 ? ImperialFemaleNames : ImperialMaleNames)
                : (sex == Sex.女性 ? AllianceFemaleNames : AllianceMaleNames);
            int index = PositiveMod(personId / ImperialFamilies.Length + seed / ImperialFamilies.Length, given.Length);
            return family + "・" + given[index];
        }

        public static string FamilyNameOf(string fullName)
        {
            if (string.IsNullOrWhiteSpace(fullName)) return string.Empty;
            string value = fullName.Trim();
            int separator = value.IndexOf('・');
            if (separator < 0) separator = value.IndexOf(' ');
            return separator > 0 ? value.Substring(0, separator) : value;
        }

        private static int PositiveMod(int value, int modulo)
        {
            int result = value % modulo;
            return result < 0 ? result + modulo : result;
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
