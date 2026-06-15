using System;
using System.Collections.Generic;
using UnityEngine;

namespace Ginei
{
    /// <summary>科挙の試験段階（#156 LIFE-6 細分化）。下から 童試＜郷試＜会試＜殿試。各段を勝ち抜くと功名が上がる。</summary>
    public enum ExamStage { 童試, 郷試, 会試, 殿試 }

    /// <summary>科挙の功名（各段の合格で得る位）。無資格＜生員(童試)＜挙人(郷試)＜貢士(会試)＜進士(殿試・首席=状元)。</summary>
    public enum ExamDegree { 無資格, 生員, 挙人, 貢士, 進士 }

    /// <summary>
    /// 科挙＝多段の登用試験ラダー（#156 LIFE-6 細分化・純ロジック・唯一の窓口）。受験者を文才順に
    /// 童試→郷試→会試→殿試 と<b>段ごとの合格枠で篩い落とし</b>、勝ち残った少数に進士（首席＝状元）の功名と高い初任等級を与える。
    /// 単段の <see cref="UniversityRules"/>（科挙の簡易版）を多段の選抜漏斗へ精緻化＝「狭き門」を表現。候補生成は
    /// <see cref="UniversityRules"/> の係数を流用（二重定義しない）。功名→等級は <see cref="SeniorityRules"/> と整合。test-first。
    /// </summary>
    public static class ImperialExamRules
    {
        // 各段の合格率（受験者のうち上位何割が通るか・文才順）。殿試は全員合格＝順位付け（状元）だけ。狭き門ほど低い。
        public const float Pass童試 = 0.5f;
        public const float Pass郷試 = 0.1f;
        public const float Pass会試 = 0.3f;
        public const float Pass殿試 = 1.0f;

        // 功名ごとの初任等級（進士ほど高位。状元は別途+1）。
        public const int Tier進士 = 7;
        public const int Tier貢士 = 6;
        public const int Tier挙人 = 5;
        public const int Tier生員 = 4;

        /// <summary>その段に合格して得る功名（童試→生員／郷試→挙人／会試→貢士／殿試→進士）。</summary>
        public static ExamDegree DegreeFor(ExamStage stage)
        {
            switch (stage)
            {
                case ExamStage.童試: return ExamDegree.生員;
                case ExamStage.郷試: return ExamDegree.挙人;
                case ExamStage.会試: return ExamDegree.貢士;
                default: return ExamDegree.進士; // 殿試
            }
        }

        /// <summary>その段の合格率（上位何割が進むか）。</summary>
        public static float PassRate(ExamStage stage)
        {
            switch (stage)
            {
                case ExamStage.童試: return Pass童試;
                case ExamStage.郷試: return Pass郷試;
                case ExamStage.会試: return Pass会試;
                default: return Pass殿試;
            }
        }

        /// <summary>その段で受験者 sitters 人のうち合格する人数（上位 PassRate ぶん・最低1・上限 sitters）。</summary>
        public static int QuotaPassing(int sitters, ExamStage stage)
        {
            if (sitters <= 0) return 0;
            // 浮動小数の誤差（例 100×0.3f=30.0000012）で +1 切り上げされないよう微小ガードを引く。
            const float guard = 1e-4f;
            return Mathf.Clamp(Mathf.CeilToInt(sitters * PassRate(stage) - guard), 0, sitters);
        }

        /// <summary>功名ごとの初任等級（進士7/貢士6/挙人5/生員4/無資格0）。</summary>
        public static int TierFor(ExamDegree degree)
        {
            switch (degree)
            {
                case ExamDegree.進士: return Tier進士;
                case ExamDegree.貢士: return Tier貢士;
                case ExamDegree.挙人: return Tier挙人;
                case ExamDegree.生員: return Tier生員;
                default: return 0;
            }
        }

        /// <summary>
        /// 受験者を文才順に多段で篩う：各段の上位だけが進み、到達した最高功名を <see cref="Person.examDegree"/> に刻む。
        /// 殿試合格（進士）には <see cref="Person.examRank"/>（状元=1）と等級（状元は+1）を与える。受験者リストを破壊的に更新して返す。
        /// </summary>
        public static List<Person> Funnel(List<Person> sitters, SeniorityRules.SeniorityParams seniority)
        {
            var list = new List<Person>();
            if (sitters != null) list.AddRange(sitters);
            if (list.Count == 0) return list;

            // 文才（運営/情報）の高い順に並べる＝実力本位の選抜
            list.Sort((a, b) => b.CivilAptitude.CompareTo(a.CivilAptitude));
            for (int i = 0; i < list.Count; i++) { list[i].examDegree = ExamDegree.無資格; list[i].examRank = 0; }

            int advancing = list.Count;
            ExamStage[] order = { ExamStage.童試, ExamStage.郷試, ExamStage.会試, ExamStage.殿試 };
            for (int si = 0; si < order.Length; si++)
            {
                int quota = QuotaPassing(advancing, order[si]);
                ExamDegree deg = DegreeFor(order[si]);
                for (int i = 0; i < quota; i++) list[i].examDegree = deg; // 上位quotaが合格（ソート済の先頭）
                advancing = quota;
                if (advancing == 0) break;
            }

            // 殿試を通った advancing 人が進士。文才順に状元(1)・榜眼(2)…
            for (int i = 0; i < advancing; i++) list[i].examRank = i + 1;

            for (int i = 0; i < list.Count; i++)
            {
                Person p = list[i];
                p.role = PersonRole.文民;
                if (p.examDegree == ExamDegree.無資格) continue;
                int tier = TierFor(p.examDegree);
                if (p.examDegree == ExamDegree.進士 && p.examRank == 1) tier += 1; // 状元
                p.rankTier = tier;
            }
            return list;
        }

        /// <summary>功名の称号（状元/進士/貢士/挙人/生員/落第）。表示用。</summary>
        public static string DegreeTitle(ExamDegree degree, int examRank)
        {
            if (degree == ExamDegree.進士 && examRank == 1) return "状元";
            return degree == ExamDegree.無資格 ? "落第" : degree.ToString();
        }

        /// <summary>1回の科挙（受験生を生成して多段で篩う・既定の席次パラメータ）。</summary>
        public static List<Person> RunExamSession(University u, int year, int sitters, int idStart, Func<int, float> roll)
            => RunExamSession(u, year, sitters, idStart, roll, SeniorityRules.SeniorityParams.Default);

        /// <summary>
        /// 1回の科挙を回す：<paramref name="sitters"/> 名の受験生（文才＝質×素質）を生成し、<see cref="Funnel"/> で篩い、
        /// 功名で命名して全受験者を返す（合格者は examDegree≥生員、最上位が状元）。候補の能力係数は <see cref="UniversityRules"/> を流用。
        /// </summary>
        public static List<Person> RunExamSession(University u, int year, int sitters, int idStart,
            Func<int, float> roll, SeniorityRules.SeniorityParams seniority)
        {
            var cands = new List<Person>();
            if (u == null) return cands;
            int n = Mathf.Max(0, sitters);
            for (int i = 0; i < n; i++)
            {
                float r = roll != null ? Mathf.Clamp01(roll(i)) : 0.5f;
                float talent = Mathf.Clamp01(u.quality * UniversityRules.QualityWeight + r * (1f - UniversityRules.QualityWeight));
                int s = UniversityRules.StatFor(talent);
                int sub = UniversityRules.StatFor(talent * 0.6f);
                var p = new Person(idStart + i, "", u.faction, PersonRole.文民);
                p.operation = s; p.intelligence = s; p.planning = s;        // 文才（行政/外交/計画）
                p.research = sub; p.engineering = sub; p.production = sub;
                p.schoolId = u.schoolId;
                p.graduationYear = year;
                p.birthYear = year - SchoolAgeRules.GraduationAge(SchoolType.科挙); // 進士登用の典型年齢≒30（科挙は年齢制限なし＝大学とは別・史実精緻化）
                cands.Add(p);
            }

            Funnel(cands, seniority);
            for (int i = 0; i < cands.Count; i++)
            {
                Person p = cands[i];
                p.name = $"{DegreeTitle(p.examDegree, p.examRank)}{year}-{p.id}";
            }
            return cands;
        }
    }
}
