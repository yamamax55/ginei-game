using System;
using System.Collections.Generic;
using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 高等専門学校（高専）の卒業ロジック（#157 LIFE-7・純ロジック・唯一の窓口）。中学校から直接入る5年制で、<b>実技重視の技術者</b>
    /// （テクノクラート文民）を若くして輩出する。大学テクノクラート（<see cref="UniversityRules"/>）より<b>実務寄り</b>＝技術/生産が高く
    /// 研究/計画はやや低く文才は低い。単段の養成（多段の選抜ではない）。能力係数は <see cref="UniversityRules"/> を流用（二重定義しない）。
    /// 経歴は <see cref="CareerPipelineRules.Stamp"/>（テクノクラート＝席次なし・実力本位）。test-first。
    /// </summary>
    public static class TechnicalCollegeRules
    {
        public const int GraduationAge = 20;       // 5年制＝大学卒(24)より若い
        public const float TechSecondary = 0.8f;   // 研究/計画は実技よりやや低い（実務志向）
        public const float CivilEmphasis = 0.5f;   // 文才（運営/情報）は低い

        /// <summary>入学・卒業できる人数＝定員と候補（工員層 #110）が支えられる数の小さい方。</summary>
        public static int Intake(TechnicalCollege c, float candidatePool)
        {
            if (c == null) return 0;
            int byPool = Mathf.FloorToInt(Mathf.Max(0f, candidatePool) * UniversityRules.CandidateFraction);
            return Mathf.Clamp(byPool, 0, Mathf.Max(0, c.capacity));
        }

        /// <summary>1学年を卒業させる（既定の席次パラメータ）。</summary>
        public static List<Person> GraduateCohort(TechnicalCollege c, int year, int intake, int idStart, Func<int, float> roll)
            => GraduateCohort(c, year, intake, idStart, roll, SeniorityRules.SeniorityParams.Default);

        /// <summary>
        /// 1学年を卒業させる：intake 名の技術者（文民・テクノクラート）を生成し、技術力順に等級を刻んで返す。
        /// 実技（技術/生産）が高く、研究/計画はやや低く、文才は低い＝現場の技術者。
        /// </summary>
        public static List<Person> GraduateCohort(TechnicalCollege c, int year, int intake, int idStart,
            Func<int, float> roll, SeniorityRules.SeniorityParams seniority)
        {
            var grads = new List<Person>();
            if (c == null) return grads;
            int n = Mathf.Max(0, intake);
            for (int i = 0; i < n; i++)
            {
                float r = roll != null ? Mathf.Clamp01(roll(i)) : 0.5f;
                float talent = Mathf.Clamp01(c.quality * UniversityRules.QualityWeight + r * (1f - UniversityRules.QualityWeight));
                int s = UniversityRules.StatFor(talent);                  // 技術/生産（実技・高い）
                int mid = UniversityRules.StatFor(talent * TechSecondary); // 研究/計画（やや低い）
                int low = UniversityRules.StatFor(talent * CivilEmphasis); // 文才（低い）
                var p = new Person(idStart + i, $"{c.name}{year}期{i + 1}", c.faction, PersonRole.文民);
                p.engineering = s; p.production = s;
                p.research = mid; p.planning = mid;
                p.operation = low; p.intelligence = low;
                p.birthYear = year - GraduationAge;
                grads.Add(p);
            }

            grads.Sort((x, y) => y.TechnicalAptitude.CompareTo(x.TechnicalAptitude)); // 技術力順（実力本位）
            for (int i = 0; i < grads.Count; i++)
            {
                CareerPipelineRules.Stamp(grads[i], CareerTrack.テクノクラート, c.schoolId, year, i + 1); // 席次は付かない（テクノクラート）
                grads[i].rankTier = SeniorityRules.InitialTier(i + 1, seniority);
            }
            return grads;
        }
    }
}
