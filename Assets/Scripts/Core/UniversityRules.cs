using System;
using System.Collections.Generic;
using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 大学の卒業ロジック（#156/#157 LIFE-6/7・純ロジック・唯一の窓口）。士官学校（<see cref="OfficerAcademyRules"/>）の文民版。
    /// 候補（官吏層 #110）と定員から輩出数を決め、1学年ぶんの新任<b>文民</b>（科挙＝文官／テクノクラート＝技術者）を生成し、
    /// 教育の質と素質で能力を、<b>才の序列で席次（首席=1）</b>を、席次から初期等級（<see cref="SeniorityRules.InitialTier"/>）を刻む。
    /// 経歴付与は <see cref="CareerPipelineRules.Stamp"/> へ委譲（科挙は <see cref="Person.examRank"/>＝文官閥の温床・テクノクラートは席次なし）。
    /// 新名簿は作らず既存ロスターへ供給する。test-first。
    /// </summary>
    public static class UniversityRules
    {
        public const int StatFloor = 30;          // 凡庸な卒業生の下限
        public const int StatCeil = 78;           // 名門首席級の上限
        public const float QualityWeight = 0.5f;  // 才能＝質×これ＋素質roll×(1-これ)
        public const int GraduationAge = 24;      // 卒業時の年齢（大学＝士官学校より少し上）
        public const float CandidateFraction = 0.15f; // 候補（官吏層）のうち入学できる割合

        /// <summary>入学・卒業できる人数＝定員と候補（官吏層 #110）が支えられる数の小さい方。</summary>
        public static int Intake(University u, float candidatePool)
        {
            if (u == null) return 0;
            int byPool = Mathf.FloorToInt(Mathf.Max(0f, candidatePool) * CandidateFraction);
            return Mathf.Clamp(byPool, 0, Mathf.Max(0, u.capacity));
        }

        /// <summary>素質(0..1)→能力値。</summary>
        public static int StatFor(float talent) => Mathf.RoundToInt(Mathf.Lerp(StatFloor, StatCeil, Mathf.Clamp01(talent)));

        /// <summary>1学年を卒業させる（既定の席次パラメータ）。</summary>
        public static List<Person> GraduateCohort(University u, int graduationYear, int intake, int idStart, Func<int, float> roll)
            => GraduateCohort(u, graduationYear, intake, idStart, roll, SeniorityRules.SeniorityParams.Default);

        /// <summary>
        /// 1学年を卒業させる：intake 名の新任文民を生成し、能力・席次（才順・首席=1）・初期等級を刻んで返す。
        /// 科挙は文才（運営/情報）を、テクノクラートは技才（研究/技術/計画/生産）を伸ばす。
        /// </summary>
        public static List<Person> GraduateCohort(University u, int graduationYear, int intake, int idStart,
            Func<int, float> roll, SeniorityRules.SeniorityParams seniority)
        {
            var grads = new List<Person>();
            if (u == null) return grads;
            int n = Mathf.Max(0, intake);
            bool technical = u.track == CareerTrack.テクノクラート;

            for (int i = 0; i < n; i++)
            {
                float r = roll != null ? Mathf.Clamp01(roll(i)) : 0.5f;
                float talent = Mathf.Clamp01(u.quality * QualityWeight + r * (1f - QualityWeight));
                int s = StatFor(talent);
                int sub = StatFor(talent * 0.6f);
                var p = new Person(idStart + i, $"{u.name}{graduationYear}期{i + 1}", u.faction, PersonRole.文民);
                if (technical)
                {
                    p.research = s; p.engineering = s; p.planning = s; p.production = s; // 技才
                    p.operation = sub; p.intelligence = sub;
                }
                else
                {
                    p.operation = s; p.intelligence = s; p.planning = s; // 文才（行政/外交/計画）
                    p.research = sub; p.engineering = sub; p.production = sub;
                }
                p.birthYear = graduationYear - GraduationAge;
                grads.Add(p);
            }

            // 才の序列で席次付け（首席=1）。技術系は技才、文官は文才で序列。
            grads.Sort((x, y) => technical
                ? y.TechnicalAptitude.CompareTo(x.TechnicalAptitude)
                : y.CivilAptitude.CompareTo(x.CivilAptitude));
            for (int i = 0; i < grads.Count; i++)
            {
                int rank = i + 1;
                CareerPipelineRules.Stamp(grads[i], u.track, u.schoolId, graduationYear, rank); // 科挙はexamRank、テクノクラートは席次なし
                grads[i].rankTier = SeniorityRules.InitialTier(rank, seniority);                 // 席次→初期等級
            }
            return grads;
        }
    }
}
