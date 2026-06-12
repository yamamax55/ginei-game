using System;
using System.Collections.Generic;
using UnityEngine;

namespace Ginei
{
    /// <summary>軍学校の段階（#155 LIFE-5 細分化）。下から 幼年学校＜士官学校＜大学校。勝ち上がるほど高位の士官になる。</summary>
    public enum MilitarySchoolStage { 幼年学校, 士官学校, 大学校 }

    /// <summary>軍学歴（各段の修了で得る）。無資格＜幼年学校卒＜士官学校卒（任官）＜大学校卒（参謀・将官候補）。</summary>
    public enum MilitaryDegree { 無資格, 幼年学校卒, 士官学校卒, 大学校卒 }

    /// <summary>
    /// 士官養成＝多段の軍学校ラダー（#155 LIFE-5 細分化・純ロジック・唯一の窓口）。候補生を軍才順に
    /// 幼年学校→士官学校→大学校 と<b>段ごとの進級枠で篩い</b>、士官学校を出た者だけが任官（席次=ハンモック付き）、
    /// さらに大学校を出た少数が参謀（将官候補）になる。単段の <see cref="OfficerAcademyRules"/> を多段へ精緻化＝
    /// 科挙(<see cref="ImperialExamRules"/>)の武版。候補生成は <see cref="OfficerAcademyRules"/> の係数を流用（二重定義しない）。
    /// 席次→等級は <see cref="SeniorityRules"/>、経歴は <see cref="CareerPipelineRules.Stamp"/> に委譲。test-first。
    /// </summary>
    public static class MilitaryAcademyRules
    {
        // 各段の進級率（軍才順で上位何割が上がるか）。
        public const float Pass幼年学校 = 0.6f;  // 幼年学校→士官学校（多くが進む）
        public const float Pass士官学校 = 0.6f;  // 士官学校を卒業＝任官
        public const float Pass大学校 = 0.25f;   // 大学校（参謀）＝狭き門

        /// <summary>大学校卒（参謀・将官候補）の初任等級の上乗せ。</summary>
        public const int WarCollegeTierBonus = 2;

        /// <summary>その段を修了して得る軍学歴。</summary>
        public static MilitaryDegree DegreeFor(MilitarySchoolStage stage)
        {
            switch (stage)
            {
                case MilitarySchoolStage.幼年学校: return MilitaryDegree.幼年学校卒;
                case MilitarySchoolStage.士官学校: return MilitaryDegree.士官学校卒;
                default: return MilitaryDegree.大学校卒; // 大学校
            }
        }

        public static float PassRate(MilitarySchoolStage stage)
        {
            switch (stage)
            {
                case MilitarySchoolStage.幼年学校: return Pass幼年学校;
                case MilitarySchoolStage.士官学校: return Pass士官学校;
                default: return Pass大学校;
            }
        }

        /// <summary>その段で sitters 人のうち進級する人数（上位 PassRate ぶん・浮動小数の誤差ガード付き・上限 sitters）。</summary>
        public static int QuotaPassing(int sitters, MilitarySchoolStage stage)
        {
            if (sitters <= 0) return 0;
            const float guard = 1e-4f; // 例 100×0.6f の誤差で +1 切り上げされるのを防ぐ
            return Mathf.Clamp(Mathf.CeilToInt(sitters * PassRate(stage) - guard), 0, sitters);
        }

        /// <summary>任官したか（士官学校卒以上＝士官名簿に載る）。幼年学校卒どまりは退校扱い。</summary>
        public static bool IsCommissioned(MilitaryDegree degree)
            => degree == MilitaryDegree.士官学校卒 || degree == MilitaryDegree.大学校卒;

        /// <summary>
        /// 候補生を軍才順に多段で篩う：到達した最高学歴を <see cref="Person.militaryDegree"/> に刻む。任官者（士官学校卒以上）には
        /// <see cref="Person.hammockNumber"/>（首席=1）と等級（大学校卒は <see cref="WarCollegeTierBonus"/> 上乗せ）を与える。破壊的更新。
        /// </summary>
        public static List<Person> Funnel(List<Person> cadets, SeniorityRules.SeniorityParams seniority)
        {
            var list = new List<Person>();
            if (cadets != null) list.AddRange(cadets);
            if (list.Count == 0) return list;

            list.Sort((a, b) => b.MilitaryAptitude.CompareTo(a.MilitaryAptitude)); // 軍才の高い順＝実力本位
            for (int i = 0; i < list.Count; i++)
            {
                list[i].militaryDegree = MilitaryDegree.無資格;
                list[i].hammockNumber = 0;
                list[i].role = PersonRole.軍人;
            }

            int q1 = QuotaPassing(list.Count, MilitarySchoolStage.幼年学校);
            for (int i = 0; i < q1; i++) list[i].militaryDegree = MilitaryDegree.幼年学校卒;
            int q2 = QuotaPassing(q1, MilitarySchoolStage.士官学校);
            for (int i = 0; i < q2; i++) list[i].militaryDegree = MilitaryDegree.士官学校卒;
            int q3 = QuotaPassing(q2, MilitarySchoolStage.大学校);
            for (int i = 0; i < q3; i++) list[i].militaryDegree = MilitaryDegree.大学校卒;

            // 任官者（上位 q2）に席次（首席=1）と初任等級を与える
            for (int i = 0; i < q2; i++)
            {
                Person p = list[i];
                int hammock = i + 1;
                CareerPipelineRules.Stamp(p, CareerTrack.士官学校, p.schoolId, p.graduationYear, hammock);
                int tier = SeniorityRules.InitialTier(hammock, seniority);
                if (p.militaryDegree == MilitaryDegree.大学校卒) tier += WarCollegeTierBonus; // 参謀＝将官候補
                p.rankTier = tier;
            }
            return list;
        }

        /// <summary>軍学歴の称号（参謀/士官/幼年学校卒/退校）。表示用。</summary>
        public static string DegreeTitle(MilitaryDegree degree)
        {
            switch (degree)
            {
                case MilitaryDegree.大学校卒: return "参謀";
                case MilitaryDegree.士官学校卒: return "士官";
                case MilitaryDegree.幼年学校卒: return "幼年学校卒";
                default: return "退校";
            }
        }

        /// <summary>1学年（既定の席次パラメータ）。</summary>
        public static List<Person> RunMilitarySession(Academy a, int year, int sitters, int idStart, Func<int, float> roll)
            => RunMilitarySession(a, year, sitters, idStart, roll, SeniorityRules.SeniorityParams.Default);

        /// <summary>
        /// 1学年を回す：<paramref name="sitters"/> 名の候補生（軍才＝質×素質）を生成し、<see cref="Funnel"/> で篩い、学歴で命名して返す。
        /// 候補の能力係数は <see cref="OfficerAcademyRules"/> を流用（二重定義しない）。
        /// </summary>
        public static List<Person> RunMilitarySession(Academy a, int year, int sitters, int idStart,
            Func<int, float> roll, SeniorityRules.SeniorityParams seniority)
        {
            var cadets = new List<Person>();
            if (a == null) return cadets;
            int n = Mathf.Max(0, sitters);
            for (int i = 0; i < n; i++)
            {
                float r = roll != null ? Mathf.Clamp01(roll(i)) : 0.5f;
                float talent = Mathf.Clamp01(a.quality * OfficerAcademyRules.QualityWeight + r * (1f - OfficerAcademyRules.QualityWeight));
                int s = OfficerAcademyRules.StatFor(talent);
                int sub = OfficerAcademyRules.StatFor(talent * 0.6f);
                var p = new Person(idStart + i, "", a.faction, PersonRole.軍人);
                p.leadership = s; p.attack = s; p.defense = s; p.mobility = s; // 軍才
                p.operation = sub; p.intelligence = sub;
                p.schoolId = a.schoolId;
                p.graduationYear = year;
                p.birthYear = year - OfficerAcademyRules.GraduationAge;
                cadets.Add(p);
            }

            Funnel(cadets, seniority);
            for (int i = 0; i < cadets.Count; i++)
            {
                Person p = cadets[i];
                p.name = $"{DegreeTitle(p.militaryDegree)}{year}-{p.id}";
            }
            return cadets;
        }
    }
}
