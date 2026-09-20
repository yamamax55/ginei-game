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

        /// <summary>選挙に必要な政治家が不足したとき、在職可能な文官から決定論で政界へ転身させる。</summary>
        public static List<Person> PromotePoliticalCandidates(
            IEnumerable<Person> people, Faction faction, int targetCount)
        {
            var promoted = new List<Person>();
            if (people == null || targetCount <= 0) return promoted;
            var candidates = new List<Person>();
            int current = 0;
            foreach (Person person in people)
            {
                if (person == null || person.faction != faction || !person.IsAvailable) continue;
                if (person.isPolitician) { current++; continue; }
                if (person.isSovereign || person.role != PersonRole.文民) continue;
                if (PersonVocationRules.VocationOf(person) != PersonVocation.文官) continue;
                candidates.Add(person);
            }
            int needed = System.Math.Max(0, targetCount - current);
            candidates.Sort((a, b) =>
            {
                int aScore = a.charisma * 2 + a.operation + a.intelligence;
                int bScore = b.charisma * 2 + b.operation + b.intelligence;
                int score = bScore.CompareTo(aScore);
                return score != 0 ? score : a.id.CompareTo(b.id);
            });
            for (int i = 0; i < needed && i < candidates.Count; i++)
            {
                candidates[i].isPolitician = true;
                promoted.Add(candidates[i]);
            }
            return promoted;
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
                int individualSeed = IndividualSeed(seed, person.id);
                ApplyGeneratedTraits(person, individualSeed);
                ApplyGeneratedChronology(person, kind, individualSeed);
                if (kind != PersonGenerationKind.初期シナリオ)
                    person.name = GenerateName(person.faction, person.sex, person.id, seed);
            }
        }

        /// <summary>生成イベントの再現seedを保ったまま、同じ期の人物ごとに個性を分ける。</summary>
        private static int IndividualSeed(int eventSeed, int personId)
        {
            unchecked
            {
                uint value = (uint)eventSeed;
                value ^= (uint)personId + 0x9e3779b9u + (value << 6) + (value >> 2);
                value ^= value >> 16;
                value *= 0x7feb352du;
                value ^= value >> 15;
                return (int)(value & 0x7fffffffu);
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

        public static string DescribeFamily(Person person, System.Func<int, Person> resolve)
        {
            if (person == null) return string.Empty;
            var parts = new List<string>();
            AppendRelative(parts, "父", person.fatherId, resolve);
            AppendRelative(parts, "母", person.motherId, resolve);
            AppendRelative(parts, "配偶者", person.spouseId, resolve);
            return parts.Count == 0 ? string.Empty : string.Join("／", parts);
        }

        public static string DescribeQualifications(Person person)
        {
            if (person == null) return string.Empty;
            var parts = new List<string>();
            if (person.militaryDegree != MilitaryDegree.無資格)
            {
                string military = "軍学歴 " + person.militaryDegree;
                if (person.hammockNumber > 0) military += $"（席次 {person.hammockNumber}）";
                parts.Add(military);
            }
            if (person.warCollegeRank > 0)
                parts.Add($"大学校席次 {person.warCollegeRank}");
            if (person.examDegree != ExamDegree.無資格)
            {
                string exam = "科挙 " + person.examDegree;
                if (person.examRank > 0) exam += $"（順位 {person.examRank}）";
                parts.Add(exam);
            }
            if (PersonVocationRules.VocationOf(person) == PersonVocation.技術者)
                parts.Add("専門 " + PersonVocationRules.TechnicalTitle(person));
            return parts.Count == 0 ? string.Empty : string.Join("／", parts);
        }

        /// <summary>人物動態画面向けに、存命人物の生成経路と技術系専門の内訳を集計する。</summary>
        public static string DescribeSupply(IEnumerable<Person> people, Faction faction)
        {
            var kinds = new Dictionary<PersonGenerationKind, int>();
            var specialties = new Dictionary<TechnicalSpecialty, int>();
            if (people != null)
                foreach (Person person in people)
                {
                    if (person == null || person.faction != faction || person.IsDeceased) continue;
                    kinds.TryGetValue(person.generationKind, out int count);
                    kinds[person.generationKind] = count + 1;
                    if (PersonVocationRules.VocationOf(person) == PersonVocation.技術者)
                    {
                        TechnicalSpecialty specialty = PersonVocationRules.TechnicalSpecialtyOf(person);
                        specialties.TryGetValue(specialty, out int technicalCount);
                        specialties[specialty] = technicalCount + 1;
                    }
                }

            var parts = new List<string>();
            foreach (PersonGenerationKind kind in System.Enum.GetValues(typeof(PersonGenerationKind)))
                if (kinds.TryGetValue(kind, out int count) && count > 0)
                    parts.Add(kind + " " + count);
            string text = parts.Count == 0 ? "生成経路なし" : string.Join("　", parts);
            parts.Clear();
            foreach (TechnicalSpecialty specialty in System.Enum.GetValues(typeof(TechnicalSpecialty)))
                if (specialties.TryGetValue(specialty, out int count) && count > 0)
                    parts.Add(specialty + " " + count);
            if (parts.Count > 0) text += "／技術系 " + string.Join("・", parts);
            return text;
        }

        /// <summary>人物名簿のID・生成イベント・年代順を読み取り専用で監査する。</summary>
        public static string DescribeIntegrity(IEnumerable<Person> people)
        {
            var ids = new HashSet<int>();
            var eventIds = new HashSet<string>();
            int duplicateIds = 0;
            int invalidIds = 0;
            int duplicateEvents = 0;
            int reversedChronology = 0;
            int mismatchedSeeds = 0;

            if (people != null)
                foreach (Person person in people)
                {
                    if (person == null) continue;
                    if (person.id <= 0) invalidIds++;
                    else if (!ids.Add(person.id)) duplicateIds++;

                    // 経歴不明の旧人物は空文字のまま許容し、重複イベントに数えない。
                    if (!string.IsNullOrEmpty(person.generationEventId))
                    {
                        if (!eventIds.Add(person.generationEventId)) duplicateEvents++;
                        if (person.generationSeed != StableSeed(person.generationEventId)) mismatchedSeeds++;
                    }

                    if (person.birthYear > 0 && person.graduationYear > 0
                        && person.graduationYear < person.birthYear)
                        reversedChronology++;
                }

            if (duplicateIds == 0 && invalidIds == 0 && duplicateEvents == 0
                && reversedChronology == 0 && mismatchedSeeds == 0)
                return "整合 OK";

            var parts = new List<string>();
            if (duplicateIds > 0) parts.Add("ID重複 " + duplicateIds);
            if (invalidIds > 0) parts.Add("不正ID " + invalidIds);
            if (duplicateEvents > 0) parts.Add("生成イベント重複 " + duplicateEvents);
            if (reversedChronology > 0) parts.Add("年代逆転 " + reversedChronology);
            if (mismatchedSeeds > 0) parts.Add("seed不整合 " + mismatchedSeeds);
            return string.Join("／", parts);
        }

        /// <summary>保存復元後も既存人物と衝突しない、次の正の人物IDを返す。</summary>
        public static int NextAvailablePersonId(IEnumerable<Person> people)
        {
            var used = new HashSet<int>();
            int max = 0;
            if (people != null)
                foreach (Person person in people)
                {
                    if (person == null || person.id <= 0) continue;
                    used.Add(person.id);
                    if (person.id > max) max = person.id;
                }

            if (max < int.MaxValue) return max + 1;

            // int 上限を含む壊れた旧セーブでも加算オーバーフローせず、空いている正のIDを探す。
            for (int candidate = 1; candidate < int.MaxValue; candidate++)
                if (!used.Contains(candidate)) return candidate;
            return -1;
        }

        private static void AppendRelative(
            List<string> parts, string relation, int personId, System.Func<int, Person> resolve)
        {
            if (personId < 0) return;
            Person relative = resolve != null ? resolve(personId) : null;
            parts.Add(relation + " " + (relative != null && !string.IsNullOrWhiteSpace(relative.name)
                ? relative.name : $"人物#{personId}"));
        }
    }
}
