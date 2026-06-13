using System;
using System.Collections.Generic;
using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 文官の登用経路（官僚制基盤・史実参考）。実力本位の科挙から門地（家柄）本位の蔭位まで、
    /// 「誰がどれだけ実力で官に入るか」を表す。
    /// <para><b>注：</b>史実の<b>宦官</b>（後宮の去勢官）の登用経路は倫理的観点から本システムでは採用しない。
    /// 宮廷の腐敗・側近の専横は別経路ではなく <see cref="OfficialMerit.integrity"/>（清廉度）と
    /// 勢力レベルの腐敗（<see cref="Regime"/>）で表現する。</para>
    /// </summary>
    public enum CivilEntryRoute
    {
        科挙,   // 試験で登用＝実力本位（ImperialExamRules・#156）
        蔭位,   // 父祖の官位による任子＝門地本位（世襲特権）
        辟召,   // 有力者の推挙・召し出し＝人脈（郷挙里選の系譜）
        流外    // 胥吏（無位の実務吏員）からの叩き上げ＝実務経験で官へ
    }

    /// <summary>官位相当（官職の格と人物の階級の釣り合い・史実：律令の官位相当制）。</summary>
    public enum AppointmentFit
    {
        適任,   // 階級が役職の要求とほぼ釣り合う＝官位相当
        格上,   // 階級が役職に対して高すぎる＝冗官（人材の浪費・左遷）
        格下    // 階級が役職に対して低すぎる＝荷が勝つ（資格不足・抜擢）
    }

    /// <summary>
    /// 銓衡（せんこう）＝文官の任用・選抜の純ロジック（官僚制基盤・史実参考）。
    /// 登用経路ごとの実力重み（門地 vs 実力）、官位相当の判定、そして空席への銓衡
    /// （階級と考課を混ぜた最適候補の選定）を一手に集約する。
    /// 純粋な席次・階級だけで継ぐ <see cref="VacancyRules"/> に対し、本ルールは<b>考課（実績）を加味して登用する</b>
    /// ＝史実の科挙官僚制の核（実力主義の度合いは経路と政体で変わる）。純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class CivilServiceRules
    {
        /// <summary>銓衡の調整値。</summary>
        public readonly struct AppointmentParams
        {
            public readonly float tierWeight;  // 候補スコアでの階級の重み
            public readonly float meritWeight;  // 候補スコアでの考課（実績）の重み
            public readonly int fitTolerance;   // 官位相当とみなす階級差（±これ以内なら適任）

            public AppointmentParams(float tierWeight, float meritWeight, int fitTolerance)
            {
                this.tierWeight = tierWeight;
                this.meritWeight = meritWeight;
                this.fitTolerance = Mathf.Max(0, fitTolerance);
            }

            /// <summary>既定＝階級0.6・考課0.4、官位相当の許容差±1。</summary>
            public static AppointmentParams Default => new AppointmentParams(0.6f, 0.4f, 1);
        }

        /// <summary>
        /// 登用経路ごとの実力の重み（0..1。高いほど実力本位＝1なら考課だけで決まり、0なら門地だけ）。
        /// 史実：科挙＝実力本位、蔭位＝門地（家柄）本位、辟召＝人脈、流外＝実務経験。
        /// </summary>
        public static float MeritWeight(CivilEntryRoute route)
        {
            switch (route)
            {
                case CivilEntryRoute.科挙: return 0.9f; // 試験で篩う＝実力本位
                case CivilEntryRoute.流外: return 0.7f; // 実務の叩き上げ＝実績寄り
                case CivilEntryRoute.辟召: return 0.5f; // 人脈と実力の折衷
                case CivilEntryRoute.蔭位: return 0.2f; // 家柄が大半＝門地本位
                default: return 0.5f;
            }
        }

        /// <summary>
        /// 官位相当の判定（人物階級 vs 役職要求階級）。差が <paramref name="prm"/>.fitTolerance 以内なら適任、
        /// 上回れば格上（冗官）、下回れば格下（抜擢・荷が勝つ）。
        /// </summary>
        public static AppointmentFit Fitness(int personTier, int officeRequiredTier, AppointmentParams prm)
        {
            int diff = personTier - officeRequiredTier;
            if (diff > prm.fitTolerance) return AppointmentFit.格上;
            if (diff < -prm.fitTolerance) return AppointmentFit.格下;
            return AppointmentFit.適任;
        }

        /// <summary>
        /// 候補のスコア（大きいほど任用に適する）＝階級（正規化）と考課平均（0..1へ正規化）の重み付き和。
        /// 考課記録が無い候補は中庸（0.5）として扱う＝新任に不当な不利を与えない。
        /// </summary>
        public static float CandidateScore(int personTier, OfficialMerit merit, AppointmentParams prm)
        {
            float tierNorm = Mathf.Clamp01(personTier / 10f);            // tier 0..10 を 0..1 へ
            float meritNorm = (merit != null && merit.HasRecord)
                ? Mathf.Clamp01(merit.AverageScore / 9f)                  // 考第平均 1..9 を 0..1 へ
                : 0.5f;
            float wSum = prm.tierWeight + prm.meritWeight;
            if (wSum <= 0f) return 0f;
            return (tierNorm * prm.tierWeight + meritNorm * prm.meritWeight) / wSum;
        }

        /// <summary>
        /// 空席への銓衡＝有資格（階級が要求 tier 以上・存命・自由）の候補から、階級＋考課の総合最高を選ぶ。
        /// 同点は若い順（生年が新しい＝後進に道を開く）。適任者がいなければ null（空席のまま＝機能低下を許容）。
        /// </summary>
        public static Person SelectForOffice(IEnumerable<Person> candidates, int requiredTier,
                                             Func<Person, OfficialMerit> meritOf, AppointmentParams prm)
        {
            if (candidates == null) return null;
            Person best = null;
            float bestScore = float.NegativeInfinity;
            foreach (var p in candidates)
            {
                if (p == null) continue;
                if (!p.IsAvailable) continue;          // 死亡・捕虜は除外（ICharacter）
                if (p.rankTier < requiredTier) continue; // 階級ゲート（#14）
                OfficialMerit m = meritOf != null ? meritOf(p) : null;
                float score = CandidateScore(p.rankTier, m, prm);
                if (score > bestScore || (Mathf.Approximately(score, bestScore) && best != null && p.birthYear > best.birthYear))
                {
                    bestScore = score;
                    best = p;
                }
            }
            return best;
        }
    }
}
