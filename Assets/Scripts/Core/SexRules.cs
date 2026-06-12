using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 性別のロジック（純ロジック・唯一の窓口）。POP の男女比（<see cref="Population.femaleShare"/>）を扱い、
    /// 偏りが出生に与える影響（番が組みにくくなる）を係数で返す。マクロ背景＝個別の交配は扱わない（タイクン化回避）。test-first。
    /// 性的指向（ストレート/LGBTQ+）は<b>別軸の隠しパラメータ案＝検討項目（現状未実装）</b>＝<c>docs/gender-orientation-design.md</c>。
    /// </summary>
    public static class SexRules
    {
        /// <summary>均衡した男女比（女性割合）。</summary>
        public const float BalancedFemaleShare = 0.5f;

        /// <summary>男女比の偏りが完全（0 or 1）のときの出生係数の下限。</summary>
        public const float MaxSkewPenalty = 0.5f;

        /// <summary>
        /// 男女比の偏り→出生係数（0.5で1.0／偏るほど低下し0 or 1で 1-<see cref="MaxSkewPenalty"/>）。
        /// 極端に偏ると番が組みにくく出生が鈍る（マクロ近似）。<see cref="DemographicsRules.VitalRates"/> の出生率に掛けられる。
        /// </summary>
        public static float BalanceFactor(float femaleShare)
        {
            float f = Mathf.Clamp01(femaleShare);
            float skew = (f - 0.5f) * 2f; // -1..1（0で均衡）
            return Mathf.Clamp01(1f - skew * skew * MaxSkewPenalty);
        }

        /// <summary>その性別が POP に占める割合（女性=femaleShare／男性=1-femaleShare）。</summary>
        public static float ShareOf(Sex sex, float femaleShare)
        {
            float f = Mathf.Clamp01(femaleShare);
            return sex == Sex.女性 ? f : 1f - f;
        }
    }
}
