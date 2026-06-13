using System;
using System.Collections.Generic;
using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 専門学校の卒業ロジック（#157 LIFE-7・純ロジック・唯一の窓口）。高校卒後2年で<b>実務specialist（職業人）</b>を養成する。
    /// 高専より学術色が薄く現場直結、短大の技術版＝<b>実務（計画/生産）寄りの中堅</b>（天井は <see cref="JuniorCollegeRules.MidStatCeil"/>）。
    /// 経歴は <see cref="CareerPipelineRules.Stamp"/>（有力者＝席次なし・資格で登用）。能力係数は <see cref="UniversityRules"/> を流用。test-first。
    /// </summary>
    public static class VocationalSchoolRules
    {
        public static readonly int GraduationAge = SchoolAgeRules.GraduationAge(SchoolType.専門学校); // 卒業年齢=20（高校卒後2年・出所＝SchoolAgeRules）

        public static int Intake(VocationalSchool s, float candidatePool)
        {
            if (s == null) return 0;
            int byPool = Mathf.FloorToInt(Mathf.Max(0f, candidatePool) * UniversityRules.CandidateFraction);
            return Mathf.Clamp(byPool, 0, Mathf.Max(0, s.capacity));
        }

        public static List<Person> GraduateCohort(VocationalSchool s, int year, int intake, int idStart, Func<int, float> roll)
            => GraduateCohort(s, year, intake, idStart, roll, SeniorityRules.SeniorityParams.Default);

        /// <summary>1学年を卒業させる：実務specialist（計画/生産寄りの中堅文民）を生成し、技術力順に等級を刻んで返す。</summary>
        public static List<Person> GraduateCohort(VocationalSchool s, int year, int intake, int idStart,
            Func<int, float> roll, SeniorityRules.SeniorityParams seniority)
        {
            var grads = new List<Person>();
            if (s == null) return grads;
            int n = Mathf.Max(0, intake);
            for (int i = 0; i < n; i++)
            {
                float r = roll != null ? Mathf.Clamp01(roll(i)) : 0.5f;
                float talent = Mathf.Clamp01(s.quality * UniversityRules.QualityWeight + r * (1f - UniversityRules.QualityWeight));
                int hi = JuniorCollegeRules.StatFor(talent);        // 計画/生産（実務・高い・中堅天井）
                int mid = JuniorCollegeRules.StatFor(talent * 0.7f); // 技術（やや低い）
                int low = JuniorCollegeRules.StatFor(talent * 0.45f);// 研究/文才（低い）
                var p = new Person(idStart + i, $"{s.name}{year}期{i + 1}", s.faction, PersonRole.文民);
                p.production = hi; p.planning = hi; p.engineering = mid; // 実務specialist
                p.research = low; p.operation = low; p.intelligence = low;
                p.birthYear = year - GraduationAge;
                grads.Add(p);
            }
            grads.Sort((x, y) => y.TechnicalAptitude.CompareTo(x.TechnicalAptitude));
            for (int i = 0; i < grads.Count; i++)
            {
                CareerPipelineRules.Stamp(grads[i], CareerTrack.有力者, s.schoolId, year, i + 1); // 有力者＝席次なし
                grads[i].rankTier = SeniorityRules.InitialTier(i + 1, seniority);
            }
            return grads;
        }
    }
}
