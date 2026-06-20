using System;
using System.Collections.Generic;

namespace Ginei
{
    /// <summary>
    /// 暦の年境界で回す加齢・老衰の統合（LIFE-2 #152・TIME-6 #952 の per-year フック・唯一の窓口）。人物ロスターを
    /// その年の年齢で老衰判定し、死亡者に没年を立てて<b>死亡者リストを返す</b>。死亡の波及（席の空席化・後任補充）は
    /// <see cref="VacancyRules"/>／<see cref="CommandStaffRules"/> が別途行う＝このエンジンは「誰が死んだか」だけを決める。
    /// 乱数は人物ごとの roll プロバイダ注入＝決定論的にテストできる。純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class AnnualLifecycleRules
    {
        /// <summary>
        /// ロスターを <paramref name="currentYear"/> 時点で1ターン（=<paramref name="yearsPerTurn"/> 年）ぶん老衰判定する。
        /// 生存かつ生年設定済み（birthYear&gt;0）の人物のみ対象。死亡者に没年(currentYear)を立て、死亡者を返す（ロスター順）。
        /// <paramref name="roll"/> は人物ごとに呼ぶ（0..1）。roster/roll が null なら空リスト。
        /// </summary>
        public static List<Person> ProcessMortality(IList<Person> roster, int currentYear, int yearsPerTurn,
            Func<Person, float> roll, LifecycleRules.LifespanParams p)
        {
            var deceased = new List<Person>();
            if (roster == null || roll == null) return deceased;

            int ypt = yearsPerTurn < 1 ? 1 : yearsPerTurn;
            for (int i = 0; i < roster.Count; i++)
            {
                Person person = roster[i];
                if (person == null || person.IsDeceased) continue;
                if (person.BirthYear <= 0) continue; // 生年未設定＝加齢しない（後方互換）

                int age = LifecycleRules.Age(person, currentYear);
                if (LifecycleRules.ShouldDieOfAge(age, roll(person), ypt, p))
                {
                    if (LifecycleRules.Kill(person, currentYear))
                        deceased.Add(person);
                }
            }
            return deceased;
        }

        /// <summary>既定寿命パラメータ版（<see cref="LifecycleRules.LifespanParams.Default"/>）。</summary>
        public static List<Person> ProcessMortality(IList<Person> roster, int currentYear, int yearsPerTurn,
            Func<Person, float> roll)
            => ProcessMortality(roster, currentYear, yearsPerTurn, roll, LifecycleRules.LifespanParams.Default);
    }
}
