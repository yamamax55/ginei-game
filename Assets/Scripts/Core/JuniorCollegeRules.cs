using System;
using System.Collections.Generic;
using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 短大の卒業ロジック（#156 LIFE-6・純ロジック・唯一の窓口）。高校卒後2年で<b>行政・事務の中堅文民</b>を輩出する。
    /// 4年制大学より<b>天井が低い</b>（<see cref="MidStatCeil"/>）＝進士級の上級官にはならないが官界の裾野。文才（運営/情報）寄り。
    /// 経歴は <see cref="CareerPipelineRules.Stamp"/>（有力者＝席次なし・登用は資格で）。単段の養成。能力係数は <see cref="UniversityRules"/> を流用。test-first。
    /// </summary>
    public static class JuniorCollegeRules
    {
        public const int StatFloor = 28;
        public const int MidStatCeil = 65;   // 2年制＝大学(78)より低い天井（中堅）
        public static readonly int GraduationAge = SchoolAgeRules.GraduationAge(SchoolType.短大); // 卒業年齢=20（高校卒後2年・出所＝SchoolAgeRules）

        public static int Intake(JuniorCollege c, float candidatePool)
        {
            if (c == null) return 0;
            int byPool = Mathf.FloorToInt(Mathf.Max(0f, candidatePool) * UniversityRules.CandidateFraction);
            return Mathf.Clamp(byPool, 0, Mathf.Max(0, c.capacity));
        }

        /// <summary>素質(0..1)→能力（中堅天井）。</summary>
        public static int StatFor(float talent) => Mathf.RoundToInt(Mathf.Lerp(StatFloor, MidStatCeil, Mathf.Clamp01(talent)));

        public static List<Person> GraduateCohort(JuniorCollege c, int year, int intake, int idStart, Func<int, float> roll)
            => GraduateCohort(c, year, intake, idStart, roll, SeniorityRules.SeniorityParams.Default);

        /// <summary>1学年を卒業させる：中堅文民（文才寄り）を生成し、文才順に等級を刻んで返す。</summary>
        public static List<Person> GraduateCohort(JuniorCollege c, int year, int intake, int idStart,
            Func<int, float> roll, SeniorityRules.SeniorityParams seniority)
        {
            var grads = new List<Person>();
            if (c == null) return grads;
            int n = Mathf.Max(0, intake);
            for (int i = 0; i < n; i++)
            {
                float r = roll != null ? Mathf.Clamp01(roll(i)) : 0.5f;
                float talent = Mathf.Clamp01(c.quality * UniversityRules.QualityWeight + r * (1f - UniversityRules.QualityWeight));
                int s = StatFor(talent);
                int mid = StatFor(talent * 0.7f);
                int low = StatFor(talent * 0.45f);
                var p = new Person(idStart + i, $"{c.name}{year}期{i + 1}", c.faction, PersonRole.文民);
                p.operation = s; p.intelligence = s; p.planning = mid; // 文才（行政/事務）寄り
                p.research = low; p.engineering = low; p.production = low;
                p.birthYear = year - GraduationAge;
                grads.Add(p);
            }
            grads.Sort((x, y) => y.CivilAptitude.CompareTo(x.CivilAptitude));
            for (int i = 0; i < grads.Count; i++)
            {
                CareerPipelineRules.Stamp(grads[i], CareerTrack.有力者, c.schoolId, year, i + 1); // 有力者＝席次なし
                grads[i].rankTier = SeniorityRules.InitialTier(i + 1, seniority);
            }
            return grads;
        }
    }
}
