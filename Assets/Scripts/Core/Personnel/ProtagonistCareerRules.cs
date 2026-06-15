using System;
using System.Collections.Generic;
using UnityEngine;

namespace Ginei
{
    /// <summary>主人公が士官学校で到達した結果（TKO-1・#2478）。一代記の起点＝入校→多段選抜→卒業→任官。</summary>
    public readonly struct ProtagonistCareerOutcome
    {
        /// <summary>到達した最高軍学歴（無資格/幼年学校卒/士官学校卒/大学校卒）。</summary>
        public readonly MilitaryDegree degree;
        /// <summary>卒業席次（1=首席。小さいほど上位。0=任官せず）。</summary>
        public readonly int hammockNumber;
        /// <summary>初任等級 tier（0=任官せず）。大学校卒は <see cref="MilitaryAcademyRules.WarCollegeTierBonus"/> 上乗せ済み。</summary>
        public readonly int rankTier;
        /// <summary>卒業年（学閥=同期の判定キー）。</summary>
        public readonly int graduationYear;
        /// <summary>任官したか（士官学校卒以上＝士官名簿に載る）。</summary>
        public readonly bool commissioned;
        /// <summary>同期の人数（席次の母数）。</summary>
        public readonly int classSize;

        public ProtagonistCareerOutcome(MilitaryDegree degree, int hammockNumber, int rankTier,
            int graduationYear, bool commissioned, int classSize)
        {
            this.degree = degree;
            this.hammockNumber = hammockNumber;
            this.rankTier = rankTier;
            this.graduationYear = graduationYear;
            this.commissioned = commissioned;
            this.classSize = classSize;
        }
    }

    /// <summary>
    /// 主人公の士官学校入学〜任官〜配属の純ロジック（TKO-1・太閤立志伝V 軍人立志伝・#2478・唯一の窓口）。
    /// <b>主人公1名を同期の集団に混ぜて既存の多段選抜（<see cref="MilitaryAcademyRules.Funnel"/>）を回し</b>、
    /// 主人公個人の到達結果（学歴・席次・初任等級・任官可否）を取り出して一代記の起点を作る。
    /// 群体選抜・候補生成は <see cref="MilitaryAcademyRules"/>、席次→等級は <see cref="SeniorityRules"/>、
    /// 経歴の刻みは <see cref="CareerPipelineRules.Stamp"/> に委譲する（作り直さない＝additive）。視点固定は
    /// <see cref="ProtagonistRules"/>。配属可否は <see cref="SchoolPostingRules"/> に委譲（在学中は艦隊配属不可）。
    /// 純データ（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class ProtagonistCareerRules
    {
        /// <summary>
        /// 主人公を同期 <paramref name="classmates"/> に混ぜて多段選抜を回し、主人公の到達結果を返す。
        /// <b>破壊的</b>＝主人公と同期の <see cref="Person"/> の学歴/席次/等級を更新する（<see cref="MilitaryAcademyRules.Funnel"/> の仕様）。
        /// 主人公の相対的な軍才（<see cref="Person.MilitaryAptitude"/>）が席次・学歴を決める＝実力本位。
        /// 学閥（同窓/同期）を効かせるため、呼び出し側で主人公の <see cref="Person.schoolId"/>/<see cref="Person.graduationYear"/> を
        /// 同期と揃えておくこと（<see cref="EnrollWithClass"/> は自動で揃える）。
        /// </summary>
        public static ProtagonistCareerOutcome Enroll(Person protagonist, List<Person> classmates,
            SeniorityRules.SeniorityParams seniority)
        {
            if (protagonist == null) return default;
            protagonist.role = PersonRole.軍人;

            var cohort = new List<Person>();
            if (classmates != null)
            {
                for (int i = 0; i < classmates.Count; i++)
                    if (classmates[i] != null && !ReferenceEquals(classmates[i], protagonist))
                        cohort.Add(classmates[i]);
            }
            cohort.Add(protagonist);

            MilitaryAcademyRules.Funnel(cohort, seniority);

            // 任官者は卒業まで在学＝卒業年を境に艦隊配属可になる（学校配属ゲート・#SCHOOL-AGE）。
            if (MilitaryAcademyRules.IsCommissioned(protagonist.militaryDegree))
                protagonist.schoolPostingUntilYear = protagonist.graduationYear;

            return OutcomeOf(protagonist, cohort.Count);
        }

        /// <summary>既定の席次パラメータで入校（<see cref="SeniorityRules.SeniorityParams.Default"/>）。</summary>
        public static ProtagonistCareerOutcome Enroll(Person protagonist, List<Person> classmates)
            => Enroll(protagonist, classmates, SeniorityRules.SeniorityParams.Default);

        /// <summary>
        /// 同期を <see cref="MilitaryAcademyRules.RunMilitarySession"/> で自動生成して入校する（候補生成の二重定義を避ける）。
        /// 生成同期の id は <paramref name="classmateIdStart"/> から採番（主人公 id と衝突させないこと）。
        /// 主人公の入校年・制度（schoolId/graduationYear）は同期に揃える＝学閥（同窓/同期）が成立する。
        /// </summary>
        public static ProtagonistCareerOutcome EnrollWithClass(Person protagonist, Academy academy,
            int classSize, int year, int classmateIdStart, Func<int, float> roll,
            SeniorityRules.SeniorityParams seniority)
        {
            if (protagonist == null) return default;
            var classmates = MilitaryAcademyRules.RunMilitarySession(
                academy, year, Mathf.Max(0, classSize), classmateIdStart, roll, seniority);
            protagonist.graduationYear = year;
            if (academy != null) protagonist.schoolId = academy.schoolId;
            return Enroll(protagonist, classmates, seniority);
        }

        /// <summary>既定の席次パラメータで自動生成入校。</summary>
        public static ProtagonistCareerOutcome EnrollWithClass(Person protagonist, Academy academy,
            int classSize, int year, int classmateIdStart, Func<int, float> roll)
            => EnrollWithClass(protagonist, academy, classSize, year, classmateIdStart, roll,
                SeniorityRules.SeniorityParams.Default);

        /// <summary>主人公の現状から到達結果を組む（席次/等級は任官者のみ有効）。</summary>
        public static ProtagonistCareerOutcome OutcomeOf(Person p, int classSize)
        {
            if (p == null) return default;
            bool commissioned = MilitaryAcademyRules.IsCommissioned(p.militaryDegree);
            return new ProtagonistCareerOutcome(
                p.militaryDegree,
                commissioned ? p.hammockNumber : 0,
                commissioned ? p.rankTier : 0,
                p.graduationYear,
                commissioned,
                classSize);
        }

        /// <summary>
        /// 任官後、現在年に艦隊（梯団）へ配属できるか＝在学中でなく就任可能（<see cref="SchoolPostingRules.CanAssignToFleet(Person,int)"/> へ委譲）。
        /// 在学中（卒業年がまだ未来）は false。任官していない（退校）者も false。
        /// </summary>
        public static bool CanDeploy(Person protagonist, int currentYear)
            => protagonist != null
               && MilitaryAcademyRules.IsCommissioned(protagonist.militaryDegree)
               && SchoolPostingRules.CanAssignToFleet(protagonist, currentYear);
    }
}
