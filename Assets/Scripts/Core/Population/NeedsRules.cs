using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// マズローの欲求段階（#403）。下位ほど根源的で、満たされて初めて上位が動機になる。
    /// 序数（添字）が低いほど下層＝<see cref="NeedsRules.DominantNeed"/> はこの順で下位優先に未充足を探す。
    /// </summary>
    public enum NeedLevel
    {
        生理,       // 0 生理的欲求（最下層）
        安全,       // 1 安全欲求
        所属,       // 2 所属と愛
        承認,       // 3 承認欲求
        自己実現,   // 4 自己実現
        自己超越    // 5 自己超越（最上層）
    }

    /// <summary>マズロー欲求段階の調整係数（#403）。</summary>
    public readonly struct NeedsParams
    {
        /// <summary>この値以上の充足度なら「満たされた」とみなす（下位の充足判定の閾値）。</summary>
        public readonly float satisfiedThreshold;
        /// <summary>動機の最大強度（下位が満たされた直後の未充足層で最大）。</summary>
        public readonly float maxMotivation;
        /// <summary>士気寄与の基準（全層が満たされたときの最大寄与）。</summary>
        public readonly float moraleScale;
        /// <summary>下層が欠けたときの士気ペナルティ係数（下層欠乏ほど士気を削る）。</summary>
        public readonly float deficitPenalty;

        public NeedsParams(float satisfiedThreshold, float maxMotivation, float moraleScale, float deficitPenalty)
        {
            this.satisfiedThreshold = satisfiedThreshold;
            this.maxMotivation = maxMotivation;
            this.moraleScale = moraleScale;
            this.deficitPenalty = deficitPenalty;
        }

        public static NeedsParams Default => new NeedsParams(0.7f, 1f, 1f, 0.5f);
    }

    /// <summary>
    /// マズロー欲求段階の純ロジック（#403）。下位（生理→安全→所属→承認→自己実現→自己超越）が
    /// 満たされて初めて上位が動機になる、という前提強度を計算する。<see cref="DominantNeed"/> が
    /// 最下層の未充足（＝今いちばん効く欲求）を返し、<see cref="Motivation"/> がその動機の強さ、
    /// <see cref="MoraleContribution"/> が充足から士気への寄与を返す。基準値非破壊（実効値パターン）。
    /// 純データ／純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class NeedsRules
    {
        /// <summary>欲求段階の数（enum の要素数）。</summary>
        public const int LevelCount = 6;

        /// <summary>
        /// 支配的な欲求（下位優先で最初の未充足層）を返す。satisfaction[i] が
        /// <see cref="NeedsParams.satisfiedThreshold"/> 未満の最下層を採用。全層満たされていれば最上層を返す。
        /// </summary>
        public static NeedLevel DominantNeed(float[] satisfaction, NeedsParams p)
        {
            if (satisfaction == null || satisfaction.Length == 0) return NeedLevel.生理;
            int count = Mathf.Min(satisfaction.Length, LevelCount);
            for (int i = 0; i < count; i++)
            {
                if (Mathf.Clamp01(satisfaction[i]) < p.satisfiedThreshold) return (NeedLevel)i;
            }
            // 全層満たされていれば最上層（自己超越）が支配的
            return (NeedLevel)(count - 1);
        }

        public static NeedLevel DominantNeed(float[] satisfaction) => DominantNeed(satisfaction, NeedsParams.Default);

        /// <summary>
        /// 支配的欲求の動機の強さ（0..maxMotivation）。下位がすべて満たされているほど、
        /// かつ当該層が欠乏しているほど強い＝「下位が満たされて初めて上位が動機になる」を表す。
        /// </summary>
        public static float Motivation(float[] satisfaction, NeedsParams p)
        {
            if (satisfaction == null || satisfaction.Length == 0) return 0f;
            int dominant = (int)DominantNeed(satisfaction, p);
            int count = Mathf.Min(satisfaction.Length, LevelCount);

            // 下位（dominant 未満）の充足度の積＝下位が揃うほど 1 に近づく前提強度
            float prerequisite = 1f;
            for (int i = 0; i < dominant; i++)
            {
                prerequisite *= Mathf.Clamp01(satisfaction[i]);
            }

            // 当該層の欠乏（1 - 充足）が大きいほど動機が強い
            float own = dominant < count ? Mathf.Clamp01(satisfaction[dominant]) : 1f;
            float deficit = 1f - own;

            return Mathf.Clamp(prerequisite * deficit * p.maxMotivation, 0f, p.maxMotivation);
        }

        public static float Motivation(float[] satisfaction) => Motivation(satisfaction, NeedsParams.Default);

        /// <summary>
        /// 充足から士気への寄与（0..moraleScale）。全層の平均充足を基準にしつつ、下層の欠乏を
        /// より重く罰する（生理・安全が欠けると士気は大きく下がる）。
        /// </summary>
        public static float MoraleContribution(float[] satisfaction, NeedsParams p)
        {
            if (satisfaction == null || satisfaction.Length == 0) return 0f;
            int count = Mathf.Min(satisfaction.Length, LevelCount);

            float sum = 0f;
            float penalty = 0f;
            for (int i = 0; i < count; i++)
            {
                float s = Mathf.Clamp01(satisfaction[i]);
                sum += s;
                // 下層ほど重み大（count - i）の欠乏をペナルティに積む
                float weight = (count - i) / (float)count;
                penalty += (1f - s) * weight;
            }

            float average = sum / count;
            float deficitTerm = (penalty / count) * p.deficitPenalty;
            float raw = (average - deficitTerm) * p.moraleScale;
            return Mathf.Clamp(raw, 0f, p.moraleScale);
        }

        public static float MoraleContribution(float[] satisfaction) => MoraleContribution(satisfaction, NeedsParams.Default);
    }
}
