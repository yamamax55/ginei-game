using System.Collections.Generic;
using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 累進課税の純ロジック（内政・財政 #161 拡張）。<see cref="FiscalRules.TaxRevenue"/> は一律税率（taxBase×rate）だが、
    /// こちらは<b>所得階層ごとに異なる税率</b>を適用し、実効税率・税収・再分配（不平等の縮小）を出す。
    /// 階層データ（しきい値・限界税率）と所得分布を float で受ける純ロジック＝FactionState 依存なし。決定論・test-first。
    /// </summary>
    public static class TaxBracketRules
    {
        /// <summary>所得階層＝この所得しきい値を超えた部分に <see cref="marginalRate"/> がかかる（限界税率方式）。</summary>
        public readonly struct Bracket
        {
            public readonly float threshold;     // この所得を超えた部分にかかる（昇順）
            public readonly float marginalRate;  // 限界税率 0..1

            public Bracket(float threshold, float marginalRate)
            {
                this.threshold = Mathf.Max(0f, threshold);
                this.marginalRate = Mathf.Clamp01(marginalRate);
            }
        }

        /// <summary>
        /// 1個人の所得 <paramref name="income"/> に累進ブラケット（昇順しきい値）を適用した納税額。
        /// しきい値を超えた部分にその階層の限界税率を掛けて積み上げる（標準的な限界税率方式）。
        /// </summary>
        public static float TaxFor(float income, IReadOnlyList<Bracket> brackets)
        {
            if (income <= 0f || brackets == null || brackets.Count == 0) return 0f;
            float tax = 0f;
            for (int i = 0; i < brackets.Count; i++)
            {
                float lower = brackets[i].threshold;
                if (income <= lower) break; // 昇順前提＝以降は対象外
                float upper = (i + 1 < brackets.Count) ? brackets[i + 1].threshold : float.PositiveInfinity;
                float top = Mathf.Min(income, upper);
                float span = top - lower;
                if (span > 0f) tax += span * brackets[i].marginalRate;
            }
            return tax;
        }

        /// <summary>所得分布（各個人＝人口加重なし。代表所得の配列）に対する総税収。</summary>
        public static float TotalRevenue(IReadOnlyList<float> incomes, IReadOnlyList<Bracket> brackets)
        {
            if (incomes == null) return 0f;
            float sum = 0f;
            for (int i = 0; i < incomes.Count; i++) sum += TaxFor(incomes[i], brackets);
            return sum;
        }

        /// <summary>総税収 ÷ 総所得＝実効税率（0..1）。所得ゼロなら0。</summary>
        public static float EffectiveRate(IReadOnlyList<float> incomes, IReadOnlyList<Bracket> brackets)
        {
            if (incomes == null || incomes.Count == 0) return 0f;
            float gross = 0f;
            for (int i = 0; i < incomes.Count; i++) gross += Mathf.Max(0f, incomes[i]);
            if (gross <= 0f) return 0f;
            return Mathf.Clamp01(TotalRevenue(incomes, brackets) / gross);
        }

        /// <summary>
        /// 累進度（0..1）。最高所得者の実効税率 − 最低所得者の実効税率。累進的なら正・一律なら0付近。
        /// 再分配の強さの目安（高いほど格差を縮める）。
        /// </summary>
        public static float Progressivity(IReadOnlyList<float> incomes, IReadOnlyList<Bracket> brackets)
        {
            if (incomes == null || incomes.Count < 2) return 0f;
            float lo = float.PositiveInfinity, hi = float.NegativeInfinity;
            for (int i = 0; i < incomes.Count; i++)
            {
                float v = Mathf.Max(0f, incomes[i]);
                if (v < lo) lo = v;
                if (v > hi) hi = v;
            }
            if (hi <= 0f || hi <= lo) return 0f;
            float rateHi = TaxFor(hi, brackets) / hi;
            float rateLo = lo > 0f ? TaxFor(lo, brackets) / lo : 0f;
            return Mathf.Clamp01(rateHi - rateLo);
        }
    }
}
