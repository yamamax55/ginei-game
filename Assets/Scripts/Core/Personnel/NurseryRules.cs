using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 保育園＝保育のロジック（#153 出生・#110 労働 連携・純ロジック・唯一の窓口）。教育（幼稚園/小学校…）とは効き先が別＝
    /// 整備率が高いほど①<b>働く親が増える</b>（労働参加＝候補/徴募の母数↑）②<b>子育ての負担が下がる</b>（出生率↑）。
    /// 「保育が充実すると人が増え働き手も増える」＝人口と労働の正のループ。マクロ背景（タイクン化回避）。test-first。
    /// </summary>
    public static class NurseryRules
    {
        /// <summary>完全整備での出生率上乗せ（×係数の上限ぶん）。</summary>
        public const float MaxFertilityBoost = 0.20f;

        /// <summary>完全整備での労働参加上乗せ（候補/徴募の母数）。</summary>
        public const float MaxLaborBoost = 0.15f;

        /// <summary>整備率→出生率の倍率（1.0..1+MaxFertilityBoost）。<see cref="DemographicsRules.VitalRates"/> の出生率に掛ける。</summary>
        public static float FertilityFactor(float coverage) => 1f + Mathf.Clamp01(coverage) * MaxFertilityBoost;

        /// <summary>整備率→労働参加の倍率（1.0..1+MaxLaborBoost）。候補/徴募プール（働ける親の数）に掛ける。</summary>
        public static float LaborParticipationFactor(float coverage) => 1f + Mathf.Clamp01(coverage) * MaxLaborBoost;
    }
}
