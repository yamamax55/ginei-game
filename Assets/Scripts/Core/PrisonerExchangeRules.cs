using UnityEngine;

namespace Ginei
{
    /// <summary>捕虜交換の調整係数。</summary>
    public readonly struct ExchangeParams
    {
        /// <summary>階級tier1段あたりの価値倍率の伸び（高位ほど高価）。</summary>
        public readonly float tierValueStep;
        /// <summary>兵卒（tier0相当）の基準価値。</summary>
        public readonly float baseValue;
        /// <summary>成立とみなす提示価値の比の許容幅（1±これ以内なら等価交換と認める）。</summary>
        public readonly float fairnessTolerance;

        public ExchangeParams(float tierValueStep, float baseValue, float fairnessTolerance)
        {
            this.tierValueStep = Mathf.Max(0f, tierValueStep);
            this.baseValue = Mathf.Max(0.0001f, baseValue);
            this.fairnessTolerance = Mathf.Max(0f, fairnessTolerance);
        }

        /// <summary>既定＝tier1段で+50%・基準価値1・許容幅±20%。</summary>
        public static ExchangeParams Default => new ExchangeParams(0.5f, 1f, 0.2f);
    }

    /// <summary>
    /// 捕虜交換交渉の純ロジック（ヤン艦隊の捕虜交換型）。捕虜の価値は数×質（階級）で決まり、
    /// 提示価値の釣り合いと双方の足元（切迫度＝人材が枯れている側は不利な交換も呑む）で成立が決まる。
    /// 成立すれば双方の人材が還流する＝人道と実利が一致する稀な外交。個々の捕虜の処遇
    /// （<see cref="CaptivityRules"/>＝解放/処断/登用）へ委譲し、ここは交渉の力学のみ。
    /// 乱数なし・決定論。純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class PrisonerExchangeRules
    {
        /// <summary>捕虜1名の交換価値＝基準×（1＋tier×段差）。tier は階級序列（#14、兵卒=0扱い）。</summary>
        public static float PrisonerValue(int rankTier, ExchangeParams p)
        {
            return p.baseValue * (1f + Mathf.Max(0, rankTier) * p.tierValueStep);
        }

        public static float PrisonerValue(int rankTier) => PrisonerValue(rankTier, ExchangeParams.Default);

        /// <summary>捕虜群の総価値＝Σ各員の価値。null/空は0。</summary>
        public static float TotalValue(int[] rankTiers, ExchangeParams p)
        {
            if (rankTiers == null) return 0f;
            float sum = 0f;
            for (int i = 0; i < rankTiers.Length; i++)
            {
                sum += PrisonerValue(rankTiers[i], p);
            }
            return sum;
        }

        public static float TotalValue(int[] rankTiers) => TotalValue(rankTiers, ExchangeParams.Default);

        /// <summary>
        /// 提示の釣り合い比＝自分が出す価値÷相手から受け取る価値。1.0=等価。受け取り0は無限大（タダ取りの要求）。
        /// </summary>
        public static float OfferRatio(float giveValue, float receiveValue)
        {
            float give = Mathf.Max(0f, giveValue);
            float receive = Mathf.Max(0f, receiveValue);
            if (receive <= 0f) return give > 0f ? float.PositiveInfinity : 1f;
            return give / receive;
        }

        /// <summary>
        /// 自分にとって受諾可能か。許容幅は切迫度 desperation(0..1) で広がる＝人材が枯れている側は
        /// 不利な交換（自分が多く出す・ratio&gt;1）も呑む。等価（ratio=1）は常に可。
        /// </summary>
        public static bool Acceptable(float giveValue, float receiveValue, float desperation, ExchangeParams p)
        {
            float ratio = OfferRatio(giveValue, receiveValue);
            if (float.IsPositiveInfinity(ratio)) return false; // タダ取りには応じない
            float tolerance = p.fairnessTolerance + Mathf.Clamp01(desperation); // 切迫が許容を広げる
            return ratio <= 1f + tolerance;
        }

        public static bool Acceptable(float giveValue, float receiveValue, float desperation)
            => Acceptable(giveValue, receiveValue, desperation, ExchangeParams.Default);

        /// <summary>
        /// 交換成立か＝双方が受諾可能（Aが出す価値はBの受け取り、相互に判定）。
        /// </summary>
        public static bool DealStruck(float valueFromA, float valueFromB, float desperationA, float desperationB, ExchangeParams p)
        {
            bool aAccepts = Acceptable(valueFromA, valueFromB, desperationA, p);
            bool bAccepts = Acceptable(valueFromB, valueFromA, desperationB, p);
            return aAccepts && bAccepts;
        }

        public static bool DealStruck(float valueFromA, float valueFromB, float desperationA, float desperationB)
            => DealStruck(valueFromA, valueFromB, desperationA, desperationB, ExchangeParams.Default);
    }
}
