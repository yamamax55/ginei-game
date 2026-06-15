using System.Collections.Generic;
using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 文官のネームド化＝官歴を回す年次オーケストレータ（日本の律令制・官僚制基盤・配線ロジック）。
    /// 文民ロスター（<see cref="Person"/> の <see cref="PersonRole.文民"/>）に対し毎年：
    /// ①未叙位なら出身時の<b>位階</b>を与え（<see cref="InitialCourtRank"/>＝高官は貴族・一般は六位以下＝五位の壁の下）、
    /// ②<b>考課</b>（<see cref="MeritEvaluationRules"/>＝能（政務適性）×徳（清廉）×績（勤続））を付け、
    /// ③その考第で<b>叙位</b>（<see cref="JapaneseCourtRankRules.AdvanceOnMerit"/>）する。
    /// <b>五位の壁</b>は朝廷の権威（<see cref="CourtAuthority"/>）が高いとき（律令が機能＝勅授が効く）だけ越えられる
    /// ＝権威が低い封建の世では門閥（蔭位）以外は五位へ上がれない。数値は各 Rules へ委譲（本層は配線）。
    /// 純ロジック（非 MonoBehaviour・test-first）・基準値非破壊（位階/考課のみ更新）。
    /// </summary>
    public static class BureaucracyCareerRules
    {
        /// <summary>官歴 Tick の調整値。</summary>
        public readonly struct CareerParams
        {
            public readonly MeritEvaluationRules.EvaluationParams evaluation;
            public readonly float fifthWallAuthority; // この朝廷の権威以上でのみ五位の壁を越えられる（勅授）

            public CareerParams(MeritEvaluationRules.EvaluationParams evaluation, float fifthWallAuthority)
            {
                this.evaluation = evaluation;
                this.fifthWallAuthority = Mathf.Clamp01(fifthWallAuthority);
            }

            /// <summary>既定＝考課既定・五位の壁は権威0.6以上（律令制/摂関政治の世）で開く。</summary>
            public static CareerParams Default =>
                new CareerParams(MeritEvaluationRules.EvaluationParams.Default, 0.6f);
        }

        /// <summary>官歴の出来事（叙位/五位突破/貶位）。</summary>
        public enum CareerEventKind { 叙位, 五位突破, 貶位 }

        /// <summary>叙位・貶位の記録（呼び出し側が通知に使う）。</summary>
        public struct CareerChange
        {
            public int personId;
            public CareerEventKind kind;
            public CourtRank from;
            public CourtRank to;
        }

        /// <summary>
        /// 出身時の位階＝官歴開始の格。高官（高 tier＝進士上位/高官登用）は貴族近く、一般出身は六位以下
        /// （＝五位の壁の下から始まる）。<see cref="Person.rankTier"/>（登用時の等級）から決める。
        /// </summary>
        public static CourtRank InitialCourtRank(Person p)
        {
            if (p == null) return CourtRank.無位;
            int t = p.rankTier;
            if (t >= 8) return CourtRank.従五位下; // 高官登用＝いきなり貴族（蔭位の最上相当）
            if (t == 7) return CourtRank.正六位上; // 進士級＝五位の壁の直下
            if (t == 6) return CourtRank.正六位下;
            if (t == 5) return CourtRank.従六位上;
            if (t == 4) return CourtRank.従六位下;
            if (t == 3) return CourtRank.正七位上;
            return CourtRank.正八位上;             // 一般出身
        }

        /// <summary>勤続年数（考課の「績」）。卒業年が分かればそこから、無ければ1年扱い。</summary>
        public static int TenureYears(Person p, int currentYear)
        {
            if (p == null) return 0;
            if (p.graduationYear > 0 && currentYear > p.graduationYear) return currentYear - p.graduationYear;
            return 1;
        }

        /// <summary>
        /// 文民ロスターの官歴を1年ぶん回す。叙位・貶位した者を <paramref name="changes"/> に積む（null 可）。
        /// 軍人・故人・捕虜は対象外（文官のみ・在任の者）。位階と考課記録のみ更新（基準能力は非破壊）。
        /// </summary>
        public static void TickYear(IList<Person> roster, float courtAuthority, int currentYear,
                                    CareerParams cp, IList<CareerChange> changes = null)
        {
            if (roster == null) return;
            bool wallOpen = courtAuthority >= cp.fifthWallAuthority;

            for (int i = 0; i < roster.Count; i++)
            {
                Person p = roster[i];
                if (p == null || p.role != PersonRole.文民) continue;
                if (!p.IsAvailable) continue; // 故人・捕虜は評定しない

                if (p.merit == null) p.merit = new OfficialMerit(p.id);
                if (p.courtRank == CourtRank.無位) p.courtRank = InitialCourtRank(p);

                // 適材適所＝文民×政務の実効力（0..100）を 0..1 へ正規化して考課の「能」に渡す。
                float competence = Mathf.Clamp01(PersonRules.Effectiveness(p, PostType.政務) / 100f);
                MeritRating rating = MeritEvaluationRules.Evaluate(
                    competence, p.merit.integrity, TenureYears(p, currentYear), cp.evaluation);
                MeritEvaluationRules.Record(p.merit, rating);

                CourtRank before = p.courtRank;
                bool allowBreak = wallOpen && MeritEvaluationRules.IsTop(rating);
                CourtRank after = JapaneseCourtRankRules.AdvanceOnMerit(before, rating, allowBreak);
                if (after == before) continue;

                p.courtRank = after;
                if (changes != null)
                {
                    CareerEventKind kind;
                    if (JapaneseCourtRankRules.Compare(after, before) < 0) kind = CareerEventKind.貶位; // 下がった
                    else if (JapaneseCourtRankRules.CrossesFifthRankWall(before, after)) kind = CareerEventKind.五位突破;
                    else kind = CareerEventKind.叙位;
                    changes.Add(new CareerChange { personId = p.id, kind = kind, from = before, to = after });
                }
            }
        }
    }
}
