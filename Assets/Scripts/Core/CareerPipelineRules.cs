using System.Collections.Generic;
using UnityEngine;

namespace Ginei
{
    /// <summary>人材の出自経路（LIFE-5/6/7 #155/#156/#157）。武＝士官学校／官＝科挙・有力者／技＝テクノクラート。</summary>
    public enum CareerTrack { 士官学校, 科挙, 有力者, テクノクラート }

    /// <summary>
    /// 出自パイプラインの純ロジック（LIFE-5/6/7 #155/#156/#157・唯一の窓口）。候補を各経路で<b>ネームド化</b>（経歴を刻む）し、
    /// <b>学閥/文官閥/専門閥</b>（同窓=同制度・同期=同年）の結束を出す。「武 vs 官 vs 技」の三系統を一つの枠組みで扱う
    /// （新派閥システムを作らず #113 内部勢力の一種）。席次→序列・merit上書きは <see cref="SeniorityRules"/> に委譲。test-first。
    /// </summary>
    public static class CareerPipelineRules
    {
        /// <summary>閥の結束の調整値。</summary>
        public readonly struct CliqueParams
        {
            public readonly float sameInstitution; // 同窓（同じ学校/制度）の結束
            public readonly float sameCohort;        // 同期（同じ卒業年/合格年）の結束

            public CliqueParams(float sameInstitution, float sameCohort)
            {
                this.sameInstitution = Mathf.Clamp01(sameInstitution);
                this.sameCohort = Mathf.Clamp01(sameCohort);
            }

            /// <summary>既定＝同窓0.3・同期0.4（両方で0.7）。</summary>
            public static CliqueParams Default => new CliqueParams(0.3f, 0.4f);
        }

        /// <summary>経路の役割（士官学校＝軍人／科挙・有力者・テクノクラート＝文民）。</summary>
        public static PersonRole TrackRole(CareerTrack track)
            => track == CareerTrack.士官学校 ? PersonRole.軍人 : PersonRole.文民;

        /// <summary>技術系経路か（テクノクラート＝研究/生産ドメインに効くスペシャリスト・LIFE-7）。</summary>
        public static bool TrackIsTechnical(CareerTrack track) => track == CareerTrack.テクノクラート;

        /// <summary>
        /// 候補をネームド化する（経歴を刻む）：役割を経路から決め、卒業/合格の年・制度・席次を刻む。
        /// 士官学校は <see cref="Person.hammockNumber"/>、科挙は <see cref="Person.examRank"/> に席次を入れる。
        /// 有力者は席次を持たない（出自で登用）。<paramref name="rank"/>≤0 で席次なし。
        /// </summary>
        public static void Stamp(Person p, CareerTrack track, int schoolId, int graduationYear, int rank)
        {
            if (p == null) return;
            p.role = TrackRole(track);
            p.schoolId = schoolId;
            p.graduationYear = graduationYear;
            if (track == CareerTrack.士官学校 && rank > 0) p.hammockNumber = rank;
            else if (track == CareerTrack.科挙 && rank > 0) p.examRank = rank;
        }

        /// <summary>
        /// 二人の閥の結束（0..1）。同窓（同じ <see cref="Person.schoolId"/>）＋同期（同じ <see cref="Person.graduationYear"/>）で高まる。
        /// 学閥（軍）・文官閥・専門閥に共通＝引き立て/結束/対立の温床（#113/#141/#145）。
        /// </summary>
        public static float CliqueBond(Person a, Person b, CliqueParams prm)
        {
            if (a == null || b == null || a == b) return 0f;
            float bond = 0f;
            if (a.schoolId != 0 && a.schoolId == b.schoolId) bond += prm.sameInstitution;
            if (a.graduationYear != 0 && a.graduationYear == b.graduationYear) bond += prm.sameCohort;
            return Mathf.Clamp01(bond);
        }

        /// <summary>テクノクラートの実効力＝専門才（実力本位・出自を問わない・LIFE-7）。役割一致は問わない。</summary>
        public static float TechnocratEffectiveness(Person p) => p == null ? 0f : p.TechnicalAptitude;

        /// <summary>勢力内で専門才が最も高い人物（技術系役職の自動配属・LIFE-7）。候補なしは null。</summary>
        public static Person BestTechnocrat(IEnumerable<Person> people, Faction faction)
        {
            if (people == null) return null;
            Person best = null;
            float bestVal = float.NegativeInfinity;
            foreach (Person p in people)
            {
                if (p == null || p.faction != faction || !p.IsAvailable) continue;
                float v = TechnocratEffectiveness(p);
                if (v > bestVal) { bestVal = v; best = p; }
            }
            return best;
        }
    }
}
