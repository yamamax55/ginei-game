using System;
using System.Collections.Generic;
using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 士官学校の卒業ロジック（#155 LIFE-5・純ロジック・唯一の窓口）。徴募源（軍属 #96）と定員から輩出数を決め、
    /// 1学年ぶんの新任士官（<see cref="Person"/>＝軍人）を生成し、教育の質と素質で能力を、<b>軍才の序列で席次（首席=1）</b>を、
    /// 席次から<b>初期階級</b>（<see cref="SeniorityRules.InitialTier"/>）を刻む。経歴付与は <see cref="CareerPipelineRules.Stamp"/> へ委譲
    /// （学閥＝同窓/同期の温床）。「人口と国力が将官を支える」＝武の入口。新派閥/名簿は作らず既存へ供給する。test-first。
    /// </summary>
    public static class OfficerAcademyRules
    {
        // 卒業生の素能レンジと係数（マジックナンバー禁止＝const）。
        public const int StatFloor = 30;          // 凡庸な卒業生の下限
        public const int StatCeil = 75;           // 名門首席級の上限（元帥級の伸びしろは成長#537で）
        public const float QualityWeight = 0.5f;  // 才能＝質×これ＋素質roll×(1-これ)
        public const int GraduationAge = 22;      // 卒業時の年齢（生年逆算）
        public const float CadetFraction = 0.15f; // 徴募源(軍属)のうち候補生になれる割合（人口が将官を支える・抽象スケール）

        /// <summary>
        /// 入学・卒業できる人数＝定員と<b>徴募源（軍属 #96）が支えられる数</b>の小さい方。人口が乏しいと将官も出せない。
        /// </summary>
        public static int Intake(Academy a, float recruitablePool)
        {
            if (a == null) return 0;
            int byPool = Mathf.FloorToInt(Mathf.Max(0f, recruitablePool) * CadetFraction);
            return Mathf.Clamp(byPool, 0, Mathf.Max(0, a.capacity));
        }

        /// <summary>素質(0..1)→能力値。質と素質を合成して下限〜上限へ写す。</summary>
        public static int StatFor(float talent) => Mathf.RoundToInt(Mathf.Lerp(StatFloor, StatCeil, Mathf.Clamp01(talent)));

        /// <summary>
        /// 1学年を卒業させる（既定の席次パラメータ）。<paramref name="roll"/>(i)∈[0,1) で素質を決定論的にばらす。
        /// </summary>
        public static List<Person> GraduateCohort(Academy a, int graduationYear, int intake, int idStart, Func<int, float> roll)
            => GraduateCohort(a, graduationYear, intake, idStart, roll, SeniorityRules.SeniorityParams.Default);

        /// <summary>
        /// 1学年を卒業させる：intake 名の新任士官を生成し、能力・席次（軍才順・首席=1）・初期階級を刻んで返す。
        /// 能力＝質×素質。生年＝卒業年−<see cref="GraduationAge"/>。経歴は <see cref="CareerPipelineRules.Stamp"/> が刻む。
        /// </summary>
        public static List<Person> GraduateCohort(Academy a, int graduationYear, int intake, int idStart,
            Func<int, float> roll, SeniorityRules.SeniorityParams seniority)
        {
            var grads = new List<Person>();
            if (a == null) return grads;
            int n = Mathf.Max(0, intake);

            for (int i = 0; i < n; i++)
            {
                float r = roll != null ? Mathf.Clamp01(roll(i)) : 0.5f;
                float talent = Mathf.Clamp01(a.quality * QualityWeight + r * (1f - QualityWeight));
                var p = new Person(idStart + i, $"{a.name}{graduationYear}期{i + 1}", a.faction, PersonRole.軍人);
                int s = StatFor(talent);
                p.leadership = s; p.attack = s; p.defense = s; p.mobility = s;   // 軍才（戦闘系）
                p.operation = StatFor(talent * 0.6f); p.intelligence = StatFor(talent * 0.6f); // 文才は控えめ
                p.birthYear = graduationYear - GraduationAge;
                grads.Add(p);
            }

            // 軍才の序列で席次付け（首席=1＝最優秀）。同点は生成順で安定。
            grads.Sort((x, y) => y.MilitaryAptitude.CompareTo(x.MilitaryAptitude));
            for (int i = 0; i < grads.Count; i++)
            {
                int hammock = i + 1; // 1=首席
                CareerPipelineRules.Stamp(grads[i], CareerTrack.士官学校, a.schoolId, graduationYear, hammock);
                grads[i].rankTier = SeniorityRules.InitialTier(hammock, seniority); // 席次→初期階級（首席ほど上）
            }
            return grads;
        }
    }
}
