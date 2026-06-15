using UnityEngine;

namespace Ginei
{
    /// <summary>勲章・栄典の調整係数。</summary>
    public readonly struct HonorsParams
    {
        /// <summary>満額価値の勲章1つが士気に返すボーナス。</summary>
        public readonly float moraleScale;
        /// <summary>満額価値の勲章1つが忠誠に返すボーナス。</summary>
        public readonly float loyaltyScale;
        /// <summary>価値が半減する累計授与数（希少性の半減期＝乱発のインフレ曲線）。</summary>
        public readonly float halfValueCount;
        /// <summary>形骸化とみなす価値の閾値（これ未満の勲章は誰も有り難がらない）。</summary>
        public readonly float debasedThreshold;

        public HonorsParams(float moraleScale, float loyaltyScale, float halfValueCount, float debasedThreshold)
        {
            this.moraleScale = Mathf.Max(0f, moraleScale);
            this.loyaltyScale = Mathf.Max(0f, loyaltyScale);
            this.halfValueCount = Mathf.Max(1f, halfValueCount);
            this.debasedThreshold = Mathf.Clamp01(debasedThreshold);
        }

        /// <summary>既定＝士気0.2・忠誠0.15・半減数20・形骸化閾値0.2。</summary>
        public static HonorsParams Default => new HonorsParams(0.2f, 0.15f, 20f, 0.2f);
    }

    /// <summary>
    /// 勲章・栄典の純ロジック（名誉の通貨）。叙勲は士気と忠誠をタダ同然で買える便利な通貨だが、
    /// 価値の源泉は希少性＝乱発するほど1つあたりの価値が逓減し（半減期カーブ）、形骸化した勲章は
    /// 誰の心も動かさない。軍功爵位（<see cref="MeritRankRules"/>＝実利を伴う身分）とは別系統＝
    /// 名誉そのものの経済。乱数なし・決定論。純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class HonorsRules
    {
        /// <summary>
        /// 勲章の現在価値（0..1）＝半減期カーブ。累計授与数が halfValueCount で価値半減、
        /// その倍でさらに半減（1/(1+n/half)）。授与ゼロなら満額1.0。
        /// </summary>
        public static float AwardValue(int totalAwarded, HonorsParams p)
        {
            float n = Mathf.Max(0, totalAwarded);
            return 1f / (1f + n / p.halfValueCount);
        }

        public static float AwardValue(int totalAwarded) => AwardValue(totalAwarded, HonorsParams.Default);

        /// <summary>受勲者の士気ボーナス＝現在価値×士気スケール。</summary>
        public static float MoraleBonus(int totalAwarded, HonorsParams p)
        {
            return AwardValue(totalAwarded, p) * p.moraleScale;
        }

        public static float MoraleBonus(int totalAwarded) => MoraleBonus(totalAwarded, HonorsParams.Default);

        /// <summary>受勲者の忠誠ボーナス＝現在価値×忠誠スケール。</summary>
        public static float LoyaltyBonus(int totalAwarded, HonorsParams p)
        {
            return AwardValue(totalAwarded, p) * p.loyaltyScale;
        }

        public static float LoyaltyBonus(int totalAwarded) => LoyaltyBonus(totalAwarded, HonorsParams.Default);

        /// <summary>形骸化したか＝現在価値が閾値未満（授与しても誰も喜ばない）。</summary>
        public static bool IsDebased(int totalAwarded, HonorsParams p)
        {
            return AwardValue(totalAwarded, p) < p.debasedThreshold;
        }

        public static bool IsDebased(int totalAwarded) => IsDebased(totalAwarded, HonorsParams.Default);

        /// <summary>
        /// 一括授与の総効果（士気合計）。n 個配るとき各授与は配るたびに価値が逓減する＝
        /// まとめて配る効果は線形に伸びない（限界効用逓減を積分で体感させる）。
        /// </summary>
        public static float BatchMoraleTotal(int alreadyAwarded, int batchCount, HonorsParams p)
        {
            float total = 0f;
            int start = Mathf.Max(0, alreadyAwarded);
            int count = Mathf.Max(0, batchCount);
            for (int i = 0; i < count; i++)
            {
                total += MoraleBonus(start + i, p);
            }
            return total;
        }

        public static float BatchMoraleTotal(int alreadyAwarded, int batchCount)
            => BatchMoraleTotal(alreadyAwarded, batchCount, HonorsParams.Default);
    }
}
