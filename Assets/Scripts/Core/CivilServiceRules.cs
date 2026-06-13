using System;
using System.Collections.Generic;
using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 文官の登用経路（日本の律令制・官僚制基盤・史実参考）。実力本位の貢挙（大学寮の試験）から
    /// 門閥本位の蔭位（<see cref="OnishikiRules"/>）まで、「誰がどれだけ実力で官に入るか」を表す。
    /// 史実の日本では貢挙（試験）が振るわず蔭位・譜第（家柄）が官界を占めた＝門閥社会。
    /// <para><b>注：</b>史実の<b>宦官</b>（後宮の去勢官）の登用経路は倫理的観点から本システムでは採用しない。
    /// 宮廷の腐敗・側近の専横は別経路ではなく <see cref="OfficialMerit.integrity"/>（清廉度）と
    /// 勢力レベルの腐敗（<see cref="Regime"/>）で表現する。</para>
    /// </summary>
    public enum CivilEntryRoute
    {
        蔭位,   // 父祖の位階による出身＝門閥（OnishikiRules・最も非実力）
        貢挙,   // 大学寮・国学から試験で出身＝実力（秀才/明経/進士/明法）。日本では家柄に押され衰退
        譜第,   // 特定の家が官職を世襲＝家柄（ふだい）
        雑任    // 下級の無位官人（史生・伴部・使部）から実務で昇る叩き上げ
    }

    /// <summary>官位相当（官職の格と人物の階級の釣り合い・史実：律令の官位相当制）。</summary>
    public enum AppointmentFit
    {
        適任,   // 階級が役職の要求とほぼ釣り合う＝官位相当
        格上,   // 階級が役職に対して高すぎる＝冗官（人材の浪費・左遷）
        格下    // 階級が役職に対して低すぎる＝荷が勝つ（資格不足・抜擢）
    }

    /// <summary>
    /// 銓衡（せんこう）＝文官の任用・選抜の純ロジック（日本の律令制・官僚制基盤・史実参考）。
    /// 律令では式部省（文官）・兵部省（武官）が選叙（任用）と考課を司った。登用経路ごとの実力重み
    /// （門閥 vs 実力）、官位相当の判定、そして空席への銓衡（階級と考課を混ぜた最適候補の選定）を一手に集約する。
    /// 純粋な席次・階級だけで継ぐ <see cref="VacancyRules"/> に対し、本ルールは<b>考課（実績）を加味して登用する</b>
    /// （実力主義の度合いは経路と政体で変わる＝日本では蔭位が優勢で実力登用が利きにくい）。
    /// 純ロジック（非 MonoBehaviour・test-first）。
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
        /// 登用経路ごとの実力の重み（0..1。高いほど実力本位＝1なら考課だけで決まり、0なら門閥だけ）。
        /// 史実（日本）：貢挙＝試験の実力本位（だが衰退）、雑任＝実務の叩き上げ、譜第＝家の世襲、蔭位＝門閥本位。
        /// </summary>
        public static float MeritWeight(CivilEntryRoute route)
        {
            switch (route)
            {
                case CivilEntryRoute.貢挙: return 0.85f; // 大学寮の試験＝理念上は最も実力本位
                case CivilEntryRoute.雑任: return 0.6f;  // 実務の叩き上げ＝実績寄り
                case CivilEntryRoute.譜第: return 0.35f; // 家の世襲＝家柄寄り
                case CivilEntryRoute.蔭位: return 0.15f; // 父祖の位階＝門閥本位
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
