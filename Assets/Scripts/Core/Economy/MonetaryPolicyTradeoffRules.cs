using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 金融政策のトレードオフ（中央銀行の政策金利）の純ロジック。
    /// 金利を下げれば投資が増えるが、需要過熱でインフレを招く＝政策のトレードオフを表す。
    /// 既存の <c>MonetaryPolicyRules</c>(#1945 CBNK) とは別系統の自己完結スカラ計算（test-first）。
    /// 既存ゲーム型に依存せず float/int/struct のみで完結する。
    /// </summary>
    public static class MonetaryPolicyTradeoffRules
    {
        /// <summary>金融政策トレードオフの調整パラメータ。</summary>
        public readonly struct Params
        {
            /// <summary>金利低下に対する投資の感応度。</summary>
            public readonly float investmentSensitivity;
            /// <summary>金利によるインフレ抑制の感応度。</summary>
            public readonly float inflationSensitivity;
            /// <summary>中立金利（投資も冷やしも煽りもしない基準金利）。</summary>
            public readonly float neutralRate;

            public Params(float investmentSensitivity, float inflationSensitivity, float neutralRate)
            {
                this.investmentSensitivity = investmentSensitivity;
                this.inflationSensitivity = inflationSensitivity;
                this.neutralRate = neutralRate;
            }

            /// <summary>既定パラメータ。</summary>
            public static Params Default => new Params(2f, 2f, 0.25f);
        }

        // 簡易テイラー則の固定ウェイト（標準的な 1.5 / 0.5）。
        const float TaylorInflationWeight = 1.5f;
        const float TaylorOutputWeight = 0.5f;
        // 投資倍率の上限（過度な低金利でも 2 倍までに留める）。
        const float MaxInvestmentFactor = 2f;

        /// <summary>
        /// 投資倍率。金利が低いほど投資が増える。
        /// clamp(1 + (中立金利 - 金利) * 投資感応度, 0, 2)。金利＝中立で 1.0。
        /// </summary>
        public static float InvestmentFactor(float interestRate, Params p)
        {
            float factor = 1f + (p.neutralRate - interestRate) * p.investmentSensitivity;
            return Mathf.Clamp(factor, 0f, MaxInvestmentFactor);
        }

        /// <summary>既定パラメータ版。</summary>
        public static float InvestmentFactor(float interestRate)
            => InvestmentFactor(interestRate, Params.Default);

        /// <summary>
        /// インフレ圧力。需要（産出ギャップ outputGap 0..1）が押し上げ、金利が中立を上回るほど冷やす。
        /// clamp01(outputGap - (金利 - 中立金利) * インフレ感応度)。
        /// </summary>
        public static float InflationPressure(float interestRate, float outputGap, Params p)
        {
            float gap = Mathf.Clamp01(outputGap);
            float pressure = gap - (interestRate - p.neutralRate) * p.inflationSensitivity;
            return Mathf.Clamp01(pressure);
        }

        /// <summary>既定パラメータ版。</summary>
        public static float InflationPressure(float interestRate, float outputGap)
            => InflationPressure(interestRate, outputGap, Params.Default);

        /// <summary>
        /// 簡易テイラー則の目標金利。
        /// 中立金利 + 1.5 * (インフレ - 目標) + 0.5 * 産出ギャップ、[0,1] にクランプ。
        /// </summary>
        public static float TaylorRate(float inflation, float inflationTarget, float outputGap, Params p)
        {
            float rate = p.neutralRate
                + TaylorInflationWeight * (inflation - inflationTarget)
                + TaylorOutputWeight * outputGap;
            return Mathf.Clamp01(rate);
        }

        /// <summary>既定パラメータ版。</summary>
        public static float TaylorRate(float inflation, float inflationTarget, float outputGap)
            => TaylorRate(inflation, inflationTarget, outputGap, Params.Default);
    }
}
