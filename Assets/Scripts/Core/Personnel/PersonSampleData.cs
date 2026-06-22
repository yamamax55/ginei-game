using System.Collections.Generic;

namespace Ginei
{
    /// <summary>
    /// 指導者・文民・官僚の<b>サンプルデータ</b>（人物システムのテスト/見本表示用）。戦役（GalaxyView ロスター）が無くても
    /// 人事画面（<see cref="PersonObserverOverlay"/>）で群像を確認でき、人物稟議（<see cref="PersonRingiRules"/>）の
    /// テストにも使える。職分の振り分けは <see cref="PersonVocationRules.VocationOf"/> が決める（君主＝<see cref="Person.isSovereign"/>／
    /// 政治家＝<see cref="Person.isPolitician"/>／文官・技術者＝才の比）。id は戦役の生成人物と衝突しない高位帯（90000+）。
    /// 読み取り専用の見本（共有・非破壊で使う）。純データ（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class PersonSampleData
    {
        /// <summary>指導者（君主/元首・政治家）。</summary>
        public static readonly IReadOnlyList<Person> Leaders = new List<Person>
        {
            new Person(90001, "皇帝 フリードリヒ", Faction.帝国, PersonRole.文民)
            { isSovereign = true, rankTier = 12, leadership = 80, operation = 72, intelligence = 66 },
            new Person(90002, "評議会議長 アルブレヒト", Faction.同盟, PersonRole.文民)
            { isPolitician = true, rankTier = 10, leadership = 62, operation = 74, intelligence = 78 },
            new Person(90003, "国務委員 ヴァルター", Faction.同盟, PersonRole.文民)
            { isPolitician = true, rankTier = 8, leadership = 55, operation = 68, intelligence = 70 },
        };

        /// <summary>文民（文官・技術者）。</summary>
        public static readonly IReadOnlyList<Person> Civilians = new List<Person>
        {
            new Person(90011, "文官長 ヴェッセル", Faction.帝国, PersonRole.文民)
            { rankTier = 9, operation = 82, intelligence = 76, research = 20 },
            new Person(90012, "技術総監 ハイネ", Faction.同盟, PersonRole.文民)
            { rankTier = 8, operation = 40, intelligence = 45, research = 84, engineering = 78, planning = 60, production = 55 },
        };

        /// <summary>官僚（省庁の事務官＝文官の一種・中堅）。</summary>
        public static readonly IReadOnlyList<Person> Bureaucrats = new List<Person>
        {
            new Person(90021, "大蔵官吏 メルダース", Faction.帝国, PersonRole.文民)
            { rankTier = 7, operation = 64, intelligence = 52, research = 15 },
            new Person(90022, "民部事務官 リンザー", Faction.同盟, PersonRole.文民)
            { rankTier = 6, operation = 58, intelligence = 55 },
        };

        /// <summary>文民系の全サンプル（指導者＋文民＋官僚）＝人事画面の CivilianRoster 代替。</summary>
        public static List<Person> AllCivilian()
        {
            var list = new List<Person>(Leaders.Count + Civilians.Count + Bureaucrats.Count);
            list.AddRange(Leaders);
            list.AddRange(Civilians);
            list.AddRange(Bureaucrats);
            return list;
        }
    }
}
