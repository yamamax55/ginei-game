using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 考課（こうか）制度＝官僚の勤務評定の純ロジック（官僚制基盤・史実参考）。
    /// 史実の唐の考課は「四善」（徳義・清慎・公平・恪勤＝<b>徳</b>）と「最」（職掌ごとの功績＝<b>能・績</b>）を
    /// 量り、九等の考第（<see cref="MeritRating"/>）に落とした。本ルールはそれを三要素へ抽象する：
    /// <list type="bullet">
    ///   <item><b>能</b>＝役職への実効力（<see cref="PersonRules.Effectiveness"/> ＝適材適所 0..1）。</item>
    ///   <item><b>徳</b>＝清廉度（<see cref="OfficialMerit.integrity"/> 0..1）。</item>
    ///   <item><b>績</b>＝勤続年数（経験。一定で頭打ち）。</item>
    /// </list>
    /// 累積の考第が昇進（上系の連続）・降格（下系の連続）・俸給（#1969 連動の実効倍率・基準非破壊）を駆動する。
    /// 純ロジック（非 MonoBehaviour・test-first）。実効値パターン（基準値は書き換えない）。
    /// </summary>
    public static class MeritEvaluationRules
    {
        /// <summary>考課の調整値（重み・閾値）。</summary>
        public readonly struct EvaluationParams
        {
            public readonly float competenceWeight; // 能（実効力）の重み
            public readonly float integrityWeight;   // 徳（清廉）の重み
            public readonly float tenureWeight;      // 績（勤続）の重み
            public readonly int tenureCapYears;      // 績が頭打ちになる勤続年数
            public readonly int promoteStreak;       // 連続上系がこの回数で昇進相当
            public readonly int demoteStreak;        // 連続下系がこの回数で降格相当

            public EvaluationParams(float competenceWeight, float integrityWeight, float tenureWeight,
                                    int tenureCapYears, int promoteStreak, int demoteStreak)
            {
                this.competenceWeight = competenceWeight;
                this.integrityWeight = integrityWeight;
                this.tenureWeight = tenureWeight;
                this.tenureCapYears = Mathf.Max(1, tenureCapYears);
                this.promoteStreak = Mathf.Max(1, promoteStreak);
                this.demoteStreak = Mathf.Max(1, demoteStreak);
            }

            /// <summary>既定＝能0.5・徳0.3・績0.2、績は20年で頭打ち、3連続上系で昇進・3連続下系で降格。</summary>
            public static EvaluationParams Default => new EvaluationParams(0.5f, 0.3f, 0.2f, 20, 3, 3);
        }

        /// <summary>
        /// 考課スコア（0..1）＝能×徳×績の重み付き和。<paramref name="competence"/>/<paramref name="integrity"/> は 0..1、
        /// <paramref name="tenureYears"/> は勤続年数（cap で頭打ち）。重みは内部で正規化するので合計1でなくてよい。
        /// </summary>
        public static float MeritScore(float competence, float integrity, int tenureYears, EvaluationParams p)
        {
            float c = Mathf.Clamp01(competence);
            float v = Mathf.Clamp01(integrity);
            float t = Mathf.Clamp01((float)Mathf.Max(0, tenureYears) / p.tenureCapYears);
            float wSum = p.competenceWeight + p.integrityWeight + p.tenureWeight;
            if (wSum <= 0f) return 0f;
            return (c * p.competenceWeight + v * p.integrityWeight + t * p.tenureWeight) / wSum;
        }

        /// <summary>考課スコア（0..1）を九等の考第へ写す。0.5 付近＝中中、上ほど上上、下ほど下下。</summary>
        public static MeritRating RatingFromScore(float score)
        {
            float s = Mathf.Clamp01(score);
            // 0..1 を 9 段（上上=0 … 下下=8）へ。高スコアほど良い＝小さい index。
            int band = Mathf.Clamp(8 - Mathf.FloorToInt(s * 9f), 0, 8);
            return (MeritRating)band;
        }

        /// <summary>能・徳・績から一回ぶんの考第を付ける（<see cref="MeritScore"/>→<see cref="RatingFromScore"/>）。</summary>
        public static MeritRating Evaluate(float competence, float integrity, int tenureYears, EvaluationParams p)
            => RatingFromScore(MeritScore(competence, integrity, tenureYears, p));

        /// <summary>考第のスコア（上上=9 … 下下=1）。累計・平均に使う。</summary>
        public static int Score(MeritRating r) => 9 - (int)r;

        /// <summary>考第の序列（上上=0 … 下下=8。小さいほど上位）。</summary>
        public static int Rank(MeritRating r) => (int)r;

        /// <summary>上系（上上/上中/上下）か＝好成績。</summary>
        public static bool IsTop(MeritRating r) => r <= MeritRating.上下;

        /// <summary>下系（下上/下中/下下）か＝不成績。</summary>
        public static bool IsPoor(MeritRating r) => r >= MeritRating.下上;

        /// <summary>
        /// 考第を考課記録へ反映（評定回数＋累計＋連続好/不評の更新）。
        /// 上系で連続好評＋連続不評リセット、下系でその逆、中系は両方リセット（途切れる）。
        /// </summary>
        public static void Record(OfficialMerit merit, MeritRating rating)
        {
            if (merit == null) return;
            merit.evaluations++;
            merit.cumulativeScore += Score(rating);
            merit.lastRating = rating;
            if (IsTop(rating)) { merit.consecutiveTop++; merit.consecutivePoor = 0; }
            else if (IsPoor(rating)) { merit.consecutivePoor++; merit.consecutiveTop = 0; }
            else { merit.consecutiveTop = 0; merit.consecutivePoor = 0; }
        }

        /// <summary>能・徳・績から考課を回して記録まで行う一手（<see cref="Evaluate"/>＋<see cref="Record"/>）。付けた考第を返す。</summary>
        public static MeritRating EvaluateAndRecord(OfficialMerit merit, float competence, int tenureYears, EvaluationParams p)
        {
            float integ = merit != null ? merit.integrity : 0.7f;
            MeritRating r = Evaluate(competence, integ, tenureYears, p);
            Record(merit, r);
            return r;
        }

        /// <summary>昇進相当か＝連続上系が <see cref="EvaluationParams.promoteStreak"/> に達した（史実：累積の好考第で進階）。</summary>
        public static bool IsPromotable(OfficialMerit merit, EvaluationParams p)
            => merit != null && merit.consecutiveTop >= p.promoteStreak;

        /// <summary>降格相当か＝連続下系が <see cref="EvaluationParams.demoteStreak"/> に達した（累積の不考第で貶降）。</summary>
        public static bool ShouldDemote(OfficialMerit merit, EvaluationParams p)
            => merit != null && merit.consecutivePoor >= p.demoteStreak;

        /// <summary>
        /// 一回の考第が即時に与える階級（tier）の増減（上上=+1 … 下下=-1）。
        /// 上下/中上=0 寄り＝上系の上端と下端で差をつける。累積の進退は <see cref="IsPromotable"/>/<see cref="ShouldDemote"/>。
        /// </summary>
        public static int PromotionTierDelta(MeritRating r)
        {
            switch (r)
            {
                case MeritRating.上上: return 1;
                case MeritRating.下下: return -1;
                default: return 0;
            }
        }

        /// <summary>
        /// 考第→俸給の実効倍率（WAGE #1969 連動・基準俸は非破壊）。上上=1.2 … 中中=1.0 … 下下=0.8 で線形。
        /// 史実：好考第は加禄・不考第は減禄。
        /// </summary>
        public static float StipendFactor(MeritRating r)
        {
            // 上上(0)→+0.2、中中(4)→0、下下(8)→-0.2 を線形。
            float t = Rank(r) / 8f;            // 0..1
            return Mathf.Lerp(1.2f, 0.8f, t);
        }
    }
}
